// Copyright (c) 2026 RaceDock contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace RaceDock.Core;

public abstract class AlkamelLiveTimingSourceFacade : ILiveTimingSource
{
    private readonly AlkamelLiveTimingSource _source;

    private protected AlkamelLiveTimingSourceFacade(
        AlkamelFeedDefinition definition,
        Func<IAlkamelDdpClient> clientFactory,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        AlkamelTimingOptions options)
    {
        _source = new(definition, clientFactory, clock, delay, alerts, options);
    }

    public event Action<TimingSnapshot>? SnapshotReceived
    {
        add => _source.SnapshotReceived += value;
        remove => _source.SnapshotReceived -= value;
    }

    public event Action<TimingDelta>? DeltaReceived
    {
        add => _source.DeltaReceived += value;
        remove => _source.DeltaReceived -= value;
    }

    public event Action<ConnectionState>? ConnectionChanged
    {
        add => _source.ConnectionChanged += value;
        remove => _source.ConnectionChanged -= value;
    }

    public event Action<FeedFailure>? Failed
    {
        add => _source.Failed += value;
        remove => _source.Failed -= value;
    }

    public Task StartAsync(CancellationToken cancellationToken) => _source.StartAsync(cancellationToken);
    public Task StopAsync() => _source.StopAsync();
    public ValueTask DisposeAsync() => _source.DisposeAsync();
}

public sealed class ImsaLiveTimingSource : AlkamelLiveTimingSourceFacade
{
    public ImsaLiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(() => new AlkamelDdpClient(AlkamelFeedDefinition.Imsa.SocketBase, "imsa"), clock,
            new SystemAsyncDelay(), alerts, AlkamelTimingOptions.Default) { }

    internal ImsaLiveTimingSource(
        Func<IAlkamelDdpClient> clientFactory,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        AlkamelTimingOptions options)
        : base(AlkamelFeedDefinition.Imsa, clientFactory, clock, delay, alerts, options) { }
}

public sealed class FormulaELiveTimingSource : AlkamelLiveTimingSourceFacade
{
    public FormulaELiveTimingSource(IClock clock, IAlertArbiter alerts)
        : this(() => new AlkamelDdpClient(AlkamelFeedDefinition.FormulaE.SocketBase, "formulae"), clock,
            new SystemAsyncDelay(), alerts, AlkamelTimingOptions.Default) { }

    internal FormulaELiveTimingSource(
        Func<IAlkamelDdpClient> clientFactory,
        IClock clock,
        IAsyncDelay delay,
        IAlertArbiter alerts,
        AlkamelTimingOptions options)
        : base(AlkamelFeedDefinition.FormulaE, clientFactory, clock, delay, alerts, options) { }
}
