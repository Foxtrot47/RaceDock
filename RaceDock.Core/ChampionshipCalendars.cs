// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>A published session or event that has not started yet.</summary>
public sealed record UpcomingSession(Championship Championship, string EventName, string? SessionName, DateTimeOffset StartUtc);

/// <summary>Reads one championship's published calendar. Implementations never throw for an empty calendar.</summary>
public interface IChampionshipCalendar
{
    Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>
/// Pure selection over each provider's calendar payload. The parsing is kept separate from the HTTP
/// clients so recorded payloads can drive the tests.
/// </summary>
public static class ChampionshipCalendars
{
    public static UpcomingSession? NextFormula1(IReadOnlyList<ScheduledSession> sessions, DateTimeOffset now) =>
        sessions.Where(session => session.Start > now)
            .OrderBy(session => session.Start)
            .Select(session => new UpcomingSession(Championship.Formula1, session.Meeting, session.Name, session.Start))
            .FirstOrDefault();

    /// <summary>
    /// MotoGP, Moto2, and Moto3 share one grand prix weekend, so a single event produces one entry per class.
    /// Tests and media days appear in the same feed and are excluded by <c>kind</c>.
    /// </summary>
    public static IReadOnlyList<UpcomingSession> NextMotorcycleGrandPrix(JsonArray? events, DateTimeOffset now)
    {
        var next = events?.OfType<JsonObject>()
            .Where(item => string.Equals(JsonSupport.String(item["kind"]), "GP", StringComparison.OrdinalIgnoreCase))
            .Select(item => (Name: JsonSupport.String(item["name"]), Start: ParseDate(JsonSupport.String(item["date_start"]))))
            .Where(item => item.Name is not null && item.Start > now)
            .OrderBy(item => item.Start)
            .Cast<(string? Name, DateTimeOffset? Start)?>()
            .FirstOrDefault();
        if (next is not { Name: { } rawName, Start: { } start }) return [];
        var name = Titleize(rawName);
        return
        [
            new(Championship.MotoGp, name, null, start),
            new(Championship.Moto2, name, null, start),
            new(Championship.Moto3, name, null, start),
        ];
    }

    public static UpcomingSession? NextDtm(JsonArray? sessions, DateTimeOffset now) =>
        sessions?.OfType<JsonObject>()
            .Select(item => (
                Event: JsonSupport.String(item["event"]?["name"]),
                Session: JsonSupport.String(item["name"]),
                Start: ParseDate(JsonSupport.String(item["startTime"]))))
            .Where(item => item.Start > now)
            .OrderBy(item => item.Start)
            .Select(item => new UpcomingSession(Championship.Dtm, item.Event ?? "DTM", item.Session, item.Start!.Value))
            .FirstOrDefault();

    /// <summary>GT World Challenge meetings carry a day-precision <c>dd.MM.yyyy</c> window and no session times.</summary>
    public static UpcomingSession? NextGtWorldChallenge(JsonObject? season, DateTimeOffset now)
    {
        if (season?["Meetings"] is not JsonObject meetings) return null;
        return meetings.Select(entry => entry.Value as JsonObject)
            .OfType<JsonObject>()
            .Select(meeting => (
                Name: JsonSupport.String(meeting["Name"]),
                Start: ParseDayPrecisionDate(JsonSupport.String(meeting["BeginDate"]))))
            .Where(meeting => meeting.Name is not null && meeting.Start > now)
            .OrderBy(meeting => meeting.Start)
            .Select(meeting => new UpcomingSession(Championship.GtWorldChallengeEurope, meeting.Name!, null, meeting.Start!.Value))
            .FirstOrDefault();
    }

    /// <summary>Plain-language lead time such as "in 3 days", used next to the exact start time.</summary>
    public static string DescribeTimeUntil(DateTimeOffset start, DateTimeOffset now)
    {
        var remaining = start - now;
        if (remaining <= TimeSpan.Zero) return "under way";
        if (remaining < TimeSpan.FromMinutes(1)) return "in under a minute";
        if (remaining < TimeSpan.FromHours(1)) return Describe((int)remaining.TotalMinutes, "minute");
        if (remaining < TimeSpan.FromDays(1)) return Describe((int)remaining.TotalHours, "hour");
        return Describe((int)remaining.TotalDays, "day");
    }

    private static string Describe(int count, string unit) => $"in {count} {unit}{(count == 1 ? string.Empty : "s")}";

    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    private static DateTimeOffset? ParseDayPrecisionDate(string? value) =>
        DateTimeOffset.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    private static readonly HashSet<string> LowercaseWords =
        new(StringComparer.OrdinalIgnoreCase) { "of", "the", "and", "at", "in", "for", "de", "del", "da", "di", "du" };

    /// <summary>The motorcycle feed publishes event names in upper case, which reads as shouting in the UI.</summary>
    private static string Titleize(string value)
    {
        if (value.Any(char.IsLower)) return value;
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select((word, index) => TitleizeWord(word, index == 0)));
    }

    private static string TitleizeWord(string word, bool isFirst)
    {
        if (!isFirst && LowercaseWords.Contains(word)) return word.ToLowerInvariant();
        // Short all-letter tokens are acronyms such as GP or USA and stay as published.
        if (word.Length <= 3 && word.All(char.IsLetter)) return word;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word.ToLowerInvariant());
    }
}
