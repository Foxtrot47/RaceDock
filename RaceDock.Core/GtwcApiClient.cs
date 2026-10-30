// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal sealed record GtwcCacheSnapshot(string? Hash, JsonObject Full);

internal sealed class GtwcApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _apiBase;
    private long _requestId;

    public GtwcApiClient(HttpClient? httpClient = null, Uri? apiBase = null)
    {
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient(CreateDefaultHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        // The provider serves RAC_PROD under the swisstiming cache root; a feed server mirrors that layout.
        _apiBase = new Uri(apiBase ?? FeedEndpoints.GtwcDefaultBase, "node/db/RAC_PROD/").ToString();
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent.Value);
    }

    internal static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
        ConnectTimeout = TimeSpan.FromSeconds(10)
    };

    public Task<GtwcCacheSnapshot> GetSeasonsAsync(CancellationToken ct) => GetFullAsync("SRO_SEASONS_JSON", ct);
    public Task<GtwcCacheSnapshot> GetSeasonAsync(string year, CancellationToken ct) =>
        GetFullAsync($"SRO_{year}_SEASON_JSON", ct);
    public Task<GtwcCacheSnapshot> GetScheduleAsync(string year, string meetingId, CancellationToken ct) =>
        GetFullAsync($"SRO_{year}_SCHEDULE_{meetingId.ToUpperInvariant()}_JSON", ct);
    public Task<GtwcCacheSnapshot> GetTimingAsync(string year, string unitId, CancellationToken ct) =>
        GetFullAsync($"SRO_{year}_TIMING_{unitId.ToUpperInvariant()}_JSON", ct);
    public Task<GtwcCacheSnapshot> GetCompetitionDetailAsync(string year, string unitId, CancellationToken ct) =>
        GetFullAsync($"SRO_{year}_COMP_DETAIL_{unitId.ToUpperInvariant()}_JSON", ct);

    private async Task<GtwcCacheSnapshot> GetFullAsync(string key, CancellationToken ct)
    {
        var requestId = Interlocked.Increment(ref _requestId);
        var uri = new Uri($"{_apiBase}{key}.json?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{requestId}");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(ct);
        FeedDump.Record("gtwc", text);
        var root = JsonNode.Parse(text) as JsonObject
            ?? throw new InvalidOperationException($"GTWC endpoint {key} returned an unexpected JSON shape.");
        var full = root["content"]?["full"] as JsonObject
            ?? throw new InvalidOperationException($"GTWC endpoint {key} did not contain a full snapshot.");
        return new(JsonSupport.String(root["hash"]), full);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
