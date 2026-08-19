// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record AlkamelFeedDefinition(
    string FeedName,
    string SourceName,
    string AlertKey,
    string DeltaTopic,
    Championship Championship,
    Uri SocketBase)
{
    public static AlkamelFeedDefinition Imsa { get; } = new(
        "imsa", "IMSA", "imsa", "ImsaStandings", Championship.Imsa,
        new Uri("wss://livetiming.alkamelsystems.com/sockjs/"));

    public static AlkamelFeedDefinition FormulaE { get; } = new(
        "fiaformulae", "Formula E", "formulae", "FormulaEStandings", Championship.FormulaE,
        new Uri("wss://livetiming-formula-e.alkamelsystems.com/sockjs/"));
}

/// <summary>Streams an Al Kamel classification over SockJS and Meteor DDP.</summary>
internal sealed class AlkamelLiveTimingSource : ILiveTimingSource
{
    private static readonly string[] DetailSubscriptions =
    [
        "entry", "trackInfo", "standings", "sessionStatus", "bestResults"
    ];
    private static readonly string[] DetailCollections =
    [
        "session_entry", "track_info", "standings", "session_status", "best_results"
    ];

    private readonly AlkamelFeedDefinition _definition;
    private readonly Func<IAlkamelDdpClient> _clientFactory;
    private readonly IClock _clock;
    private readonly IAsyncDelay _delay;
    private readonly IAlertArbiter _alerts;
    private readonly AlkamelTimingOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly Dictionary<string, Dictionary<string, JsonObject>> _collections = [];
    private readonly JsonObject _feedState = [];
    private readonly List<string> _discoverySubscriptions = [];
    private readonly List<string> _detailSubscriptions = [];
    private readonly HashSet<string> _activeSubscriptions = [];
    private readonly HashSet<string> _pendingUnsubscribes = [];
    private CancellationTokenSource? _runCts;
    private Task? _worker;
    private IAlkamelDdpClient? _client;
    private ConnectionState _connection = ConnectionState.Disconnected;
    private string? _sessionIdsFingerprint;
    private string? _selectedSessionId;
    private int _nextSubscriptionId;
    private bool _sessionStarted;
    private bool? _observedEnded;
    private TrackCondition _trackCondition = TrackCondition.Unknown;
    private string? _lastStandingsFingerprint;
    private bool _hasLiveSnapshot;
    private bool _sessionRunning;
    private bool _disposed;

    internal AlkamelLiveTimingSource(
        AlkamelFeedDefinition definition,
        Func<IAlkamelDdpClient> clientFactory,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        AlkamelTimingOptions options)
    {
        _definition = definition;
        _clientFactory = clientFactory;
        _clock = clock;
        _delay = delay;
        _alerts = alerts;
        _options = options;
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
            if (_worker is not null) return;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _worker = Task.Run(() => RunAsync(_runCts.Token), _runCts.Token);
        }
        finally { _lifecycle.Release(); }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ResetProtocolState();
                SetConnection(ConnectionState.Connecting);
                PublishHidden(ConnectionState.Connecting);
                _client = _clientFactory();
                await _client.ConnectAsync(cancellationToken);
                await _client.SendAsync(new JsonObject
                {
                    ["msg"] = "connect",
                    ["version"] = "1",
                    ["support"] = new JsonArray("1")
                }, cancellationToken);

                var lastData = Stopwatch.GetTimestamp();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var timeout = _hasLiveSnapshot && _sessionRunning
                        ? _options.StaleTimeout - Stopwatch.GetElapsedTime(lastData)
                        : _options.StaleTimeout;
                    if (timeout <= TimeSpan.Zero) throw new TimeoutException($"{_definition.SourceName} live timing stopped sending data.");
                    using var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    receiveCts.CancelAfter(timeout);
                    JsonObject? message;
                    try { message = await _client.ReceiveAsync(receiveCts.Token); }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        throw new TimeoutException($"{_definition.SourceName} live timing stopped sending data.");
                    }
                    if (message is null) throw new WebSocketException($"{_definition.SourceName} live-timing connection closed.");
                    var type = JsonSupport.String(message["msg"]);
                    if (type is "added" or "changed" or "removed") lastData = Stopwatch.GetTimestamp();
                    await ProcessMessageAsync(message, cancellationToken);
                    if (type == "connected") attempt = 0;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (TimeoutException ex)
            {
                PublishHidden(ConnectionState.Stale);
                SetConnection(ConnectionState.Stale);
                Failed?.Invoke(new(ex.Message, ex, _clock.UtcNow));
                await CloseClientAsync();
                await DelayForRetryAsync(attempt++, cancellationToken);
            }
            catch (Exception ex)
            {
                PublishHidden(ConnectionState.Faulted);
                SetConnection(ConnectionState.Faulted);
                Failed?.Invoke(new($"{_definition.SourceName} feed failed: {ex.Message}", ex, _clock.UtcNow));
                await CloseClientAsync();
                await DelayForRetryAsync(attempt++, cancellationToken);
            }
            finally { await CloseClientAsync(); }
        }
    }

    private async Task ProcessMessageAsync(JsonObject message, CancellationToken cancellationToken)
    {
        var type = JsonSupport.String(message["msg"]);
        if (type == "_sockjsHeartbeat") return;
        if (type == "connected")
        {
            SetConnection(ConnectionState.Connected);
            await SubscribeAsync("livetimingFeed", new JsonArray(_definition.FeedName), cancellationToken);
            return;
        }
        if (type == "ping")
        {
            var pong = new JsonObject { ["msg"] = "pong" };
            if (message["id"] is { } id) pong["id"] = id.DeepClone();
            await _client!.SendAsync(pong, cancellationToken);
            return;
        }
        if (type == "nosub")
        {
            var id = JsonSupport.String(message["id"]);
            var expected = id is not null && _pendingUnsubscribes.Remove(id);
            if (id is not null) _activeSubscriptions.Remove(id);
            if (expected && message["error"] is null) return;
            throw new InvalidOperationException($"{_definition.SourceName} live-timing subscription failed.");
        }
        if (type is "error" or "failed") throw new InvalidOperationException($"{_definition.SourceName} live-timing protocol failed.");
        if (type is not ("added" or "changed" or "removed")) return;

        var collection = JsonSupport.String(message["collection"]);
        if (string.IsNullOrWhiteSpace(collection)) return;
        if (collection == "feeds")
        {
            if (type == "removed")
            {
                _feedState.Clear();
                _sessionIdsFingerprint = null;
                await ClearSessionSubscriptionsAsync(cancellationToken);
                ClearCollection("sessions");
                ClearCollection("session_info");
                PublishHidden(ConnectionState.Connected);
            }
            else
            {
                ApplyFeedPatch(message);
                await ApplyFeedAsync(_feedState, cancellationToken);
            }
            return;
        }

        ApplyCollectionMessage(type, collection, message);
        if (collection is "sessions" or "session_info")
            await UpdateSelectedSessionAsync(cancellationToken);
        if (DetailCollections.Contains(collection, StringComparer.Ordinal))
        {
            if (type == "removed" && collection is "session_entry" or "track_info" or "standings" or "session_status")
                PublishHidden(ConnectionState.Connected);
            else PublishCurrent();
        }
    }

    private async Task ApplyFeedAsync(JsonObject fields, CancellationToken cancellationToken)
    {
        if (fields["running"] is { } running && !JsonSupport.Bool(running))
        {
            _sessionIdsFingerprint = null;
            await ClearSessionSubscriptionsAsync(cancellationToken);
            ClearCollection("sessions");
            ClearCollection("session_info");
            PublishHidden(ConnectionState.Connected);
            return;
        }
        if (fields["sessions"] is not JsonArray sessions) return;
        var fingerprint = string.Join('|', sessions.Select(SessionReferenceValue).Where(value => value is not null));
        if (fingerprint == _sessionIdsFingerprint) return;
        _sessionIdsFingerprint = fingerprint;
        await ClearSessionSubscriptionsAsync(cancellationToken);
        ClearCollection("sessions");
        ClearCollection("session_info");
        PublishHidden(ConnectionState.Connected);
        var parameter = new JsonArray { sessions.DeepClone() };
        _discoverySubscriptions.Add(await SubscribeAsync("sessions", (JsonArray)parameter.DeepClone(), cancellationToken));
        _discoverySubscriptions.Add(await SubscribeAsync("sessionInfo", parameter, cancellationToken));
    }

    private void ApplyFeedPatch(JsonObject message)
    {
        if (message["fields"] is JsonObject fields)
            foreach (var name in new[] { "running", "sessions" })
                if (fields[name] is { } value) _feedState[name] = value.DeepClone();
        if (message["cleared"] is JsonArray cleared)
            foreach (var name in cleared.Select(JsonSupport.String).Where(name => name is "running" or "sessions"))
                _feedState.Remove(name!);
    }

    private void ApplyCollectionMessage(string type, string collection, JsonObject message)
    {
        var id = JsonSupport.String(message["id"]);
        if (string.IsNullOrWhiteSpace(id)) return;
        if (!_collections.TryGetValue(collection, out var documents))
            _collections[collection] = documents = new(StringComparer.Ordinal);
        if (type == "removed")
        {
            documents.Remove(id);
            return;
        }
        if (!documents.TryGetValue(id, out var document)) documents[id] = document = [];
        if (message["fields"] is JsonObject fields)
            foreach (var pair in fields) document[pair.Key] = pair.Value?.DeepClone();
        if (message["cleared"] is JsonArray cleared)
            foreach (var field in cleared.Select(JsonSupport.String).Where(value => value is not null)) document.Remove(field!);
    }

    private async Task UpdateSelectedSessionAsync(CancellationToken cancellationToken)
    {
        var candidates = Collection("session_info").Values
            .Where(document => document["info"] is JsonObject info &&
                (_definition.Championship != Championship.Imsa || ImsaTiming.IsWeatherTechSession(info)) &&
                SessionReferenceValue(document["session"]) is { } id && Collection("sessions").ContainsKey(id))
            .ToList();
        var open = candidates.Where(document => !JsonSupport.Bool(document["info"]?["closed"])).ToList();
        var now = _clock.UtcNow.ToUnixTimeMilliseconds();
        var selected = open.Where(document => SessionDate(document) <= now &&
                (SessionEndDate(document) == 0 || SessionEndDate(document) >= now))
            .OrderByDescending(SessionDate).FirstOrDefault()
            ?? open.Where(document => SessionDate(document) <= now).OrderByDescending(SessionDate).FirstOrDefault()
            ?? open.Where(document => SessionDate(document) > now).OrderBy(SessionDate).FirstOrDefault()
            ?? candidates.OrderByDescending(SessionDate).FirstOrDefault();
        var sessionId = selected is null ? null : SessionReferenceValue(selected["session"]);
        if (sessionId == _selectedSessionId) return;

        await StopSubscriptionsAsync(_detailSubscriptions, cancellationToken);
        ClearDetailCollections();
        _selectedSessionId = sessionId;
        ResetSessionState();
        if (selected?["session"] is not JsonObject sessionReference || string.IsNullOrWhiteSpace(sessionId))
        {
            PublishHidden(ConnectionState.Connected);
            return;
        }

        PublishHidden(ConnectionState.Connected);
        foreach (var subscription in DetailSubscriptions)
            _detailSubscriptions.Add(await SubscribeAsync(subscription,
                new JsonArray { sessionReference.DeepClone() }, cancellationToken));
    }

    private void PublishCurrent()
    {
        if (string.IsNullOrWhiteSpace(_selectedSessionId)) return;
        if (!Collection("sessions").TryGetValue(_selectedSessionId, out var session)) return;
        var infoDocument = FindSessionDocument("session_info");
        var entryDocument = FindSessionDocument("session_entry");
        var trackDocument = FindSessionDocument("track_info");
        var standingsDocument = FindSessionDocument("standings");
        var statusDocument = FindSessionDocument("session_status");
        if (infoDocument?["info"] is not JsonObject info ||
            entryDocument?["entry"] is not JsonObject entries ||
            trackDocument?["track"] is not JsonObject track ||
            standingsDocument?["standings"] is not JsonObject standings ||
            statusDocument?["status"] is not JsonObject status) return;
        var bestResults = FindSessionDocument("best_results")?["bestResults"] as JsonObject;
        var snapshot = _definition.Championship == Championship.FormulaE
            ? FormulaETiming.BuildSnapshot(session, info, entries, track, standings, status,
                bestResults, _clock.UtcNow, _connection)
            : ImsaTiming.BuildSnapshot(session, info, entries, track, standings, status,
                bestResults, _clock.UtcNow, _connection);
        _hasLiveSnapshot = snapshot.Lifecycle == SessionLifecycle.Live;
        _sessionRunning = JsonSupport.Bool(status["isSessionRunning"]);
        ProcessAlerts(snapshot, status);
        var fingerprint = standings.ToJsonString();
        if (fingerprint != _lastStandingsFingerprint)
        {
            _lastStandingsFingerprint = fingerprint;
            DeltaReceived?.Invoke(new(_definition.DeltaTopic, standings.DeepClone(), _clock.UtcNow));
        }
        SnapshotReceived?.Invoke(snapshot);
    }

    private void ProcessAlerts(TimingSnapshot snapshot, JsonObject status)
    {
        if (!_sessionStarted && snapshot.Lifecycle == SessionLifecycle.Live)
        {
            _sessionStarted = true;
            _alerts.Accept(new(AlertKind.SessionStart, 10, false, $"{_definition.SourceName.ToUpperInvariant()} SESSION STARTED", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"{_definition.AlertKey}:{_selectedSessionId}:start", TimeSpan.FromSeconds(4)));
        }
        var track = AlkamelTiming.TrackState(status);
        if (track.Condition != _trackCondition)
        {
            _trackCondition = track.Condition;
            if (track.Condition != TrackCondition.Unknown)
                _alerts.ApplyTrackCondition(track.Condition, track.Detail, snapshot.CurrentLap, _clock.UtcNow);
        }
        var ended = snapshot.Lifecycle == SessionLifecycle.Ended;
        if (_observedEnded == false && ended)
            _alerts.Accept(new(AlertKind.Chequered, 90, false, "CHEQUERED FLAG", snapshot.Session,
                snapshot.CurrentLap, _clock.UtcNow, $"{_definition.AlertKey}:{_selectedSessionId}:chequered", TimeSpan.FromSeconds(6)));
        _observedEnded = ended;
    }

    private async Task<string> SubscribeAsync(string name, JsonArray parameters, CancellationToken cancellationToken)
    {
        var id = (++_nextSubscriptionId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await _client!.SendAsync(new JsonObject
        {
            ["msg"] = "sub",
            ["id"] = id,
            ["name"] = name,
            ["params"] = parameters
        }, cancellationToken);
        _activeSubscriptions.Add(id);
        return id;
    }

    private async Task StopSubscriptionsAsync(List<string> subscriptions, CancellationToken cancellationToken)
    {
        if (_client is not null)
            foreach (var id in subscriptions)
            {
                if (_activeSubscriptions.Contains(id)) _pendingUnsubscribes.Add(id);
                await _client.SendAsync(new JsonObject { ["msg"] = "unsub", ["id"] = id }, cancellationToken);
            }
        subscriptions.Clear();
    }

    private async Task ClearSessionSubscriptionsAsync(CancellationToken cancellationToken)
    {
        await StopSubscriptionsAsync(_discoverySubscriptions, cancellationToken);
        await StopSubscriptionsAsync(_detailSubscriptions, cancellationToken);
        _selectedSessionId = null;
        ClearDetailCollections();
        ResetSessionState();
    }

    private JsonObject? FindSessionDocument(string collection) => Collection(collection).Values
        .FirstOrDefault(document => SessionReferenceValue(document["session"]) == _selectedSessionId);

    private Dictionary<string, JsonObject> Collection(string name)
    {
        if (!_collections.TryGetValue(name, out var values)) _collections[name] = values = new(StringComparer.Ordinal);
        return values;
    }

    private void ClearCollection(string name) => Collection(name).Clear();
    private void ClearDetailCollections()
    {
        foreach (var collection in DetailCollections) ClearCollection(collection);
    }

    private long SessionDate(JsonObject infoDocument)
    {
        var sessionId = SessionReferenceValue(infoDocument["session"]);
        if (sessionId is not null && Collection("sessions").TryGetValue(sessionId, out var session) &&
            long.TryParse(JsonSupport.String(session["date"]), out var date)) return date;
        return long.TryParse(JsonSupport.String(infoDocument["info"]?["date"]), out var infoDate) ? infoDate : 0;
    }

    private long SessionEndDate(JsonObject infoDocument)
    {
        var sessionId = SessionReferenceValue(infoDocument["session"]);
        return sessionId is not null && Collection("sessions").TryGetValue(sessionId, out var session) &&
            long.TryParse(JsonSupport.String(session["endDate"]), out var endDate) ? endDate : 0;
    }

    private static string? SessionReferenceValue(JsonNode? node) =>
        node is JsonObject value ? JsonSupport.String(value["$value"]) : JsonSupport.String(node);

    private async Task CloseClientAsync()
    {
        if (_client is null) return;
        await _client.DisposeAsync();
        _client = null;
    }

    private async Task DelayForRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var values = _options.ReconnectBackoff;
        var delay = values.Count == 0 ? TimeSpan.FromSeconds(1) : values[Math.Min(attempt, values.Count - 1)];
        try { await _delay.DelayAsync(delay, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void ResetProtocolState()
    {
        _collections.Clear();
        _feedState.Clear();
        _discoverySubscriptions.Clear();
        _detailSubscriptions.Clear();
        _activeSubscriptions.Clear();
        _pendingUnsubscribes.Clear();
        _sessionIdsFingerprint = null;
        _selectedSessionId = null;
        _nextSubscriptionId = 0;
        ResetSessionState();
    }

    private void ResetSessionState()
    {
        _sessionStarted = false;
        _observedEnded = null;
        _trackCondition = TrackCondition.Unknown;
        _lastStandingsFingerprint = null;
        _hasLiveSnapshot = false;
        _sessionRunning = false;
    }

    private void PublishHidden(ConnectionState connection)
    {
        _hasLiveSnapshot = false;
        SnapshotReceived?.Invoke(new(
            "", "", "", 0, null, TrackCondition.Unknown, [], _clock.UtcNow,
            SessionLifecycle.OffSession, connection, Championship: _definition.Championship));
    }

    private void SetConnection(ConnectionState value)
    {
        if (_connection == value) return;
        _connection = value;
        ConnectionChanged?.Invoke(value);
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        if (_worker is null) return;
        if (_runCts is not null) await _runCts.CancelAsync();
        try { await _worker; }
        catch (OperationCanceledException) { }
        _worker = null;
        _runCts?.Dispose();
        _runCts = null;
        ResetProtocolState();
        SetConnection(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            await StopCoreAsync();
        }
        finally { _lifecycle.Release(); }
    }
}
