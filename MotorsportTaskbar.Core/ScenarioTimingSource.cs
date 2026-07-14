// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace MotorsportTaskbar.Core;

public sealed class ScenarioTimingSource(TimingStateProcessor processor, IClock clock) : ITimingScenarioSource
{
    private CancellationTokenSource? _cts;
    private int _step;
    private readonly SemaphoreSlim _advance = new(0);
    public bool IsPaused { get; private set; } = true;
    public double Speed { get; set; } = 4;
    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived;
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null) return Task.CompletedTask;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        processor.SnapshotChanged += Forward;
        processor.ProcessInitial(CreateInitial()); processor.SetConnection(ConnectionState.Connected);
        ConnectionChanged?.Invoke(ConnectionState.Connected);
        _ = Task.Run(() => RunAsync(_cts.Token), _cts.Token).ContinueWith(task =>
        {
            if (task.Exception is { } ex) Failed?.Invoke(new(ex.GetBaseException().Message, ex.GetBaseException(), clock.UtcNow));
        }, TaskScheduler.Default);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;
        await _cts.CancelAsync(); _cts.Dispose(); _cts = null; processor.SnapshotChanged -= Forward;
        ConnectionChanged?.Invoke(ConnectionState.Disconnected);
    }

    public void Pause() => IsPaused = true;
    public void Resume() { IsPaused = false; _advance.Release(); }
    public void Restart() { IsPaused = true; _step = 0; processor.ClearAlerts(); processor.ProcessInitial(CreateInitial()); _advance.Release(); }
    public void Step() { IsPaused = true; ApplyStep(_step++ % 12); }
    public void Trigger(ScenarioCommand command) => ApplyCommand(command);
    public void SetTopFive(IReadOnlyList<ManualStanding> standings)
    {
        var ids = new[] { "4", "81", "16", "1", "63" }; var drivers = new JsonObject(); var lines = new JsonObject();
        for (var i = 0; i < Math.Min(5, standings.Count); i++)
        {
            drivers[ids[i]] = new JsonObject { ["Tla"] = standings[i].Code.ToUpperInvariant() };
            lines[ids[i]] = new JsonObject { ["Position"] = i + 1, ["GapToLeader"] = i == 0 ? "LEAD" : standings[i].GapToLeader };
        }
        Feed("DriverList", drivers); Feed("TimingData", new JsonObject { ["Lines"] = lines });
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (IsPaused) { await _advance.WaitAsync(ct); continue; }
            try { await Task.Delay(TimeSpan.FromSeconds(2 / Math.Clamp(Speed, .25, 20)), ct); }
            catch (OperationCanceledException) { break; }
            ApplyStep(_step++ % 12);
        }
    }

    private void ApplyStep(int step)
    {
        switch (step)
        {
            case 0: Feed("TrackStatus", new JsonObject { ["Status"] = "1", ["Message"] = "All clear" }); break;
            case 1: UpdatePositions(("4", 1, "LEAD"), ("81", 2, "+1.201"), ("16", 3, "+2.840"), ("1", 4, "+3.110"), ("63", 5, "+4.024")); break;
            case 2: UpdatePositions(("4", 1, "LEAD"), ("16", 2, "+1.950"), ("81", 3, "+2.101"), ("1", 4, "+3.400"), ("63", 5, "+5.020")); break;
            case 3: Feed("TimingData", new JsonObject { ["Lines"] = new JsonObject { ["16"] = new JsonObject { ["BestLapTime"] = new JsonObject { ["Value"] = "1:27.420", ["OverallFastest"] = true }, ["NumberOfLaps"] = 12 } } }); break;
            case 4: ApplyCommand(ScenarioCommand.Yellow); break;
            case 5: ApplyCommand(ScenarioCommand.SafetyCar); break;
            case 6: ApplyCommand(ScenarioCommand.Green); break;
            case 7: ApplyCommand(ScenarioCommand.VirtualSafetyCar); break;
            case 8: ApplyCommand(ScenarioCommand.RedFlag); break;
            case 9: ApplyCommand(ScenarioCommand.Green); break;
            case 10: ApplyCommand(ScenarioCommand.Disconnect); break;
            case 11: ApplyCommand(ScenarioCommand.Reconnect); break;
        }
    }

    private void ApplyCommand(ScenarioCommand command)
    {
        var (status, message) = command switch
        {
            ScenarioCommand.Yellow => ("2", "Yellow flag sector 2"), ScenarioCommand.DoubleYellow => ("3", "Double yellow sector 2"),
            ScenarioCommand.SafetyCar => ("4", "Safety car deployed"), ScenarioCommand.VirtualSafetyCar => ("6", "VSC deployed"),
            ScenarioCommand.VirtualSafetyCarEnding => ("7", "VSC ending"), ScenarioCommand.Green => ("1", "All clear"),
            ScenarioCommand.RedFlag => ("5", "Session red flagged"), _ => ("", "")
        };
        if (status.Length > 0) Feed("TrackStatus", new JsonObject { ["Status"] = status, ["Message"] = message });
        else if (command == ScenarioCommand.FastestLap) Feed("TimingData", new JsonObject { ["Lines"] = new JsonObject { ["1"] = new JsonObject { ["BestLapTime"] = new JsonObject { ["Value"] = "1:26.999", ["OverallFastest"] = true }, ["NumberOfLaps"] = 20 } } });
        else if (command == ScenarioCommand.Chequered) Feed("RaceControlMessages", new JsonObject { ["Messages"] = new JsonObject { [$"manual-{clock.UtcNow.Ticks}"] = new JsonObject { ["Flag"] = "CHEQUERED", ["Message"] = "Chequered flag", ["Lap"] = 57 } } });
        else if (command is ScenarioCommand.Disconnect or ScenarioCommand.Stale) { processor.SetConnection(command == ScenarioCommand.Stale ? ConnectionState.Stale : ConnectionState.Disconnected); ConnectionChanged?.Invoke(command == ScenarioCommand.Stale ? ConnectionState.Stale : ConnectionState.Disconnected); }
        else if (command == ScenarioCommand.Reconnect) { processor.SetConnection(ConnectionState.Connected); ConnectionChanged?.Invoke(ConnectionState.Connected); }
    }

    private void UpdatePositions(params (string id, int position, string gap)[] changes)
    {
        var lines = new JsonObject();
        foreach (var c in changes) lines[c.id] = new JsonObject { ["Position"] = c.position, ["GapToLeader"] = c.gap, ["IntervalToPositionAhead"] = c.position == 1 ? null : "+0.750", ["NumberOfLaps"] = 10 + _step };
        Feed("TimingData", new JsonObject { ["Lines"] = lines });
    }

    private void Feed(string topic, JsonObject data)
    {
        var delta = new TimingDelta(topic, data, clock.UtcNow); DeltaReceived?.Invoke(delta); processor.ProcessDelta(topic, data, delta.Timestamp);
    }
    private void Forward(TimingSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot);
    public async ValueTask DisposeAsync() { await StopAsync(); _advance.Dispose(); }

    private static JsonObject CreateInitial() => JsonNode.Parse("""
    {"SessionInfo":{"Name":"Race","Meeting":{"Name":"Synthetic Grand Prix","Circuit":{"ShortName":"Test Ring"}}},"LapCount":{"CurrentLap":1,"TotalLaps":57},"TrackStatus":{"Status":"1","Message":"All clear"},"DriverList":{"4":{"Tla":"NOR","FullName":"Lando Norris","TeamName":"McLaren"},"81":{"Tla":"PIA","FullName":"Oscar Piastri","TeamName":"McLaren"},"16":{"Tla":"LEC","FullName":"Charles Leclerc","TeamName":"Ferrari"},"1":{"Tla":"VER","FullName":"Max Verstappen","TeamName":"Red Bull Racing"},"63":{"Tla":"RUS","FullName":"George Russell","TeamName":"Mercedes"},"12":{"Tla":"ANT","FullName":"Kimi Antonelli","TeamName":"Mercedes"}},"TimingData":{"Lines":{"4":{"Position":"1","GapToLeader":"LEAD","NumberOfLaps":1},"81":{"Position":2,"GapToLeader":"+0.821","IntervalToPositionAhead":{"Value":"+0.821"},"NumberOfLaps":1},"16":{"Position":"3","GapToLeader":{"Value":"+1.510"},"IntervalToPositionAhead":"+0.689","NumberOfLaps":1},"1":{"Position":4,"GapToLeader":"+2.040","IntervalToPositionAhead":"+0.530","NumberOfLaps":1},"63":{"Position":5,"GapToLeader":"+2.800","IntervalToPositionAhead":"+0.760","NumberOfLaps":1},"12":{"Position":6,"GapToLeader":"+3.400","IntervalToPositionAhead":"+0.600","NumberOfLaps":1}}},"TimingAppData":{"Lines":{"4":{"Stints":{"0":{"Compound":"MEDIUM"}}},"81":{"Stints":{"0":{"Compound":"SOFT"}}},"16":{"Stints":{"0":{"Compound":"MEDIUM"}}},"1":{"Stints":{"0":{"Compound":"HARD"}}}}}}
    """)!.AsObject();
}
