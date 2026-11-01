namespace RaceDock.Core;

/// <summary>What the taskbar's status edge reports: which coloured strip, and the code it prints.</summary>
public enum TaskbarFlagStrip { None, Caution, RedFlag, Finished }

/// <summary>A strip state together with the code the strip writes down its length, if it writes one.</summary>
public sealed record TaskbarFlagBadge(TaskbarFlagStrip Strip, string? Code);

public static class TaskbarFlagDisplay
{
    /// <summary>
    /// The badge for the selected snapshot. A clear track and an unknown one both print nothing: the strip is there to
    /// say what needs saying, so a green flag takes no room at all. Safety car periods count as cautions and say which;
    /// the chequered tiles come only from that snapshot's ended lifecycle, never a shared transient alert.
    /// </summary>
    public static TaskbarFlagBadge Badge(TrackCondition trackCondition, bool finished = false)
    {
        return trackCondition switch
        {
            TrackCondition.Yellow or TrackCondition.DoubleYellow => new(TaskbarFlagStrip.Caution, "YEL"),
            TrackCondition.FullCourseYellow => new(TaskbarFlagStrip.Caution, "FCY"),
            TrackCondition.SafetyCar => new(TaskbarFlagStrip.Caution, "SC"),
            TrackCondition.VirtualSafetyCar or TrackCondition.VirtualSafetyCarEnding => new(TaskbarFlagStrip.Caution, "VSC"),
            TrackCondition.RedFlag => new(TaskbarFlagStrip.RedFlag, "RED"),
            _ => finished
                ? new(TaskbarFlagStrip.Finished, null)
                : new(TaskbarFlagStrip.None, null)
        };
    }
}
