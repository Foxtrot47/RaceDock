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
    MichelinLeMansCup = 15,
    NascarCup = 16,
    NascarXfinity = 17,
    NascarTrucks = 18,
    F1Academy = 19
}
public enum TrackCondition { Unknown, AllClear, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, RedFlag, FullCourseYellow }
public enum SectorTimingState { None, OverallBest, PersonalBest, Slower }

public sealed record SectorTiming(string Value, SectorTimingState State);

/// <summary>
/// State of one F1 timing-loop segment ("mini-sector") on the lap in progress, as published in
/// TimingData Lines[n].Sectors[i].Segments[j].Status.
/// </summary>
public enum MiniSectorState { NotReached, Slower, PersonalBest, OverallBest, PitLane, Other }
public sealed record RallySplitPoint(int SplitPointId, int Number, double DistanceKm);
public sealed record RallySplitTiming(int SplitPointId, string Value);
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

public enum AlertKind { Information, SessionStart, FastestLap, Yellow, DoubleYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Chequered, RedFlag, FullCourseYellow }

public sealed record CompetitorStanding(
    string DriverId, int Position, string Code, string Name, string Team,
    string? GapToLeader, string? IntervalToPositionAhead, string? Tyre, int Lap,
    bool InPit, bool Retired, bool Stopped, bool IsOverallFastest,
    string? ResultTime = null, string? StatusLabel = null, string? PositionLabel = null, string? Category = null,
    string? Manufacturer = null, string? LastLapTime = null,
    SectorTimingSet? CurrentSectors = null, SectorTimingSet? BestLapSectors = null,
    IReadOnlyList<RallySplitTiming>? StageSplits = null,
    IReadOnlyList<IReadOnlyList<MiniSectorState>>? CurrentMiniSectors = null)
{
    /// <summary>The provider's car entry number, distinct from position and internal competitor identity.</summary>
    public string? CarNumber { get; init; }
    public CarTyres? CurrentTyres { get; init; }
    public int? TyreAge { get; init; }
    public double? VirtualEnergyPercent { get; init; }

    /// <summary>
    /// Whether the championship counts this competitor as playoff-eligible (NASCAR's <c>is_in_chase</c>).
    /// Null when the feed publishes no such field, so an absent flag never reads as "not eligible".
    /// </summary>
    public bool? IsPlayoffEligible { get; init; }
}

/// <summary>Which lap time a session is judged on: the best lap set, or the most recent one.</summary>
public enum LapTimeMode { BestLap, LastLap }
public enum SectorTimeMode { CurrentLast, BestLap }

public sealed record WheelTyre(int? AgeInLaps, string? Compound);
public sealed record CarTyres(WheelTyre? FrontLeft, WheelTyre? FrontRight, WheelTyre? RearLeft, WheelTyre? RearRight);

public sealed record TimingSnapshot(
    string Meeting, string Session, string Circuit, int CurrentLap, int? TotalLaps,
    TrackCondition TrackCondition, IReadOnlyList<CompetitorStanding> Competitors,
    DateTimeOffset FreshnessTimestamp, SessionLifecycle Lifecycle,
    ConnectionState ConnectionState = ConnectionState.Connected, string? TimeRemaining = null,
    Championship Championship = Championship.Unknown)
{
    public string? SessionShortName { get; init; }
    public IReadOnlyList<RallySplitPoint> StageSplitPoints { get; init; } = [];

    /// <summary>
    /// The stage being run (NASCAR's <c>stage.stage_num</c>) and, when the feed publishes it, the lap that
    /// stage ends on and its length. All three stay null outside a series that runs stages.
    /// </summary>
    public int? StageNumber { get; init; }
    public int? StageFinishLap { get; init; }
    public int? StageLength { get; init; }

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

/// <summary>One championship a composite can currently display, in rotation order.</summary>
public sealed record LiveFeedStatus(Championship Championship, bool Retained);

/// <summary>
/// A live source that runs several championship adapters at once and reports what each of them publishes,
/// independently of which one the rotation happens to be displaying.
/// </summary>
public interface ILiveFeedDirectory
{
    /// <summary>Every snapshot any adapter accepts, not only the rotation-selected one.</summary>
    event Action<TimingSnapshot>? FeedSnapshotReceived;

    /// <summary>Raised when a championship starts, ends, is retained, or leaves the displayable set.</summary>
    event Action<IReadOnlyList<LiveFeedStatus>>? LiveFeedsChanged;

    /// <summary>The championships that currently have a classification to show, in rotation order.</summary>
    IReadOnlyList<LiveFeedStatus> LiveFeeds { get; }
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

public enum ScenarioCommand { Yellow, DoubleYellow, FullCourseYellow, SafetyCar, VirtualSafetyCar, VirtualSafetyCarEnding, Green, FastestLap, RedFlag, Chequered, Disconnect, Stale, Reconnect }

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
    /// <summary>Withdraws an alert of this kind whose moment has passed, so it stops outranking what follows.</summary>
    void Dismiss(AlertKind kind);
}
