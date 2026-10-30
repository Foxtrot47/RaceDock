// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.FeedServer;

/// <summary>Command-line contract for the local recorded-feed server.</summary>
public sealed record FeedServerOptions
{
    /// <summary>Where the listener binds. Loopback only by default, because this is a test tool.</summary>
    public string Bind { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8099;

    /// <summary>A folder searched recursively for feed dumps when a stream has no explicit dump.</summary>
    public string? Root { get; init; }

    /// <summary>Explicit <c>feed=path</c> assignments that win over anything found under <see cref="Root"/>.</summary>
    public IReadOnlyDictionary<string, string> Dumps { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Playback speed relative to the recorded wall clock.</summary>
    public double Speed { get; init; } = 4;

    /// <summary>Whether a finished recording starts again, so a test session never runs dry.</summary>
    public bool Loop { get; init; } = true;

    /// <summary>Rewrites NASCAR's assembly stamp to now; the app hides payloads older than two minutes.</summary>
    public bool RetimeNascar { get; init; } = true;

    public bool ShowHelp { get; init; }

    public static FeedServerOptions Parse(string[] args)
    {
        var options = new FeedServerOptions();
        var dumps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--help" or "-h":
                    options = options with { ShowHelp = true };
                    break;
                case "--once":
                    options = options with { Loop = false };
                    break;
                case "--verbatim":
                    options = options with { RetimeNascar = false };
                    break;
                case "--port" when index + 1 < args.Length:
                    if (int.TryParse(args[++index], out var port) && port is >= 0 and <= 65535)
                        options = options with { Port = port };
                    break;
                case "--bind" when index + 1 < args.Length:
                    options = options with { Bind = args[++index] };
                    break;
                case "--root" when index + 1 < args.Length:
                    options = options with { Root = args[++index] };
                    break;
                case "--speed" when index + 1 < args.Length:
                    if (double.TryParse(args[++index], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var speed) && speed > 0)
                        options = options with { Speed = Math.Clamp(speed, 0.25, 2000) };
                    break;
                case "--dump" when index + 1 < args.Length:
                    ReadDump(args[++index], dumps);
                    break;
                default:
                    if (argument.StartsWith("--dump=", StringComparison.OrdinalIgnoreCase))
                        ReadDump(argument["--dump=".Length..], dumps);
                    else if (argument.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(argument["--port=".Length..], out var inlinePort))
                        options = options with { Port = inlinePort };
                    else if (argument.StartsWith("--root=", StringComparison.OrdinalIgnoreCase))
                        options = options with { Root = argument["--root=".Length..] };
                    else if (argument.StartsWith("--speed=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(argument["--speed=".Length..],
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var inlineSpeed) && inlineSpeed > 0)
                        options = options with { Speed = Math.Clamp(inlineSpeed, 0.25, 2000) };
                    break;
            }
        }
        return options with { Dumps = dumps };
    }

    private static void ReadDump(string value, Dictionary<string, string> dumps)
    {
        var separator = value.IndexOf('=');
        if (separator <= 0 || separator == value.Length - 1) return;
        var feed = value[..separator].Trim().ToLowerInvariant();
        var path = value[(separator + 1)..].Trim().Trim('"');
        if (feed.Length > 0 && path.Length > 0) dumps[feed] = path;
    }
}
