// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http;
using System.Text.Json.Nodes;

namespace RaceDock.Core;

/// <summary>A published release that is newer than the running build.</summary>
public sealed record ReleaseUpdate(string Version, string Url);

/// <summary>
/// Compares the running build against the published releases. Store installations update through the Store,
/// so this is only used by builds installed from the project's own downloads.
/// </summary>
public static class UpdateCheck
{
    /// <summary>
    /// Compares release numbers only. A prerelease suffix is ignored, because the published releases are
    /// stable versions and an unparsable value must not be reported as an update.
    /// </summary>
    public static bool IsNewer(string? candidate, string? current) =>
        TryParse(candidate, out var latest) && TryParse(current, out var running) && latest > running;

    private static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0);
        var text = ReleaseNotes.Normalize(value);
        var suffix = text.IndexOf('-');
        if (suffix >= 0) text = text[..suffix];
        return Version.TryParse(text, out version!);
    }
}

/// <summary>Reads the latest published release from the project's GitHub repository.</summary>
public sealed class GitHubReleaseClient : IDisposable
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Foxtrot47/RaceDock/releases/latest";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public GitHubReleaseClient(HttpClient? httpClient = null)
    {
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd(BrowserUserAgent.Value);
        _http.DefaultRequestHeaders.Accept.TryParseAdd("application/vnd.github+json");
    }

    /// <summary>Returns the published release when it is newer than <paramref name="currentVersion"/>.</summary>
    public async Task<ReleaseUpdate?> GetNewerReleaseAsync(string currentVersion, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(LatestReleaseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
        var tag = JsonSupport.String(payload?["tag_name"]);
        var url = JsonSupport.String(payload?["html_url"]) ?? "https://github.com/Foxtrot47/RaceDock/releases";
        return UpdateCheck.IsNewer(tag, currentVersion) ? new(ReleaseNotes.Normalize(tag), url) : null;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
