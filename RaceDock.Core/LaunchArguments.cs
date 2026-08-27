// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Command-line contract between the Windows startup registration and the application.
/// Registered startup entries pass <see cref="StartupFlag"/> so an automatic sign-in launch can stay
/// in the notification area while a launch started by the user opens a window.
/// </summary>
public static class LaunchArguments
{
    public const string StartupFlag = "--startup";

    public static bool IndicatesStartup(IEnumerable<string>? arguments)
    {
        if (arguments is null) return false;
        foreach (var argument in arguments)
        {
            if (string.IsNullOrWhiteSpace(argument)) continue;
            var normalized = argument.Trim().TrimStart('-', '/');
            if (string.Equals(normalized, "startup", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the executable path out of a Windows Run registry value, which may be quoted and may carry
    /// the startup flag. Returns null when the value holds no usable path.
    /// </summary>
    public static string? ExecutablePathFromRunValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed[0] == '"')
        {
            var closing = trimmed.IndexOf('"', 1);
            if (closing <= 1) return null;
            var quoted = trimmed[1..closing].Trim();
            return quoted.Length == 0 ? null : quoted;
        }

        var flag = trimmed.IndexOf(StartupFlag, StringComparison.OrdinalIgnoreCase);
        var path = (flag < 0 ? trimmed : trimmed[..flag]).Trim();
        return path.Length == 0 ? null : path;
    }
}
