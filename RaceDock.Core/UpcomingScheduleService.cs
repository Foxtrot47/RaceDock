// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed class Formula1Calendar(F1ScheduleClient client) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [Championship.Formula1];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await client.GetSessionsAsync(now, cancellationToken);
        return ChampionshipCalendars.NextFormula1(sessions, now) is { } next ? [next] : [];
    }

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.Formula1Weekend(await client.GetSessionsAsync(now, cancellationToken), now);
}

internal sealed class MotorcycleGrandPrixCalendar(MotoGpScheduleClient client) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [Championship.MotoGp, Championship.Moto2, Championship.Moto3];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.NextMotorcycleGrandPrix(await client.GetEventsAsync(now, cancellationToken), now);

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.MotorcycleGrandPrixWeekend(await client.GetEventsAsync(now, cancellationToken), now);
}

internal sealed class GriiipCalendar(IGriiipApiClient client, GriiipSeries series) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [series.Championship];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await client.GetSessionsAsync(now, cancellationToken);
        var next = ChampionshipCalendars.NextGriiip(sessions, now, series);
        return next is null ? [] : [next];
    }

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.GriiipEvent(await client.GetSessionsAsync(now, cancellationToken), now, series);
}

internal sealed class GtWorldChallengeCalendar(GtwcApiClient client) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [Championship.GtWorldChallengeEurope];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var meeting = await NextMeetingScheduleAsync(now, cancellationToken);
        return meeting is null ? [] : [ChampionshipCalendars.NextGtWorldChallenge(meeting.Value.Meeting, meeting.Value.Schedule, now)];
    }

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var meeting = await NextMeetingScheduleAsync(now, cancellationToken);
        return meeting is null ? [] : ChampionshipCalendars.GtWorldChallengeMeeting(meeting.Value.Meeting, meeting.Value.Schedule, now);
    }

    private async Task<(ChampionshipCalendars.GtwcMeeting Meeting, JsonObject? Schedule)?> NextMeetingScheduleAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seasons = await client.GetSeasonsAsync(cancellationToken);
        var year = JsonSupport.String(seasons.Full["CurrentSeason"]);
        if (string.IsNullOrWhiteSpace(year)) return null;
        var season = await client.GetSeasonAsync(year, cancellationToken);
        if (ChampionshipCalendars.NextGtWorldChallengeMeeting(season.Full, now) is not { } meeting) return null;
        return (meeting, await TryGetScheduleAsync(year, meeting.Id, cancellationToken));
    }

    /// <summary>A missing timetable still leaves the round worth showing, so it degrades to the meeting dates.</summary>
    private async Task<JsonObject?> TryGetScheduleAsync(string year, string meetingId, CancellationToken cancellationToken)
    {
        try
        {
            return (await client.GetScheduleAsync(year, meetingId, cancellationToken)).Full;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            AppLog.Warning("Schedule", $"GT World Challenge meeting {meetingId} published no timetable.", failure);
            return null;
        }
    }
}

/// <summary>
/// The WRC calendar carries round windows only, so the selected round is resolved to its name. A rally that
/// is running keeps its place ahead of the following round.
/// </summary>
internal sealed class WrcCalendar(WrcApiClient client) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [Championship.WorldRallyChampionship];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rally = await NextRallyAsync(now, cancellationToken);
        return rally is null
            ? []
            : [ChampionshipCalendars.NextWorldRally(rally.Value.Name, rally.Value.Start, rally.Value.Stages, now, rally.Value.Location)];
    }

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rally = await NextRallyAsync(now, cancellationToken);
        return rally is null
            ? []
            : ChampionshipCalendars.WorldRally(rally.Value.Name, rally.Value.Start, rally.Value.Stages, now, rally.Value.Location);
    }

    private async Task<(string Name, DateTimeOffset Start, IReadOnlyList<(string Code, string Name, DateTimeOffset? StartUtc)> Stages, string? Location)?>
        NextRallyAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seasons = WrcTiming.OrderSeasons(await client.GetSeasonsAsync(cancellationToken), now.Year);
        foreach (var season in seasons)
        {
            var events = await client.GetSeasonEventsAsync(season.SeasonId, cancellationToken);
            var selection = WrcTiming.SelectEvent(events, now);
            if ((selection.Current ?? selection.Next) is not { } next) continue;
            var detail = await client.GetEventAsync(next.EventId, cancellationToken);
            var stages = (await TryGetStagesAsync(next.EventId, cancellationToken)).ToList();
            return (detail.Name, next.StartUtc, stages, detail.Location);
        }
        return null;
    }

    /// <summary>An itinerary is only published close to the rally, so its absence keeps the rally start.</summary>
    private async Task<IEnumerable<(string Code, string Name, DateTimeOffset? StartUtc)>> TryGetStagesAsync(int eventId, CancellationToken cancellationToken)
    {
        try
        {
            var stages = await client.GetStagesAsync(eventId, cancellationToken);
            return stages.Select(stage => (stage.Code, stage.Name, stage.StartUtc)).ToList();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            AppLog.Warning("Schedule", $"WRC event {eventId} published no itinerary.", failure);
            return [];
        }
    }
}

public sealed class MotoGpScheduleClient(HttpClient? httpClient = null) : IDisposable
{
    private readonly HttpClient _http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://api.pulselive.motogp.com/motogp/v1/") };
    private readonly bool _ownsHttp = httpClient is null;

    public async Task<JsonArray?> GetEventsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync($"events?seasonYear={now.Year}&isFinished=false", cancellationToken);
        return JsonNode.Parse(text) as JsonArray;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}

/// <summary>
/// Collects the next published event for the championships the user enabled. A provider is contacted only
/// when it covers an enabled championship, matching the rule that enabling a championship is what causes a
/// connection. Sources are queried concurrently and independently: a slow, broken, or offline provider drops
/// out of the result instead of failing the whole list, because this only feeds an informational section.
/// </summary>
public sealed class UpcomingScheduleService : IDisposable
{
    private readonly IReadOnlyList<IChampionshipCalendar> _calendars;
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly IClock _clock;
    private readonly TimeSpan _cacheLifetime;
    private readonly TimeSpan _sourceTimeout;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private readonly CalendarCache _upcoming = new();
    private readonly CalendarCache _eventSchedules = new();
    private bool _disposed;

    public UpcomingScheduleService(IClock? clock = null, TimeSpan? cacheLifetime = null, TimeSpan? sourceTimeout = null)
    {
        _clock = clock ?? new SystemClock();
        _cacheLifetime = cacheLifetime ?? TimeSpan.FromHours(6);
        _sourceTimeout = sourceTimeout ?? TimeSpan.FromSeconds(15);
        var f1 = new F1ScheduleClient();
        var motogp = new MotoGpScheduleClient();
        var dtm = new GriiipApiClient(GriiipSeries.Dtm);
        var wec = new GriiipApiClient(GriiipSeries.Wec);
        var elms = new GriiipApiClient(GriiipSeries.Elms);
        var michelinLeMansCup = new GriiipApiClient(GriiipSeries.MichelinLeMansCup);
        var gtwc = new GtwcApiClient();
        var wrc = new WrcApiClient();
        _owned = [f1, motogp, dtm, wec, elms, michelinLeMansCup, gtwc, wrc];
        _calendars =
        [
            new Formula1Calendar(f1),
            new MotorcycleGrandPrixCalendar(motogp),
            new WrcCalendar(wrc),
            new GriiipCalendar(dtm, GriiipSeries.Dtm),
            new GriiipCalendar(wec, GriiipSeries.Wec),
            new GriiipCalendar(elms, GriiipSeries.Elms),
            new GriiipCalendar(michelinLeMansCup, GriiipSeries.MichelinLeMansCup),
            new GtWorldChallengeCalendar(gtwc),
        ];
    }

    internal UpcomingScheduleService(IReadOnlyList<IChampionshipCalendar> calendars, IClock clock, TimeSpan cacheLifetime, TimeSpan sourceTimeout)
    {
        _calendars = calendars;
        _owned = [];
        _clock = clock;
        _cacheLifetime = cacheLifetime;
        _sourceTimeout = sourceTimeout;
    }

    public Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(IReadOnlySet<Championship> enabled, CancellationToken cancellationToken) =>
        GetAsync(_upcoming, enabled, (calendar, now, token) => calendar.GetUpcomingAsync(now, token), cancellationToken);

    /// <summary>
    /// Every remaining session of each enabled championship's next event, soonest first, cached and isolated per
    /// source like <see cref="GetUpcomingAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<UpcomingSession>> GetEventSchedulesAsync(IReadOnlySet<Championship> enabled, CancellationToken cancellationToken) =>
        GetAsync(_eventSchedules, enabled, (calendar, now, token) => calendar.GetEventScheduleAsync(now, token), cancellationToken);

    private async Task<IReadOnlyList<UpcomingSession>> GetAsync(
        CalendarCache cache,
        IReadOnlySet<Championship> enabled,
        Func<IChampionshipCalendar, DateTimeOffset, CancellationToken, Task<IReadOnlyList<UpcomingSession>>> read,
        CancellationToken cancellationToken)
    {
        if (IsFresh(cache, enabled)) return cache.Sessions;
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh(cache, enabled)) return cache.Sessions;
            var now = _clock.UtcNow;
            var sources = _calendars.Where(calendar => calendar.Championships.Any(enabled.Contains));
            var results = await Task.WhenAll(sources.Select(calendar => QueryAsync(calendar, now, read, cancellationToken)));
            cache.Sessions = results
                .SelectMany(sessions => sessions)
                .Where(session => enabled.Contains(session.Championship) && session.StartUtc > now)
                .OrderBy(session => session.StartUtc)
                .ToList();
            cache.For = new HashSet<Championship>(enabled);
            cache.At = _clock.UtcNow;
            return cache.Sessions;
        }
        finally { _refresh.Release(); }
    }

    private bool IsFresh(CalendarCache cache, IReadOnlySet<Championship> enabled) =>
        _clock.UtcNow - cache.At < _cacheLifetime && cache.For.SetEquals(enabled) &&
        cache.Sessions.All(session => session.StartUtc > _clock.UtcNow);

    private async Task<IReadOnlyList<UpcomingSession>> QueryAsync(
        IChampionshipCalendar calendar,
        DateTimeOffset now,
        Func<IChampionshipCalendar, DateTimeOffset, CancellationToken, Task<IReadOnlyList<UpcomingSession>>> read,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_sourceTimeout);
        try
        {
            return await read(calendar, now, timeout.Token);
        }
        catch (Exception failure) when (failure is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            AppLog.Warning("Schedule", $"{calendar.GetType().Name} did not return a calendar.", failure);
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var client in _owned) client.Dispose();
        _refresh.Dispose();
    }

    /// <summary>One cached calendar result and the championship set it was read for.</summary>
    private sealed class CalendarCache
    {
        public IReadOnlyList<UpcomingSession> Sessions = [];
        public IReadOnlySet<Championship> For = new HashSet<Championship>();
        public DateTimeOffset At = DateTimeOffset.MinValue;
    }
}
