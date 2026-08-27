// SPDX-License-Identifier: GPL-3.0-or-later
using System.Threading;
using RaceDock.Core;

namespace RaceDock.App;

/// <summary>
/// Lets a second launch ask the running instance to show its window instead of exiting with no visible
/// result. The event is session-local, so it never crosses user sessions.
/// </summary>
internal sealed class SecondInstanceSignal : IDisposable
{
    private const string EventName = @"Local\RaceDock.ShowWindow";

    private readonly EventWaitHandle _handle;
    private readonly RegisteredWaitHandle _registration;

    private SecondInstanceSignal(EventWaitHandle handle, RegisteredWaitHandle registration)
    {
        _handle = handle;
        _registration = registration;
    }

    /// <summary>Starts listening in the first instance. Returns null when the event cannot be created.</summary>
    public static SecondInstanceSignal? Listen(Action onRequested)
    {
        try
        {
            var handle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            var registration = ThreadPool.RegisterWaitForSingleObject(handle, (_, _) => onRequested(), null, -1, false);
            return new SecondInstanceSignal(handle, registration);
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            AppLog.Warning("App", "Could not listen for second-instance launches.", failure);
            return null;
        }
    }

    /// <summary>Called by a second instance before it exits.</summary>
    public static void RequestExistingWindow()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(EventName, out var handle)) return;
            using (handle) handle.Set();
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        {
            AppLog.Warning("App", "Could not reach the running RaceDock instance.", failure);
        }
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _handle.Dispose();
    }
}
