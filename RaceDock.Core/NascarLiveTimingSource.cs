// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>
/// One series on the shared <see cref="NascarFeed"/>: it only maps what the feed hands it, so the endpoint is
/// requested once for every subscribed series and not at all while no race is near.
/// </summary>
public sealed class NascarLiveTimingSource : ILiveTimingSource
{
    /// <summary>
    /// The endpoint keeps answering after a session with the field it last assembled, so a payload older than
    /// this is reported as stale instead of as the running session.
    /// </summary>
    private static readonly TimeSpan MaximumFeedAge = TimeSpan.FromMinutes(2);

    private readonly NascarFeed _feed;
    private readonly NascarSeries _series;
    private readonly IClock _clock;
    private readonly IAlertArbiter _alerts;
    private readonly bool _ownsFeed;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private IDisposable? _subscription;
    private ConnectionState? _connection;
    private string? _sessionKey;
    private bool _sessionStarted;
    private bool? _observedEnded;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private string? _lastFingerprint;
    private bool _disposed;

    internal NascarLiveTimingSource(NascarFeed feed, NascarSeries series, IClock clock, IAlertArbiter alerts,
        bool ownsFeed = false)
    {
        _feed = feed;
        _series = series;
        _clock = clock;
        _alerts = alerts;
        _ownsFeed = ownsFeed;
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_subscription is not null) return;
            _feed.Failed += FeedFailed;
            _subscription = _feed.Subscribe(_series, Receive);
        }
        finally { _lifecycle.Release(); }
    }

    private void Receive(JsonObject payload, ConnectionState connection)
    {
        var now = _clock.UtcNow;
        if (_connection is null or ConnectionState.Disconnected or ConnectionState.Faulted)
            SetConnection(ConnectionState.Connecting);
        // Another series is on the shared feed: this source shows nothing rather than that classification.
        if (JsonSupport.Int(payload["series_id"]) != _series.SeriesId)
        {
            PublishHidden(connection);
            return;
        }
        // Between sessions the endpoint keeps answering with an empty field, which is no session at all.
        if (payload["vehicles"] is not JsonArray { Count: > 0 })
        {
            ResetSession();
            SetConnection(connection);
            PublishHidden(connection);
            return;
        }
        if (NascarTiming.FeedTimestamp(payload) is not { } assembled || now - assembled > MaximumFeedAge)
        {
            ResetSession();
            SetConnection(ConnectionState.Stale);
            PublishHidden(ConnectionState.Stale);
            return;
        }
        var snapshot = NascarTiming.BuildSnapshot(payload, now, connection, _series);
        SetConnection(connection);
        ProcessAlerts(payload, snapshot);
        PublishDelta(payload);
        SnapshotReceived?.Invoke(snapshot);
    }

    private void PublishDelta(JsonObject payload)
    {
        // The provider re-serves an identical payload until it assembles a new one, and stamps every assembly with
        // time_of_day_os (in the 24 Oct recording no payload changed without it), so the stamp identifies a change
        // without serializing the whole field. Receive only gets here when the stamp is present.
        var fingerprint = JsonSupport.String(payload["time_of_day_os"]);
        if (fingerprint == _lastFingerprint) return;
        _lastFingerprint = fingerprint;
        DeltaReceived?.Invoke(new($"{_series.Label}Timing", payload.DeepClone(), _clock.UtcNow));
    }

    private void ProcessAlerts(JsonObject payload, TimingSnapshot snapshot)
    {
        _sessionKey = NascarTiming.SessionKey(payload) ?? _sessionKey;
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{_series.Label} SESSION STARTED", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"nascar:{_sessionKey}:start", TimeSpan.FromSeconds(4)));
        }
        if (snapshot.TrackCondition != _trackCondition)
        {
            _trackCondition = snapshot.TrackCondition;
            if (_trackCondition != TrackCondition.Unknown)
                _alerts.ApplyTrackCondition(_trackCondition, null, snapshot.CurrentLap, _clock.UtcNow);
        }
        var ended = snapshot.Lifecycle == SessionLifecycle.Ended;
        if (_observedEnded == false && ended && NascarTiming.IsRace(payload))
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"nascar:{_sessionKey}:chequered", TimeSpan.FromSeconds(6)));
        _observedEnded = ended;
    }

    private void FeedFailed(FeedFailure failure)
    {
        ResetSession();
        SetConnection(ConnectionState.Faulted);
        PublishHidden(ConnectionState.Faulted);
        Failed?.Invoke(failure);
    }

    private void ResetSession()
    {
        _sessionKey = null;
        _sessionStarted = false;
        _observedEnded = null;
        _trackCondition = TrackCondition.Unknown;
        _lastFingerprint = null;
    }

    private void PublishHidden(ConnectionState connection) => SnapshotReceived?.Invoke(new(
        "", "", "", 0, null, TrackCondition.Unknown, [], _clock.UtcNow,
        SessionLifecycle.OffSession, connection, Championship: _series.Championship));

    private void SetConnection(ConnectionState state)
    {
        if (_connection == state) return;
        _connection = state;
        ConnectionChanged?.Invoke(state);
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            Unsubscribe();
            ResetSession();
            SetConnection(ConnectionState.Disconnected);
        }
        finally { _lifecycle.Release(); }
    }

    private void Unsubscribe()
    {
        if (_subscription is null) return;
        _feed.Failed -= FeedFailed;
        _subscription.Dispose();
        _subscription = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            Unsubscribe();
            ResetSession();
            SetConnection(ConnectionState.Disconnected);
            // A caller that shares one feed between sources keeps it; a source that made its own releases it.
            if (_ownsFeed) _feed.Dispose();
        }
        finally { _lifecycle.Release(); }
    }
}
