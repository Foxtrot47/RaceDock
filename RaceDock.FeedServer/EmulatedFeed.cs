// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.FeedServer;

/// <summary>The stream ids a feed server can serve, which double as the provider path prefixes.</summary>
internal static class EmulatedFeed
{
    public const string Formula1 = "f1";
    public const string Formula2 = "f2";
    public const string Formula3 = "f3";
    public const string F1Academy = "f1academy";
    public const string Gtwc = "gtwc";
    public const string Wec = "wec";
    public const string Nascar = "nascar";

    public static readonly IReadOnlyList<string> All =
        [Formula1, Formula2, Formula3, F1Academy, Gtwc, Wec, Nascar];

    /// <summary>The dump-file stem each feed is recorded under.</summary>
    public static readonly IReadOnlyDictionary<string, string> FileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Formula1] = "formula1",
        [Formula2] = "f2",
        [Formula3] = "f3",
        [F1Academy] = "f1academy",
        [Gtwc] = "gtwc",
        [Wec] = "wec",
        [Nascar] = "nascar"
    };

    /// <summary>The championship a stream reports, used by <c>/streams</c> so the app can enable it.</summary>
    public static readonly IReadOnlyDictionary<string, string> Championships =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Formula1] = "Formula1",
            [Formula2] = "Formula2",
            [Formula3] = "Formula3",
            [F1Academy] = "F1Academy",
            [Gtwc] = "GtWorldChallengeEurope",
            [Wec] = "WorldEnduranceChampionship",
            [Nascar] = "NascarCup"
        };

    public static string? FromFileName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        foreach (var (feed, fileName) in FileNames)
            if (string.Equals(stem, fileName, StringComparison.OrdinalIgnoreCase)) return feed;
        return null;
    }
}
