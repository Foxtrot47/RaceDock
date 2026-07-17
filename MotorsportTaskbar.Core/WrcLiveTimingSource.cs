// Copyright (c) 2026 MotorsportTaskbar contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MotorsportTaskbar.Core;

/// <summary>Polling adapter for the public WRC Promoter timing API.</summary>
public sealed class WrcLiveTimingSource(TimingSnapshot seed, IClock clock, IAlertArbiter alerts) : ILiveTimingSource
{
    private const string ApiBase = "https://p-p.redbull.com/rb-wrccom-lintegration-yv-prod/api";
    private readonly HttpClient _http = new();
    private readonly Dictionary<int, JsonObject> _entries = [];
    private CancellationTokenSource? _runCts;
    private int _eventId;
    private int _rallyId;
    private string _meeting = "Rally Estonia";
    private DateTimeOffset _eventEnd;
    private DateTimeOffset _shakedownStart;
    private DateTimeOffset _shakedownEnd;
    private int _lastStageId;
    private string _lastStageStatus = "";
    private TimingSnapshot _lastPublished = seed;
    private TimingSnapshot _current = seed;

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_runCts is not null) return Task.CompletedTask;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MotorsportTaskbar/1.0");
        _ = Task.Run(() => RunAsync(_runCts.Token), _runCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_runCts is null) return;
        await _runCts.CancelAsync();
        _runCts.Dispose(); _runCts = null;
        ConnectionChanged?.Invoke(ConnectionState.Disconnected);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                ConnectionChanged?.Invoke(ConnectionState.Connecting);
                await LoadEventAsync(ct);
                ConnectionChanged?.Invoke(ConnectionState.Connected);
                attempt = 0;
                while (!ct.IsCancellationRequested)
                {
                    await PollAsync(ct);
                    await Task.Delay(TimeSpan.FromSeconds(15), ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Publish(_lastPublished with { ConnectionState = ConnectionState.Faulted, FreshnessTimestamp = clock.UtcNow });
                Failed?.Invoke(new($"WRC feed failed: {ex.Message}", ex, clock.UtcNow));
                ConnectionChanged?.Invoke(ConnectionState.Faulted);
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt++, 5))));
                try { await Task.Delay(delay, ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task LoadEventAsync(CancellationToken ct)
    {
        var season = await GetAsync<JsonObject>("season-detail.json?seasonId=47", ct) ?? throw new InvalidOperationException("WRC season metadata was empty.");
        var round = (season["seasonRounds"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(x => JsonSupport.String(x["event"]?["name"])?.Contains("Estonia", StringComparison.OrdinalIgnoreCase) == true)
            ?? throw new InvalidOperationException("Rally Estonia was not found in the WRC schedule.");
        var eventNode = round["event"] as JsonObject ?? throw new InvalidOperationException("Rally Estonia event metadata was empty.");
        _eventId = JsonSupport.Int(eventNode["eventId"]) ?? throw new InvalidOperationException("Rally Estonia event id was missing.");
        _meeting = JsonSupport.String(eventNode["name"]) ?? "Rally Estonia";
        var finish = DateOnly.TryParse(JsonSupport.String(eventNode["finishDate"]), out var date) ? date : DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var start = DateOnly.TryParse(JsonSupport.String(eventNode["startDate"]), out var eventDate) ? eventDate : date;
        var offsetMinutes = JsonSupport.Int(eventNode["timeZoneOffset"]) ?? 180;
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        _eventEnd = new DateTimeOffset(finish.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
        _shakedownStart = new DateTimeOffset(start.ToDateTime(new TimeOnly(8, 1)), offset).ToUniversalTime();
        _shakedownEnd = _shakedownStart.AddHours(2);
        var detail = await GetAsync<JsonObject>($"events/{_eventId}.json", ct) ?? throw new InvalidOperationException("Rally detail was empty.");
        var rally = (detail["rallies"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(x => JsonSupport.Bool(x["isMain"]))
            ?? (detail["rallies"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault()
            ?? throw new InvalidOperationException("Rally entry was missing.");
        _rallyId = JsonSupport.Int(rally["rallyId"]) ?? throw new InvalidOperationException("Rally id was missing.");
        var entries = await GetAsync<JsonArray>($"events/{_eventId}/rallies/{_rallyId}/entries.json", ct) ?? [];
        _entries.Clear();
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var id = JsonSupport.Int(entry["entryId"]);
            if (id.HasValue) _entries[id.Value] = entry;
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (now < _shakedownStart || now >= _eventEnd) { Publish(TimingSnapshot.Hidden(now)); return; }
        if (now < _shakedownEnd) { PublishShakedown(now); return; }
        var stages = await GetAsync<JsonArray>($"events/{_eventId}/stages.json", ct) ?? [];
        var stage = stages.OfType<JsonObject>().FirstOrDefault(x => JsonSupport.String(x["status"]) == "Running")
            ?? stages.OfType<JsonObject>().Where(x => JsonSupport.String(x["status"]) == "Completed").LastOrDefault()
            ?? stages.OfType<JsonObject>().FirstOrDefault();
        if (stage is null) { Publish(TimingSnapshot.Hidden(clock.UtcNow)); return; }
        var stageId = JsonSupport.Int(stage["stageId"]) ?? 0;
        var status = JsonSupport.String(stage["status"]) ?? "ToRun";
        DeltaReceived?.Invoke(new("WrcStage", stage.DeepClone(), now));
        if (status is not ("Running" or "Completed")) { Publish(TimingSnapshot.Hidden(now)); return; }
        var code = JsonSupport.String(stage["code"]) ?? "SS";
        var location = JsonSupport.String(stage["name"]) ?? JsonSupport.String(stage["location"]) ?? "";
        JsonArray times;
        if (status == "Running") times = await GetAsync<JsonArray>($"events/{_eventId}/stages/{stageId}/stagetimes.json?rallyId={_rallyId}", ct) ?? [];
        else times = await GetAsync<JsonArray>($"events/{_eventId}/stages/{stageId}/results.json?rallyId={_rallyId}", ct) ?? [];
        var standings = BuildStandings(times);
        var lifecycle = SessionLifecycle.Live;
        if (_lastStageId != stageId || _lastStageStatus != status)
        {
            if (status == "Running") alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{code} STARTED", location, null, clock.UtcNow, $"wrc:stage:start:{stageId}", TimeSpan.FromSeconds(4)));
            if (status == "Completed") alerts.Accept(new(AlertKind.Information, 10, false, $"{code} COMPLETE", location, null, clock.UtcNow, $"wrc:stage:complete:{stageId}", TimeSpan.FromSeconds(4)));
            _lastStageId = stageId; _lastStageStatus = status;
        }
        Publish(new(_meeting, $"{code}  {location}", "", 0, null, TrackCondition.AllClear, standings, clock.UtcNow, lifecycle, ConnectionState.Connected));
    }

    private void PublishShakedown(DateTimeOffset now)
    {
        var standings = _entries.Values.OrderBy(x => JsonSupport.Int(x["entryListOrder"]) ?? int.MaxValue).Select((entry, index) =>
        {
            var driver = entry["driver"] as JsonObject;
            return new CompetitorStanding(JsonSupport.String(entry["entryId"]) ?? index.ToString(CultureInfo.InvariantCulture), index + 1, JsonSupport.String(driver?["code"]) ?? JsonSupport.String(entry["identifier"]) ?? "—", JsonSupport.String(driver?["fullName"]) ?? "", JsonSupport.String(entry["manufacturer"]?["name"]) ?? "", index == 0 ? "LEAD" : null, null, null, 0, false, false, false, false);
        }).ToList();
        Publish(new(_meeting, "SHAKEDOWN  Kastre", "Kastre", 0, null, TrackCondition.AllClear, standings, now, SessionLifecycle.Live, ConnectionState.Connected));
    }

    private IReadOnlyList<CompetitorStanding> BuildStandings(JsonArray times)
    {
        var timesByEntry = times.OfType<JsonObject>()
            .Select(time => (Id: JsonSupport.Int(time["entryId"]), Time: time))
            .Where(item => item.Id.HasValue)
            .GroupBy(item => item.Id!.Value)
            .ToDictionary(group => group.Key, group => group.Last().Time);
        var classifiedCount = timesByEntry.Values.Count(time => JsonSupport.Int(time["position"]).HasValue);
        var unclassifiedIndex = 0;
        return _entries.Values
            .Select(entry =>
            {
                var entryId = JsonSupport.Int(entry["entryId"]) ?? 0;
                timesByEntry.TryGetValue(entryId, out var time);
                return (Entry: entry, EntryId: entryId, Time: time, Position: JsonSupport.Int(time?["position"]),
                    Order: JsonSupport.Int(entry["entryListOrder"]) ?? int.MaxValue,
                    StateOrder: StageStateOrder(JsonSupport.String(time?["status"])));
            })
            .OrderBy(item => item.Position ?? int.MaxValue)
            .ThenBy(item => item.StateOrder)
            .ThenBy(item => item.Order)
            .Select(item =>
            {
                var driver = item.Entry["driver"] as JsonObject;
                var stageStatus = JsonSupport.String(item.Time?["status"]);
                var sortPosition = item.Position ?? classifiedCount + ++unclassifiedIndex;
                return new CompetitorStanding(item.EntryId.ToString(CultureInfo.InvariantCulture), sortPosition,
                    JsonSupport.String(driver?["code"]) ?? JsonSupport.String(item.Entry["identifier"]) ?? "—",
                    JsonSupport.String(driver?["fullName"]) ?? "", JsonSupport.String(item.Entry["manufacturer"]?["name"]) ?? "",
                    item.Position == 1 ? "LEAD" : FormatGap(JsonSupport.Int(item.Time?["diffFirstMs"])),
                    item.Position == 1 ? null : FormatGap(JsonSupport.Int(item.Time?["diffPrevMs"])), null, 0,
                    false, IsRetired(stageStatus), false, false,
                    FormatElapsed(JsonSupport.Int(item.Time?["elapsedDurationMs"])), StageStatusLabel(stageStatus),
                    item.Position?.ToString(CultureInfo.InvariantCulture) ?? "—", RallyCategory(item.Entry));
            })
            .ToList();
    }

    private static string RallyCategory(JsonObject entry) => JsonSupport.String(entry["group"]?["name"]) switch
    {
        "Rally1" => "WRC1",
        "Rally2" => "WRC2",
        "Rally3" => "WRC3",
        "Rally4" => "WRC4",
        { Length: > 0 } group => group.ToUpperInvariant(),
        _ => "—"
    };

    private static int StageStateOrder(string? status) => status switch { "Running" => 0, "ToRun" or null => 1, _ => 2 };
    private static string? StageStatusLabel(string? status) => status switch { "Running" => "RUN", "ToRun" or null => "DUE", _ => null };
    private static string? FormatElapsed(int? ms)
    {
        if (!ms.HasValue) return null;
        var value = TimeSpan.FromMilliseconds(ms.Value);
        return $"{(int)value.TotalMinutes}:{value.Seconds:00}.{value.Milliseconds / 100:0}";
    }

    private static bool IsRetired(string? status) => status is not null && (status.Equals("DNF", StringComparison.OrdinalIgnoreCase) || status.Equals("Retired", StringComparison.OrdinalIgnoreCase) || status.Equals("DSQ", StringComparison.OrdinalIgnoreCase));
    private static string? FormatGap(int? ms) => ms is null || ms == 0 ? null : FormatDuration(ms.Value);
    private static string FormatDuration(int ms)
    {
        var value = TimeSpan.FromMilliseconds(Math.Abs(ms));
        return $"+{(int)value.TotalMinutes}:{value.Seconds:00}.{value.Milliseconds / 100:0}";
    }
    private void Publish(TimingSnapshot value) { _lastPublished = value; SnapshotReceived?.Invoke(value); }
    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : JsonNode => await _http.GetFromJsonAsync<T>($"{ApiBase}/{path}", ct);
    public async ValueTask DisposeAsync() { await StopAsync(); _http.Dispose(); }
}
