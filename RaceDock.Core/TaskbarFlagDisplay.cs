namespace RaceDock.Core;

/// <summary>What the taskbar's status edge reports: which coloured strip, and the code it prints.</summary>
public enum TaskbarFlagStrip { None, Caution, RedFlag, Finished }

/// <summary>A strip state together with the code the strip writes down its length, if it writes one.</summary>
public sealed record TaskbarFlagBadge(TaskbarFlagStrip Strip, string? Code);

public static class TaskbarFlagDisplay
{
    /// <summary>
    /// The badge for a track condition. A clear track and an unknown one both print nothing: the strip is there to say
    /// what needs saying, so a green flag takes no room at all. Safety car periods count as cautions and say which,
    /// and a session that has ended shows the chequered tiles whether or not a chequered alert reached the surface:
    /// replay sources keep their own alert arbiter, so the end of a session is read from its lifecycle instead.
    /// </summary>
    public static TaskbarFlagBadge Badge(TrackCondition trackCondition, AlertKind? transientAlert = null, bool finished = false)
    {
        return trackCondition switch
        {
            TrackCondition.Yellow or TrackCondition.DoubleYellow => new(TaskbarFlagStrip.Caution, "YEL"),
            TrackCondition.FullCourseYellow => new(TaskbarFlagStrip.Caution, "FCY"),
            TrackCondition.SafetyCar => new(TaskbarFlagStrip.Caution, "SC"),
            TrackCondition.VirtualSafetyCar or TrackCondition.VirtualSafetyCarEnding => new(TaskbarFlagStrip.Caution, "VSC"),
            TrackCondition.RedFlag => new(TaskbarFlagStrip.RedFlag, "RED"),
            _ => finished || transientAlert == AlertKind.Chequered
                ? new(TaskbarFlagStrip.Finished, null)
                : new(TaskbarFlagStrip.None, null)
        };
    }
}
