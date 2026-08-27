// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

namespace RaceDock.App.Settings;

/// <summary>One published event on the Home page, formatted for display in the user's local time.</summary>
internal sealed class UpcomingSessionItem
{
    public UpcomingSessionItem(UpcomingSession session, string championshipName, string iconSource, DateTimeOffset now)
    {
        ChampionshipName = championshipName;
        IconSource = iconSource;
        EventName = session.EventName;
        SessionName = session.SessionName ?? string.Empty;
        HasSessionName = !string.IsNullOrWhiteSpace(session.SessionName);
        var local = session.StartUtc.ToLocalTime();
        // Meetings without a published session time carry a date only, so the clock would be misleading.
        StartText = session.StartUtc.TimeOfDay == TimeSpan.Zero
            ? local.ToString("ddd d MMM")
            : local.ToString("ddd d MMM, HH:mm");
        RelativeText = ChampionshipCalendars.DescribeTimeUntil(session.StartUtc, now);
    }

    public string ChampionshipName { get; }
    public string IconSource { get; }
    public string EventName { get; }
    public string SessionName { get; }
    public bool HasSessionName { get; }
    public string StartText { get; }
    public string RelativeText { get; }
}
