// Copyright (c) 2026 MotorsportTaskbar contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace MotorsportTaskbar.Core;

public enum ConnectionState { Disconnected, Connecting, Connected, Stale, Faulted }
public enum SessionLifecycle { OffSession, PreSession, Live, Ended }
public enum TrackCondition { Unknown, AllClear, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, RedFlag }
public enum AlertKind { Information, SessionStart, FastestLap, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Chequered, RedFlag }

public sealed record CompetitorStanding(
    string DriverId, int Position, string Code, string Name, string Team,
    string? GapToLeader, string? IntervalToPositionAhead, string? Tyre, int Lap,
    bool InPit, bool Retired, bool Stopped, bool IsOverallFastest,
    string? ResultTime = null, string? StatusLabel = null, string? PositionLabel = null, string? Category = null);

public sealed record TimingSnapshot(
    string Meeting, string Session, string Circuit, int CurrentLap, int? TotalLaps,
    TrackCondition TrackCondition, IReadOnlyList<CompetitorStanding> Competitors,
    DateTimeOffset FreshnessTimestamp, SessionLifecycle Lifecycle,
    ConnectionState ConnectionState = ConnectionState.Connected, string? TimeRemaining = null)
{
    public static TimingSnapshot Hidden(DateTimeOffset now) =>
        new("", "", "", 0, null, TrackCondition.Unknown, [], now, SessionLifecycle.OffSession, ConnectionState.Disconnected);
}

public sealed record RaceAlert(
    AlertKind Kind, int Priority, bool Persistent, string Title, string? Detail,
    int? Lap, DateTimeOffset SourceTimestamp, string DeduplicationKey,
    TimeSpan? Duration = null);

public sealed record TimingDelta(string Topic, System.Text.Json.Nodes.JsonNode Data, DateTimeOffset Timestamp);
public sealed record FeedFailure(string Message, Exception? Exception, DateTimeOffset Timestamp);

public interface ILiveTimingSource : IAsyncDisposable
{
    event Action<TimingSnapshot>? SnapshotReceived;
    event Action<TimingDelta>? DeltaReceived;
    event Action<ConnectionState>? ConnectionChanged;
    event Action<FeedFailure>? Failed;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
}

public interface ITimingScenarioSource : ILiveTimingSource
{
    bool IsPaused { get; }
    double Speed { get; set; }
    void Pause();
    void Resume();
    void Restart();
    void Step();
    void Trigger(ScenarioCommand command);
    void SetTopFive(IReadOnlyList<ManualStanding> standings);
}

public sealed record ManualStanding(string Code, string? GapToLeader);

public enum ScenarioCommand { Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Green, FastestLap, RedFlag, Chequered, Disconnect, Stale, Reconnect }

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public interface IAlertArbiter
{
    event Action<RaceAlert?>? VisibleAlertChanged;
    RaceAlert? Current { get; }
    void Accept(RaceAlert alert);
    void ApplyTrackCondition(TrackCondition condition, string? detail, int? lap, DateTimeOffset timestamp);
    void Tick();
    void Clear();
}
