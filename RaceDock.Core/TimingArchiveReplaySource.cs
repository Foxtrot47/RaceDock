// SPDX-License-Identifier: GPL-3.0-or-later

namespace RaceDock.Core;

public sealed class TimingArchiveReplaySource : ITimingArchiveSource
{
    private readonly IClock _clock;
    /// <summary>When each recorded frame was captured, which is what paces the replay.</summary>
    private readonly IReadOnlyList<DateTimeOffset> _timestamps;
    private readonly Func<int, TimingSnapshot> _readSnapshot;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private int _nextFrame;
    private int _generation;
    private double _speed = 4;
    private volatile bool _paused;
    private volatile bool _completed;
    private bool _disposed;
    private bool _active;

    internal TimingArchiveReplaySource(string path, string name, IClock clock,
        IReadOnlyList<TimingSnapshot> snapshots, IReadOnlyList<DateTimeOffset> timestamps)
    {
        _clock = clock;
        ArchivePath = path;
        ArchiveName = name;
        _timestamps = timestamps;
        _readSnapshot = index => snapshots[index];
    }

    public string ArchivePath { get; }
    public string ArchiveName { get; }
    public int FrameIndex { get { lock (_gate) return Math.Clamp(_nextFrame, 1, _timestamps.Count); } }
    public int FrameCount => _timestamps.Count;
    public bool IsPaused => _paused;
    public bool IsCompleted => _completed;
    public double Speed
    {
        get => Volatile.Read(ref _speed);
        set => Volatile.Write(ref _speed, Math.Clamp(value, .25, 20));
    }

    public event Action<TimingSnapshot>? SnapshotReceived;
    public event Action<TimingDelta>? DeltaReceived { add { } remove { } }
    public event Action<ConnectionState>? ConnectionChanged;
    public event Action<FeedFailure>? Failed;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (_cts is not null) return;
            CancellationToken runToken;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                runToken = _cts.Token;
                _nextFrame = 1;
                _generation++;
                // A replay always opens paused on its first frame, so the reader decides when it runs.
                _completed = _timestamps.Count == 1;
                _paused = true;
                _active = true;
                ConnectionChanged?.Invoke(ConnectionState.Connected);
                Publish(_readSnapshot(0));
            }
            _worker = Task.Run(() => RunAsync(runToken), runToken);
        }
        finally { _lifecycle.Release(); }
    }

    public void Pause()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _paused = true;
            _generation++;
        }
    }
    public void Resume()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active || _completed) return;
            _paused = false;
        }
        Signal();
    }

    public void Restart()
    {
        _paused = true;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            _nextFrame = 1;
            _generation++;
            _completed = _timestamps.Count == 1;
            Publish(_readSnapshot(0));
        }
        Signal();
    }

    public void Step()
    {
        _paused = true;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_active) return;
            if (_nextFrame >= _timestamps.Count)
            {
                _completed = true;
                return;
            }
            var frame = _readSnapshot(_nextFrame++);
            _generation++;
            _completed = _nextFrame >= _timestamps.Count;
            Publish(frame);
        }
        Signal();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_paused) await _wake.WaitAsync(cancellationToken);
                int index;
                int generation;
                lock (_gate) { index = _nextFrame; generation = _generation; }
                if (index >= _timestamps.Count)
                {
                    _completed = true;
                    _paused = true;
                    continue;
                }

                var rawDelay = _timestamps[index] - _timestamps[index - 1];
                var replayDelay = rawDelay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(50)
                    : rawDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(1)
                    : rawDelay;
                if (!await DelayForArchiveTimeAsync(replayDelay, generation, cancellationToken)) continue;

                lock (_gate)
                {
                    if (generation != _generation || index != _nextFrame) continue;
                    var frame = _readSnapshot(_nextFrame++);
                    if (_nextFrame >= _timestamps.Count)
                    {
                        _completed = true;
                        _paused = true;
                    }
                    Publish(frame);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _paused = true;
            ConnectionChanged?.Invoke(ConnectionState.Faulted);
            Failed?.Invoke(new($"Archive replay failed: {ex.Message}", ex, _clock.UtcNow));
        }
    }

    private async Task<bool> DelayForArchiveTimeAsync(TimeSpan duration, int generation, CancellationToken cancellationToken)
    {
        var remaining = duration.TotalSeconds;
        while (remaining > 0)
        {
            if (_paused)
            {
                await _wake.WaitAsync(cancellationToken);
                continue;
            }
            lock (_gate) if (generation != _generation) return false;
            var speed = Speed;
            var wallSeconds = Math.Min(.1, remaining / speed);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(.001, wallSeconds)), cancellationToken);
            remaining -= System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds * speed;
        }
        return true;
    }

    private void Publish(TimingSnapshot snapshot) => SnapshotReceived?.Invoke(snapshot with
    {
        FreshnessTimestamp = _clock.UtcNow,
        ConnectionState = ConnectionState.Connected
    });

    private void Signal()
    {
        try { _wake.Release(); }
        catch (SemaphoreFullException) { }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        if (_cts is null) return;
        lock (_gate)
        {
            _active = false;
            _paused = true;
            _generation++;
        }
        await _cts.CancelAsync();
        Signal();
        if (_worker is not null)
            try { await _worker; }
            catch (OperationCanceledException) { }
        _worker = null;
        _cts.Dispose();
        _cts = null;
        ConnectionChanged?.Invoke(ConnectionState.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _active = false;
                _generation++;
            }
            await StopCoreAsync();
        }
        finally { _lifecycle.Release(); }
    }


}
