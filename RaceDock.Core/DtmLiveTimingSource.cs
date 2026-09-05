// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json.Nodes;

namespace RaceDock.Core;

public sealed class DtmLiveTimingSource : GriiipLiveTimingSource
{
    public DtmLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : base(new GriiipApiClient(GriiipSeries.Dtm), GriiipSeries.Dtm, clock, new SystemAsyncDelay(), alerts,
            GriiipPollingOptions.Default) { }

    internal static JsonObject? ActiveSession(JsonArray sessions) =>
        GriiipLiveTimingSource.ActiveSession(sessions, GriiipSeries.Dtm);
}
