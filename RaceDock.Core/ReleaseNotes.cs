// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>One "### Added" style block of a release entry.</summary>
public sealed record ReleaseNoteGroup(string Heading, IReadOnlyList<string> Items);

/// <summary>One released version, as recorded in the changelog.</summary>
public sealed record ReleaseNote(string Version, string Date, IReadOnlyList<ReleaseNoteGroup> Groups);

/// <summary>
/// Reads the changelog the project already maintains, so the application shows the same release notes that
/// ship with the source instead of sending the reader to the commit history.
/// </summary>
public static class ReleaseNotes
{
    public static IReadOnlyList<ReleaseNote> Parse(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return [];
        List<ReleaseNote> notes = [];
        List<ReleaseNoteGroup> groups = [];
        List<string> items = [];
        string? version = null;
        string date = "";
        string heading = "";

        void CloseGroup()
        {
            if (items.Count > 0) groups.Add(new(heading, [.. items]));
            items.Clear();
        }

        void CloseNote()
        {
            CloseGroup();
            if (version is not null && groups.Count > 0) notes.Add(new(version, date, [.. groups]));
            groups.Clear();
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                CloseNote();
                (version, date) = ParseReleaseHeading(line);
                heading = "";
            }
            else if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                CloseGroup();
                heading = line[4..].Trim();
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) && version is not null)
            {
                items.Add(line[2..].Trim());
            }
        }
        CloseNote();
        return notes;
    }

    public static ReleaseNote? ForVersion(IReadOnlyList<ReleaseNote> notes, string? version) =>
        notes.FirstOrDefault(note => string.Equals(note.Version, Normalize(version), StringComparison.OrdinalIgnoreCase));

    /// <summary>Reads "## [1.0.2] — 2026-08-28" into its version and date, tolerating a missing date.</summary>
    private static (string? Version, string Date) ParseReleaseHeading(string line)
    {
        var text = line[3..].Trim();
        var open = text.IndexOf('[');
        var close = text.IndexOf(']');
        if (open < 0 || close < open) return (null, "");
        var version = text[(open + 1)..close].Trim();
        var remainder = text[(close + 1)..].Trim().TrimStart('—', '-', '–').Trim();
        return (version.Length == 0 ? null : version, remainder);
    }

    /// <summary>Strips a leading "v" and any build metadata so a tag, a display version, and a heading match.</summary>
    public static string Normalize(string? version)
    {
        var text = (version ?? "").Trim();
        if (text.StartsWith('v') || text.StartsWith('V')) text = text[1..];
        var metadata = text.IndexOf('+');
        return metadata < 0 ? text : text[..metadata];
    }
}
