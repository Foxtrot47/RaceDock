// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;

namespace MotorsportTaskbar.App;

internal static class AppVersion
{
    public static string Display { get; } = GetDisplayVersion();

    private static string GetDisplayVersion()
    {
        var informationalVersion = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion)) return "unknown";

        var buildMetadata = informationalVersion.IndexOf('+');
        return buildMetadata < 0 ? informationalVersion : informationalVersion[..buildMetadata];
    }
}
