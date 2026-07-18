// Copyright (c) 2026 MotorsportTaskbar contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;

namespace MotorsportTaskbar.Core;

public sealed record UserSettings
{
    public bool EnableFormula1 { get; init; } = true;
    public bool EnableFormula2 { get; init; } = true;
    public bool EnableFormula3 { get; init; } = true;
    public bool EnableWrc { get; init; } = true;
    public int RotationSeconds { get; init; } = 5;
    public int DriverCount { get; init; } = 5;
    public bool ShowEventHeader { get; init; } = true;
    public bool ShowSessionProgress { get; init; } = true;
    public bool ShowGaps { get; init; } = true;
    public bool ShowPositionColors { get; init; } = true;
    public bool UseManufacturerLogos { get; init; }
    public bool ShowFlagAlerts { get; init; } = true;

    public bool HasEnabledStream => EnableFormula1 || EnableFormula2 || EnableFormula3 || EnableWrc;

    public UserSettings Normalize()
    {
        var normalized = this with
        {
            RotationSeconds = Math.Clamp(RotationSeconds, 2, 30),
            DriverCount = Math.Clamp(DriverCount, 1, 5)
        };
        return normalized.HasEnabledStream ? normalized : normalized with { EnableFormula1 = true };
    }
}

public sealed class UserSettingsStore(string? path = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string Path { get; } = path ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MotorsportTaskbar", "settings.json");

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(Path)) return new UserSettings();
            return (JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(Path), JsonOptions) ?? new UserSettings()).Normalize();
        }
        catch (JsonException) { return new UserSettings(); }
        catch (IOException) { return new UserSettings(); }
        catch (UnauthorizedAccessException) { return new UserSettings(); }
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
