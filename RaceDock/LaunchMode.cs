// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

namespace RaceDock.App;

/// <summary>
/// Decides whether the process was started automatically by Windows or deliberately by the user.
/// Unpackaged builds carry <see cref="LaunchArguments.StartupFlag"/> on their Run entry; packaged builds
/// report a startup-task activation instead, because the manifest startup task passes no arguments.
/// </summary>
internal static class LaunchMode
{
    public static bool IsAutomaticStartup(string[]? arguments) =>
        LaunchArguments.IndicatesStartup(arguments) || IsPackagedStartupActivation();

#if RACEDOCK_PACKAGED
    private static bool IsPackagedStartupActivation()
    {
        if (!WindowsStartupRegistration.IsPackaged) return false;
        try
        {
            var activation = global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            return activation?.Kind == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        catch (Exception failure)
        {
            // Activation info is unavailable on unexpected shell states. Treating that as a user launch
            // keeps the window visible rather than starting the app with nothing on screen.
            AppLog.Warning("App", "Could not read activation info.", failure);
            return false;
        }
    }
#else
    // Unpackaged builds are never activated as a startup task; their Run entry carries the flag instead.
    private static bool IsPackagedStartupActivation() => false;
#endif
}
