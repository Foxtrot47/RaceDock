// SPDX-License-Identifier: GPL-3.0-or-later
using RaceDock.FeedServer.Recordings;

namespace RaceDock.FeedServer.Streams;

/// <summary>
/// Advances a poll-style stream over its recording at the recorded cadence, so two endpoints (such as GTWC
/// timing and competitor detail) always answer from the same moment.
/// </summary>
internal sealed class PlaybackCursor : IDisposable
{
    private readonly TimeSpan[] _delays;
    private readonly bool _loop;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _worker;
    private int _position;

    public PlaybackCursor(IReadOnlyList<RecordedEntry> entries, double speed, bool loop)
    {
        _loop = loop;
        _delays = new TimeSpan[entries.Count];
        for (var index = 1; index < entries.Count; index++)
            _delays[index] = RecordedDelay.Between(entries[index - 1].Timestamp, entries[index].Timestamp, speed);
        Count = entries.Count;
    }

    /// <summary>The newest entry the stream has reached.</summary>
    public int Position => Volatile.Read(ref _position);

    public int Count { get; }

    public void Start()
    {
        if (_worker is not null || Count == 0) return;
        _worker = Task.Run(() => RunAsync(_lifetime.Token));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var index = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (index > 0)
            {
                try { await Task.Delay(_delays[index], cancellationToken); }
                catch (OperationCanceledException) { return; }
            }
            Volatile.Write(ref _position, index);
            index++;
            if (index < Count) continue;
            if (!_loop) return;
            try { await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken); }
            catch (OperationCanceledException) { return; }
            index = 0;
        }
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
}

/// <summary>Keeps a recording's real-time gaps but bounds them, so a long idle stretch does not stall a test.</summary>
internal static class RecordedDelay
{
    private static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan Maximum = TimeSpan.FromSeconds(1);

    public static TimeSpan Between(DateTimeOffset previous, DateTimeOffset next, double speed)
    {
        var gap = next - previous;
        if (gap <= TimeSpan.Zero) gap = Minimum;
        else if (gap > Maximum) gap = Maximum;
        var scaled = TimeSpan.FromTicks((long)(gap.Ticks / Math.Max(0.25, speed)));
        return scaled < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : scaled;
    }
}
