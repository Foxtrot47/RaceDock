// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using RaceDock.FeedServer.Recordings;
using RaceDock.FeedServer.Streams;

namespace RaceDock.FeedServer;

/// <summary>
/// A local stand-in for the provider hosts. Every stream is served from one origin and advances on its own
/// recorded cadence, so several championships can run at once exactly as they do live.
/// </summary>
public sealed class FeedServerHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly IReadOnlyList<IFeedEndpoint> _endpoints;
    private readonly bool _loop;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _started;

    private FeedServerHost(WebApplication app, IReadOnlyList<IFeedEndpoint> endpoints, bool loop) =>
        (_app, _endpoints, _loop) = (app, endpoints, loop);

    public static FeedServerHost Create(FeedServerOptions options, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var catalog = DumpCatalog.Resolve(options, log);
        var endpoints = EndpointFactory.Create(catalog.Dumps, options, log);
        if (endpoints.Count == 0)
            throw new InvalidOperationException(
                "No recorded stream was found. Pass --root <folder> or --dump <feed>=<path>.");

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://{options.Bind}:{options.Port}");
        var app = builder.Build();
        app.UseWebSockets();
        foreach (var endpoint in endpoints) endpoint.Map(app);
        app.MapGet("/streams", () => Results.Json(endpoints.Select(endpoint => new FeedStreamInfo(
            endpoint.Id, endpoint.Championship, endpoint.DumpPath, endpoint.FrameCount, options.Loop))));
        return new(app, endpoints, options.Loop);
    }

    public Uri BaseAddress { get; private set; } = new("http://127.0.0.1");

    public IReadOnlyList<FeedStreamInfo> Streams => _endpoints
        .Select(endpoint => new FeedStreamInfo(endpoint.Id, endpoint.Championship, endpoint.DumpPath, endpoint.FrameCount, _loop))
        .ToList();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started) return;
        await _app.StartAsync(cancellationToken);
        _started = true;
        var addresses = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>();
        BaseAddress = new Uri(addresses?.Addresses.FirstOrDefault() ?? _app.Urls.First());
        foreach (var endpoint in _endpoints) endpoint.Start(_lifetime.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        if (_started) await _app.StopAsync(TimeSpan.FromSeconds(2));
        await _app.DisposeAsync();
    }
}
