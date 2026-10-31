// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Chooses and phrases the session the widget shows while nothing is on track. The wording is deliberately
/// short: it shares the two lines the live widget uses for the event and the session progress.
/// </summary>
public static class NextSessionDisplay
{
    /// <summary>
    /// How long a session that has reached its published start stays listed while its timing has not arrived. The
    /// calendars publish no end time to wait for instead, so a session that is simply late drops off after this.
    /// </summary>
    public static TimeSpan WaitingWindow { get; } = TimeSpan.FromMinutes(30);

    /// <summary>The soonest session that has not started yet, or is due and still waiting for its timing.</summary>
    public static UpcomingSession? Next(IEnumerable<UpcomingSession> sessions, DateTimeOffset now, TimeZoneInfo? timeZone = null) =>
        sessions.Where(session => IsScheduled(session, now, timeZone)).MinBy(session => session.StartUtc);

    /// <summary>
    /// Whether the session is due but has not reported: its published start has passed and it is still inside the
    /// waiting window. A date-only entry names no moment, so it never waits.
    /// </summary>
    public static bool IsWaiting(UpcomingSession session, DateTimeOffset now)
    {
        if (!session.HasStartTime) return false;
        var elapsed = now - session.StartUtc;
        return elapsed >= TimeSpan.Zero && elapsed <= WaitingWindow;
    }

    /// <summary>Whether the schedule still lists the session: ahead of its start, or due and waiting for data.</summary>
    public static bool IsScheduled(UpcomingSession session, DateTimeOffset now, TimeZoneInfo? timeZone = null) =>
        session.IsAhead(now, timeZone ?? TimeZoneInfo.Local) || IsWaiting(session, now);

    /// <summary>Top line: which championship, and which event.</summary>
    public static string Headline(UpcomingSession session) =>
        string.IsNullOrWhiteSpace(session.EventName)
            ? ChampionshipDisplay.ShortName(session.Championship)
            : $"{ChampionshipDisplay.ShortName(session.Championship)} · {ChampionshipDisplay.ShortEventName(session.Championship, session.EventName)}";

    /// <summary>Bottom line: which session, and when it starts.</summary>
    public static string Detail(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        // A session whose start has passed has nothing left to count down to: it is waiting for its feed.
        var when = IsWaiting(session, now) ? "Waiting for data" : When(session, now, timeZone);
        return string.IsNullOrWhiteSpace(session.SessionName) ? when : $"{session.SessionName} · {when}";
    }

    /// <summary>Every published session the schedule still lists, soonest first.</summary>
    public static IReadOnlyList<UpcomingSession> Schedule(IEnumerable<UpcomingSession> sessions, DateTimeOffset now, TimeZoneInfo? timeZone = null) =>
        sessions.Where(session => IsScheduled(session, now, timeZone)).OrderBy(session => session.StartUtc).ToList();

    /// <summary>
    /// Schedule row second line: the session, and where it is held. The venue is dropped when the event name
    /// already carries it, because "07 Sachsenring — Sachsenring" tells the reader nothing twice.
    /// </summary>
    public static string RowSubtitle(UpcomingSession session)
    {
        var eventName = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName);
        var venue = ChampionshipDisplay.ShortVenue(session.Venue);
        if (Repeats(eventName, venue)) venue = null;
        return string.Join(" \u00b7 ", new[] { session.SessionName, venue }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    /// <summary>Heading for a day of the agenda, named relatively while that is unambiguous.</summary>
    public static string GroupDay(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        // A date-only entry is grouped by the date the calendar published, which no timezone conversion may move.
        var date = session.StartDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.StartUtc, timeZone).Date);
        var days = date.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date).DayNumber;
        var day = days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            < 7 => date.ToString("dddd d MMM"),
            _ => date.ToString("ddd d MMM")
        };
        return day.ToUpperInvariant();
    }

    /// <summary>
    /// Schedule row clock time in the reader's own timezone, or empty when the calendar published a date only:
    /// a meeting window carries no start time, and inventing midnight would read as a real session.
    /// </summary>
    public static string RowTime(UpcomingSession session, TimeZoneInfo timeZone) =>
        session.HasStartTime ? TimeZoneInfo.ConvertTime(session.StartUtc, timeZone).ToString("HH:mm") : "";

    /// <summary>The row's date, taken from the published date when the source carries no clock time.</summary>
    public static string RowDate(UpcomingSession session, TimeZoneInfo timeZone) =>
        (session.StartDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(session.StartUtc, timeZone).Date))
            .ToString("ddd d MMM");

    /// <summary>
    /// Schedule row lead time. A date-only entry is counted in calendar days against the reader's own date, so it
    /// stays "today"/"tomorrow" instead of counting down to the midnight that only orders it.
    /// </summary>
    public static string RowLead(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (IsWaiting(session, now)) return "waiting for data";
        if (session.StartDate is not { } date) return ChampionshipCalendars.DescribeTimeUntil(session.StartUtc, now);
        var days = date.DayNumber - DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date).DayNumber;
        return days switch
        {
            < 0 => "under way",
            0 => "today",
            1 => "tomorrow",
            < 7 => $"in {days} days",
            _ => date.ToString("ddd d MMM")
        };
    }

    private static bool Repeats(string eventName, string? venue) =>
        !string.IsNullOrWhiteSpace(venue)
        && (eventName.Contains(venue, StringComparison.OrdinalIgnoreCase)
            || venue.Contains(eventName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The unabbreviated form, for the tooltip that the two short lines cannot carry.</summary>
    public static string Tooltip(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        var name = ChampionshipDisplay.FullName(session.Championship);
        var subject = string.IsNullOrWhiteSpace(session.EventName)
            ? name
            : $"{name} · {ChampionshipDisplay.ShortEventName(session.Championship, session.EventName)}";
        // A date-only entry keeps the published date and says the time is unknown instead of inventing one.
        var start = session.StartDate is { } date
            ? $"{date:ddd d MMM}, time TBA"
            : TimeZoneInfo.ConvertTime(session.StartUtc, timeZone).ToString("ddd d MMM, HH:mm");
        var what = string.IsNullOrWhiteSpace(session.SessionName) ? start : $"{session.SessionName}, {start}";
        // A session that has started is named by what the reader is waiting for, not by a start time that has gone.
        return IsWaiting(session, now) ? $"Waiting for data: {subject} — {what}" : $"Next session: {subject} — {what}";
    }

    /// <summary>
    /// Counts down while the wait is short enough for a countdown to mean something, and switches to the
    /// local clock time once it is not. A date-only entry has no clock time to count down to.
    /// </summary>
    private static string When(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (session.StartDate is { } date) return date.ToString("ddd d MMM");
        var startUtc = session.StartUtc;
        var wait = startUtc - now;
        if (wait <= TimeSpan.Zero) return "now";
        if (wait < TimeSpan.FromHours(1)) return $"in {Math.Max(1, (int)wait.TotalMinutes)}m";
        if (wait < TimeSpan.FromHours(24)) return $"in {(int)wait.TotalHours}h {wait.Minutes:00}m";
        var local = TimeZoneInfo.ConvertTime(startUtc, timeZone);
        return wait < TimeSpan.FromDays(7) ? local.ToString("ddd HH:mm") : local.ToString("ddd d MMM");
    }
}
