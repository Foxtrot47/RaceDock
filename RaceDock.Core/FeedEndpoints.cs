// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

/// <summary>
/// Where each live adapter connects. Production values are the provider endpoints; a test harness can point
/// every transport at one local server instead, without changing how any feed is parsed or mapped.
/// </summary>
/// <remarks>
/// The values are constructor data rather than ambient state so two composites with different endpoints can
/// coexist in one process, which is what the end-to-end feed-server tests rely on.
/// </remarks>
public sealed record FeedEndpoints
{
    public const string Formula1DefaultWebSocket = "wss://livetiming.formula1.com/signalrcore";
    public const string Formula2DefaultBase = "https://ltss.fiaformula2.com/streaming";
    public const string Formula3DefaultBase = "https://ltss.fiaformula3.com/streaming";
    public const string F1AcademyDefaultBase = "https://f2f3-prod-livetiming.azurewebsites.net/streaming";
    public static readonly Uri GtwcDefaultBase = new("https://ps-cache.web.swisstiming.com/");
    public static readonly Uri GriiipDefaultBase = new("https://insights.griiip.com/");
    public static readonly Uri NascarDefaultBase = new("https://cf.nascar.com/");

    public string Formula1WebSocket { get; init; } = Formula1DefaultWebSocket;
    public string Formula2Base { get; init; } = Formula2DefaultBase;
    public string Formula3Base { get; init; } = Formula3DefaultBase;
    public string F1AcademyBase { get; init; } = F1AcademyDefaultBase;
    public Uri GtwcBase { get; init; } = GtwcDefaultBase;
    public Uri GriiipBase { get; init; } = GriiipDefaultBase;
    public Uri NascarBase { get; init; } = NascarDefaultBase;

    /// <summary>The provider endpoints every release ships with.</summary>
    public static FeedEndpoints Live { get; } = new();

    /// <summary>
    /// Routes every provider transport at one local feed server. The path layout mirrors the providers, so the
    /// adapters keep requesting the same relative resources they request in production.
    /// </summary>
    public static FeedEndpoints ForServer(Uri server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var root = server.GetLeftPart(UriPartial.Authority);
        var webSocketScheme = string.Equals(server.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? "wss"
            : "ws";
        var authority = server.Authority;
        return new()
        {
            Formula1WebSocket = $"{webSocketScheme}://{authority}/f1/signalrcore",
            Formula2Base = $"{root}/f2/streaming",
            Formula3Base = $"{root}/f3/streaming",
            F1AcademyBase = $"{root}/f1academy/streaming",
            GtwcBase = new Uri($"{root}/"),
            GriiipBase = new Uri($"{root}/"),
            NascarBase = new Uri($"{root}/")
        };
    }
}
