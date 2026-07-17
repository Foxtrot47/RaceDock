// Copyright (c) 2026 MotorsportTaskbar contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace MotorsportTaskbar.Core;

/// <summary>Starts the WRC adapter only during the published Rally Estonia event window.</summary>
public sealed class WrcScheduledLiveTimingSource(Func<ILiveTimingSource> sourceFactory, IClock clock) : ILiveTimingSource
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://p-p.redbull.com/rb-wrccom-lintegration-yv-prod/api/") };
    private readonly CancellationTokenSource _stop = new();
    private CancellationTokenSource? _linked;
    private ILiveTimingSource? _active;
    private Task? _run;
    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_run is not null) return Task.CompletedTask;
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token);
        _run = Task.Run(() => RunAsync(_linked.Token), _linked.Token);
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("MotorsportTaskbar/1.0");
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var session = await FindRallyEstoniaAsync(ct);
                if (session is null) { await Task.Delay(TimeSpan.FromHours(6), ct); continue; }
                var wait = session.Start - clock.UtcNow;
                if (wait > TimeSpan.Zero) { ConnectionChanged?.Invoke(ConnectionState.Disconnected); await Task.Delay(wait, ct); }
                _active = sourceFactory(); Attach(_active); await _active.StartAsync(ct);
                var remaining = session.End - clock.UtcNow;
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
                await CloseActiveAsync(); SnapshotReceived?.Invoke(TimingSnapshot.Hidden(clock.UtcNow));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                Failed?.Invoke(new($"WRC scheduler failed: {ex.Message}", ex, clock.UtcNow));
                ConnectionChanged?.Invoke(ConnectionState.Faulted);
                try { await Task.Delay(TimeSpan.FromMinutes(10), ct); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task<ScheduledSession?> FindRallyEstoniaAsync(CancellationToken ct)
    {
        var root = await _http.GetFromJsonAsync<JsonObject>("season-detail.json?seasonId=47", ct);
        var eventNode = (root?["seasonRounds"] as JsonArray)?.OfType<JsonObject>()
            .Select(x => x["event"] as JsonObject).FirstOrDefault(x => JsonSupport.String(x?["name"])?.Contains("Estonia", StringComparison.OrdinalIgnoreCase) == true);
        if (eventNode is null || !DateOnly.TryParse(JsonSupport.String(eventNode["startDate"]), out var start) || !DateOnly.TryParse(JsonSupport.String(eventNode["finishDate"]), out var finish)) return null;
        var offset = TimeSpan.FromMinutes(JsonSupport.Int(eventNode["timeZoneOffset"]) ?? 180);
        return new(JsonSupport.String(eventNode["name"]) ?? "Rally Estonia", "WRC", new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime(), new DateTimeOffset(finish.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime());
    }

    private void Attach(ILiveTimingSource source) { source.SnapshotReceived += ForwardSnapshot; source.DeltaReceived += ForwardDelta; source.ConnectionChanged += ForwardConnection; source.Failed += ForwardFailure; }
    private void Detach(ILiveTimingSource source) { source.SnapshotReceived -= ForwardSnapshot; source.DeltaReceived -= ForwardDelta; source.ConnectionChanged -= ForwardConnection; source.Failed -= ForwardFailure; }
    private void ForwardSnapshot(TimingSnapshot x) => SnapshotReceived?.Invoke(x);
    private void ForwardDelta(TimingDelta x) => DeltaReceived?.Invoke(x);
    private void ForwardConnection(ConnectionState x) => ConnectionChanged?.Invoke(x);
    private void ForwardFailure(FeedFailure x) => Failed?.Invoke(x);
    private async Task CloseActiveAsync() { if (_active is null) return; Detach(_active); await _active.DisposeAsync(); _active = null; }
    public async Task StopAsync() { await _stop.CancelAsync(); if (_run is not null) try { await _run; } catch (OperationCanceledException) { } await CloseActiveAsync(); _run = null; }
    public async ValueTask DisposeAsync() { await StopAsync(); _linked?.Dispose(); _http.Dispose(); _stop.Dispose(); }
}
