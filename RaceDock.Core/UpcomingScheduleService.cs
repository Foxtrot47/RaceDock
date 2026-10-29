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
            : [ChampionshipCalendars.NextWorldRally(rally.Value.Name, rally.Value.Start, rally.Value.Stages, now,
                rally.Value.Location, rally.Value.Date)];
    }

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rally = await NextRallyAsync(now, cancellationToken);
        return rally is null
            ? []
            : ChampionshipCalendars.WorldRally(rally.Value.Name, rally.Value.Start, rally.Value.Stages, now,
                rally.Value.Location, rally.Value.Date);
    }

    private async Task<(string Name, DateTimeOffset Start, DateOnly? Date, IReadOnlyList<(string Code, string Name, DateTimeOffset? StartUtc)> Stages, string? Location)?>
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
            return (detail.Name, next.StartUtc, next.StartDate, stages, detail.Location);
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

/// <summary>
/// F1 Academy publishes no keyless schedule API; its calendar page embeds the season as Next.js page data, which
/// carries every round's sessions.
/// </summary>
internal sealed class F1AcademyCalendar(IF1AcademyScheduleClient client) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [Championship.F1Academy];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        (await GetEventScheduleAsync(now, cancellationToken)).Take(1).ToList();

    public async Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.F1AcademyRound(await client.GetRacesAsync(cancellationToken), now, TimeZoneInfo.Local);
}

internal interface IF1AcademyScheduleClient : IDisposable
{
    Task<JsonArray?> GetRacesAsync(CancellationToken cancellationToken);
}

public sealed class F1AcademyScheduleClient : IF1AcademyScheduleClient
{
    private readonly HttpClient _http;

    public F1AcademyScheduleClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://www.f1academy.com/") };
        // The site refuses a request that names no browser.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent.Value);
    }

    public async Task<JsonArray?> GetRacesAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("Racing-Series/Calendar", cancellationToken);
        response.EnsureSuccessStatusCode();
        return ReadRaces(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>The races in the page's <c>__NEXT_DATA__</c> block; a page without one is a layout this does not know.</summary>
    internal static JsonArray? ReadRaces(string html)
    {
        const string marker = "<script id=\"__NEXT_DATA__\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new InvalidDataException("The F1 Academy calendar page carries no __NEXT_DATA__ block.");
        start = html.IndexOf('>', start) + 1;
        var end = html.IndexOf("</script>", start, StringComparison.Ordinal);
        if (start <= 0 || end < 0) throw new InvalidDataException("The F1 Academy calendar page data is not closed.");
        return JsonNode.Parse(html[start..end])?["props"]?["pageProps"]?["pageData"]?["Races"] as JsonArray;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Each NASCAR series schedule is published one season at a time, so the next season is read once the current
/// one has no race left. A season the provider has not released yet answers without a race list.
/// </summary>
internal sealed class NascarCalendar(INascarScheduleClient client, NascarSeries series) : IChampionshipCalendar
{
    public IReadOnlyList<Championship> Championships { get; } = [series.Championship];

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await NextEventSessionsAsync(now, cancellationToken);
        return sessions.Count > 0 ? [sessions[0]] : [];
    }

    public Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        NextEventSessionsAsync(now, cancellationToken);

    private async Task<IReadOnlyList<UpcomingSession>> NextEventSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var year in new[] { now.Year, now.Year + 1 })
        {
            var races = await client.GetRaceListAsync(series.SeriesId, year, cancellationToken);
            if (ChampionshipCalendars.NextNascar(races, now, series.Championship) is null) continue;
            var raceId = ChampionshipCalendars.NextNascarRaceId(races, now, series.Championship);
            var feed = await client.GetWeekendFeedAsync(year, series.SeriesId, raceId, cancellationToken);
            var sessions = ChampionshipCalendars.NascarEvent(races, feed, now, series.Championship);
            if (sessions.Count > 0) return sessions;
        }
        return [];
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
/// The published NASCAR schedule, one series and season per static path: no identifier has to be discovered
/// first, and a season without a released list answers with an error status instead of a payload.
/// </summary>
internal interface INascarScheduleClient : IDisposable
{
    Task<JsonArray?> GetRaceListAsync(int seriesId, int year, CancellationToken cancellationToken);

    Task<JsonObject?> GetWeekendFeedAsync(int year, int seriesId, int? raceId, CancellationToken cancellationToken) =>
        Task.FromResult<JsonObject?>(null);
}

public sealed class NascarScheduleClient(HttpClient? httpClient = null) : INascarScheduleClient
{
    private readonly HttpClient _http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://cf.nascar.com/") };

    public async Task<JsonArray?> GetRaceListAsync(int seriesId, int year, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync($"cacher/{year}/{seriesId}/race_list_basic.json", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonNode.Parse(text) as JsonArray;
    }

    public async Task<JsonObject?> GetWeekendFeedAsync(int year, int seriesId, int? raceId, CancellationToken cancellationToken)
    {
        if (raceId is not > 0) return null;
        using var response = await _http.GetAsync($"cacher/{year}/{seriesId}/{raceId}/weekend-feed.json", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonNode.Parse(text) as JsonObject;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Shares NASCAR schedule reads across the three series calendars and across the upcoming and event-schedule
/// queries, which would otherwise each request the same race lists and weekend feeds. An answer, including a
/// season the provider has not released, is reused for <c>lifetime</c>; a failed or cancelled request is not
/// kept, so the next refresh retries it. Callers only read the returned nodes, and the schedule service runs one
/// refresh at a time, so a cached node is never read concurrently.
/// </summary>
internal sealed class CachedNascarScheduleClient(INascarScheduleClient inner, IClock clock, TimeSpan lifetime)
    : INascarScheduleClient
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (Task<JsonNode?> Request, DateTimeOffset At)> _entries = [];

    public Task<JsonArray?> GetRaceListAsync(int seriesId, int year, CancellationToken cancellationToken) =>
        GetAsync($"races/{year}/{seriesId}", () => inner.GetRaceListAsync(seriesId, year, cancellationToken));

    public Task<JsonObject?> GetWeekendFeedAsync(int year, int seriesId, int? raceId, CancellationToken cancellationToken) =>
        raceId is not > 0
            ? Task.FromResult<JsonObject?>(null)
            : GetAsync($"weekend/{year}/{seriesId}/{raceId}", () => inner.GetWeekendFeedAsync(year, seriesId, raceId, cancellationToken));

    private async Task<T?> GetAsync<T>(string key, Func<Task<T?>> load) where T : JsonNode
    {
        Task<JsonNode?> request;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var entry) || clock.UtcNow - entry.At >= lifetime ||
                entry.Request.IsFaulted || entry.Request.IsCanceled)
            {
                entry = (Widen(load), clock.UtcNow);
                _entries[key] = entry;
            }
            request = entry.Request;
        }
        try
        {
            return (T?)await request;
        }
        catch
        {
            lock (_gate)
                if (_entries.TryGetValue(key, out var entry) && entry.Request == request) _entries.Remove(key);
            throw;
        }
    }

    private static async Task<JsonNode?> Widen<T>(Func<Task<T?>> load) where T : JsonNode => await load();

    public void Dispose() => inner.Dispose();
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
        var nascar = new CachedNascarScheduleClient(new NascarScheduleClient(), _clock, TimeSpan.FromMinutes(30));
        var f1Academy = new F1AcademyScheduleClient();
        _owned = [f1, motogp, dtm, wec, elms, michelinLeMansCup, gtwc, wrc, nascar, f1Academy];
        _calendars =
        [
            new Formula1Calendar(f1),
            new F1AcademyCalendar(f1Academy),
            new MotorcycleGrandPrixCalendar(motogp),
            new WrcCalendar(wrc),
            new GriiipCalendar(dtm, GriiipSeries.Dtm),
            new GriiipCalendar(wec, GriiipSeries.Wec),
            new GriiipCalendar(elms, GriiipSeries.Elms),
            new GriiipCalendar(michelinLeMansCup, GriiipSeries.MichelinLeMansCup),
            new GtWorldChallengeCalendar(gtwc),
            new NascarCalendar(nascar, NascarSeries.Cup),
            new NascarCalendar(nascar, NascarSeries.Xfinity),
            new NascarCalendar(nascar, NascarSeries.Trucks),
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
                .Where(session => enabled.Contains(session.Championship) && session.IsAhead(now, TimeZoneInfo.Local))
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
        cache.Sessions.All(session => session.IsAhead(_clock.UtcNow, TimeZoneInfo.Local));

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
