// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal static class ImsaTiming
{
    public static TimingSnapshot BuildSnapshot(
        JsonObject session,
        JsonObject info,
        JsonObject entries,
        JsonObject track,
        JsonObject standings,
        JsonObject status,
        JsonObject? bestResults,
        DateTimeOffset now,
        ConnectionState connection) =>
        AlkamelTiming.BuildSnapshot(session, info, entries, track, standings, status, bestResults,
            now, connection, Championship.Imsa, "IMSA");

    public static AlkamelTrackState TrackState(JsonObject status) => AlkamelTiming.TrackState(status);
    public static string? TimeRemaining(JsonObject status, DateTimeOffset now) => AlkamelTiming.TimeRemaining(status, now);
    public static string? StatusLabel(string classification, string location, bool checkered) =>
        AlkamelTiming.StatusLabel(classification, location, checkered);
}
