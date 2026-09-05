// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public sealed class ElmsLiveTimingSource : GriiipLiveTimingSource
{
    public ElmsLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : base(new GriiipApiClient(GriiipSeries.Elms), GriiipSeries.Elms, clock, new SystemAsyncDelay(), alerts,
            GriiipPollingOptions.Default) { }
}
