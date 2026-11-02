using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Analytics;
using Microsoft.Win32;

namespace ContextSwitcher.Infrastructure.Windows.Away;

/// <summary>
/// Hears from Windows when nobody can be using the PC, and when that ends:
/// <list type="bullet">
/// <item>sleep and wake - <c>PowerModeChanged</c>, <c>Suspend</c> and <c>Resume</c>;</item>
/// <item>the screen locking and unlocking - <c>SessionSwitch</c>, <c>SessionLock</c> and <c>SessionUnlock</c>;</item>
/// <item>another user's session coming to the front (fast user switching) and leaving, or this session
/// being disconnected from the console or from a remote desktop and connected again -
/// <c>ConsoleDisconnect</c> and <c>RemoteDisconnect</c>, <c>ConsoleConnect</c> and <c>RemoteConnect</c>.</item>
/// </list>
/// Windows reports them on a thread of its own; they are raised on the thread that first subscribed, which
/// is the UI thread - <see cref="IAwayEvents"/> says they arrive there - by posting to its
/// synchronisation context. Reasons overlap as on the Mac - a PC that sleeps also locks - and
/// <see cref="AwayPause"/> counts again only once every one has cleared. A PC with Modern Standby may
/// not report a suspend; its lock is still heard.
/// </summary>
public sealed class WindowsAwayEvents : IAwayEvents, IDisposable
{
    private readonly ISystemEvents source;
    private readonly object gate = new();
    private SynchronizationContext? context;
    private bool contextTaken;
    private EventHandler<AwayReason>? away;
    private EventHandler<AwayReason>? back;

    public WindowsAwayEvents(ISystemEvents source)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
        this.source.PowerModeChanged += this.OnPowerModeChanged;
        this.source.SessionSwitch += this.OnSessionSwitch;
    }

    /// <inheritdoc />
    public event EventHandler<AwayReason>? Away
    {
        add
        {
            this.TakeContext();
            this.away += value;
        }

        remove => this.away -= value;
    }

    /// <inheritdoc />
    public event EventHandler<AwayReason>? Back
    {
        add
        {
            this.TakeContext();
            this.back += value;
        }

        remove => this.back -= value;
    }

    /// <summary>What this reports for <paramref name="reason"/> of a session change: away, back, or null for one that means neither.</summary>
    public static (bool Away, AwayReason Reason)? ForSession(SessionSwitchReason reason) => reason switch
    {
        SessionSwitchReason.SessionLock => (true, AwayReason.ScreenLocked),
        SessionSwitchReason.SessionUnlock => (false, AwayReason.ScreenLocked),
        SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect => (true, AwayReason.OtherUser),
        SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect => (false, AwayReason.OtherUser),
        _ => null
    };

    /// <inheritdoc />
    public void Dispose()
    {
        this.source.PowerModeChanged -= this.OnPowerModeChanged;
        this.source.SessionSwitch -= this.OnSessionSwitch;
    }

    /// <summary>Remembers the context of whoever first subscribes, which is where every later notice goes.</summary>
    private void TakeContext()
    {
        lock (this.gate)
        {
            if (!this.contextTaken)
            {
                this.context = SynchronizationContext.Current;
                this.contextTaken = true;
            }
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                this.Raise(away: true, AwayReason.Sleep);
                break;
            case PowerModes.Resume:
                this.Raise(away: false, AwayReason.Sleep);
                break;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (ForSession(e.Reason) is { } change)
        {
            this.Raise(change.Away, change.Reason);
        }
    }

    private void Raise(bool away, AwayReason reason)
    {
        EventHandler<AwayReason>? handlers = away ? this.away : this.back;
        if (handlers is null)
        {
            return;
        }

        SynchronizationContext? target;
        lock (this.gate)
        {
            target = this.context;
        }

        if (target is null)
        {
            handlers(this, reason);
        }
        else
        {
            target.Post(_ => handlers(this, reason), null);
        }
    }
}
