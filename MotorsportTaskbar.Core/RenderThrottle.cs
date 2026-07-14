// SPDX-License-Identifier: GPL-3.0-or-later
namespace MotorsportTaskbar.Core;

public sealed class RenderThrottle(IClock clock, TimeSpan minimumInterval)
{
    private DateTimeOffset _last = DateTimeOffset.MinValue;
    private TimingSnapshot? _pending;
    public void Offer(TimingSnapshot snapshot) => _pending = snapshot;
    public TimingSnapshot? TakeIfDue()
    {
        if (_pending is null || clock.UtcNow - _last < minimumInterval) return null;
        var value = _pending; _pending = null; _last = clock.UtcNow; return value;
    }
}

public static class TaskbarRecovery
{
    public static bool NeedsReattach(nint expectedTaskbar, nint currentParent, bool taskbarCreatedMessage) =>
        taskbarCreatedMessage || expectedTaskbar != 0 && currentParent != expectedTaskbar;
}
