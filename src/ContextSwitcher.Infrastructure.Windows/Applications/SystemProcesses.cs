using System.Runtime.InteropServices;
using System.Text;
using ContextSwitcher.Core.Abstractions;
using ContextSwitcher.Core.Logging;
using Microsoft.Win32.SafeHandles;

namespace ContextSwitcher.Infrastructure.Windows.Applications;

/// <summary>
/// The processes of this PC, from a snapshot Windows takes (<c>CreateToolhelp32Snapshot</c>), each
/// asked for its executable and package with the least access Windows gives (<c>PROCESS_QUERY_LIMITED_INFORMATION</c>),
/// which any process of this user allows. A process that won't say has no path, and so is no app's.
/// </summary>
public sealed class SystemProcesses : ISystemProcesses
{
    private const uint SnapshotProcesses = 0x2;
    private const uint QueryLimitedInformation = 0x1000;
    private const uint ProcessTerminate = 0x0001;
    private const uint ExitCodeWhenEnded = 1;
    private const int PathCharacters = 32768;
    private const int PackageFamilyCharacters = 66;

    private readonly ILogger logger;

    public SystemProcesses(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;
    }

    /// <inheritdoc />
    public int CurrentProcessId => Environment.ProcessId;

    /// <inheritdoc />
    public IReadOnlyList<ProcessInfo>? List()
    {
        using SafeFileHandle snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
        if (snapshot.IsInvalid)
        {
            this.logger.Warn("App", "ProcessesNotListed", $"Windows wouldn't list the processes running (error {Marshal.GetLastPInvokeError()}).");
            return null;
        }

        List<ProcessInfo> processes = [];
        ProcessEntry entry = new() { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
        for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
        {
            int pid = (int)entry.Pid;
            (string? image, string? family, DateTime? started) = Describe(pid);
            processes.Add(new ProcessInfo(pid, (int)entry.ParentPid, image, family, started));
        }

        return processes;
    }

    /// <inheritdoc />
    public bool Terminate(int pid)
    {
        using SafeProcessHandle process = OpenProcess(ProcessTerminate, false, pid);
        return !process.IsInvalid && TerminateProcess(process, ExitCodeWhenEnded);
    }

    private static (string? Image, string? Family, DateTime? Started) Describe(int pid)
    {
        using SafeProcessHandle process = OpenProcess(QueryLimitedInformation, false, pid);
        if (process.IsInvalid)
        {
            return (null, null, null);
        }

        StringBuilder path = new(PathCharacters);
        uint pathLength = PathCharacters;
        string? image = QueryFullProcessImageNameW(process, 0, path, ref pathLength) ? path.ToString(0, (int)pathLength) : null;

        StringBuilder family = new(PackageFamilyCharacters);
        uint familyLength = PackageFamilyCharacters;
        string? package = GetPackageFamilyName(process, ref familyLength, family) == 0 && familyLength > 1
            ? family.ToString(0, (int)familyLength - 1)
            : null;

        DateTime? started = GetProcessTimes(process, out long created, out _, out _, out _) && created > 0
            ? DateTime.FromFileTimeUtc(created)
            : null;
        return (image, package, started);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint Pid;
        public nuint DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentPid;
        public int PriorityClassBase;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(SafeFileHandle snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(SafeFileHandle snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder path, ref uint length);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFamilyName(SafeProcessHandle process, ref uint length, StringBuilder family);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out long kernel, out long user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
