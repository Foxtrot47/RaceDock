namespace MotorsportTaskbar.Core;

public static class TaskbarFlagDisplay
{
    public static AlertKind? Resolve(TrackCondition trackCondition, AlertKind? transientAlert = null)
    {
        if (transientAlert == AlertKind.Chequered) return AlertKind.Chequered;
        return trackCondition switch
        {
            TrackCondition.RedFlag => AlertKind.RedFlag,
            TrackCondition.DoubleYellow => AlertKind.DoubleYellow,
            TrackCondition.Yellow => AlertKind.Yellow,
            _ => null
        };
    }
}
