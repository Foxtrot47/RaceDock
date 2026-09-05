// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

internal static class DtmTiming
{
    public static TimingSnapshot BuildSnapshot(JsonObject session, JsonObject bootstrap, DateTimeOffset now, ConnectionState connection) =>
        GriiipTiming.BuildSnapshot(session, bootstrap, now, connection, GriiipSeries.Dtm);
}
