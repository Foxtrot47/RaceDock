// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Remembers that a session ended badly, so the next launch can say so.
/// </summary>
/// <remarks>
/// The log already holds the exception, so this carries only the one line a notice can show. It is
/// written from a dying process and so stays a single file write, with nothing that can fail first.
/// </remarks>
public static class CrashMarker
{
    private const string FileName = "last-session-fatal.txt";
    private static string _folder = AppPaths.Root;

    /// <summary>Redirects the marker. For tests, and for any host that resolves its own root.</summary>
    public static void UseFolder(string folder) => _folder = folder;

    /// <summary>Records that the session is ending badly.</summary>
    public static void Set(string summary)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, FileName), summary);
        }
        // A process on its way out has nowhere left to report this.
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Returns the previous session's summary and clears it, or null when it ended cleanly.</summary>
    public static string? Consume()
    {
        var path = Path.Combine(_folder, FileName);
        try
        {
            if (!File.Exists(path)) return null;
            var summary = File.ReadAllText(path);
            File.Delete(path);
            return summary;
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException) { return null; }
    }
}
