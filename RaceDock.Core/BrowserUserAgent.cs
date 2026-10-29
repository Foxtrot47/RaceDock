// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// The User-Agent every provider request sends: Google Chrome's default on Windows, in the reduced form Chrome itself
/// reports (only the major version is real). Some providers refuse a request that names no browser.
/// </summary>
internal static class BrowserUserAgent
{
    public const string Value =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/157.0.0.0 Safari/537.36";
}
