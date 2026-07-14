// SPDX-License-Identifier: GPL-3.0-or-later
namespace MotorsportTaskbar.Core;

public sealed class AlertArbiter(IClock clock) : IAlertArbiter
{
    private readonly HashSet<string> _seen = [];
    private DateTimeOffset? _expires;
    public event Action<RaceAlert?>? VisibleAlertChanged;
    public RaceAlert? Current { get; private set; }

    public void Accept(RaceAlert alert)
    {
        if (!_seen.Add(alert.DeduplicationKey)) return;
        if (Current is { Persistent: true } current && alert.Priority < current.Priority) return;
        if (Current is { Persistent: true } && !alert.Persistent) return;
        if (Current is { } existing && alert.Priority < existing.Priority && _expires > clock.UtcNow) return;
        Set(alert, alert.Persistent ? null : clock.UtcNow + (alert.Duration ?? TimeSpan.FromSeconds(4)));
    }

    public void ApplyTrackCondition(TrackCondition condition, string? detail, int? lap, DateTimeOffset timestamp)
    {
        if (condition == TrackCondition.AllClear)
        {
            if (Current is { Kind: AlertKind.RedFlag or AlertKind.SafetyCar or AlertKind.VirtualSafetyCar or AlertKind.VirtualSafetyCarEnding or AlertKind.Yellow or AlertKind.DoubleYellow })
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
            _ => (AlertKind.Information, 0, "")
        };
        if (priority == 0) return;
        Accept(new(kind, priority, true, title, detail, lap, timestamp, $"track:{condition}:{detail}:{lap}"));
    }

    public void Tick()
    {
        if (_expires is not null && clock.UtcNow >= _expires) Set(null, null);
    }

    public void Clear() { _seen.Clear(); Set(null, null); }

    private void Set(RaceAlert? alert, DateTimeOffset? expires)
    {
        if (Equals(Current, alert)) return;
        Current = alert; _expires = expires; VisibleAlertChanged?.Invoke(alert);
    }
}
