// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record NascarFeedOptions(
    TimeSpan PollInterval,
    TimeSpan PreSessionLead,
    TimeSpan SessionWindow,
    TimeSpan ScheduleRefreshInterval,
    TimeSpan FailureInterval)
{
    public static NascarFeedOptions Default { get; } = new(
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(4),
        TimeSpan.FromHours(6),
        TimeSpan.FromMinutes(1));
}

/// <summary>
/// The one unofficial NASCAR live feed every subscribed series shares: the endpoint publishes a single payload
/// for all of them, so one request per tick serves them all and each subscriber is routed by <c>series_id</c>.
/// Nothing is requested while no race is near: the feed sleeps until the lead time of the next published race
/// and stops entirely once the last subscriber has gone.
/// </summary>
internal sealed class NascarFeed : IDisposable
{
    private const string LiveFeedKey = "nascar";

    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly INascarScheduleClient _schedule;
    private readonly HttpClient _http;
    private readonly NascarFeedOptions _options;
    private readonly Lock _gate = new();
    private readonly Dictionary<NascarSeries, List<Action<JsonObject, ConnectionState>>> _subscribers = [];
    private readonly Dictionary<int, CachedSchedule> _schedules = [];
    private readonly Dictionary<(int Year, int SeriesId, int RaceId), CachedWeekend> _weekends = [];
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _run = new();
    private CancellationTokenSource _reschedule = new();
    private readonly Task _worker;
    private bool _disposed;

    internal NascarFeed(
        IClock clock,
        NascarFeedOptions? options = null,
        IAsyncDelay? delay = null,
        INascarScheduleClient? schedule = null,
        HttpClient? httpClient = null)
    {
        _clock = clock;
        _options = options ?? NascarFeedOptions.Default;
        _delay = delay ?? new SystemAsyncDelay();
        _schedule = schedule ?? new NascarScheduleClient();
        _http = httpClient ?? CreateHttpClient();
        _worker = Task.Run(() => RunAsync(_run.Token));
    }

    /// <summary>Reported once per failed tick, shared by every subscriber so each one renders its own failure.</summary>
    public event Action<FeedFailure>? Failed;

    /// <summary>
    /// Registers a handler for one series. The handler is called with the live payload while its series is the one
    /// the feed carries, and with an empty payload while another series is on feed, so it can hide itself.
    /// </summary>
    internal IDisposable Subscribe(NascarSeries series, Action<JsonObject, ConnectionState> handler)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        bool first;
        CancellationTokenSource? interrupted = null;
        lock (_gate)
        {
            first = _subscribers.Count == 0;
            if (!_subscribers.TryGetValue(series, out var handlers))
            {
                _subscribers[series] = handlers = [];
                // A newly subscribed series has its own calendar, so an idle sleep computed without it is stale.
                interrupted = _reschedule;
                _reschedule = new CancellationTokenSource();
            }
            handlers.Add(handler);
        }
        if (first) _wake.Release();
        interrupted?.Cancel();
        return new Subscription(this, series, handler);
    }

    /// <summary>Waits for a subscriber instead of requesting while nobody is listening.</summary>
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!HasSubscribers)
            {
                try { await _wake.WaitAsync(cancellationToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            try
            {
                await TickAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception failure)
            {
                Failed?.Invoke(new($"NASCAR live timing failed: {failure.Message}", failure, _clock.UtcNow));
                try { await _delay.DelayAsync(_options.FailureInterval, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        // Captured before the calendars are read, so a series subscribed while they load still interrupts the sleep.
        CancellationToken reschedule;
        lock (_gate) reschedule = _reschedule.Token;
        var windows = await WindowsAsync(_clock.UtcNow, cancellationToken);
        var delay = NextIdleDelay(windows, _clock.UtcNow, _options);
        if (delay != _options.PollInterval)
        {
            using var sleep = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, reschedule);
            try { await _delay.DelayAsync(delay, sleep.Token); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            return;
        }
        Dispatch(await GetLiveFeedAsync(cancellationToken), ConnectionState.Connected);
        await _delay.DelayAsync(_options.PollInterval, cancellationToken);
    }

    /// <summary>
    /// Hands one payload to the subscription it belongs to. A subscriber whose series is not on the feed receives
    /// an empty payload instead, so it hides rather than reading another series' classification. One failing
    /// subscriber never stops the others.
    /// </summary>
    private void Dispatch(JsonObject payload, ConnectionState connection)
    {
        foreach (var (series, handler) in Subscriptions())
        {
            var own = JsonSupport.Int(payload["series_id"]) == series.SeriesId;
            try { handler(own ? payload : new JsonObject { ["series_id"] = series.SeriesId }, connection); }
            catch (Exception failure) { AppLog.Warning("NASCAR", $"The {series.Label} live source failed.", failure); }
        }
    }

    private async Task<JsonObject> GetLiveFeedAsync(CancellationToken cancellationToken)
    {
        var text = await _http.GetStringAsync("live/feeds/live-feed.json", cancellationToken);
        FeedDump.Record(LiveFeedKey, text);
        return JsonNode.Parse(text) as JsonObject ??
            throw new InvalidDataException("NASCAR live timing returned invalid JSON.");
    }

    private async Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> WindowsAsync(
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<(DateTimeOffset Start, DateTimeOffset End)> windows = [];
        foreach (var series in SubscribedSeries()) windows.AddRange(await WindowsAsync(series, now, cancellationToken));
        return windows;
    }

    /// <summary>
    /// The window the series is in or is counting down to. The calendar only ever names the next race, so an
    /// earlier reference keeps a race that has already started instead of jumping to the round after it.
    /// A round published without a clock time names a whole day rather than a moment, so it is polled for that
    /// day instead of for a window that would begin at midnight UTC whatever the race time.
    /// </summary>
    private async Task<IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)>> WindowsAsync(
        NascarSeries series, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reference = now - _options.SessionWindow;
        var races = await RacesAsync(series, now, cancellationToken);
        if (ChampionshipCalendars.NextNascar(races, reference, series.Championship) is not { } race) return [];
        var raceId = ChampionshipCalendars.NextNascarRaceId(races, reference, series.Championship);
        var weekend = await WeekendAsync(series, raceId, race, now, cancellationToken);
        var sessions = ChampionshipCalendars.NascarEvent(races, weekend, reference, series.Championship);
        if (weekend is not null && sessions.Count > 0)
            return sessions.Select(session => (session.StartUtc, session.StartUtc + _options.SessionWindow)).ToList();
        if (race.StartDate is { } date)
        {
            var start = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return [(start, start.AddDays(1))];
        }
        return [(race.StartUtc, race.StartUtc + _options.SessionWindow)];
    }

    /// <summary>
    /// The published season, cached for <see cref="NascarFeedOptions.ScheduleRefreshInterval"/> because the
    /// schedule only moves between rounds. A season the provider has not released yet answers without a list.
    /// </summary>
    private async Task<JsonArray?> RacesAsync(NascarSeries series, DateTimeOffset now, CancellationToken cancellationToken)
    {
        lock (_gate)
            if (_schedules.TryGetValue(series.SeriesId, out var cached) &&
                now - cached.At < _options.ScheduleRefreshInterval)
                return cached.Races;
        JsonArray? races = null;
        foreach (var year in new[] { now.Year, now.Year + 1 })
        {
            races = await _schedule.GetRaceListAsync(series.SeriesId, year, cancellationToken);
            if (ChampionshipCalendars.NextNascar(races, now - _options.SessionWindow, series.Championship) is not null) break;
        }
        lock (_gate) _schedules[series.SeriesId] = new(races, now);
        return races;
    }

    private async Task<JsonObject?> WeekendAsync(
        NascarSeries series, int? raceId, UpcomingSession race, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (raceId is not > 0) return null;
        var key = (Year: race.StartUtc.Year, SeriesId: series.SeriesId, RaceId: raceId.Value);
        lock (_gate)
            if (_weekends.TryGetValue(key, out var cached) &&
                now - cached.At < _options.ScheduleRefreshInterval)
                return cached.Feed;
        var feed = await _schedule.GetWeekendFeedAsync(key.Year, key.SeriesId, key.RaceId, cancellationToken);
        lock (_gate) _weekends[key] = new(feed, now);
        return feed;
    }

    /// <summary>
    /// How long to sleep before the next tick: the poll interval inside a window, the time to its lead before it,
    /// and the schedule refresh interval while every window is still far off.
    /// </summary>
    internal static TimeSpan NextIdleDelay(
        IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> windows, DateTimeOffset now, NascarFeedOptions options)
    {
        if (windows.Any(window => now >= window.Start - options.PreSessionLead && now < window.End))
            return options.PollInterval;
        var next = windows.Select(window => window.Start).Where(start => start > now).OrderBy(start => start).FirstOrDefault();
        if (next == default) return options.ScheduleRefreshInterval;
        var untilLead = next - now - options.PreSessionLead;
        if (untilLead <= TimeSpan.Zero) return options.PollInterval;
        return untilLead <= options.ScheduleRefreshInterval ? untilLead : options.ScheduleRefreshInterval;
    }

    private bool HasSubscribers
    {
        get { lock (_gate) return _subscribers.Count > 0; }
    }

    private List<NascarSeries> SubscribedSeries()
    {
        lock (_gate) return [.. _subscribers.Keys];
    }

    private List<(NascarSeries Series, Action<JsonObject, ConnectionState> Handler)> Subscriptions()
    {
        lock (_gate)
            return [.. _subscribers.SelectMany(entry =>
                entry.Value.Select(handler => (entry.Key, handler)))];
    }

    private void Unsubscribe(NascarSeries series, Action<JsonObject, ConnectionState> handler)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(series, out var handlers)) return;
            handlers.Remove(handler);
            if (handlers.Count == 0) _subscribers.Remove(series);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://cf.nascar.com/"),
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RaceDock", "1.0"));
        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _run.Cancel();
        _http.Dispose();
        _schedule.Dispose();
        _wake.Dispose();
        _run.Dispose();
        lock (_gate) _reschedule.Dispose();
    }

    private sealed record CachedSchedule(JsonArray? Races, DateTimeOffset At);
    private sealed record CachedWeekend(JsonObject? Feed, DateTimeOffset At);

    private sealed class Subscription(NascarFeed feed, NascarSeries series, Action<JsonObject, ConnectionState> handler)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            feed.Unsubscribe(series, handler);
        }
    }
}
