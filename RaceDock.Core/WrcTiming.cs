// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record WrcSeason(int SeasonId, string Name, int Year);

internal sealed record WrcCalendarEvent(int EventId, DateTimeOffset StartUtc, DateTimeOffset EndUtc);

internal sealed record WrcEventDetail(
    int EventId,
    string Name,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    int ShakedownCount,
    IReadOnlyList<WrcRally> Rallies);

internal sealed record WrcRally(int RallyId, int? ItineraryId, bool IsMain);

internal sealed record WrcEntry(
    int EntryId,
    int Order,
    string Code,
    string Name,
    string Manufacturer,
    string Group,
    string Eligibility,
    string EventClass,
    string Status);

internal enum WrcStageStatus { Unknown, ToRun, Running, Completed, Interrupted, Cancelled }

internal sealed record WrcStage(
    int StageId,
    int Number,
    string Code,
    string Name,
    WrcStageStatus Status,
    DateTimeOffset? StartUtc,
    JsonObject Raw);

internal sealed record WrcStageTime(
    int EntryId,
    int? Position,
    int? ElapsedDurationMs,
    int? DiffFirstMs,
    int? DiffPrevMs,
    string Status);

internal sealed record WrcStageResult(int EntryId, int? Position, int? TotalTimeMs);
internal sealed record WrcShakedownTime(int EntryId, int RunNumber, int? DurationMs);
internal sealed record WrcEventSelection(WrcCalendarEvent? Current, WrcCalendarEvent? Next);
internal sealed record WrcEventContext(WrcEventDetail Event, WrcRally Rally, IReadOnlyDictionary<int, WrcEntry> Entries);

internal sealed record WrcPollingOptions(
    TimeSpan PollInterval,
    TimeSpan IdlePollInterval,
    TimeSpan StageStartLead,
    TimeSpan ScheduleRefreshInterval,
    TimeSpan MetadataRefreshInterval,
    TimeSpan ShakedownQuietGrace,
    int ShakedownProbeLimit,
    int ResultStablePolls,
    IReadOnlyList<TimeSpan> DiscoveryBackoff,
    IReadOnlyList<TimeSpan> PollBackoff)
{
    public static WrcPollingOptions Default { get; } = new(
        TimeSpan.FromSeconds(15),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(2),
        5,
        3,
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30)],
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)]);
}

internal static class WrcTiming
{
    private static readonly string[] ChampionshipNames = ["WRC", "World Rally Championship", "FIA World Rally Championship"];

    public static IReadOnlyList<WrcSeason> OrderSeasons(IReadOnlyList<WrcSeason> seasons, int year) => seasons
        .Where(season => ChampionshipNames.Contains(season.Name.Trim(), StringComparer.OrdinalIgnoreCase))
        .OrderBy(season => season.Year == year ? 0 : season.Year > year ? 1 : 2)
        .ThenBy(season => season.Year > year ? season.Year : -season.Year)
        .ToList();

    public static WrcEventSelection SelectEvent(IReadOnlyList<WrcCalendarEvent> events, DateTimeOffset now)
    {
        var current = events
            .Where(item => item.StartUtc <= now && now < item.EndUtc)
            .OrderByDescending(item => item.StartUtc)
            .FirstOrDefault();
        var next = events
            .Where(item => item.StartUtc > now)
            .OrderBy(item => item.StartUtc)
            .FirstOrDefault();
        return new(current, next);
    }

    public static (DateTimeOffset Start, DateTimeOffset End) EventWindow(
        DateOnly start,
        DateOnly finish,
        string? timeZoneId,
        int? offsetMinutes)
    {
        if (finish < start) throw new InvalidOperationException("WRC event finish date preceded its start date.");
        var startLocal = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = finish.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return (new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(startLocal, zone), TimeSpan.Zero),
                    new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(endLocal, zone), TimeSpan.Zero));
            }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        if (!offsetMinutes.HasValue) throw new InvalidOperationException("WRC event timezone metadata was missing.");
        var offset = TimeSpan.FromMinutes(offsetMinutes.Value);
        return (new DateTimeOffset(startLocal, offset).ToUniversalTime(), new DateTimeOffset(endLocal, offset).ToUniversalTime());
    }

    public static WrcRally SelectMainRally(IReadOnlyList<WrcRally> rallies) =>
        rallies.FirstOrDefault(rally => rally.IsMain)
        ?? rallies.FirstOrDefault()
        ?? throw new InvalidOperationException("WRC event did not contain a rally.");

    public static WrcStage? SelectRunningStage(IReadOnlyList<WrcStage> stages) => stages
        .Where(stage => stage.Status == WrcStageStatus.Running)
        .OrderByDescending(stage => stage.Number)
        .FirstOrDefault();

    public static IReadOnlyList<WrcStage> DueStages(IReadOnlyList<WrcStage> stages, DateTimeOffset now) => stages
        .Where(stage => stage.Status == WrcStageStatus.ToRun && stage.StartUtc.HasValue && stage.StartUtc.Value <= now)
        .OrderByDescending(stage => stage.StartUtc)
        .ThenByDescending(stage => stage.Number)
        .ToList();

    public static WrcStage? SelectNextScheduledStage(IReadOnlyList<WrcStage> stages, DateTimeOffset now) => stages
        .Where(stage => (stage.Status is WrcStageStatus.ToRun or WrcStageStatus.Unknown) &&
            stage.StartUtc.HasValue && stage.StartUtc.Value > now)
        .OrderBy(stage => stage.StartUtc)
        .ThenBy(stage => stage.Number)
        .ThenBy(stage => stage.StageId)
        .FirstOrDefault();

    public static TimeSpan NextPollDelay(
        DateTimeOffset now,
        bool activityActive,
        WrcStage? nextStage,
        DateTimeOffset nextMetadataRefresh,
        DateTimeOffset eventEnd,
        WrcPollingOptions options)
    {
        var delay = activityActive ? options.PollInterval : options.IdlePollInterval;
        if (!activityActive && nextStage?.StartUtc is { } start)
        {
            var untilLead = start - options.StageStartLead - now;
            delay = untilLead > TimeSpan.Zero ? Min(delay, untilLead) : Min(delay, options.PollInterval);
        }
        delay = Min(delay, nextMetadataRefresh - now);
        delay = Min(delay, eventEnd - now);
        return delay;
    }

    public static IReadOnlyList<CompetitorStanding> BuildStageStandings(
        IReadOnlyDictionary<int, WrcEntry> entries,
        IReadOnlyList<WrcStageTime> times)
    {
        var timesByEntry = times
            .GroupBy(time => time.EntryId)
            .ToDictionary(group => group.Key, group => group.Last());
        var classified = timesByEntry.Values.Where(time => time.Position.HasValue).ToList();
        var maxPosition = classified.Count == 0 ? 0 : classified.Max(time => time.Position!.Value);
        var unclassified = 0;
        return entries.Values
            .Select(entry =>
            {
                timesByEntry.TryGetValue(entry.EntryId, out var time);
                var sortPosition = time?.Position ?? maxPosition + ++unclassified;
                return (Entry: entry, Time: time, SortPosition: sortPosition, State: StageStateOrder(time?.Status));
            })
            .OrderBy(item => item.Time?.Position ?? int.MaxValue)
            .ThenBy(item => item.State)
            .ThenBy(item => item.Entry.Order)
            .Select(item => new CompetitorStanding(
                item.Entry.EntryId.ToString(CultureInfo.InvariantCulture),
                item.SortPosition,
                item.Entry.Code,
                item.Entry.Name,
                item.Entry.Manufacturer,
                item.Time?.Position == 1 ? "LEAD" : FormatGap(item.Time?.DiffFirstMs),
                item.Time?.Position == 1 ? null : FormatGap(item.Time?.DiffPrevMs),
                null,
                0,
                false,
                IsRetired(item.Time?.Status) || IsRetired(item.Entry.Status),
                false,
                false,
                FormatElapsed(item.Time?.ElapsedDurationMs),
                StageStatusLabel(item.Time?.Status),
                item.Time?.Position?.ToString(CultureInfo.InvariantCulture) ?? "—",
                RallyCategory(item.Entry)))
            .ToList();
    }

    public static IReadOnlyList<CompetitorStanding> BuildShakedownStandings(
        IReadOnlyDictionary<int, WrcEntry> entries,
        IReadOnlyList<WrcShakedownTime> times)
    {
        var best = times
            .Where(time => time.DurationMs.HasValue)
            .GroupBy(time => time.EntryId)
            .Select(group => group.OrderBy(time => time.DurationMs).ThenBy(time => time.RunNumber).First())
            .OrderBy(time => time.DurationMs)
            .ToList();
        if (best.Count == 0) return [];
        var leader = best[0].DurationMs!.Value;
        return best.Select((time, index) =>
        {
            entries.TryGetValue(time.EntryId, out var entry);
            var duration = time.DurationMs!.Value;
            return new CompetitorStanding(
                time.EntryId.ToString(CultureInfo.InvariantCulture),
                index + 1,
                entry?.Code ?? time.EntryId.ToString(CultureInfo.InvariantCulture),
                entry?.Name ?? "",
                entry?.Manufacturer ?? "",
                index == 0 ? "LEAD" : FormatGap(duration - leader),
                index == 0 ? null : FormatGap(duration - best[index - 1].DurationMs!.Value),
                null,
                0,
                false,
                false,
                false,
                false,
                FormatElapsed(duration),
                null,
                (index + 1).ToString(CultureInfo.InvariantCulture),
                entry is null ? "—" : RallyCategory(entry));
        }).ToList();
    }

    public static bool Wrc1ResultsComplete(
        IReadOnlyDictionary<int, WrcEntry> entries,
        IReadOnlyList<WrcStageTime> times,
        IReadOnlyList<WrcStageResult> results)
    {
        var expectedIds = entries.Values
            .Where(entry => RallyCategory(entry) == "WRC1" && !IsNonStarter(entry.Status) && !IsRetired(entry.Status))
            .Select(entry => entry.EntryId)
            .ToHashSet();
        if (expectedIds.Count == 0) return false;
        var latestTimes = times.GroupBy(time => time.EntryId).ToDictionary(group => group.Key, group => group.Last());
        var classified = results.Select(result => result.EntryId).ToHashSet();
        foreach (var id in expectedIds)
        {
            if (!latestTimes.TryGetValue(id, out var time) || !IsTerminalTimingStatus(time.Status)) return false;
            if (!IsNonStarter(time.Status) && !IsRetired(time.Status) && !classified.Contains(id)) return false;
        }
        return true;
    }

    public static string StageFingerprint(WrcStage stage, IReadOnlyList<WrcStageTime> times, IReadOnlyList<WrcStageResult> results) =>
        string.Join('|',
            stage.StageId,
            stage.Status,
            string.Join(',', times
                .OrderBy(time => time.EntryId)
                .ThenBy(time => time.Position)
                .ThenBy(time => time.ElapsedDurationMs)
                .ThenBy(time => time.Status, StringComparer.Ordinal)
                .Select(time => $"{time.EntryId}:{time.Position}:{time.ElapsedDurationMs}:{time.Status}")),
            string.Join(',', results
                .OrderBy(result => result.EntryId)
                .ThenBy(result => result.Position)
                .ThenBy(result => result.TotalTimeMs)
                .Select(result => $"{result.EntryId}:{result.Position}:{result.TotalTimeMs}")));

    public static string ShakedownFingerprint(IReadOnlyList<WrcShakedownTime> times) => string.Join(',', times
        .OrderBy(time => time.EntryId)
        .ThenBy(time => time.RunNumber)
        .Select(time => $"{time.EntryId}:{time.RunNumber}:{time.DurationMs}"));

    public static string RallyCategory(WrcEntry entry)
    {
        if (entry.Eligibility.StartsWith("WRC2", StringComparison.OrdinalIgnoreCase)) return "WRC2";
        if (entry.Eligibility.StartsWith("WRC3", StringComparison.OrdinalIgnoreCase)) return "WRC3";
        if (entry.Group.Equals("Rally1", StringComparison.OrdinalIgnoreCase)) return "WRC1";
        if (!string.IsNullOrWhiteSpace(entry.EventClass)) return entry.EventClass.ToUpperInvariant();
        return string.IsNullOrWhiteSpace(entry.Group) ? "—" : entry.Group.ToUpperInvariant();
    }

    public static WrcStageStatus ParseStageStatus(string? value) => NormalizeStatus(value) switch
    {
        "TORUN" => WrcStageStatus.ToRun,
        "RUNNING" => WrcStageStatus.Running,
        "COMPLETED" => WrcStageStatus.Completed,
        "INTERRUPTED" => WrcStageStatus.Interrupted,
        "CANCELLED" or "CANCELED" => WrcStageStatus.Cancelled,
        _ => WrcStageStatus.Unknown
    };

    public static DateTimeOffset? ParseUtcTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? result : null;

    private static int StageStateOrder(string? status) => NormalizeStatus(status) switch
    {
        "RUNNING" => 0,
        "TORUN" or "" => 1,
        _ => 2
    };

    private static string? StageStatusLabel(string? status) => NormalizeStatus(status) switch
    {
        "RUNNING" => "RUN",
        "TORUN" or "" => "DUE",
        "DNS" or "NONSTARTER" => "DNS",
        "DNF" or "RETIRED" => "DNF",
        "DSQ" => "DSQ",
        _ => null
    };

    private static bool IsRetired(string? status) => NormalizeStatus(status) is "DNF" or "RETIRED" or "DSQ";
    private static bool IsNonStarter(string? status) => NormalizeStatus(status) is "DNS" or "NONSTARTER";
    private static bool IsTerminalTimingStatus(string? status) => NormalizeStatus(status) is "COMPLETED" or "DNS" or "NONSTARTER" or "DNF" or "RETIRED" or "DSQ";
    private static string NormalizeStatus(string? status) => status?.Trim().ToUpperInvariant() ?? "";
    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static string? FormatGap(int? ms) => ms is null || ms == 0 ? null : FormatDuration(ms.Value);
    private static string? FormatElapsed(int? ms)
    {
        if (!ms.HasValue) return null;
        var value = TimeSpan.FromMilliseconds(ms.Value);
        return $"{(int)value.TotalMinutes}:{value.Seconds:00}.{value.Milliseconds / 100:0}";
    }
    private static string FormatDuration(int ms)
    {
        var value = TimeSpan.FromMilliseconds(Math.Abs(ms));
        return $"+{(int)value.TotalMinutes}:{value.Seconds:00}.{value.Milliseconds / 100:0}";
    }
}
