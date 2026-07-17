// Copyright (c) 2026 MotorsportTaskbar contributors
// Portions conceptually adapted from mbot; SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace MotorsportTaskbar.Core;

public sealed class TimingStateProcessor(IClock clock, IAlertArbiter alerts)
{
    private readonly Dictionary<string, JsonObject> _topics = [];
    private readonly HashSet<string> _raceControlKeys = [];
    private readonly Dictionary<string, string> _lastBestLap = [];
    private TrackCondition _track = TrackCondition.Unknown;
    private string? _fastestDriver;
    private double? _fastestSeconds;
    private ConnectionState _connection = ConnectionState.Disconnected;
    public TimingSnapshot Current { get; private set; } = TimingSnapshot.Hidden(clock.UtcNow);
    public event Action<TimingSnapshot>? SnapshotChanged;

    public void SetConnection(ConnectionState state)
    {
        _connection = state;
        Publish(Current with { ConnectionState = state, FreshnessTimestamp = state == ConnectionState.Connected ? clock.UtcNow : Current.FreshnessTimestamp });
    }

    public void ClearAlerts() => alerts.Clear();

    public void ProcessInitial(JsonObject initial)
    {
        _topics.Clear(); _raceControlKeys.Clear(); _lastBestLap.Clear(); _fastestDriver = null; _fastestSeconds = null;
        foreach (var pair in initial)
            if (pair.Value is JsonObject value) _topics[pair.Key] = (JsonObject)value.DeepClone();
        ProcessTrackStatus(_topics.GetValueOrDefault("TrackStatus"));
        ProcessRaceControl(_topics.GetValueOrDefault("RaceControlMessages"), suppressAlerts: true);
        Rebuild();
    }

    public void ProcessDelta(string topic, JsonNode data, DateTimeOffset timestamp)
    {
        if (data is JsonObject obj)
        {
            if (!_topics.TryGetValue(topic, out var state)) _topics[topic] = state = new();
            JsonSupport.DeepMerge(state, obj);
        }
        if (topic == "TrackStatus") ProcessTrackStatus(_topics.GetValueOrDefault(topic));
        if (topic == "RaceControlMessages") ProcessRaceControl(data as JsonObject, suppressAlerts: false);
        if (topic == "TimingData") DetectFastestLaps();
        Rebuild(timestamp);
    }

    public void MarkStale(TimeSpan maximumAge)
    {
        if (Current.Lifecycle == SessionLifecycle.Live && clock.UtcNow - Current.FreshnessTimestamp >= maximumAge)
            Publish(Current with { Lifecycle = SessionLifecycle.OffSession, ConnectionState = ConnectionState.Stale });
    }

    private void ProcessTrackStatus(JsonObject? obj)
    {
        var raw = JsonSupport.String(obj?["Status"] ?? obj?["Value"]);
        var message = JsonSupport.String(obj?["Message"]);
        var next = (raw?.Trim().ToUpperInvariant(), message?.Trim().ToUpperInvariant()) switch
        {
            ("1", _) or ("ALLCLEAR", _) or ("GREEN", _) => TrackCondition.AllClear,
            ("2", _) or ("YELLOW", _) => TrackCondition.Yellow,
            ("3", _) or ("DOUBLEYELLOW", _) => TrackCondition.DoubleYellow,
            ("4", _) or ("SC", _) or ("SAFETYCAR", _) => TrackCondition.SafetyCar,
            ("5", _) or ("RED", _) or ("REDFLAG", _) => TrackCondition.RedFlag,
            ("6", _) or ("VSC", _) or ("VSCDEPLOYED", _) => TrackCondition.VirtualSafetyCar,
            ("7", _) or ("VSCENDING", _) => TrackCondition.VirtualSafetyCarEnding,
            (_, var m) when m?.Contains("VSC END") == true => TrackCondition.VirtualSafetyCarEnding,
            (_, var m) when m?.Contains("VIRTUAL SAFETY CAR") == true => TrackCondition.VirtualSafetyCar,
            (_, var m) when m?.Contains("SAFETY CAR") == true => TrackCondition.SafetyCar,
            (_, var m) when m?.Contains("RED") == true => TrackCondition.RedFlag,
            (_, var m) when m?.Contains("YELLOW") == true => TrackCondition.Yellow,
            _ => TrackCondition.Unknown
        };
        if (next == TrackCondition.Unknown || next == _track) return;
        _track = next;
        alerts.ApplyTrackCondition(next, message, Current.CurrentLap, clock.UtcNow);
    }

    private void ProcessRaceControl(JsonObject? data, bool suppressAlerts)
    {
        var messages = data?["Messages"] as JsonObject ?? data;
        if (messages is null) return;
        foreach (var pair in messages)
        {
            if (!_raceControlKeys.Add(pair.Key) || pair.Value is not JsonObject msg) continue;
            var text = JsonSupport.String(msg["Message"]) ?? "";
            var flag = JsonSupport.String(msg["Flag"])?.ToUpperInvariant();
            var lap = JsonSupport.Int(msg["Lap"]);
            if (flag == "CHEQUERED" || text.Contains("CHEQUERED", StringComparison.OrdinalIgnoreCase))
            {
                if (!suppressAlerts) alerts.Accept(new(AlertKind.Chequered, 90, true, "CHEQUERED FLAG", text, lap, clock.UtcNow, $"rc:{pair.Key}"));
            }
        }
    }

    private void DetectFastestLaps()
    {
        if (_topics.GetValueOrDefault("TimingData")?["Lines"] is not JsonObject lines) return;
        foreach (var pair in lines)
        {
            if (pair.Value is not JsonObject line || line["BestLapTime"] is not JsonObject best) continue;
            var value = JsonSupport.TimingValue(best);
            if (value is null || _lastBestLap.GetValueOrDefault(pair.Key) == value) continue;
            _lastBestLap[pair.Key] = value;
            var seconds = ParseLapSeconds(value);
            var overall = JsonSupport.Bool(best["OverallFastest"]) || seconds is not null && (_fastestSeconds is null || seconds < _fastestSeconds);
            if (!overall) continue;
            _fastestSeconds = seconds; _fastestDriver = pair.Key;
            var code = DriverCode(pair.Key);
            alerts.Accept(new(AlertKind.FastestLap, 50, false, "FASTEST LAP", $"{code}  {value}", JsonSupport.Int(line["NumberOfLaps"]), clock.UtcNow, $"fastest:{pair.Key}:{value}", TimeSpan.FromSeconds(6)));
        }
    }

    private void Rebuild(DateTimeOffset? timestamp = null)
    {
        var timing = _topics.GetValueOrDefault("TimingData");
        var lines = timing?["Lines"] as JsonObject;
        var drivers = _topics.GetValueOrDefault("DriverList");
        var appLines = _topics.GetValueOrDefault("TimingAppData")?["Lines"] as JsonObject;
        List<CompetitorStanding> standings = [];
        Dictionary<string, double> bestLaps = [];
        if (lines is not null)
        {
            foreach (var pair in lines)
            {
                if (pair.Value is not JsonObject line) continue;
                var meta = drivers?[pair.Key] as JsonObject;
                var bestLap = JsonSupport.TimingValue(line["BestLapTime"]);
                var bestSeconds = bestLap is null ? null : ParseLapSeconds(bestLap);
                if (bestSeconds.HasValue) bestLaps[pair.Key] = bestSeconds.Value;
                var pos = JsonSupport.Int(line["Position"]) ?? int.MaxValue;
                var status = JsonSupport.Int(line["Status"]);
                standings.Add(new(pair.Key, pos, JsonSupport.String(meta?["Tla"]) ?? pair.Key,
                    JsonSupport.String(meta?["FullName"]) ?? "", JsonSupport.String(meta?["TeamName"]) ?? "",
                    pos == 1 ? "LEAD" : JsonSupport.TimingValue(line["GapToLeader"]),
                    JsonSupport.TimingValue(line["IntervalToPositionAhead"]), LatestCompound(appLines?[pair.Key] as JsonObject),
                    JsonSupport.Int(line["NumberOfLaps"]) ?? 0, JsonSupport.Bool(line["InPit"]),
                    JsonSupport.Bool(line["Retired"]) || status == 68, JsonSupport.Bool(line["Stopped"]) || status == 64,
                    pair.Key == _fastestDriver));
            }
        }
        standings = [.. standings.Where(x => x.Position > 0 && x.Position < int.MaxValue).OrderBy(x => x.Position)];
        if (standings.Count > 0 && bestLaps.TryGetValue(standings[0].DriverId, out var leaderBest))
        {
            for (var index = 0; index < standings.Count; index++)
            {
                var standing = standings[index];
                if (!bestLaps.TryGetValue(standing.DriverId, out var best)) continue;
                var gap = standing.GapToLeader;
                var interval = standing.IntervalToPositionAhead;
                if (index == 0) gap = "LEAD";
                else
                {
                    gap ??= FormatLapDelta(best - leaderBest);
                    if (bestLaps.TryGetValue(standings[index - 1].DriverId, out var previousBest))
                        interval ??= FormatLapDelta(best - previousBest);
                }
                standings[index] = standing with { GapToLeader = gap, IntervalToPositionAhead = interval };
            }
        }
        var info = _topics.GetValueOrDefault("SessionInfo");
        var meeting = info?["Meeting"] as JsonObject;
        var lapCount = _topics.GetValueOrDefault("LapCount");
        var statusText = LatestSessionStatus(_topics.GetValueOrDefault("SessionData")?["StatusSeries"])
            ?? JsonSupport.String(_topics.GetValueOrDefault("SessionStatus")?["Status"] ?? _topics.GetValueOrDefault("SessionStatus")?["Started"]);
        var ended = alerts.Current?.Kind == AlertKind.Chequered || statusText is "Finished" or "Finalised" or "Ended";
        var lifecycle = standings.Count == 0 ? SessionLifecycle.OffSession : ended ? SessionLifecycle.Ended : SessionLifecycle.Live;
        Publish(new(JsonSupport.String(meeting?["Name"]) ?? "F1", JsonSupport.String(info?["Name"]) ?? "Live Session",
            JsonSupport.String(meeting?["Circuit"]?["ShortName"]) ?? "", JsonSupport.Int(lapCount?["CurrentLap"]) ?? standings.FirstOrDefault()?.Lap ?? 0,
            JsonSupport.Int(lapCount?["TotalLaps"]), _track, standings, timestamp ?? clock.UtcNow, lifecycle, _connection));
    }

    private static string? LatestSessionStatus(JsonNode? series) => series switch
    {
        JsonArray array => array.OfType<JsonObject>().Select(value => JsonSupport.String(value["SessionStatus"])).LastOrDefault(value => value is not null),
        JsonObject obj => obj.Select(pair => JsonSupport.String(pair.Value?["SessionStatus"])).LastOrDefault(value => value is not null),
        _ => null
    };

    private void Publish(TimingSnapshot snapshot) { Current = snapshot; SnapshotChanged?.Invoke(snapshot); }
    private string DriverCode(string key) => JsonSupport.String(_topics.GetValueOrDefault("DriverList")?[key]?["Tla"]) ?? key;
    private static string? LatestCompound(JsonObject? driver) => driver?["Stints"] switch
    {
        JsonArray array => array.OfType<JsonObject>().LastOrDefault()?["Compound"]?.ToString(),
        JsonObject obj => obj.OrderBy(x => int.TryParse(x.Key, out var i) ? i : -1).LastOrDefault().Value?["Compound"]?.ToString(),
        _ => null
    };
    private static string FormatLapDelta(double seconds) => $"+{Math.Max(0, seconds).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}";
    private static double? ParseLapSeconds(string text)
    {
        var parts = text.Split(':');
        return parts.Length == 2 && double.TryParse(parts[0], out var min) && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var sec) ? min * 60 + sec : double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var direct) ? direct : null;
    }
}
