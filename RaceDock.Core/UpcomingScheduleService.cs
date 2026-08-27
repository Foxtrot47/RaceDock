// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed class Formula1Calendar(F1ScheduleClient client) : IChampionshipCalendar
{
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await client.GetSessionsAsync(now, cancellationToken);
        return ChampionshipCalendars.NextFormula1(sessions, now) is { } next ? [next] : [];
    }
}

internal sealed class MotorcycleGrandPrixCalendar(MotoGpScheduleClient client) : IChampionshipCalendar
{
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        ChampionshipCalendars.NextMotorcycleGrandPrix(await client.GetEventsAsync(now, cancellationToken), now);
}

internal sealed class DtmCalendar(IDtmApiClient client) : IChampionshipCalendar
{
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sessions = await client.GetSessionsAsync(now, cancellationToken);
        return ChampionshipCalendars.NextDtm(sessions, now) is { } next ? [next] : [];
    }
}

internal sealed class GtWorldChallengeCalendar(GtwcApiClient client) : IChampionshipCalendar
{
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seasons = await client.GetSeasonsAsync(cancellationToken);
        var year = JsonSupport.String(seasons.Full["CurrentSeason"]);
        if (string.IsNullOrWhiteSpace(year)) return [];
        var season = await client.GetSeasonAsync(year, cancellationToken);
        return ChampionshipCalendars.NextGtWorldChallenge(season.Full, now) is { } next ? [next] : [];
    }
}

/// <summary>The WRC calendar carries round windows only, so the selected round is resolved to its name.</summary>
internal sealed class WrcCalendar(WrcApiClient client) : IChampionshipCalendar
{
    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seasons = WrcTiming.OrderSeasons(await client.GetSeasonsAsync(cancellationToken), now.Year);
        foreach (var season in seasons)
        {
            var events = await client.GetSeasonEventsAsync(season.SeasonId, cancellationToken);
            if (WrcTiming.SelectEvent(events, now).Next is not { } next) continue;
            var detail = await client.GetEventAsync(next.EventId, cancellationToken);
            return [new(Championship.WorldRallyChampionship, detail.Name, null, next.StartUtc)];
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
/// Collects the next published event for every championship that exposes a calendar. Sources are queried
/// concurrently and independently: a provider that is slow, broken, or offline drops out of the result
/// instead of failing the whole list, because this only feeds an informational Settings section.
/// </summary>
public sealed class UpcomingScheduleService : IDisposable
{
    private readonly IReadOnlyList<IChampionshipCalendar> _calendars;
    private readonly IReadOnlyList<IDisposable> _owned;
    private readonly IClock _clock;
    private readonly TimeSpan _cacheLifetime;
    private readonly TimeSpan _sourceTimeout;
    private readonly SemaphoreSlim _refresh = new(1, 1);
    private IReadOnlyList<UpcomingSession> _cached = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private bool _disposed;

    public UpcomingScheduleService(IClock? clock = null, TimeSpan? cacheLifetime = null, TimeSpan? sourceTimeout = null)
    {
        _clock = clock ?? new SystemClock();
        _cacheLifetime = cacheLifetime ?? TimeSpan.FromHours(6);
        _sourceTimeout = sourceTimeout ?? TimeSpan.FromSeconds(15);
        var f1 = new F1ScheduleClient();
        var motogp = new MotoGpScheduleClient();
        var dtm = new DtmApiClient();
        var gtwc = new GtwcApiClient();
        var wrc = new WrcApiClient();
        _owned = [f1, motogp, dtm, gtwc, wrc];
        _calendars =
        [
            new Formula1Calendar(f1),
            new MotorcycleGrandPrixCalendar(motogp),
            new WrcCalendar(wrc),
            new DtmCalendar(dtm),
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

    public async Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(CancellationToken cancellationToken)
    {
        if (_clock.UtcNow - _cachedAt < _cacheLifetime) return _cached;
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (_clock.UtcNow - _cachedAt < _cacheLifetime) return _cached;
            var now = _clock.UtcNow;
            var results = await Task.WhenAll(_calendars.Select(calendar => QueryAsync(calendar, now, cancellationToken)));
            _cached = results.SelectMany(sessions => sessions).OrderBy(session => session.StartUtc).ToList();
            _cachedAt = _clock.UtcNow;
            return _cached;
        }
        finally { _refresh.Release(); }
    }

    private async Task<IReadOnlyList<UpcomingSession>> QueryAsync(IChampionshipCalendar calendar, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_sourceTimeout);
        try
        {
            return await calendar.GetUpcomingAsync(now, timeout.Token);
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
}
