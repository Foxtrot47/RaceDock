// Copyright (c) 2026 RaceDock contributors
// Portions conceptually adapted from mbot; SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public sealed class TimingStateProcessor(IClock clock, IAlertArbiter alerts)
{
    private readonly Dictionary<string, JsonObject> _topics = [];
    private readonly HashSet<string> _raceControlKeys = [];
    private readonly Dictionary<string, string> _lastBestLap = [];
    private readonly Dictionary<string, SectorTimingSet> _bestLapSectors = [];
    private readonly Dictionary<string, string> _capturedSectorLap = [];
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
        _topics.Clear(); _raceControlKeys.Clear(); _lastBestLap.Clear(); _bestLapSectors.Clear(); _capturedSectorLap.Clear();
        _fastestDriver = null; _fastestSeconds = null; _track = TrackCondition.Unknown;
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
        var info = _topics.GetValueOrDefault("SessionInfo");
        var meeting = info?["Meeting"] as JsonObject;
        var lapCount = _topics.GetValueOrDefault("LapCount");
        var sessionData = _topics.GetValueOrDefault("SessionData");
        var statusText = LatestSessionStatus(sessionData?["StatusSeries"])
            ?? JsonSupport.String(_topics.GetValueOrDefault("SessionStatus")?["Status"] ?? _topics.GetValueOrDefault("SessionStatus")?["Started"])
            ?? JsonSupport.String(info?["SessionStatus"]);
        var qualifyingPart = QualifyingPart(sessionData);
        var sessionName = SessionName(info, qualifyingPart);
        var requiresTimedClassification = RequiresTimedClassification(info, sessionName);
        var standings = BuildStandings(timing, requiresTimedClassification);
        var classificationReady = !requiresTimedClassification || standings.Any(HasTimedResult);
        var qualifyingSession = IsQualifyingSession(info, sessionName);
        var betweenQualifyingParts = qualifyingSession && qualifyingPart is >= 1 and < 3;
        var lifecycle = SessionLifecycleFor(
            standings.Count, statusText, !qualifyingSession && alerts.Current?.Kind == AlertKind.Chequered,
            classificationReady, betweenQualifyingParts);
        if (lifecycle == SessionLifecycle.Live && requiresTimedClassification)
            standings = [.. standings.Where(HasTimedResult)];
        Publish(new(JsonSupport.String(meeting?["Name"]) ?? "F1", sessionName,
            JsonSupport.String(meeting?["Circuit"]?["ShortName"]) ?? "", JsonSupport.Int(lapCount?["CurrentLap"]) ?? standings.FirstOrDefault()?.Lap ?? 0,
            JsonSupport.Int(lapCount?["TotalLaps"]), _track, standings, timestamp ?? clock.UtcNow, lifecycle, _connection, null, Championship.Formula1));
    }

    private List<CompetitorStanding> BuildStandings(JsonObject? timing, bool useBestLapDeltas)
    {
        var lines = timing?["Lines"] as JsonObject;
        if (lines is null) return [];
        var drivers = _topics.GetValueOrDefault("DriverList");
        var appLines = _topics.GetValueOrDefault("TimingAppData")?["Lines"] as JsonObject;
        List<CompetitorStanding> standings = [];
        Dictionary<string, double> bestLaps = [];
        foreach (var pair in lines)
        {
            if (pair.Value is not JsonObject line) continue;
            if (line["ShowPosition"] is { } showPosition && !JsonSupport.Bool(showPosition)) continue;
            var meta = drivers?[pair.Key] as JsonObject;
            var bestLap = JsonSupport.TimingValue(line["BestLapTime"]);
            var currentSectors = ParseSectors(line["Sectors"]);
            CaptureBestLapSectors(pair.Key, line, bestLap, currentSectors);
            var bestSeconds = bestLap is null ? null : ParseLapSeconds(bestLap);
            if (bestSeconds.HasValue) bestLaps[pair.Key] = bestSeconds.Value;
            var position = JsonSupport.Int(line["Position"]) ?? int.MaxValue;
            standings.Add(new(pair.Key, position, JsonSupport.String(meta?["Tla"]) ?? pair.Key,
                JsonSupport.String(meta?["FullName"]) ?? "", JsonSupport.String(meta?["TeamName"]) ?? "",
                JsonSupport.TimingValue(line["GapToLeader"]),
                JsonSupport.TimingValue(line["IntervalToPositionAhead"]), LatestCompound(appLines?[pair.Key] as JsonObject),
                JsonSupport.Int(line["NumberOfLaps"]) ?? 0, JsonSupport.Bool(line["InPit"]),
                JsonSupport.Bool(line["Retired"]), JsonSupport.Bool(line["Stopped"]), pair.Key == _fastestDriver,
                ResultTime: bestLap,
                LastLapTime: JsonSupport.TimingValue(line["LastLapTime"]),
                CurrentSectors: currentSectors,
                BestLapSectors: _bestLapSectors.GetValueOrDefault(pair.Key)));
        }
        standings = [.. standings.Where(x => x.Position > 0 && x.Position < int.MaxValue).OrderBy(x => x.Position)];
        NormalizeStandings(standings, bestLaps, useBestLapDeltas);
        return standings;
    }

    private void CaptureBestLapSectors(
        string driverId, JsonObject line, string? bestLap, SectorTimingSet? currentSectors)
    {
        if (bestLap is null || _capturedSectorLap.GetValueOrDefault(driverId) == bestLap) return;
        // A new best time invalidates the old split set even when its replacement sectors have not arrived yet.
        _bestLapSectors.Remove(driverId);
        _capturedSectorLap.Remove(driverId);
        if (currentSectors is null || !HasAllSectors(currentSectors)) return;

        var bestLapNumber = JsonSupport.Int(line["BestLapTime"]?["Lap"]);
        var completedLaps = JsonSupport.Int(line["NumberOfLaps"]);
        var lastLap = JsonSupport.TimingValue(line["LastLapTime"]);
        var lapMatches = bestLapNumber.HasValue && bestLapNumber == completedLaps;
        var timeMatches = string.Equals(bestLap, lastLap, StringComparison.Ordinal);
        if (!lapMatches && !timeMatches) return;

        _bestLapSectors[driverId] = currentSectors;
        _capturedSectorLap[driverId] = bestLap;
    }

    private static SectorTimingSet? ParseSectors(JsonNode? node)
    {
        var sectors = new SectorTimingSet(
            ParseSector(SectorAt(node, 0)),
            ParseSector(SectorAt(node, 1)),
            ParseSector(SectorAt(node, 2)));
        return sectors.Sector1 is null && sectors.Sector2 is null && sectors.Sector3 is null ? null : sectors;
    }

    private static JsonNode? SectorAt(JsonNode? node, int index) => node switch
    {
        JsonArray array when index < array.Count => array[index],
        JsonObject obj => obj[index.ToString(System.Globalization.CultureInfo.InvariantCulture)],
        _ => null
    };

    private static SectorTiming? ParseSector(JsonNode? node)
    {
        var value = JsonSupport.TimingValue(node);
        if (value is null) return null;
        var sector = node as JsonObject;
        var state = JsonSupport.Bool(sector?["OverallFastest"])
            ? SectorTimingState.OverallBest
            : JsonSupport.Bool(sector?["PersonalFastest"])
                ? SectorTimingState.PersonalBest
                : SectorTimingState.Slower;
        return new(value, state);
    }

    private static bool HasAllSectors(SectorTimingSet sectors) =>
        sectors.Sector1 is not null && sectors.Sector2 is not null && sectors.Sector3 is not null;

    private static void NormalizeStandings(
        List<CompetitorStanding> standings, IReadOnlyDictionary<string, double> bestLaps, bool useBestLapDeltas)
    {
        if (standings.Count == 0) return;
        standings[0] = standings[0] with { GapToLeader = "LEAD" };
        var hasLeaderBest = bestLaps.TryGetValue(standings[0].DriverId, out var leaderBest);
        for (var index = 1; index < standings.Count; index++)
        {
            var standing = standings[index];
            var gap = IsLeaderLabel(standing.GapToLeader) ? null : standing.GapToLeader;
            var interval = standing.IntervalToPositionAhead;
            if (hasLeaderBest && bestLaps.TryGetValue(standing.DriverId, out var best))
            {
                var bestLapGap = FormatLapDelta(best - leaderBest);
                gap = useBestLapDeltas ? bestLapGap : gap ?? bestLapGap;
                if (bestLaps.TryGetValue(standings[index - 1].DriverId, out var previousBest))
                {
                    var bestLapInterval = FormatLapDelta(best - previousBest);
                    interval = useBestLapDeltas ? bestLapInterval : interval ?? bestLapInterval;
                }
            }
            if (gap is null && standing.Position == standings[0].Position) gap = FormatLapDelta(0);
            if (interval is null && standing.Position == standings[index - 1].Position) interval = FormatLapDelta(0);
            standings[index] = standing with { GapToLeader = gap, IntervalToPositionAhead = interval };
        }
    }

    private static string SessionName(JsonObject? info, int? qualifyingPart)
    {
        var name = JsonSupport.String(info?["Name"]) ?? "Live Session";
        if (!IsQualifyingSession(info, name)) return name;
        return qualifyingPart is >= 1 and <= 3 ? $"{name} {qualifyingPart}" : name;
    }

    private static bool IsQualifyingSession(JsonObject? info, string name) =>
        string.Equals(JsonSupport.String(info?["Type"]), "Qualifying", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Qualifying", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Shootout", StringComparison.OrdinalIgnoreCase);

    private static int? QualifyingPart(JsonObject? sessionData) =>
        LatestQualifyingPart(sessionData?["Series"]);

    private static int? LatestQualifyingPart(JsonNode? series) => series switch
    {
        JsonArray array => array.OfType<JsonObject>().Select(value => JsonSupport.Int(value["QualifyingPart"])).LastOrDefault(value => value is not null),
        JsonObject obj => obj.Select(pair => JsonSupport.Int(pair.Value?["QualifyingPart"])).LastOrDefault(value => value is not null),
        _ => null
    };

    private static string? LatestSessionStatus(JsonNode? series) => series switch
    {
        JsonArray array => array.OfType<JsonObject>().Select(value => JsonSupport.String(value["SessionStatus"])).LastOrDefault(value => value is not null),
        JsonObject obj => obj.Select(pair => JsonSupport.String(pair.Value?["SessionStatus"])).LastOrDefault(value => value is not null),
        _ => null
    };

    private static SessionLifecycle SessionLifecycleFor(
        int standingsCount, string? status, bool chequered, bool classificationReady,
        bool betweenQualifyingParts)
    {
        if (standingsCount == 0) return SessionLifecycle.OffSession;
        if (betweenQualifyingParts && classificationReady) return SessionLifecycle.Live;
        if (chequered || IsEndedSessionStatus(status)) return SessionLifecycle.Ended;
        return IsPreSessionStatus(status) || !classificationReady
            ? SessionLifecycle.PreSession
            : SessionLifecycle.Live;
    }

    private static bool RequiresTimedClassification(JsonObject? info, string sessionName)
    {
        var type = JsonSupport.String(info?["Type"]) ?? "";
        return MentionsTimedSession(type) || MentionsTimedSession(sessionName);
    }

    private static bool MentionsTimedSession(string value) =>
        value.Contains("practice", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("qualif", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("shootout", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("warm", StringComparison.OrdinalIgnoreCase);

    private static bool HasTimedResult(CompetitorStanding standing) =>
        !string.IsNullOrWhiteSpace(standing.ResultTime) || !string.IsNullOrWhiteSpace(standing.LastLapTime);

    private static bool IsEndedSessionStatus(string? value) => value?.Trim().ToUpperInvariant() is
        "FINISHED" or "FINALISED" or "FINALIZED" or "ENDED" or "ENDS" or "COMPLETE" or "COMPLETED";

    private static bool IsPreSessionStatus(string? value) =>
        string.Equals(value?.Trim(), "Inactive", StringComparison.OrdinalIgnoreCase);

    private void Publish(TimingSnapshot snapshot) { Current = snapshot; SnapshotChanged?.Invoke(snapshot); }
    private string DriverCode(string key) => JsonSupport.String(_topics.GetValueOrDefault("DriverList")?[key]?["Tla"]) ?? key;
    private static string? LatestCompound(JsonObject? driver) => driver?["Stints"] switch
    {
        JsonArray array => array.OfType<JsonObject>().LastOrDefault()?["Compound"]?.ToString(),
        JsonObject obj => obj.OrderBy(x => int.TryParse(x.Key, out var i) ? i : -1).LastOrDefault().Value?["Compound"]?.ToString(),
        _ => null
    };
    private static bool IsLeaderLabel(string? value) => value?.Trim().ToUpperInvariant() is "LEAD" or "LEADER";
    private static string FormatLapDelta(double seconds) => $"+{Math.Max(0, seconds).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}";
    private static double? ParseLapSeconds(string text)
    {
        var parts = text.Split(':');
        return parts.Length == 2 && double.TryParse(parts[0], out var min) && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var sec) ? min * 60 + sec : double.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var direct) ? direct : null;
    }
}
