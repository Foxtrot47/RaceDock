// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDock.Core.Presentation;

/// <summary>
/// A plain, serializable view of what the panel strip should draw. It deliberately carries no
/// toolkit types: the Windows, Plasma, and future front ends all render the same values.
/// </summary>
public sealed record RaceDockWidgetState
{
    public string Mode { get; init; } = "hidden";
    /// <summary>What is driving the widget: "live", "scenario", or "archive".</summary>
    public string ViewMode { get; init; } = "live";
    public string? Championship { get; init; }
    public string? ChampionshipShort { get; init; }
    public string? ChampionshipName { get; init; }
    public string? Event { get; init; }
    public string? FullEvent { get; init; }
    public string? Session { get; init; }
    public string? Progress { get; init; }
    public string? Lap { get; init; }
    public string? TrackCondition { get; init; }
    public string? TrackLabel { get; init; }
    public string? Connection { get; init; }
    public string? Accent { get; init; }
    public RaceDockAlert? Alert { get; init; }
    public RaceDockFlag? Flag { get; init; }
    public IReadOnlyList<RaceDockCompetitor> Competitors { get; init; } = [];
    public RaceDockIdle? Idle { get; init; }
    public RaceDockDisplay Display { get; init; } = new();
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record RaceDockDisplay
{
    public bool SingleRow { get; init; }
    public int CompetitorCount { get; init; } = 5;
    public bool ShowNextSessionWhenIdle { get; init; } = true;
    public bool Animations { get; init; } = true;
    public bool PositionColors { get; init; } = true;
    public bool ManufacturerLogos { get; init; }
    public double WidthHint { get; init; } = 520;
}

public sealed record RaceDockCompetitor
{
    public int Position { get; init; }
    public string PositionLabel { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Team { get; init; } = "";
    /// <summary>Logo asset file name resolved from the manufacturer, when a logo exists.</summary>
    public string? ManufacturerLogo { get; init; }
    public string? Gap { get; init; }
    public string? Interval { get; init; }
    public string? Status { get; init; }
    public string? Category { get; init; }
    public string? Manufacturer { get; init; }
    public string PositionColorStart { get; init; } = "#34373F";
    public string PositionColorEnd { get; init; } = "#535864";
    public bool IsLeader { get; init; }
    public bool IsFastest { get; init; }
    public bool InPit { get; init; }
    public bool Retired { get; init; }
    public bool Stopped { get; init; }
}

public sealed record RaceDockAlert
{
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Detail { get; init; }
    public bool Persistent { get; init; }
    public string Color { get; init; } = "#D27719";
}

public sealed record RaceDockFlag
{
    public string Kind { get; init; } = "";
    public string Label { get; init; } = "";
    public string? Detail { get; init; }
    public string Color { get; init; } = "#DAA416";
}

public sealed record RaceDockIdle
{
    public string Headline { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Event { get; init; } = "";
    public string Session { get; init; } = "";
    public string When { get; init; } = "";
    public string Tooltip { get; init; } = "";
    public string Championship { get; init; } = "";
    public string ChampionshipShort { get; init; } = "";
    public string Accent { get; init; } = "#7E8798";
    public string LogoText { get; init; } = "";
}

public sealed record RaceDockFlyoutState
{
    public string Mode { get; init; } = "hidden";
    /// <summary>What is driving the flyout: "live", "scenario", or "archive".</summary>
    public string ViewMode { get; init; } = "live";
    public string Title { get; init; } = "";
    public string? Championship { get; init; }
    public string? ChampionshipShort { get; init; }
    public string? ChampionshipName { get; init; }
    public string? Accent { get; init; }
    public string? Event { get; init; }
    public string? Session { get; init; }
    public string? Circuit { get; init; }
    public string? Progress { get; init; }
    public string? Connection { get; init; }
    public string? TrackCondition { get; init; }
    public string? TrackLabel { get; init; }
    public string? TrackColor { get; init; }
    public string? EmptyMessage { get; init; }
    public bool ShowManufacturerLogos { get; init; }
    public double Width { get; init; } = 460;
    public double Height { get; init; } = 420;
    public double DriverColumnWidth { get; init; } = 190;
    public IReadOnlyList<RaceDockColumn> Columns { get; init; } = [];
    public IReadOnlyList<RaceDockRow> Rows { get; init; } = [];
    public IReadOnlyList<RaceDockSplitPoint> SplitPoints { get; init; } = [];
    public IReadOnlyList<RaceDockScheduleItem> Schedule { get; init; } = [];
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record RaceDockColumn
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string Align { get; init; } = "left";
    public double Width { get; init; }
    public bool Visible { get; init; } = true;
}

public sealed record RaceDockRow
{
    public int Position { get; init; }
    public string PositionLabel { get; init; } = "";
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string Team { get; init; } = "";
    /// <summary>Logo asset file name resolved from the manufacturer, when a logo exists.</summary>
    public string? ManufacturerLogo { get; init; }
    public string? Gap { get; init; }
    public string? Interval { get; init; }
    public string? LapTime { get; init; }
    public string? Category { get; init; }
    public int Lap { get; init; }
    public string PositionColorStart { get; init; } = "#34373F";
    public string PositionColorEnd { get; init; } = "#535864";
    public RaceDockTyre? Tyre { get; init; }
    public RaceDockStatus? Status { get; init; }
    public RaceDockEnergy? Energy { get; init; }
    public IReadOnlyList<RaceDockSector> Sectors { get; init; } = [];
    public IReadOnlyList<RaceDockSplit> Splits { get; init; } = [];
    public bool IsOverallFastest { get; init; }
    public bool InPit { get; init; }
    public bool Retired { get; init; }
    public bool Stopped { get; init; }
    public double Opacity { get; init; } = 1;
}

public sealed record RaceDockTyre
{
    public string Letter { get; init; } = "";
    public string Compound { get; init; } = "";
    public int? Age { get; init; }
    public string Color { get; init; } = "#5C606A";
    public string TextColor { get; init; } = "#FFFFFF";
    public bool ShowAges { get; init; }
    public IReadOnlyList<RaceDockTyreWheel> Wheels { get; init; } = [];
}

public sealed record RaceDockTyreWheel
{
    public string Name { get; init; } = "";
    public string? Compound { get; init; }
    public int? Age { get; init; }
    public string Color { get; init; } = "#7E8798";
}

public sealed record RaceDockStatus
{
    public string Text { get; init; } = "";
    public string Color { get; init; } = "transparent";
    public string Tone { get; init; } = "neutral";
}

public sealed record RaceDockEnergy
{
    public string Text { get; init; } = "—";
    public double? Percent { get; init; }
    public string Level { get; init; } = "unavailable";
    public string Color { get; init; } = "#7E8798";
    public bool Available { get; init; }
}

public sealed record RaceDockSector
{
    public string Value { get; init; } = "—";
    public string State { get; init; } = "none";
    public string Color { get; init; } = "#707784";
    public IReadOnlyList<RaceDockMiniSector> MiniSectors { get; init; } = [];
}

public sealed record RaceDockMiniSector
{
    public string State { get; init; } = "notReached";
    public string Color { get; init; } = "#8E94A0";
}

public sealed record RaceDockSplit
{
    public int Id { get; init; }
    public string Value { get; init; } = "—";
}

public sealed record RaceDockSplitPoint
{
    public int Id { get; init; }
    public int Number { get; init; }
    public double DistanceKm { get; init; }
    public string Label { get; init; } = "";
}

public sealed record RaceDockScheduleItem
{
    public string Championship { get; init; } = "";
    public string ChampionshipShort { get; init; } = "";
    public string ChampionshipName { get; init; } = "";
    public string Accent { get; init; } = "#7E8798";
    public string Event { get; init; } = "";
    public string? Session { get; init; }
    public string? Venue { get; init; }
    public string Subtitle { get; init; } = "";
    public string Time { get; init; } = "";
    public string Date { get; init; } = "";
    public string Lead { get; init; } = "";
    public string Tooltip { get; init; } = "";
    public string LogoText { get; init; } = "";
    public bool Featured { get; init; }
}

public static class RaceDockJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);

    public static string SerializeSettings(UserSettings settings) => Serialize(settings);

    public static UserSettings DeserializeSettings(string json) =>
        (Deserialize<UserSettings>(json) ?? new UserSettings()).Normalize();
}
