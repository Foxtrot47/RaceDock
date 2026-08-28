// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.Core;

namespace RaceDock.App;

/// <summary>
/// One update check per application session, shared by the pages that report it. Store installations update
/// through the Store, so the packaged build never reaches the network here.
/// </summary>
internal static class UpdateProbe
{
    private static Task<ReleaseUpdate?>? _pending;

    /// <summary>Whether this build looks for its own updates at all.</summary>
    public static bool IsSupported =>
#if RACEDOCK_PACKAGED
        false;
#else
        true;
#endif

    public static Task<ReleaseUpdate?> CheckAsync(bool refresh = false)
    {
        if (!IsSupported) return Task.FromResult<ReleaseUpdate?>(null);
        if (refresh || _pending is null || _pending.IsFaulted) _pending = QueryAsync();
        return _pending;
    }

    private static async Task<ReleaseUpdate?> QueryAsync()
    {
        using var client = new GitHubReleaseClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        return await client.GetNewerReleaseAsync(AppVersion.Display, timeout.Token);
    }
}
