// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public sealed class MichelinLeMansCupLiveTimingSource : GriiipLiveTimingSource
{
    public MichelinLeMansCupLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : base(new GriiipApiClient(GriiipSeries.MichelinLeMansCup), GriiipSeries.MichelinLeMansCup, clock,
            new SystemAsyncDelay(), alerts, GriiipPollingOptions.Default) { }
}
