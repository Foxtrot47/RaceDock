// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Runs multiple championship adapters, rotates their active snapshots at a fixed display interval, and
/// publishes each adapter's own snapshot plus the set of championships that currently have data.
/// </summary>
public sealed class CompositeLiveTimingSource(
    IReadOnlyList<Func<ILiveTimingSource>> factories,
    IClock clock,
    TimeSpan? displayInterval = null,
    TimeSpan? completedSessionRetention = null) : ILiveTimingSource, ILiveFeedDirectory
{
    private readonly TimeSpan _displayInterval = displayInterval ?? TimeSpan.FromSeconds(5);
    private readonly TimeSpan _completedSessionRetention = completedSessionRetention ?? TimeSpan.FromMinutes(5);
    private readonly List<ILiveTimingSource> _sources = [];
    private readonly Dictionary<ILiveTimingSource, TimingSnapshot> _snapshots = [];
    private readonly Dictionary<ILiveTimingSource, ConnectionState> _connections = [];
    private readonly Dictionary<ILiveTimingSource, Subscription> _subscriptions = [];
    private readonly Dictionary<ILiveTimingSource, DateTimeOffset> _retainedUntil = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _runCts;
    private Task? _rotation;
    private ILiveTimingSource? _selected;
    private IReadOnlyList<LiveFeedStatus> _liveFeeds = [];

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;
    public event Action<TimingSnapshot>? FeedSnapshotReceived;
    public event Action<IReadOnlyList<LiveFeedStatus>>? LiveFeedsChanged;

    public IReadOnlyList<LiveFeedStatus> LiveFeeds
    {
        get { lock (_gate) return _liveFeeds; }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_rotation is not null) return;
        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        foreach (var factory in factories)
        {
            var source = factory();
            _sources.Add(source);
            Attach(source);
        }
        await Task.WhenAll(_sources.Select(source => source.StartAsync(_runCts.Token)));
        RefreshLiveFeeds();
        _rotation = Task.Run(() => RotateAsync(_runCts.Token), _runCts.Token);
    }

    private void Attach(ILiveTimingSource source)
    {
        Action<TimingSnapshot> snapshot = value => OnSnapshot(source, value);
        Action<TimingDelta> delta = value => DeltaReceived?.Invoke(value);
        Action<ConnectionState> connection = value => OnConnection(source, value);
        Action<FeedFailure> failure = value => Failed?.Invoke(value);
        _subscriptions[source] = new(snapshot, delta, connection, failure);
        source.SnapshotReceived += snapshot;
        source.DeltaReceived += delta;
        source.ConnectionChanged += connection;
        source.Failed += failure;
    }

    private void Detach(ILiveTimingSource source)
    {
        if (!_subscriptions.Remove(source, out var handlers)) return;
        source.SnapshotReceived -= handlers.Snapshot;
        source.DeltaReceived -= handlers.Delta;
        source.ConnectionChanged -= handlers.Connection;
        source.Failed -= handlers.Failure;
    }

    private void OnSnapshot(ILiveTimingSource source, TimingSnapshot snapshot)
    {
        TimingSnapshot? selected = null;
        lock (_gate)
        {
            if (snapshot.Lifecycle == SessionLifecycle.Ended && _snapshots.TryGetValue(source, out var previous) &&
                previous.Lifecycle == SessionLifecycle.Live)
            {
                _retainedUntil.TryAdd(source, clock.UtcNow + _completedSessionRetention);
            }
            else
            {
                _snapshots[source] = snapshot;
                if (snapshot.Lifecycle == SessionLifecycle.Live) _retainedUntil.Remove(source);
            }
            var active = ActiveSources();
            if (active.Count == 0)
            {
                _selected = null;
                selected = TimingSnapshot.Hidden(clock.UtcNow);
            }
            else
            {
                if (_selected is null || !active.Contains(_selected)) _selected = active[0];
                if (active.Count == 1 || ReferenceEquals(source, _selected)) selected = _snapshots[_selected];
            }
        }
        FeedSnapshotReceived?.Invoke(snapshot);
        if (selected is not null) SnapshotReceived?.Invoke(selected);
        RefreshLiveFeeds();
    }

    private async Task RotateAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_displayInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            TimingSnapshot? selected = null;
            lock (_gate)
            {
                var active = ActiveSources();
                if (active.Count == 0)
                {
                    if (_selected is not null) selected = TimingSnapshot.Hidden(clock.UtcNow);
                    _selected = null;
                }
                else if (active.Count == 1) _selected = active[0];
                else
                {
                    var current = _selected is null ? -1 : active.IndexOf(_selected);
                    _selected = active[(current + 1) % active.Count];
                }
                if (_selected is not null) selected = _snapshots[_selected];
            }
            if (selected is not null) SnapshotReceived?.Invoke(selected);
            // Retention expiry is only observable when the active set is recomputed, which happens here.
            RefreshLiveFeeds();
        }
    }

    private List<ILiveTimingSource> ActiveSources() => _sources
        .Where(source => TryActiveSnapshot(source, out _))
        .ToList();

    private bool TryActiveSnapshot(ILiveTimingSource source, out TimingSnapshot snapshot)
    {
        if (_snapshots.TryGetValue(source, out snapshot!) &&
            snapshot.Lifecycle == SessionLifecycle.Live && snapshot.Competitors.Count > 0 &&
            (!_retainedUntil.TryGetValue(source, out var until) || until > clock.UtcNow))
            return true;
        snapshot = null!;
        return false;
    }

    /// <summary>
    /// Recomputes the displayable championship set and publishes it when it changed. Called after every
    /// snapshot, rotation tick, and connection change so the directory also reflects retention expiry.
    /// </summary>
    private void RefreshLiveFeeds()
    {
        IReadOnlyList<LiveFeedStatus>? changed = null;
        lock (_gate)
        {
            var next = ActiveLiveFeeds();
            if (!_liveFeeds.SequenceEqual(next))
            {
                _liveFeeds = next;
                changed = next;
            }
        }
        if (changed is not null) LiveFeedsChanged?.Invoke(changed);
    }

    /// <summary>The active adapters mapped to championships, keeping the rotation order and naming each once.</summary>
    private List<LiveFeedStatus> ActiveLiveFeeds()
    {
        var result = new List<LiveFeedStatus>();
        var seen = new HashSet<Championship>();
        foreach (var source in _sources)
        {
            if (!TryActiveSnapshot(source, out var snapshot) || snapshot.Championship == Championship.Unknown) continue;
            if (!seen.Add(snapshot.Championship)) continue;
            result.Add(new LiveFeedStatus(
                snapshot.Championship,
                _retainedUntil.TryGetValue(source, out var until) && until > clock.UtcNow));
        }
        return result;
    }

    private void OnConnection(ILiveTimingSource source, ConnectionState state)
    {
        ConnectionState aggregate;
        lock (_gate)
        {
            _connections[source] = state;
            aggregate = _connections.Values.Any(value => value == ConnectionState.Connected) ? ConnectionState.Connected
                : _connections.Values.Any(value => value == ConnectionState.Connecting) ? ConnectionState.Connecting
                : _connections.Values.Any(value => value == ConnectionState.Stale) ? ConnectionState.Stale
                : _connections.Values.Any(value => value == ConnectionState.Faulted) ? ConnectionState.Faulted
                : ConnectionState.Disconnected;
        }
        ConnectionChanged?.Invoke(aggregate);
        RefreshLiveFeeds();
    }

    public async Task StopAsync()
    {
        if (_runCts is not null) await _runCts.CancelAsync();
        if (_rotation is not null) try { await _rotation; } catch (OperationCanceledException) { }
        foreach (var source in _sources.ToArray())
        {
            Detach(source);
            await source.DisposeAsync();
        }
        lock (_gate)
        {
            _sources.Clear();
            _snapshots.Clear();
            _connections.Clear();
            _selected = null;
            _liveFeeds = [];
        }
        _rotation = null;
        _runCts?.Dispose();
        _runCts = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private sealed record Subscription(
        Action<TimingSnapshot> Snapshot,
        Action<TimingDelta> Delta,
        Action<ConnectionState> Connection,
        Action<FeedFailure> Failure);
}
