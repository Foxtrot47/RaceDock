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
    private readonly HashSet<int> _completedStages = [];
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
        var stageList = stages.OfType<JsonObject>().ToList();
        var stage = stageList
            .Where(value => !IsCompletedStage(value) && JsonSupport.String(value["status"]) == "Running")
            .OrderByDescending(value => JsonSupport.Int(value["number"]) ?? 0)
            .FirstOrDefault()
            ?? stageList
                .Select(value => (Value: value, Start: StageStartUtc(value)))
                .Where(item => !IsCompletedStage(item.Value) && JsonSupport.String(item.Value["status"]) == "ToRun" && item.Start.HasValue && item.Start.Value <= now)
                .OrderByDescending(item => item.Start)
                .Select(item => item.Value)
                .FirstOrDefault();
        if (stage is null) { Publish(TimingSnapshot.Hidden(now)); return; }
        var stageId = JsonSupport.Int(stage["stageId"]) ?? 0;
        var status = "Running";
        DeltaReceived?.Invoke(new("WrcStage", stage.DeepClone(), now));
        var code = JsonSupport.String(stage["code"]) ?? "SS";
        var location = JsonSupport.String(stage["name"]) ?? JsonSupport.String(stage["location"]) ?? "";
        var times = await GetAsync<JsonArray>($"events/{_eventId}/stages/{stageId}/stagetimes.json?rallyId={_rallyId}", ct) ?? [];
        var finalResults = await GetAsync<JsonArray>($"events/{_eventId}/stages/{stageId}/results.json?rallyId={_rallyId}", ct) ?? [];
        if (Wrc1ResultsComplete(_entries, times, finalResults)) { _completedStages.Add(stageId); Publish(TimingSnapshot.Hidden(now)); return; }
        var standings = BuildStandings(_entries, times);
        if (_lastStageId != stageId || _lastStageStatus != status)
        {
            alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{code} STARTED", location, null, clock.UtcNow, $"wrc:stage:start:{stageId}", TimeSpan.FromSeconds(4)));
            _lastStageId = stageId; _lastStageStatus = status;
        }
        Publish(new(_meeting, $"{code}  {location}", "", 0, null, TrackCondition.AllClear, standings, clock.UtcNow, SessionLifecycle.Live, ConnectionState.Connected, null, Championship.WorldRallyChampionship));
    }

    internal static bool Wrc1ResultsComplete(IReadOnlyDictionary<int, JsonObject> entries, JsonArray times, JsonArray finalResults)
    {
        var wrc1Ids = entries.Values
            .Where(entry => RallyCategory(entry) == "WRC1")
            .Select(entry => JsonSupport.Int(entry["entryId"]))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        var expected = times.OfType<JsonObject>()
            .Where(time => JsonSupport.Int(time["entryId"]) is int id && wrc1Ids.Contains(id) && JsonSupport.String(time["status"]) is not ("DNS" or "NonStarter"))
            .Select(time => JsonSupport.Int(time["entryId"])!.Value)
            .ToHashSet();
        var classified = finalResults.OfType<JsonObject>()
            .Select(result => JsonSupport.Int(result["entryId"]))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
        return expected.Count > 0 && expected.IsSubsetOf(classified);
    }

    private bool IsCompletedStage(JsonObject stage) => _completedStages.Contains(JsonSupport.Int(stage["stageId"]) ?? 0);

    internal static bool StageResultsComplete(JsonArray times, JsonArray finalResults)
    {
        var expected = times.OfType<JsonObject>().Count(time => JsonSupport.String(time["status"]) is not ("DNS" or "NonStarter"));
        return expected > 0 && finalResults.OfType<JsonObject>().Count() >= expected;
    }

    private static DateTimeOffset? StageStartUtc(JsonObject stage)
    {
        var raw = (stage["controls"] as JsonArray)?.OfType<JsonObject>()
            .FirstOrDefault(control => JsonSupport.String(control["type"]) == "StageStart")?["firstCarDueDateTime"];
        return DateTimeOffset.TryParse(JsonSupport.String(raw), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start) ? start : null;
    }

    private void PublishShakedown(DateTimeOffset now)
    {
        var standings = _entries.Values.OrderBy(x => JsonSupport.Int(x["entryListOrder"]) ?? int.MaxValue).Select((entry, index) =>
        {
            var driver = entry["driver"] as JsonObject;
            return new CompetitorStanding(JsonSupport.String(entry["entryId"]) ?? index.ToString(CultureInfo.InvariantCulture), index + 1, JsonSupport.String(driver?["code"]) ?? JsonSupport.String(entry["identifier"]) ?? "—", JsonSupport.String(driver?["fullName"]) ?? "", JsonSupport.String(entry["manufacturer"]?["name"]) ?? "", index == 0 ? "LEAD" : null, null, null, 0, false, false, false, false);
        }).ToList();
        Publish(new(_meeting, "SHAKEDOWN  Kastre", "Kastre", 0, null, TrackCondition.AllClear, standings, now, SessionLifecycle.Live, ConnectionState.Connected, null, Championship.WorldRallyChampionship));
    }

    internal static IReadOnlyList<CompetitorStanding> BuildStandings(IReadOnlyDictionary<int, JsonObject> entries, JsonArray times)
    {
        var timesByEntry = times.OfType<JsonObject>()
            .Select(time => (Id: JsonSupport.Int(time["entryId"]), Time: time))
            .Where(item => item.Id.HasValue)
            .GroupBy(item => item.Id!.Value)
            .ToDictionary(group => group.Key, group => group.Last().Time);
        var classifiedCount = timesByEntry.Values.Count(time => JsonSupport.Int(time["position"]).HasValue);
        var unclassifiedIndex = 0;
        return entries.Values
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

    internal static string RallyCategory(JsonObject entry)
    {
        var eligibility = JsonSupport.String(entry["eligibility"])?.Trim();
        if (eligibility?.StartsWith("WRC2", StringComparison.OrdinalIgnoreCase) == true) return "WRC2";
        if (eligibility?.StartsWith("WRC3", StringComparison.OrdinalIgnoreCase) == true) return "WRC3";
        var eventClass = (entry["eventClasses"] as JsonArray)?.OfType<JsonObject>()
            .Select(value => JsonSupport.String(value["name"]))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return JsonSupport.String(entry["group"]?["name"]) switch
        {
            "Rally1" => "WRC1",
            _ when eventClass is not null => eventClass.ToUpperInvariant(),
            { Length: > 0 } group => group.ToUpperInvariant(),
            _ => "—"
        };
    }

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
