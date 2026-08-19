// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaceDock.Core;

public enum TaskbarWidgetPosition
{
    LeftOfStart,
    RightOfStart
}

public enum TaskbarCompetitorLabel
{
    Code,
    FullName,
    DriverNumber
}

public sealed record ChampionshipDisplaySettings
{
    public int TaskbarCompetitorCount { get; init; } = 5;
    public TaskbarCompetitorLabel TaskbarCompetitorLabel { get; init; } = TaskbarCompetitorLabel.Code;
    public bool TaskbarShowEventHeader { get; init; } = true;
    public bool TaskbarShowSessionProgress { get; init; } = true;
    public bool TaskbarShowGaps { get; init; } = true;
    public bool TaskbarShowPositionMarker { get; init; } = true;
    public bool TaskbarShowPositionColors { get; init; } = true;
    public bool TaskbarUseManufacturerLogos { get; init; }
    public bool TaskbarShowFlagAlerts { get; init; } = true;

    public int FlyoutCompetitorCount { get; init; } = 20;
    public bool FlyoutShowEventHeader { get; init; } = true;
    public bool FlyoutShowSessionDetails { get; init; } = true;
    public bool FlyoutShowTrackStatus { get; init; } = true;
    public bool FlyoutShowConnectionStatus { get; init; } = true;
    public bool FlyoutShowTeamNames { get; init; } = true;
    public bool FlyoutUseManufacturerLogos { get; init; }
    public bool FlyoutShowPositionColors { get; init; } = true;
    public bool FlyoutShowGaps { get; init; } = true;
    public bool FlyoutShowIntervals { get; init; } = true;
    public bool FlyoutShowTyreOrResult { get; init; } = true;
    public bool FlyoutShowCategoryOrLap { get; init; } = true;
    public bool FlyoutShowStatus { get; init; } = true;

    public ChampionshipDisplaySettings Normalize() => this with
    {
        TaskbarCompetitorCount = Math.Clamp(TaskbarCompetitorCount, 1, 5),
        TaskbarCompetitorLabel = Enum.IsDefined(TaskbarCompetitorLabel)
            ? TaskbarCompetitorLabel
            : TaskbarCompetitorLabel.Code,
        FlyoutCompetitorCount = Math.Clamp(FlyoutCompetitorCount, 5, 40)
    };

    internal ChampionshipDisplaySettings ApplyCapabilities(Championship championship) => championship switch
    {
        Championship.Formula2 or Championship.Formula3 => this with
        {
            TaskbarUseManufacturerLogos = false,
            FlyoutUseManufacturerLogos = false,
            FlyoutShowTyreOrResult = false
        },
        Championship.MotoGp or Championship.Moto2 or Championship.Moto3 => this with
        {
            FlyoutShowTyreOrResult = false
        },
        Championship.WorldRallyChampionship => this with
        {
            FlyoutShowTeamNames = false
        },
        _ => this
    };

    internal static ChampionshipDisplaySettings FromLegacy(UserSettings settings) => new()
    {
        TaskbarCompetitorCount = settings.DriverCount,
        TaskbarShowEventHeader = settings.ShowEventHeader,
        TaskbarShowSessionProgress = settings.ShowSessionProgress,
        TaskbarShowGaps = settings.ShowGaps,
        TaskbarShowPositionColors = settings.ShowPositionColors,
        TaskbarUseManufacturerLogos = settings.UseManufacturerLogos,
        TaskbarShowFlagAlerts = settings.ShowFlagAlerts,
        FlyoutUseManufacturerLogos = settings.UseManufacturerLogos,
        FlyoutShowPositionColors = settings.ShowPositionColors
    };
}

public sealed record ChampionshipDisplayProfiles
{
    public ChampionshipDisplaySettings Formula1 { get; init; } = new();
    public ChampionshipDisplaySettings Formula2 { get; init; } = new();
    public ChampionshipDisplaySettings Formula3 { get; init; } = new();
    public ChampionshipDisplaySettings MotoGp { get; init; } = new();
    public ChampionshipDisplaySettings Moto2 { get; init; } = new();
    public ChampionshipDisplaySettings Moto3 { get; init; } = new();
    public ChampionshipDisplaySettings GtWorldChallengeEurope { get; init; } = new();
    public ChampionshipDisplaySettings WorldRallyChampionship { get; init; } = new();
    public ChampionshipDisplaySettings Imsa { get; init; } = new();
    public ChampionshipDisplaySettings FormulaE { get; init; } = new();
    public ChampionshipDisplaySettings Dtm { get; init; } = new();
    public ChampionshipDisplaySettings IndyCar { get; init; } = new();

    public ChampionshipDisplaySettings For(Championship championship) => (championship switch
    {
        Championship.Formula2 => Formula2,
        Championship.Formula3 => Formula3,
        Championship.MotoGp => MotoGp,
        Championship.Moto2 => Moto2,
        Championship.Moto3 => Moto3,
        Championship.GtWorldChallengeEurope => GtWorldChallengeEurope,
        Championship.WorldRallyChampionship => WorldRallyChampionship,
        Championship.Imsa => Imsa,
        Championship.FormulaE => FormulaE,
        Championship.Dtm => Dtm,
        Championship.IndyCar => IndyCar,
        _ => Formula1
    }) ?? new();

    public ChampionshipDisplayProfiles With(Championship championship, ChampionshipDisplaySettings settings) =>
        championship switch
        {
            Championship.Formula2 => this with { Formula2 = settings },
            Championship.Formula3 => this with { Formula3 = settings },
            Championship.MotoGp => this with { MotoGp = settings },
            Championship.Moto2 => this with { Moto2 = settings },
            Championship.Moto3 => this with { Moto3 = settings },
            Championship.GtWorldChallengeEurope => this with { GtWorldChallengeEurope = settings },
            Championship.WorldRallyChampionship => this with { WorldRallyChampionship = settings },
            Championship.Imsa => this with { Imsa = settings },
            Championship.FormulaE => this with { FormulaE = settings },
            Championship.Dtm => this with { Dtm = settings },
            Championship.IndyCar => this with { IndyCar = settings },
            _ => this with { Formula1 = settings }
        };

    public ChampionshipDisplayProfiles Normalize() => this with
    {
        Formula1 = (Formula1 ?? new()).Normalize(),
        Formula2 = (Formula2 ?? new()).Normalize(),
        Formula3 = (Formula3 ?? new()).Normalize(),
        MotoGp = (MotoGp ?? new()).Normalize(),
        Moto2 = (Moto2 ?? new()).Normalize(),
        Moto3 = (Moto3 ?? new()).Normalize(),
        GtWorldChallengeEurope = (GtWorldChallengeEurope ?? new()).Normalize(),
        WorldRallyChampionship = (WorldRallyChampionship ?? new()).Normalize(),
        Imsa = (Imsa ?? new()).Normalize(),
        FormulaE = (FormulaE ?? new()).Normalize(),
        Dtm = (Dtm ?? new()).Normalize(),
        IndyCar = (IndyCar ?? new()).Normalize()
    };

    internal static ChampionshipDisplayProfiles Create(ChampionshipDisplaySettings settings) => new()
    {
        Formula1 = settings,
        Formula2 = settings,
        Formula3 = settings,
        MotoGp = settings,
        Moto2 = settings,
        Moto3 = settings,
        GtWorldChallengeEurope = settings,
        WorldRallyChampionship = settings,
        Imsa = settings,
        FormulaE = settings,
        Dtm = settings,
        IndyCar = settings
    };
}

public sealed record UserSettings
{
    /// <summary>Every championship, in presentation order. Any of them can be hidden.</summary>
    public static readonly IReadOnlyList<Championship> AllChampionships =
    [
        Championship.Formula1,
        Championship.Formula2,
        Championship.Formula3,
        Championship.FormulaE,
        Championship.Dtm,
        Championship.IndyCar,
        Championship.MotoGp,
        Championship.Moto2,
        Championship.Moto3,
        Championship.GtWorldChallengeEurope,
        Championship.WorldRallyChampionship,
        Championship.Imsa
    ];

    /// <summary>Championships that start hidden; the user can hide or show any championship afterwards.</summary>
    /// <remarks>Hiding a championship also disables it, so nothing is fetched for it while it is out of sight.</remarks>
    public static readonly IReadOnlyList<Championship> DefaultHiddenChampionships =
    [
        Championship.Formula2,
        Championship.Formula3,
        Championship.Moto2,
        Championship.Moto3,
        Championship.Dtm,
        Championship.IndyCar
    ];

    public bool EnableFormula1 { get; init; } = true;
    public bool EnableFormula2 { get; init; }
    public bool EnableFormula3 { get; init; }
    public bool EnableMotoGp { get; init; }
    public bool EnableMoto2 { get; init; }
    public bool EnableMoto3 { get; init; }
    public bool EnableGtWorldChallengeEurope { get; init; } = true;
    public bool EnableWrc { get; init; } = true;
    public bool EnableImsa { get; init; }
    public bool EnableFormulaE { get; init; }
    public bool EnableDtm { get; init; }
    public bool EnableIndyCar { get; init; }
    public int RotationSeconds { get; init; } = 5;
    public int CompletedSessionRetentionMinutes { get; init; } = 5;
    public int DriverCount { get; init; } = 5;
    public bool ShowEventHeader { get; init; } = true;
    public bool ShowSessionProgress { get; init; } = true;
    public bool ShowGaps { get; init; } = true;
    public bool ShowPositionColors { get; init; } = true;
    public bool UseManufacturerLogos { get; init; }
    public bool ShowFlagAlerts { get; init; } = true;
    public bool AutoStartOnWindowsBoot { get; init; } = true;
    public TaskbarWidgetPosition WidgetPosition { get; init; } = TaskbarWidgetPosition.LeftOfStart;
    public ChampionshipDisplayProfiles? ChampionshipDisplay { get; init; }

    /// <summary>Optional championships the user has hidden; <c>null</c> means a settings file written before visibility existed.</summary>
    public IReadOnlyList<Championship>? HiddenChampionships { get; init; }

    public bool IsChampionshipVisible(Championship championship) =>
        HiddenChampionships is null ? IsChampionshipEnabled(championship) || !DefaultHiddenChampionships.Contains(championship)
        : !HiddenChampionships.Contains(championship);

    public bool IsChampionshipEnabled(Championship championship) => championship switch
    {
        Championship.Formula1 => EnableFormula1,
        Championship.Formula2 => EnableFormula2,
        Championship.Formula3 => EnableFormula3,
        Championship.MotoGp => EnableMotoGp,
        Championship.Moto2 => EnableMoto2,
        Championship.Moto3 => EnableMoto3,
        Championship.GtWorldChallengeEurope => EnableGtWorldChallengeEurope,
        Championship.WorldRallyChampionship => EnableWrc,
        Championship.Imsa => EnableImsa,
        Championship.FormulaE => EnableFormulaE,
        Championship.Dtm => EnableDtm,
        Championship.IndyCar => EnableIndyCar,
        _ => false
    };

    private UserSettings WithChampionshipDisabled(Championship championship) => championship switch
    {
        Championship.Formula1 => this with { EnableFormula1 = false },
        Championship.Formula2 => this with { EnableFormula2 = false },
        Championship.Formula3 => this with { EnableFormula3 = false },
        Championship.MotoGp => this with { EnableMotoGp = false },
        Championship.Moto2 => this with { EnableMoto2 = false },
        Championship.Moto3 => this with { EnableMoto3 = false },
        Championship.GtWorldChallengeEurope => this with { EnableGtWorldChallengeEurope = false },
        Championship.WorldRallyChampionship => this with { EnableWrc = false },
        Championship.Imsa => this with { EnableImsa = false },
        Championship.FormulaE => this with { EnableFormulaE = false },
        Championship.Dtm => this with { EnableDtm = false },
        Championship.IndyCar => this with { EnableIndyCar = false },
        _ => this
    };

    private UserSettings WithChampionshipEnabled(Championship championship) => championship switch
    {
        Championship.Formula2 => this with { EnableFormula2 = true },
        Championship.Formula3 => this with { EnableFormula3 = true },
        Championship.MotoGp => this with { EnableMotoGp = true },
        Championship.Moto2 => this with { EnableMoto2 = true },
        Championship.Moto3 => this with { EnableMoto3 = true },
        Championship.GtWorldChallengeEurope => this with { EnableGtWorldChallengeEurope = true },
        Championship.WorldRallyChampionship => this with { EnableWrc = true },
        Championship.Imsa => this with { EnableImsa = true },
        Championship.FormulaE => this with { EnableFormulaE = true },
        Championship.Dtm => this with { EnableDtm = true },
        Championship.IndyCar => this with { EnableIndyCar = true },
        _ => this with { EnableFormula1 = true }
    };


    public bool HasEnabledStream => EnableFormula1 || EnableFormula2 || EnableFormula3 || EnableMotoGp || EnableMoto2 || EnableMoto3 || EnableGtWorldChallengeEurope || EnableWrc || EnableImsa || EnableFormulaE || EnableDtm || EnableIndyCar;
    public ChampionshipDisplaySettings DisplayFor(Championship championship) =>
        (ChampionshipDisplay ?? ChampionshipDisplayProfiles.Create(ChampionshipDisplaySettings.FromLegacy(this)))
        .For(championship)
        .Normalize()
        .ApplyCapabilities(championship);

    public UserSettings Normalize()
    {
        var legacyDisplay = ChampionshipDisplaySettings.FromLegacy(this).Normalize();
        var hidden = AllChampionships.Where(championship => !IsChampionshipVisible(championship)).ToArray();
        var normalized = this with
        {
            HiddenChampionships = hidden,
            RotationSeconds = Math.Clamp(RotationSeconds, 2, 30),
            CompletedSessionRetentionMinutes = Math.Clamp(CompletedSessionRetentionMinutes, 5, 30),
            DriverCount = Math.Clamp(DriverCount, 1, 5),
            WidgetPosition = Enum.IsDefined(WidgetPosition) ? WidgetPosition : TaskbarWidgetPosition.LeftOfStart,
            ChampionshipDisplay = (ChampionshipDisplay ?? ChampionshipDisplayProfiles.Create(legacyDisplay)).Normalize()
        };
        foreach (var championship in hidden) normalized = normalized.WithChampionshipDisabled(championship);
        if (normalized.HasEnabledStream) return normalized;

        // Something has to run: fall back to the first visible championship, or bring Formula 1 back if all are hidden.
        var fallback = AllChampionships.FirstOrDefault(normalized.IsChampionshipVisible, Championship.Formula1);
        return normalized.WithChampionshipEnabled(fallback) with
        {
            HiddenChampionships = [.. normalized.HiddenChampionships!.Where(championship => championship != fallback)]
        };
    }
}

public sealed class UserSettingsStore(string? path = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RaceDock", "settings.json");

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new UserSettings().Normalize();
            return (JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path), JsonOptions) ?? new UserSettings()).Normalize();
        }
        catch (JsonException) { return new UserSettings().Normalize(); }
        catch (IOException) { return new UserSettings().Normalize(); }
        catch (UnauthorizedAccessException) { return new UserSettings().Normalize(); }
    }

    public void Save(UserSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings.Normalize(), JsonOptions));
        File.Move(temporary, Path, true);
    }
}
