using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;

namespace ContextSwitcher.Infrastructure.Windows;

/// <summary>
/// One running copy on Windows, which otherwise starts as many as are launched, each with a tray icon
/// of its own. The first copy holds a named mutex and listens on a named pipe; a later one finds the
/// mutex taken, sends its arguments down the pipe - so the running copy shows its window - and ends.
///
/// One copy for each folder of settings: two copies writing the same files would fight over them,
/// while a development build keeps a folder of its own and so runs beside an installed copy. The names
/// are this user's too, in this session, so another person signed in to the same PC runs their own.
/// </summary>
public sealed class WindowsSingleInstance : ISingleInstance, IDisposable
{
    /// <summary>AllowSetForegroundWindow's word for any process.</summary>
    private const int AnyProcess = -1;

    /// <summary>How long a new copy waits for the running one to take its message.</summary>
    private static readonly TimeSpan HandOverTimeout = TimeSpan.FromSeconds(3);

    private readonly string name;
    private readonly ILogger logger;
    private readonly CancellationTokenSource stopListening = new();
    private Mutex? mutex;

    /// <param name="settingsFolder">Where this copy keeps everything; one copy runs for each.</param>
    /// <param name="logger">Says when a later copy couldn't reach the running one, or wasn't understood.</param>
    public WindowsSingleInstance(string settingsFolder, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFolder);
        ArgumentNullException.ThrowIfNull(logger);

        using Process current = Process.GetCurrentProcess();
        this.name = NameFor(settingsFolder, Environment.UserName, current.SessionId);
        this.logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<IReadOnlyList<string>>? Reopened;

    /// <summary>
    /// The mutex's and the pipe's name: the folder, which can't be part of a name as it is - it has
    /// backslashes - as a short hash of its full path, whatever its case, and the user and session.
    /// </summary>
    public static string NameFor(string settingsFolder, string user, int session)
    {
        string folder = Path.GetFullPath(settingsFolder).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)))[..16];
        return $"ContextSwitcher-{hash}-{user}-{session}";
    }

    /// <inheritdoc />
    public bool TryClaim(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        this.mutex = new Mutex(initiallyOwned: true, $@"Local\{this.name}", out bool createdNew);
        if (createdNew)
        {
            return true;
        }

        this.mutex.Dispose();
        this.mutex = null;
        this.HandOver(arguments);
        return false;
    }

    /// <inheritdoc />
    public void Listen() => _ = Task.Run(() => this.ListenAsync(this.stopListening.Token));

    /// <inheritdoc />
    public void Dispose()
    {
        this.stopListening.Cancel();
        this.stopListening.Dispose();
        this.mutex?.Dispose();
        this.mutex = null;
    }

    /// <summary>Tells the running copy it was opened again, with these arguments, a line each.</summary>
    private void HandOver(IReadOnlyList<string> arguments)
    {
        // Windows lets only the app in front bring a window forward. This copy was just opened, so it
        // is; it lets the running one take its place there, to show its window in front.
        AllowSetForegroundWindow(AnyProcess);

        try
        {
            using NamedPipeClientStream pipe = new(".", this.name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(HandOverTimeout);
            using StreamWriter writer = new(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            foreach (string argument in arguments)
            {
                writer.WriteLine(argument);
            }
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // The running copy is starting or ending and not listening: this copy ends all the same,
            // and opening the app again reaches it.
            this.logger.Warn("App", "HandOverFailed", "The app is already running, and couldn't be asked to show its window.", ex);
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using NamedPipeServerStream pipe = new(
                    this.name, PipeDirection.In, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using StreamReader reader = new(pipe, Encoding.UTF8);
                List<string> arguments = [];
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    arguments.Add(line);
                }

                this.Reopened?.Invoke(this, arguments);
            }
            catch (OperationCanceledException)
            {
                // Not logged: the app is ending, and stopped listening on purpose.
                return;
            }
            catch (IOException ex)
            {
                // A copy that went before it finished writing: the next one is heard as usual.
                this.logger.Warn("App", "ReopenNotHeard", "Another copy of the app was opened, and what it said couldn't be read.", ex);
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
