using ContextSwitcher.Core.Analytics;
using ContextSwitcher.Infrastructure.Windows.Away;
using Microsoft.Win32;

namespace ContextSwitcher.Tests.Windows.Away;

/// <summary>What Windows says of sleep, the screen lock and the session changing, and what is made of it.</summary>
public sealed class WindowsAwayEventsTests
{
    private readonly FakeSystemEvents windows = new();

    /// <summary>
    /// Listening from a thread with no context, as the tests' own has one: what is heard is then raised where
    /// Windows says it, at once, and a test can look straight away.
    /// </summary>
    private (WindowsAwayEvents Events, List<(string Kind, AwayReason Reason)> Heard) Listening()
    {
        SynchronizationContext? before = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            WindowsAwayEvents events = new(this.windows);
            List<(string, AwayReason)> heard = [];
            events.Away += (_, reason) => heard.Add(("away", reason));
            events.Back += (_, reason) => heard.Add(("back", reason));
            return (events, heard);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
        }
    }

    [Fact]
    public void SuspendingIsAwayAndResumingIsBackForSleep()
    {
        (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        this.windows.Power(PowerModes.Suspend);
        this.windows.Power(PowerModes.Resume);

        Assert.Equal([("away", AwayReason.Sleep), ("back", AwayReason.Sleep)], heard);
    }

    [Fact]
    public void ABatteryStatusChangeIsNeitherAwayNorBack()
    {
        (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        this.windows.Power(PowerModes.StatusChange);

        Assert.Empty(heard);
    }

    [Theory]
    [InlineData(SessionSwitchReason.SessionLock, true, AwayReason.ScreenLocked)]
    [InlineData(SessionSwitchReason.SessionUnlock, false, AwayReason.ScreenLocked)]
    [InlineData(SessionSwitchReason.ConsoleDisconnect, true, AwayReason.OtherUser)]
    [InlineData(SessionSwitchReason.RemoteDisconnect, true, AwayReason.OtherUser)]
    [InlineData(SessionSwitchReason.ConsoleConnect, false, AwayReason.OtherUser)]
    [InlineData(SessionSwitchReason.RemoteConnect, false, AwayReason.OtherUser)]
    public void ASessionChangeIsAwayOrBackForItsReason(SessionSwitchReason reason, bool away, AwayReason expected)
    {
        (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        this.windows.Session(reason);

        Assert.Equal([(away ? "away" : "back", expected)], heard);
    }

    /// <summary>Signing in or out of a session says nothing of whether anyone is at the PC.</summary>
    [Theory]
    [InlineData(SessionSwitchReason.SessionLogon)]
    [InlineData(SessionSwitchReason.SessionLogoff)]
    [InlineData(SessionSwitchReason.SessionRemoteControl)]
    public void ASignInOrOutIsNeitherAwayNorBack(SessionSwitchReason reason)
    {
        (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        this.windows.Session(reason);

        Assert.Empty(heard);
        Assert.Null(WindowsAwayEvents.ForSession(reason));
    }

    /// <summary>A PC that sleeps also locks, and wakes to a locked screen: each reason is told on its own, in whatever order.</summary>
    [Fact]
    public void ReasonsThatOverlapAreEachToldOnTheirOwn()
    {
        (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        this.windows.Session(SessionSwitchReason.SessionLock);
        this.windows.Power(PowerModes.Suspend);
        this.windows.Power(PowerModes.Resume);
        this.windows.Session(SessionSwitchReason.SessionUnlock);

        Assert.Equal(
            [("away", AwayReason.ScreenLocked), ("away", AwayReason.Sleep), ("back", AwayReason.Sleep), ("back", AwayReason.ScreenLocked)],
            heard);
    }

    /// <summary>Windows says it on a thread of its own, and the UI wants it on the UI thread.</summary>
    [Fact]
    public async Task WhatWindowsSaysIsPostedToTheContextOfWhoFirstListened()
    {
        QueueContext ui = new();
        WindowsAwayEvents events = new(this.windows);
        List<AwayReason> heard = [];
        SynchronizationContext? before = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(ui);
        try
        {
            events.Away += (_, reason) => heard.Add(reason);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
        }

        await Task.Run(() => this.windows.Power(PowerModes.Suspend));

        Assert.Empty(heard);
        Assert.Equal(1, ui.Queued);

        ui.RunAll();

        Assert.Equal([AwayReason.Sleep], heard);
    }

    [Fact]
    public void WithNoContextToPostToItIsToldWhereWindowsSaysIt()
    {
        SynchronizationContext? before = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            (_, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

            this.windows.Power(PowerModes.Suspend);

            Assert.Single(heard);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
        }
    }

    [Fact]
    public void ANoticeWithNobodyListeningIsNotAnError()
    {
        _ = new WindowsAwayEvents(this.windows);

        this.windows.Power(PowerModes.Suspend);
        this.windows.Session(SessionSwitchReason.SessionLock);
    }

    [Fact]
    public void AListenerThatLeavesIsNotToldAgain()
    {
        (WindowsAwayEvents events, List<(string Kind, AwayReason Reason)> heard) = this.Listening();
        this.windows.Power(PowerModes.Suspend);
        Assert.Single(heard);

        // The listener Listening gave is the only one; taking away one that was never added changes nothing.
        EventHandler<AwayReason> other = (_, _) => { };
        events.Away -= other;
        this.windows.Power(PowerModes.Suspend);
        Assert.Equal(2, heard.Count);

        events.Dispose();
        this.windows.Power(PowerModes.Suspend);
        Assert.Equal(2, heard.Count);
    }

    [Fact]
    public void DisposingStopsListeningToWindows()
    {
        (WindowsAwayEvents events, List<(string Kind, AwayReason Reason)> heard) = this.Listening();

        events.Dispose();
        this.windows.Power(PowerModes.Suspend);
        this.windows.Session(SessionSwitchReason.SessionLock);

        Assert.Empty(heard);
        Assert.False(this.windows.HasListeners);
    }

    /// <summary>The real thing: subscribing to Windows' broadcasts and letting go of them works. Nothing is slept or locked.</summary>
    [Fact]
    public void ListeningToTheRealWindowsAndStoppingWorks()
    {
        using WindowsAwayEvents events = new(new SystemEventsSource());
        events.Away += (_, _) => { };
    }

    private sealed class FakeSystemEvents : ISystemEvents
    {
        public event PowerModeChangedEventHandler? PowerModeChanged;

        public event SessionSwitchEventHandler? SessionSwitch;

        public bool HasListeners => this.PowerModeChanged is not null || this.SessionSwitch is not null;

        public void Power(PowerModes mode) => this.PowerModeChanged?.Invoke(this, new PowerModeChangedEventArgs(mode));

        public void Session(SessionSwitchReason reason) => this.SessionSwitch?.Invoke(this, new SessionSwitchEventArgs(reason));
    }

    /// <summary>A UI thread's context, which runs what is posted to it when asked.</summary>
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> queue = [];

        public int Queued => this.queue.Count;

        public override void Post(SendOrPostCallback d, object? state) => this.queue.Enqueue((d, state));

        public void RunAll()
        {
            while (this.queue.Count > 0)
            {
                (SendOrPostCallback callback, object? state) = this.queue.Dequeue();
                callback(state);
            }
        }
    }
}
