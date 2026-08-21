// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// The single place that decides where RaceDock keeps everything it writes.
/// </summary>
/// <remarks>
/// Nothing may be written beside the executable: that directory is unwritable under Program Files
/// and read-only inside a package. Callers receive a path only, because creating a directory is the
/// writer's business.
/// </remarks>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RaceDock");

    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Logs => Path.Combine(Root, "Logs");
}
