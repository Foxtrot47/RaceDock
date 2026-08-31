// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public sealed record ScheduledSession(string Meeting, string Name, DateTimeOffset Start, DateTimeOffset End, string? Venue = null);

public sealed class F1ScheduleClient(HttpClient? httpClient = null) : IDisposable
{
    private readonly HttpClient _http = httpClient ?? new HttpClient { BaseAddress = new Uri("https://api.jolpi.ca/ergast/f1/") };
    public async Task<IReadOnlyList<ScheduledSession>> GetSessionsAsync(DateTimeOffset now, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"{now.Year}.json?limit=100", ct); response.EnsureSuccessStatusCode();
        var root = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
        var races = root?["MRData"]?["RaceTable"]?["Races"] as JsonArray;
        if (races is null) return [];
        List<ScheduledSession> result = [];
        foreach (var race in races.OfType<JsonObject>()) AddRaceSessions(result, race);
        return result.OrderBy(session => session.Start).ToList();
    }
    private static void AddRaceSessions(List<ScheduledSession> result, JsonObject race)
    {
        var meeting = JsonSupport.String(race["raceName"]) ?? "Formula 1";
        var venue = JsonSupport.String(race["Circuit"]?["circuitName"]);
        AddSession(result, race, "Race", TimeSpan.FromHours(4), meeting, venue);
        AddSession(result, race["FirstPractice"] as JsonObject, "Practice 1", TimeSpan.FromHours(1), meeting, venue);
        AddSession(result, race["SecondPractice"] as JsonObject, "Practice 2", TimeSpan.FromHours(1), meeting, venue);
        AddSession(result, race["ThirdPractice"] as JsonObject, "Practice 3", TimeSpan.FromHours(1), meeting, venue);
        AddSession(result, race["Qualifying"] as JsonObject, "Qualifying", TimeSpan.FromHours(2), meeting, venue);
        AddSession(result, race["Sprint"] as JsonObject, "Sprint", TimeSpan.FromHours(2), meeting, venue);
        AddSession(result, race["SprintQualifying"] as JsonObject, "Sprint Qualifying", TimeSpan.FromHours(1.5), meeting, venue);
    }

    private static void AddSession(List<ScheduledSession> result, JsonObject? source, string name,
        TimeSpan duration, string meeting, string? venue)
    {
        var date = JsonSupport.String(source?["date"]);
        var time = JsonSupport.String(source?["time"]);
        if (date is null || !DateTimeOffset.TryParse($"{date}T{(string.IsNullOrWhiteSpace(time) ? "00:00:00Z" : time)}", out var start)) return;
        result.Add(new(meeting, name, start, start + duration, venue));
    }

    public void Dispose() => _http.Dispose();
}

public sealed class ScheduledLiveTimingSource(Func<ILiveTimingSource> sourceFactory, IClock clock, TimeSpan? preSessionBuffer = null) : ILiveTimingSource
{
    private readonly TimeSpan _buffer = preSessionBuffer ?? TimeSpan.FromMinutes(30);
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
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token); _run = Task.Run(() => RunAsync(_linked.Token), _linked.Token); return Task.CompletedTask;
    }
    private async Task RunAsync(CancellationToken ct)
    {
        using var schedule = new F1ScheduleClient();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sessions = await schedule.GetSessionsAsync(clock.UtcNow, ct);
                var next = sessions.FirstOrDefault(x => x.End > clock.UtcNow && x.Start - _buffer <= clock.UtcNow)
                    ?? sessions.FirstOrDefault(x => x.End > clock.UtcNow);
                if (next is null) { await Task.Delay(TimeSpan.FromHours(6), ct); continue; }
                var wait = next.Start - _buffer - clock.UtcNow;
                if (wait > TimeSpan.Zero) { ConnectionChanged?.Invoke(ConnectionState.Disconnected); await Task.Delay(wait, ct); }
                _active = sourceFactory(); Attach(_active); await _active.StartAsync(ct);
                var remaining = next.End + TimeSpan.FromMinutes(15) - clock.UtcNow;
                if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
                await CloseActiveAsync(); SnapshotReceived?.Invoke(TimingSnapshot.Hidden(clock.UtcNow));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Failed?.Invoke(new(ex.Message, ex, clock.UtcNow)); ConnectionChanged?.Invoke(ConnectionState.Faulted); try { await Task.Delay(TimeSpan.FromMinutes(15), ct); } catch (OperationCanceledException) { break; } }
        }
    }
    private void Attach(ILiveTimingSource source) { source.SnapshotReceived += ForwardSnapshot; source.DeltaReceived += ForwardDelta; source.ConnectionChanged += ForwardConnection; source.Failed += ForwardFailure; }
    private void Detach(ILiveTimingSource source) { source.SnapshotReceived -= ForwardSnapshot; source.DeltaReceived -= ForwardDelta; source.ConnectionChanged -= ForwardConnection; source.Failed -= ForwardFailure; }
    private void ForwardSnapshot(TimingSnapshot x) => SnapshotReceived?.Invoke(x); private void ForwardDelta(TimingDelta x) => DeltaReceived?.Invoke(x); private void ForwardConnection(ConnectionState x) => ConnectionChanged?.Invoke(x); private void ForwardFailure(FeedFailure x) => Failed?.Invoke(x);
    private async Task CloseActiveAsync() { if (_active is null) return; Detach(_active); await _active.DisposeAsync(); _active = null; }
    public async Task StopAsync() { await _stop.CancelAsync(); if (_run is not null) try { await _run; } catch (OperationCanceledException) { } await CloseActiveAsync(); _run = null; }
    public async ValueTask DisposeAsync() { await StopAsync(); _linked?.Dispose(); _stop.Dispose(); }
}
