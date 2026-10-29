// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>A published session or event that has not started yet.</summary>
/// <param name="Venue">Circuit, track, or rally base as the source publishes it, absent when it publishes none.</param>
public sealed record UpcomingSession(Championship Championship, string EventName, string? SessionName, DateTimeOffset StartUtc, string? Venue = null)
{
    /// <summary>
    /// The published calendar date when the source released a date without a clock time, such as a GTWC round whose
    /// timetable is not out yet. While it is set, <see cref="StartUtc"/> is only an ordering approximation and no
    /// local clock time may be derived from it.
    /// </summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>False when the source published a date without a time.</summary>
    public bool HasStartTime => StartDate is null;

    /// <summary>
    /// True while the entry is still ahead for a reader in <paramref name="timeZone"/>. A date-only entry names a
    /// calendar date, so it stays ahead until that date has passed in the reader's zone rather than at midnight UTC.
    /// </summary>
    public bool IsAhead(DateTimeOffset now, TimeZoneInfo timeZone) =>
        StartDate is { } date
            ? date >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).Date)
            : StartUtc > now;
}

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
                Name: GtwcTiming.MeetingName(meeting),
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
            : [new UpcomingSession(Championship.GtWorldChallengeEurope, meeting.Name, null, meeting.StartUtc, meeting.Location)
                { StartDate = DateOnly.FromDateTime(meeting.StartUtc.UtcDateTime) }];
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
            .Where(unit => unit.Name is not null && !GtwcTiming.IsTestSession(unit.Name) && unit.Start > now)
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
        ["GER"] = "Europe/Berlin",
        ["ESP"] = "Europe/Madrid",
        ["FRA"] = "Europe/Paris",
        ["GBR"] = "Europe/London",
        ["ITA"] = "Europe/Rome",
        ["NED"] = "Europe/Amsterdam",
        ["POR"] = "Europe/Lisbon",
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
        string? location = null,
        DateOnly? eventDate = null) =>
        WorldRally(eventName, eventStartUtc, stages, now, location, eventDate)[0];

    /// <summary>The rally's remaining stages, or the rally start when no itinerary is published.</summary>
    public static IReadOnlyList<UpcomingSession> WorldRally(
        string eventName,
        DateTimeOffset eventStartUtc,
        IEnumerable<(string Code, string Name, DateTimeOffset? StartUtc)> stages,
        DateTimeOffset now,
        string? location = null,
        DateOnly? eventDate = null)
    {
        var remaining = stages
            .Where(item => item.StartUtc > now)
            .OrderBy(item => item.StartUtc)
            .Select(item => new UpcomingSession(Championship.WorldRallyChampionship, eventName,
                string.IsNullOrWhiteSpace(item.Name) ? item.Code : $"{item.Code} {item.Name}", item.StartUtc!.Value, location))
            .ToList();
        return remaining.Count > 0
            ? remaining
            : [new UpcomingSession(Championship.WorldRallyChampionship, eventName, null, eventStartUtc, location)
                { StartDate = eventDate }];
    }

    /// <summary>
    /// NASCAR publishes one race-list entry per event. The selected event can be Cup, Xfinity, or Trucks;
    /// session rows are added separately from the weekend feed when that feed has been published.
    /// </summary>
    public static UpcomingSession? NextNascarCup(JsonArray? races, DateTimeOffset now) =>
        NextNascar(races, now, Championship.NascarCup);

    public static UpcomingSession? NextNascarXfinity(JsonArray? races, DateTimeOffset now) =>
        NextNascar(races, now, Championship.NascarXfinity);

    public static UpcomingSession? NextNascarTrucks(JsonArray? races, DateTimeOffset now) =>
        NextNascar(races, now, Championship.NascarTrucks);

    internal static UpcomingSession? NextNascar(JsonArray? races, DateTimeOffset now, Championship championship) =>
        NascarRaces(races, now, championship).Select(race => race.Session).FirstOrDefault();

    /// <summary>
    /// The provider id of the race <see cref="NextNascar"/> selects. Race names repeat within a season (two
    /// Duels, two "Cook Out 400" rounds), so the weekend feed must be found by id rather than by name.
    /// </summary>
    internal static int? NextNascarRaceId(JsonArray? races, DateTimeOffset now, Championship championship) =>
        NascarRaces(races, now, championship).Select(race => race.Id).FirstOrDefault();

    public static IReadOnlyList<UpcomingSession> NascarCupEvent(JsonArray? races, DateTimeOffset now) =>
        NascarEvent(races, null, now, Championship.NascarCup);

    internal static IReadOnlyList<UpcomingSession> NascarEvent(
        JsonArray? races, JsonObject? weekendFeed, DateTimeOffset now, Championship championship)
    {
        var race = NextNascar(races, now, championship);
        if (race is null) return [];
        var sessions = NascarWeekendSessions(weekendFeed, race, now, championship).ToList();
        return sessions.Count > 0 ? sessions : [race];
    }

    private static IEnumerable<(UpcomingSession Session, int? Id)> NascarRaces(
        JsonArray? races, DateTimeOffset now, Championship championship) =>
        (races?.OfType<JsonObject>() ?? [])
            .Select(race => (Session: NascarRace(race, championship), Id: JsonSupport.Int(race["race_id"])))
            .Where(race => race.Session is not null && NascarSelectionEnd(race.Session) > now)
            .Select(race => (race.Session!, race.Id))
            .OrderBy(race => race.Item1.StartUtc);

    /// <summary>
    /// The race start from the published <c>schedule[]</c> (UTC) when present. Otherwise the race list's
    /// <c>date_scheduled</c>/<c>race_date</c>, which are US Eastern wall-clock times; a midnight value there
    /// names a date whose time is not out yet. A UTC schedule start at midnight is a real time (8 pm EDT).
    /// </summary>
    private static UpcomingSession? NascarRace(JsonObject race, Championship championship)
    {
        if (JsonSupport.String(race["race_name"])?.Trim() is not { Length: > 0 } name) return null;
        var venue = JsonSupport.String(race["track_name"]);
        if (NascarRunStart(race, 3) is { } scheduled) return new UpcomingSession(championship, name, null, scheduled, venue);
        if ((NascarLocalTimestamp(race["date_scheduled"]) ?? NascarLocalTimestamp(race["race_date"])) is not { } local) return null;
        return local.TimeOfDay == TimeSpan.Zero
            ? new UpcomingSession(championship, name, null,
                new DateTimeOffset(local.Date, TimeSpan.Zero), venue) { StartDate = DateOnly.FromDateTime(local) }
            : new UpcomingSession(championship, name, null, EasternToUtc(local), venue);
    }

    private static DateTimeOffset? NascarRunStart(JsonObject race, int runType) =>
        (race["schedule"] as JsonArray)?.OfType<JsonObject>()
            .Where(item => JsonSupport.Int(item["run_type"]) == runType)
            .Select(item => NascarTimestamp(item["start_time_utc"]))
            .Where(start => start is not null)
            .OrderBy(start => start)
            .FirstOrDefault();

    private static IEnumerable<UpcomingSession> NascarWeekendSessions(
        JsonObject? weekendFeed, UpcomingSession race, DateTimeOffset now, Championship championship) =>
        (weekendFeed?["weekend_race"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault()?["schedule"] is not JsonArray schedule
            ? []
            : schedule.OfType<JsonObject>()
                .Select(item => (
                    Name: JsonSupport.String(item["event_name"]),
                    Type: JsonSupport.Int(item["run_type"]),
                    Start: NascarTimestamp(item["start_time_utc"])))
                .Where(item => item.Type is 1 or 2 or 3 && item.Start > now)
                .OrderBy(item => item.Start)
                .Select(item => new UpcomingSession(
                    championship,
                    race.EventName,
                    item.Type == 3 ? null : NascarSessionName(item.Name),
                    item.Start!.Value,
                    race.Venue));

    private static string NascarSessionName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Session" : name.Trim();

    /// <summary>
    /// A <c>*_utc</c> timestamp published without an offset. The provider stamps <c>1900-01-01</c> on a date it
    /// has not released.
    /// </summary>
    private static DateTimeOffset? NascarTimestamp(JsonNode? value) =>
        DateTimeOffset.TryParse(JsonSupport.String(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) &&
        parsed.Year > 1900
            ? parsed
            : null;

    /// <summary>A race-list wall-clock time without an offset, kept unconverted.</summary>
    private static DateTime? NascarLocalTimestamp(JsonNode? value) =>
        DateTime.TryParse(JsonSupport.String(value), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) &&
        parsed.Year > 1900
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)
            : null;

    private static readonly TimeZoneInfo UsEastern = FindEasternTimeZone();

    private static TimeZoneInfo FindEasternTimeZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        // Without time-zone data the standard offset is the closest stable approximation.
        return TimeZoneInfo.CreateCustomTimeZone("US Eastern", TimeSpan.FromHours(-5), "US Eastern", "US Eastern");
    }

    internal static DateTimeOffset EasternToUtc(DateTime local)
    {
        // A clock time skipped by the spring-forward change does not exist; the hour after it is meant.
        if (UsEastern.IsInvalidTime(local)) local = local.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, UsEastern), TimeSpan.Zero);
    }

    /// <summary>
    /// A dated race stays selected for its whole published day, so the countdown does not move on to the
    /// following round on the morning of the race. <see cref="UpcomingSession.IsAhead"/> then keeps the
    /// entry visible until the date has passed in the reader's zone.
    /// </summary>
    private static DateTimeOffset NascarSelectionEnd(UpcomingSession session) =>
        session.StartDate is { } date
            ? new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
            : session.StartUtc;

    /// <summary>The remaining sessions of the next F1 Academy round.</summary>
    public static IReadOnlyList<UpcomingSession> F1AcademyRound(JsonArray? races, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        // Each round runs over a few days, and later rounds sit ahead of it, so the first round with a session still
        // to come is the one being followed.
        foreach (var round in (races?.OfType<JsonObject>() ?? []).OrderBy(race => JsonSupport.String(race["RaceStartDate"])))
        {
            var name = JsonSupport.String(round["CircuitShortName"]) ?? JsonSupport.String(round["CountryName"]) ?? "F1 Academy";
            var venue = JsonSupport.String(round["CircuitName"]);
            var sessions = (round["Sessions"] as JsonArray)?.OfType<JsonObject>()
                .Select(session => F1AcademySession(session, name, venue))
                .OfType<UpcomingSession>()
                .Where(session => session.IsAhead(now, timeZone))
                .OrderBy(session => session.StartUtc)
                .ToList() ?? [];
            if (sessions.Count > 0) return sessions;
        }
        return [];
    }

    /// <summary>
    /// A session the series has not confirmed carries a placeholder clock time (the official page shows it as TBC),
    /// so only its local date is kept.
    /// </summary>
    private static UpcomingSession? F1AcademySession(JsonObject session, string eventName, string? venue)
    {
        if (ParseDate(JsonSupport.String(session["SessionStartTime"])) is not { } start) return null;
        var name = JsonSupport.String(session["SessionName"]);
        return JsonSupport.Bool(session["Unconfirmed"])
            ? new UpcomingSession(Championship.F1Academy, eventName, name, new DateTimeOffset(start.Date, TimeSpan.Zero), venue)
                { StartDate = DateOnly.FromDateTime(start.DateTime) }
            : new UpcomingSession(Championship.F1Academy, eventName, name, start.ToUniversalTime(), venue);
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
