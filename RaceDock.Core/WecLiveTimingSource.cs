// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later
namespace RaceDock.Core;

public sealed class WecLiveTimingSource : GriiipLiveTimingSource
{
    public WecLiveTimingSource(IClock clock, IAlertArbiter alerts, Uri? apiBase = null)
        : base(new GriiipApiClient(GriiipSeries.Wec, apiBase), GriiipSeries.Wec, clock, new SystemAsyncDelay(), alerts,
            GriiipPollingOptions.Default) { }
}
