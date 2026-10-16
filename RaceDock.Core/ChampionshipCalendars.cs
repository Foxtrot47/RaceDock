// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>A published session or event that has not started yet.</summary>
/// <param name="Venue">Circuit, track, or rally base as the source publishes it, absent when it publishes none.</param>
public sealed record UpcomingSession(Championship Championship, string EventName, string? SessionName, DateTimeOffset StartUtc, string? Venue = null);

/// <summary>Reads one championship's published calendar. Implementations never throw for an empty calendar.</summary>
public interface IChampionshipCalendar
{
    /// <summary>Championships this calendar covers, so a source is only contacted for a championship the user enabled.</summary>
    IReadOnlyList<Championship> Championships { get; }

    Task<IReadOnlyList<UpcomingSession>> GetUpcomingAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Every remaining session of each covered championship's next event, soonest first. A calendar that cannot
    /// see beyond the next session reports just that one.
    /// </summary>
    Task<IReadOnlyList<UpcomingSession>> GetEventScheduleAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        GetUpcomingAsync(now, cancellationToken);
}

/// <summary>
/// Pure selection over each provider's calendar payload. The parsing is kept separate from the HTTP
/// clients so recorded payloads can drive the tests.
/// </summary>
public static class ChampionshipCalendars
{
    public static UpcomingSession? NextFormula1(IReadOnlyList<ScheduledSession> sessions, DateTimeOffset now) =>
        Formula1Sessions(sessions, now).FirstOrDefault();

    /// <summary>The remaining sessions of the next grand prix weekend.</summary>
    public static IReadOnlyList<UpcomingSession> Formula1Weekend(IReadOnlyList<ScheduledSession> sessions, DateTimeOffset now) =>
        SameEvent(Formula1Sessions(sessions, now));

    private static IEnumerable<UpcomingSession> Formula1Sessions(IReadOnlyList<ScheduledSession> sessions, DateTimeOffset now) =>
        sessions.Where(session => session.Start > now)
            .OrderBy(session => session.Start)
            .Select(session => new UpcomingSession(Championship.Formula1, session.Meeting, session.Name, session.Start, session.Venue));

    /// <summary>The sessions that belong to the first (soonest) session's event.</summary>
    private static IReadOnlyList<UpcomingSession> SameEvent(IEnumerable<UpcomingSession> ordered)
    {
        var sessions = ordered.ToList();
        return sessions.Count == 0 ? [] : [.. sessions.Where(session => session.EventName == sessions[0].EventName)];
    }

    /// <summary>
    /// MotoGP, Moto2, and Moto3 share one grand prix weekend, so a single event produces one entry per class.
    /// Tests and media days appear in the same feed and are excluded by <c>kind</c>. A weekend that has already
    /// begun stays selected until it ends, because that is the race weekend a viewer cares about.
    /// </summary>
    public static IReadOnlyList<UpcomingSession> NextMotorcycleGrandPrix(JsonArray? events, DateTimeOffset now) =>
        MotorcycleGrandPrix(events, now, sessionsPerClass: 1);

    /// <summary>Every remaining session of the selected grand prix weekend, for each class.</summary>
    public static IReadOnlyList<UpcomingSession> MotorcycleGrandPrixWeekend(JsonArray? events, DateTimeOffset now) =>
        MotorcycleGrandPrix(events, now, sessionsPerClass: int.MaxValue);

    private static IReadOnlyList<UpcomingSession> MotorcycleGrandPrix(JsonArray? events, DateTimeOffset now, int sessionsPerClass)
    {
        if (SelectGrandPrixWeekend(events, now) is not { } weekend) return [];
        var name = Titleize(weekend.Name);
        var venue = JsonSupport.String(weekend.Event["circuit"]?["name"]);
        return MotorcycleClasses
            .SelectMany(entry =>
            {
                var sessions = MotorcycleSessions(weekend.Event, entry.Acronym, now).Take(sessionsPerClass).ToList();
                return sessions.Count > 0
                    ? sessions.Select(session => new UpcomingSession(entry.Championship, name, session.Name, session.Start, venue))
                    : [new UpcomingSession(entry.Championship, name, null, weekend.Start, venue)];
            })
            .ToList();
    }

    private static (JsonObject Event, string Name, DateTimeOffset Start)? SelectGrandPrixWeekend(JsonArray? events, DateTimeOffset now) =>
        events?.OfType<JsonObject>()
            .Where(item => string.Equals(JsonSupport.String(item["kind"]), "GP", StringComparison.OrdinalIgnoreCase))
            .Select(item => (
                Event: item,
                Name: JsonSupport.String(item["name"]),
                Start: ParseDate(JsonSupport.String(item["date_start"])),
                End: ParseDate(JsonSupport.String(item["date_end"]))))
            .Where(item => item.Name is not null && item.Start is not null && (item.End ?? item.Start) > now)
            .OrderBy(item => item.Start)
            .Select(item => ((JsonObject Event, string Name, DateTimeOffset Start)?)(item.Event, item.Name!, item.Start!.Value))
            .FirstOrDefault();

    private static readonly (Championship Championship, string Acronym)[] MotorcycleClasses =
    [
        (Championship.MotoGp, "MGP"),
        (Championship.Moto2, "MT2"),
        (Championship.Moto3, "MT3"),
    ];

    /// <summary>
    /// The events payload embeds the weekend timetable, so the next track session for one class is read
    /// without a further request. Media and press entries are excluded by <c>type</c>.
    /// </summary>
    private static IEnumerable<(string Name, DateTimeOffset Start)> MotorcycleSessions(JsonObject grandPrix, string acronym, DateTimeOffset now) =>
        (grandPrix["broadcasts"] as JsonArray)?.OfType<JsonObject>()
            .Where(item => string.Equals(JsonSupport.String(item["type"]), "SESSION", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(JsonSupport.String(item["category"]?["acronym"]), acronym, StringComparison.OrdinalIgnoreCase))
            .Select(item => (Name: JsonSupport.String(item["name"]), Start: ParseDate(JsonSupport.String(item["date_start"]))))
            .Where(item => item.Name is not null && item.Start > now)
            .OrderBy(item => item.Start)
            .Select(item => (item.Name!, item.Start!.Value))
        ?? [];

    public static UpcomingSession? NextDtm(JsonArray? sessions, DateTimeOffset now) =>
        NextGriiip(sessions, now, GriiipSeries.Dtm);

    public static UpcomingSession? NextWorldEnduranceChampionship(JsonArray? sessions, DateTimeOffset now) =>
        NextGriiip(sessions, now, GriiipSeries.Wec);

    public static UpcomingSession? NextEuropeanLeMansSeries(JsonArray? sessions, DateTimeOffset now) =>
        NextGriiip(sessions, now, GriiipSeries.Elms);

    public static UpcomingSession? NextMichelinLeMansCup(JsonArray? sessions, DateTimeOffset now) =>
        NextGriiip(sessions, now, GriiipSeries.MichelinLeMansCup);

    internal static UpcomingSession? NextGriiip(JsonArray? sessions, DateTimeOffset now, GriiipSeries series) =>
        GriiipSessions(sessions, now, series).FirstOrDefault();

    /// <summary>The remaining sessions of the series' next event.</summary>
    internal static IReadOnlyList<UpcomingSession> GriiipEvent(JsonArray? sessions, DateTimeOffset now, GriiipSeries series) =>
        SameEvent(GriiipSessions(sessions, now, series));

    private static IEnumerable<UpcomingSession> GriiipSessions(JsonArray? sessions, DateTimeOffset now, GriiipSeries series) =>
        sessions?.OfType<JsonObject>()
            .Where(item => string.Equals(JsonSupport.String(item["series"]?["name"]),
                series.ProviderName, StringComparison.OrdinalIgnoreCase))
            .Select(item => (
                Event: JsonSupport.String(item["event"]?["name"]),
                Session: JsonSupport.String(item["name"]),
                Venue: JsonSupport.String(item["event"]?["trackConfig"]?["track"]?["name"]),
                Start: ParseDate(JsonSupport.String(item["startTime"]))))
            .Where(item => item.Start > now)
            .OrderBy(item => item.Start)
            .Select(item => new UpcomingSession(
                series.Championship, item.Event ?? series.FallbackEvent, item.Session, item.Start!.Value, item.Venue))
        ?? [];

    /// <summary>A GT World Challenge round, identified so its session timetable can be fetched.</summary>
    public sealed record GtwcMeeting(string Id, string Name, string CountryCode, DateTimeOffset StartUtc, string? Location = null);

    /// <summary>
    /// GT World Challenge meetings carry a day-precision <c>dd.MM.yyyy</c> window. The closing date is
    /// inclusive, so a meeting runs until the end of that day.
    /// </summary>
    public static GtwcMeeting? NextGtWorldChallengeMeeting(JsonObject? season, DateTimeOffset now)
    {
        if (season?["Meetings"] is not JsonObject meetings) return null;
        return meetings.Select(entry => entry.Value as JsonObject)
            .OfType<JsonObject>()
            .Select(meeting => (
                Id: JsonSupport.String(meeting["Id"]),
                Name: JsonSupport.String(meeting["Name"]),
                Country: JsonSupport.String(meeting["CountryCode"]) ?? "",
                Location: JsonSupport.String(meeting["Location"]) ?? JsonSupport.String(meeting["TrackName"]),
                Start: ParseDayPrecisionDate(JsonSupport.String(meeting["BeginDate"])),
                End: ParseDayPrecisionDate(JsonSupport.String(meeting["EndDate"]))?.AddDays(1)))
            .Where(meeting => meeting is { Id: not null, Name: not null, Start: not null } && (meeting.End ?? meeting.Start) > now)
            .OrderBy(meeting => meeting.Start)
            .Select(meeting => new GtwcMeeting(meeting.Id!, meeting.Name!, meeting.Country, meeting.Start!.Value, meeting.Location))
            .FirstOrDefault();
    }

    /// <summary>
    /// Session times in the meeting timetable are track-local with no offset, so they are read in the venue's
    /// timezone. Support races share the timetable and are filtered out by series. Without a usable timetable
    /// the meeting itself is reported, which carries a date but no clock time.
    /// </summary>
    public static UpcomingSession NextGtWorldChallenge(GtwcMeeting meeting, JsonObject? schedule, DateTimeOffset now) =>
        GtWorldChallengeMeeting(meeting, schedule, now)[0];

    /// <summary>The meeting's remaining sessions, or the meeting itself when it publishes no usable timetable.</summary>
    public static IReadOnlyList<UpcomingSession> GtWorldChallengeMeeting(GtwcMeeting meeting, JsonObject? schedule, DateTimeOffset now)
    {
        var sessions = GtWorldChallengeSessions(meeting, schedule, now)
            .Select(session => new UpcomingSession(
                Championship.GtWorldChallengeEurope, meeting.Name, session.Name, session.Start, meeting.Location))
            .ToList();
        return sessions.Count > 0
            ? sessions
            : [new UpcomingSession(Championship.GtWorldChallengeEurope, meeting.Name, null, meeting.StartUtc, meeting.Location)];
    }

    private static IEnumerable<(string Name, DateTimeOffset Start)> GtWorldChallengeSessions(
        GtwcMeeting meeting, JsonObject? schedule, DateTimeOffset now)
    {
        if (schedule is null || VenueZone(meeting.CountryCode) is not { } zone) return [];
        var competitions = GtwcTiming.GtwcEuropeCompetitionIds(schedule);
        return (schedule["Units"] as JsonObject)?.Select(entry => entry.Value as JsonObject)
            .OfType<JsonObject>()
            .Where(unit => competitions.Contains(JsonSupport.String(unit["CompetitionId"]) ?? ""))
            .Select(unit => (
                Name: JsonSupport.String(unit["Name"]) ?? JsonSupport.String(unit["Code"]),
                Start: ParseVenueLocalTime(JsonSupport.String(unit["BeginTime"]), zone)))
            .Where(unit => unit.Name is not null && unit.Start > now)
            .OrderBy(unit => unit.Start)
            .Select(unit => (unit.Name!, unit.Start!.Value))
            .ToList()
        ?? [];
    }

    /// <summary>
    /// Timetables that carry no offset are read in the venue's own time. Only countries the calendar has
    /// visited are mapped; an unlisted country falls back to the meeting date rather than an invented time.
    /// </summary>
    private static readonly Dictionary<string, string> VenueZones = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BEL"] = "Europe/Brussels",
        ["DEU"] = "Europe/Berlin",
        ["ESP"] = "Europe/Madrid",
        ["FRA"] = "Europe/Paris",
        ["GBR"] = "Europe/London",
        ["ITA"] = "Europe/Rome",
        ["NED"] = "Europe/Amsterdam",
        ["PRT"] = "Europe/Lisbon",
    };

    private static TimeZoneInfo? VenueZone(string countryCode) =>
        VenueZones.TryGetValue(countryCode, out var id) ? TimeZoneInfo.FindSystemTimeZoneById(id) : null;

    private static DateTimeOffset? ParseVenueLocalTime(string? value, TimeZoneInfo zone) =>
        DateTime.TryParseExact(value, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(parsed, zone), TimeSpan.Zero)
            : null;

    /// <summary>
    /// A rally runs as a sequence of stages, so the countdown targets the next stage rather than the rally
    /// start. Rallies without a published itinerary fall back to the rally start.
    /// </summary>
    public static UpcomingSession NextWorldRally(
        string eventName,
        DateTimeOffset eventStartUtc,
        IEnumerable<(string Code, string Name, DateTimeOffset? StartUtc)> stages,
        DateTimeOffset now,
        string? location = null) =>
        WorldRally(eventName, eventStartUtc, stages, now, location)[0];

    /// <summary>The rally's remaining stages, or the rally start when no itinerary is published.</summary>
    public static IReadOnlyList<UpcomingSession> WorldRally(
        string eventName,
        DateTimeOffset eventStartUtc,
        IEnumerable<(string Code, string Name, DateTimeOffset? StartUtc)> stages,
        DateTimeOffset now,
        string? location = null)
    {
        var remaining = stages
            .Where(item => item.StartUtc > now)
            .OrderBy(item => item.StartUtc)
            .Select(item => new UpcomingSession(Championship.WorldRallyChampionship, eventName,
                string.IsNullOrWhiteSpace(item.Name) ? item.Code : $"{item.Code} {item.Name}", item.StartUtc!.Value, location))
            .ToList();
        return remaining.Count > 0
            ? remaining
            : [new UpcomingSession(Championship.WorldRallyChampionship, eventName, null, eventStartUtc, location)];
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
