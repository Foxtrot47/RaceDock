// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public sealed class WecLiveTimingSource : GriiipLiveTimingSource
{
    public WecLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : base(new GriiipApiClient(GriiipSeries.Wec), GriiipSeries.Wec, clock, new SystemAsyncDelay(), alerts,
            GriiipPollingOptions.Default) { }
}
