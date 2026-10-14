// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace RaceDock.Core;

public enum ConnectionState { Disconnected, Connecting, Connected, Stale, Faulted }
public enum SessionLifecycle { OffSession, PreSession, Live, Ended }
public enum Championship
{
    Unknown = 0,
    Formula1 = 1,
    Formula2 = 2,
    Formula3 = 3,
    WorldRallyChampionship = 4,
    GtWorldChallengeEurope = 5,
    Imsa = 6,
    MotoGp = 7,
    Moto2 = 8,
    Moto3 = 9,
    FormulaE = 10,
    Dtm = 11,
    IndyCar = 12,
    WorldEnduranceChampionship = 13,
    EuropeanLeMansSeries = 14,
    MichelinLeMansCup = 15
}
public enum TrackCondition { Unknown, AllClear, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, RedFlag }
public enum SectorTimingState { None, OverallBest, PersonalBest, Slower }

public sealed record SectorTiming(string Value, SectorTimingState State);

/// <summary>
/// State of one F1 timing-loop segment ("mini-sector") on the lap in progress, as published in
/// TimingData Lines[n].Sectors[i].Segments[j].Status.
/// </summary>
public enum MiniSectorState { NotReached, Slower, PersonalBest, OverallBest, PitLane, Other }
public sealed record SectorTimingSet(SectorTiming? Sector1, SectorTiming? Sector2, SectorTiming? Sector3)
{
    public SectorTiming? At(int index) => index switch
    {
        0 => Sector1,
        1 => Sector2,
        2 => Sector3,
        _ => null
    };
}

public enum AlertKind { Information, SessionStart, FastestLap, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Chequered, RedFlag }

public sealed record CompetitorStanding(
    string DriverId, int Position, string Code, string Name, string Team,
    string? GapToLeader, string? IntervalToPositionAhead, string? Tyre, int Lap,
    bool InPit, bool Retired, bool Stopped, bool IsOverallFastest,
    string? ResultTime = null, string? StatusLabel = null, string? PositionLabel = null, string? Category = null,
    string? Manufacturer = null, string? LastLapTime = null,
    SectorTimingSet? CurrentSectors = null, SectorTimingSet? BestLapSectors = null,
    IReadOnlyList<IReadOnlyList<MiniSectorState>>? CurrentMiniSectors = null);

/// <summary>Which lap time a session is judged on: the best lap set, or the most recent one.</summary>
public enum LapTimeMode { BestLap, LastLap }
public enum SectorTimeMode { CurrentLast, BestLap }

public sealed record TimingSnapshot(
    string Meeting, string Session, string Circuit, int CurrentLap, int? TotalLaps,
    TrackCondition TrackCondition, IReadOnlyList<CompetitorStanding> Competitors,
    DateTimeOffset FreshnessTimestamp, SessionLifecycle Lifecycle,
    ConnectionState ConnectionState = ConnectionState.Connected, string? TimeRemaining = null,
    Championship Championship = Championship.Unknown)
{
    public string? SessionShortName { get; init; }

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

public interface ITimingPlaybackSource : ILiveTimingSource
{
    bool IsPaused { get; }
    double Speed { get; set; }
    void Pause();
    void Resume();
    void Restart();
    void Step();
}

public interface ITimingScenarioSource : ITimingPlaybackSource
{
    void Trigger(ScenarioCommand command);
}

public interface ITimingArchiveSource : ITimingPlaybackSource
{
    string ArchivePath { get; }
    string ArchiveName { get; }
    bool IsCompleted { get; }
    int FrameIndex { get; }
    int FrameCount { get; }
}

public enum ScenarioCommand { Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Green, FastestLap, RedFlag, Chequered, Disconnect, Stale, Reconnect }

public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

public interface IAsyncDelay { Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken); }
public sealed class SystemAsyncDelay : IAsyncDelay { public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.Delay(duration, cancellationToken); }

public interface IAlertArbiter
{
    event Action<RaceAlert?>? VisibleAlertChanged;
    RaceAlert? Current { get; }
    void Accept(RaceAlert alert);
    void ApplyTrackCondition(TrackCondition condition, string? detail, int? lap, DateTimeOffset timestamp);
    void Tick();
    void Clear();
}
