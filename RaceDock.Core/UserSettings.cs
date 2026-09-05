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

/// <summary>
/// A classification column whose position the user controls. Position and driver are pinned to the left of
/// the flyout and are deliberately absent.
/// </summary>
public enum FlyoutColumn
{
    Gap,
    Interval,
    LapTime,
    Tyre,
    Category,
    Status
}

public sealed record ChampionshipDisplaySettings
{
    public int TaskbarCompetitorCount { get; init; } = 5;
    public bool TaskbarUseManufacturerLogos { get; init; } = true;

    public int FlyoutCompetitorCount { get; init; } = 20;
    public bool FlyoutShowTeamNames { get; init; } = true;
    public bool FlyoutUseManufacturerLogos { get; init; }
    public bool FlyoutShowPositionColors { get; init; } = true;
    // Interval to the car ahead is the more useful figure in every session type, so the gap to the
    // leader is available but off by default.
    public bool FlyoutShowGaps { get; init; }
    public bool FlyoutShowIntervals { get; init; } = true;
    public bool FlyoutShowLapTime { get; init; } = true;
    public bool FlyoutShowTyre { get; init; } = true;
    public bool FlyoutShowCategoryOrLap { get; init; } = true;
    public bool FlyoutShowStatus { get; init; } = true;
    public IReadOnlyList<FlyoutColumn> FlyoutColumnOrder { get; init; } = DefaultColumnOrder;

    /// <summary>The order columns appear in before the user rearranges them.</summary>
    public static readonly IReadOnlyList<FlyoutColumn> DefaultColumnOrder =
    [
        FlyoutColumn.Gap,
        FlyoutColumn.Interval,
        FlyoutColumn.LapTime,
        FlyoutColumn.Tyre,
        FlyoutColumn.Category,
        FlyoutColumn.Status
    ];

    /// <summary>
    /// Repairs a stored order so the flyout always receives every column exactly once. Hand-edited or
    /// older settings files can repeat, omit, or name columns that no longer exist.
    /// </summary>
    private IReadOnlyList<FlyoutColumn> NormalizeColumnOrder()
    {
        var ordered = new List<FlyoutColumn>(DefaultColumnOrder.Count);
        foreach (var column in FlyoutColumnOrder ?? [])
            if (Enum.IsDefined(column) && !ordered.Contains(column))
                ordered.Add(column);
        foreach (var column in DefaultColumnOrder)
            if (!ordered.Contains(column))
                ordered.Add(column);
        return ordered;
    }

    public ChampionshipDisplaySettings Normalize() => this with
    {
        FlyoutColumnOrder = NormalizeColumnOrder(),
        TaskbarCompetitorCount = Math.Clamp(TaskbarCompetitorCount, 1, 5),
        FlyoutCompetitorCount = Math.Clamp(FlyoutCompetitorCount, 5, 40)
    };

    internal ChampionshipDisplaySettings ApplyCapabilities(Championship championship) => championship switch
    {
        Championship.Formula2 or Championship.Formula3 => this with
        {
            TaskbarUseManufacturerLogos = false,
            FlyoutUseManufacturerLogos = false,
            FlyoutShowTyre = false
        },
        // Bike and sportscar feeds carry no tyre compound, so the column would only hold placeholders.
        Championship.MotoGp or Championship.Moto2 or Championship.Moto3
            or Championship.GtWorldChallengeEurope or Championship.WorldEnduranceChampionship
            or Championship.EuropeanLeMansSeries or Championship.MichelinLeMansCup or Championship.Imsa
            or Championship.FormulaE or Championship.Dtm => this with
        {
            FlyoutShowTyre = false
        },
        Championship.WorldRallyChampionship => this with
        {
            FlyoutShowTeamNames = false,
            FlyoutShowTyre = false
        },
        _ => this
    };

    internal static ChampionshipDisplaySettings FromLegacy(UserSettings settings) => new()
    {
        TaskbarCompetitorCount = settings.DriverCount,
        // The widget draws manufacturer marks by default, so the retired global flag only carries
        // over to the flyout, where marks stay off until they are asked for.
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
    public ChampionshipDisplaySettings WorldEnduranceChampionship { get; init; } = new();
    public ChampionshipDisplaySettings EuropeanLeMansSeries { get; init; } = new();
    public ChampionshipDisplaySettings MichelinLeMansCup { get; init; } = new();
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
        Championship.WorldEnduranceChampionship => WorldEnduranceChampionship,
        Championship.EuropeanLeMansSeries => EuropeanLeMansSeries,
        Championship.MichelinLeMansCup => MichelinLeMansCup,
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
            Championship.WorldEnduranceChampionship => this with { WorldEnduranceChampionship = settings },
            Championship.EuropeanLeMansSeries => this with { EuropeanLeMansSeries = settings },
            Championship.MichelinLeMansCup => this with { MichelinLeMansCup = settings },
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
        WorldEnduranceChampionship = (WorldEnduranceChampionship ?? new()).Normalize(),
        EuropeanLeMansSeries = (EuropeanLeMansSeries ?? new()).Normalize(),
        MichelinLeMansCup = (MichelinLeMansCup ?? new()).Normalize(),
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
        WorldEnduranceChampionship = settings,
        EuropeanLeMansSeries = settings,
        MichelinLeMansCup = settings,
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
        Championship.WorldEnduranceChampionship,
        Championship.EuropeanLeMansSeries,
        Championship.MichelinLeMansCup,
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
        Championship.IndyCar,
        Championship.EuropeanLeMansSeries,
        Championship.MichelinLeMansCup
    ];

    public int Version { get; init; }
    public bool EnableFormula1 { get; init; } = true;
    public bool EnableFormula2 { get; init; }
    public bool EnableFormula3 { get; init; }
    public bool EnableMotoGp { get; init; }
    public bool EnableMoto2 { get; init; }
    public bool EnableMoto3 { get; init; }
    public bool EnableGtWorldChallengeEurope { get; init; } = true;
    public bool EnableWec { get; init; } = true;
    public bool EnableElms { get; init; }
    public bool EnableMichelinLeMansCup { get; init; }
    public bool EnableWrc { get; init; } = true;
    public bool EnableImsa { get; init; }
    public bool EnableFormulaE { get; init; }
    public bool EnableDtm { get; init; }
    public bool EnableIndyCar { get; init; }
    public int RotationSeconds { get; init; } = 5;
    public int CompletedSessionRetentionMinutes { get; init; } = 5;
    public int DriverCount { get; init; } = 5;
    public bool ShowPositionColors { get; init; } = true;
    public bool UseManufacturerLogos { get; init; }
    public bool AutoStartOnWindowsBoot { get; init; } = true;

    /// <summary>Keeps the widget on the taskbar between sessions, counting down to the next published
    /// session. Off leaves the taskbar untouched until a session is on track.</summary>
    public bool ShowNextSessionWhenIdle { get; init; } = true;

    /// <summary>Eases the flyout open and lights the widget under the pointer. Off returns every
    /// transition to an immediate state change.</summary>
    public bool InterfaceAnimations { get; init; } = true;

    /// <summary>Blurs the desktop behind the classification. Experimental, so it is off by default:
    /// the effect is composited by Windows rather than drawn by the app, and behaves differently
    /// across Windows builds and graphics drivers.</summary>
    public bool FlyoutAcrylicBlur { get; init; }

    /// <summary>How strongly the blurred surface is tinted, as a percentage. Lower lets more of the
    /// desktop through; higher approaches a solid panel.</summary>
    public int FlyoutBlurTint { get; init; } = 70;
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
        Championship.WorldEnduranceChampionship => EnableWec,
        Championship.EuropeanLeMansSeries => EnableElms,
        Championship.MichelinLeMansCup => EnableMichelinLeMansCup,
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
        Championship.WorldEnduranceChampionship => this with { EnableWec = false },
        Championship.EuropeanLeMansSeries => this with { EnableElms = false },
        Championship.MichelinLeMansCup => this with { EnableMichelinLeMansCup = false },
        Championship.WorldRallyChampionship => this with { EnableWrc = false },
        Championship.Imsa => this with { EnableImsa = false },
        Championship.FormulaE => this with { EnableFormulaE = false },
        Championship.Dtm => this with { EnableDtm = false },
        Championship.IndyCar => this with { EnableIndyCar = false },
        _ => this
    };

    public bool HasEnabledStream => EnableFormula1 || EnableFormula2 || EnableFormula3 || EnableMotoGp || EnableMoto2 || EnableMoto3 || EnableGtWorldChallengeEurope || EnableWec || EnableElms || EnableMichelinLeMansCup || EnableWrc || EnableImsa || EnableFormulaE || EnableDtm || EnableIndyCar;
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
            FlyoutBlurTint = Math.Clamp(FlyoutBlurTint, 30, 95),
            DriverCount = Math.Clamp(DriverCount, 1, 5),
            WidgetPosition = Enum.IsDefined(WidgetPosition) ? WidgetPosition : TaskbarWidgetPosition.LeftOfStart,
            ChampionshipDisplay = (ChampionshipDisplay ?? ChampionshipDisplayProfiles.Create(legacyDisplay)).Normalize()
        };
        foreach (var championship in hidden) normalized = normalized.WithChampionshipDisabled(championship);
        // Turning every championship off is a valid idle state: nothing is fetched and the widget stays hidden.
        return normalized;
    }
}

public sealed class UserSettingsStore(string? path = null, string? legacyPath = null)
{
    private readonly string? _legacyPath = legacyPath;
    /// <summary>Raise it when a stored field needs migrating.</summary>
    public const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string Path { get; } = path ?? AppPaths.Settings;

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return TryImportLegacySettings() ?? new UserSettings().Normalize();
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path), JsonOptions) ?? new UserSettings();
            if (settings.Version > CurrentVersion)
                AppLog.Warning("Settings", $"Settings were written by a newer build (version {settings.Version}); unrecognised fields are ignored.");
            return Upgrade(settings).Normalize();
        }
        catch (JsonException exception)
        {
            var kept = Preserve();
            AppLog.Error("Settings", "Settings are unreadable; starting from defaults.", exception, new { Preserved = kept });
            return new UserSettings().Normalize();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("Settings", "Settings could not be read; starting from defaults.", failure);
            return new UserSettings().Normalize();
        }
    }

    private UserSettings? TryImportLegacySettings()
    {
        var legacyPath = _legacyPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (string.Equals(System.IO.Path.GetFullPath(legacyPath), System.IO.Path.GetFullPath(Path), StringComparison.OrdinalIgnoreCase) || !File.Exists(legacyPath)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(legacyPath), JsonOptions);
            if (settings is null) return null;
            var normalized = Upgrade(settings).Normalize();
            Save(normalized);
            AppLog.Info("Settings", $"Imported settings from the previous installation at {legacyPath}.");
            return normalized;
        }
        catch (JsonException exception)
        {
            AppLog.Error("Settings", "The previous installation settings could not be imported.", exception);
            return null;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("Settings", "The previous installation settings could not be imported.", failure);
            return null;
        }
    }

    private static UserSettings Upgrade(UserSettings settings)
    {
        if (settings.Version >= 2 || settings.HiddenChampionships is null) return settings;
        var hidden = settings.HiddenChampionships.ToHashSet();
        hidden.Add(Championship.EuropeanLeMansSeries);
        hidden.Add(Championship.MichelinLeMansCup);
        return settings with { Version = 2, HiddenChampionships = [.. hidden] };
    }

    /// <summary>Moves the unreadable file aside and returns where it went, or null if it could not be kept.</summary>
    private string? Preserve()
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(Path);
        var kept = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Path) ?? "", $"{name}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        try { File.Move(Path, kept, true); return kept; }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) { return null; }
    }

    public void Save(UserSettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize((settings with { Version = CurrentVersion }).Normalize(), JsonOptions));
        File.Move(temporary, Path, true);
    }
}
