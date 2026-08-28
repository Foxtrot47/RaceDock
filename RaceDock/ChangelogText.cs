// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Reflection;

namespace RaceDock.App;

/// <summary>The project changelog, embedded so the build always carries the notes for the version it is.</summary>
internal static class ChangelogText
{
    private const string ResourceName = "RaceDock.CHANGELOG.md";

    public static string Read()
    {
        using var stream = typeof(ChangelogText).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
