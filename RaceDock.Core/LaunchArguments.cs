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
    public const string ReplayFlag = "--replay";
    public const string FeedServerFlag = "--feed-server";

    public static string? ReplayPath(IEnumerable<string>? arguments) => FlagValue(arguments, ReplayFlag);

    /// <summary>The local feed server the session should point every adapter at, when one was requested.</summary>
    public static string? FeedServerUrl(IEnumerable<string>? arguments) => FlagValue(arguments, FeedServerFlag);

    private static string? FlagValue(IEnumerable<string>? arguments, string flag)
    {
        if (arguments is null) return null;
        var values = arguments.ToArray();
        for (var index = 0; index < values.Length; index++)
        {
            var argument = values[index]?.Trim();
            if (string.IsNullOrEmpty(argument)) continue;
            if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
            {
                var next = index + 1 < values.Length ? values[index + 1] : null;
                return string.IsNullOrWhiteSpace(next) ? null : next.Trim().Trim('"');
            }
            var prefix = flag + "=";
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return argument[prefix.Length..].Trim().Trim('"');
        }
        return null;
    }

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
