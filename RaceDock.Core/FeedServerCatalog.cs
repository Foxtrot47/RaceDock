// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDock.Core;

/// <summary>
/// Reads which recorded streams a local feed server is currently serving, so the application enables exactly
/// those championships instead of asking providers for everything.
/// </summary>
public static class FeedServerCatalog
{
    /// <summary>Every championship a feed server can emulate with the recordings RaceDock already supports.</summary>
    public static IReadOnlySet<Championship> EmulatedChampionships { get; } = new HashSet<Championship>
    {
        Championship.Formula1, Championship.Formula2, Championship.Formula3, Championship.F1Academy,
        Championship.GtWorldChallengeEurope, Championship.WorldEnduranceChampionship, Championship.NascarCup
    };

    /// <summary>
    /// Asks the server for its stream list. An unreachable or malformed answer yields no ids, so the caller can
    /// fall back to the complete emulated set rather than silently reaching live providers.
    /// </summary>
    public static async Task<IReadOnlyList<string>> GetStreamIdsAsync(Uri server, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        var address = new Uri(server.GetLeftPart(UriPartial.Authority) + "/streams");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            var streams = await http.GetFromJsonAsync<List<FeedServerStream>>(address, cancellationToken);
            return streams?.Select(stream => stream.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Maps the server's stream ids onto the championships their adapters publish.</summary>
    public static IReadOnlySet<Championship> Championships(IEnumerable<string> streamIds)
    {
        var served = new HashSet<Championship>();
        foreach (var id in streamIds)
        {
            var championship = id.Trim().ToLowerInvariant() switch
            {
                "f1" or "formula1" => Championship.Formula1,
                "f2" or "formula2" => Championship.Formula2,
                "f3" or "formula3" => Championship.Formula3,
                "f1academy" => Championship.F1Academy,
                "gtwc" => Championship.GtWorldChallengeEurope,
                "wec" => Championship.WorldEnduranceChampionship,
                "nascar" => Championship.NascarCup,
                _ => Championship.Unknown
            };
            if (championship != Championship.Unknown) served.Add(championship);
        }
        return served;
    }

    private sealed record FeedServerStream(string Id);
}
