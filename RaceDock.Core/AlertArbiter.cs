// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public sealed class AlertArbiter(IClock clock) : IAlertArbiter
{
    private readonly HashSet<string> _seen = [];
    private DateTimeOffset? _expires;
    private TrackCondition? _lastTrackCondition;
    private string? _lastTrackDetail;
    private int _trackOccurrence;
    public event Action<RaceAlert?>? VisibleAlertChanged;
    public RaceAlert? Current { get; private set; }

    public void Accept(RaceAlert alert)
    {
        if (_seen.Contains(alert.DeduplicationKey)) return;
        if (Current is { Persistent: true } current && alert.Priority < current.Priority) return;
        if (Current is { Persistent: true } && !alert.Persistent) return;
        if (Current is { } existing && alert.Priority < existing.Priority && _expires > clock.UtcNow) return;
        // Only an alert that was actually shown is consumed, so an alert that lost the priority race can still
        // appear if it is submitted again once the higher-priority alert has cleared.
        _seen.Add(alert.DeduplicationKey);
        Set(alert, alert.Persistent ? null : clock.UtcNow + (alert.Duration ?? TimeSpan.FromSeconds(4)));
    }

    public void ApplyTrackCondition(TrackCondition condition, string? detail, int? lap, DateTimeOffset timestamp)
    {
        if (condition == _lastTrackCondition && string.Equals(detail, _lastTrackDetail, StringComparison.Ordinal))
        {
            // Feeds resubmit the current state on every message; unchanged samples are the same occurrence.
            return;
        }

        _lastTrackCondition = condition;
        _lastTrackDetail = detail;
        if (condition == TrackCondition.AllClear)
        {
            if (Current is { Kind: AlertKind.RedFlag or AlertKind.SafetyCar or AlertKind.VirtualSafetyCar or AlertKind.VirtualSafetyCarEnding or AlertKind.Yellow or AlertKind.DoubleYellow or AlertKind.FullCourseYellow })
                Set(null, null);
            return;
        }

        var (kind, priority, title) = condition switch
        {
            TrackCondition.RedFlag => (AlertKind.RedFlag, 100, "RED FLAG"),
            TrackCondition.SafetyCar => (AlertKind.SafetyCar, 80, "SAFETY CAR"),
            TrackCondition.VirtualSafetyCar => (AlertKind.VirtualSafetyCar, 70, "VIRTUAL SAFETY CAR"),
            TrackCondition.VirtualSafetyCarEnding => (AlertKind.VirtualSafetyCarEnding, 70, "VSC ENDING"),
            TrackCondition.DoubleYellow => (AlertKind.DoubleYellow, 60, "DOUBLE YELLOW"),
            TrackCondition.Yellow => (AlertKind.Yellow, 60, "YELLOW FLAG"),
            TrackCondition.FullCourseYellow => (AlertKind.FullCourseYellow, 60, "FULL COURSE YELLOW"),
            _ => (AlertKind.Information, 0, "")
        };
        if (priority == 0) return;
        // Each transition is its own occurrence, so a second yellow later in the session is not swallowed by the
        // first one's deduplication key.
        _trackOccurrence++;
        Accept(new(kind, priority, true, title, detail, lap, timestamp,
            $"track:{condition}:{detail}:{lap}:{_trackOccurrence}"));
    }

    public void Tick()
    {
        if (_expires is not null && clock.UtcNow >= _expires) Set(null, null);
    }

    /// <summary>Withdraws a spent alert so it stops outranking what follows, without letting it be raised again.</summary>
    public void Dismiss(AlertKind kind)
    {
        if (Current?.Kind == kind) Set(null, null);
    }

    public void Clear()
    {
        _seen.Clear();
        _lastTrackCondition = null;
        _lastTrackDetail = null;
        _trackOccurrence = 0;
        Set(null, null);
    }

    private void Set(RaceAlert? alert, DateTimeOffset? expires)
    {
        if (Equals(Current, alert)) return;
        Current = alert; _expires = expires; VisibleAlertChanged?.Invoke(alert);
    }
}
