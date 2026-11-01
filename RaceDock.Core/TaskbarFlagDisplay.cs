namespace RaceDock.Core;

/// <summary>What the taskbar's status edge reports: which coloured strip, and the code it prints.</summary>
public enum TaskbarFlagStrip { None, Caution, RedFlag, Finished }

/// <summary>A strip state together with the code the strip writes down its length, if it writes one.</summary>
public sealed record TaskbarFlagBadge(TaskbarFlagStrip Strip, string? Code);

public static class TaskbarFlagDisplay
{
    /// <summary>
    /// The strip for a track condition. A clear or unknown track prints nothing, safety car periods count as cautions,
    /// and only an ended session shows the chequered tiles.
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
