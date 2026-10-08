using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Lokad.Lython.Benchmarks.Comparison;

internal sealed record WorkerLaunch(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory);

// Trusted benchmark workers only. Ownership is established before worker code
// runs, and survives the root exiting while a descendant still holds a pipe.
// No name-based enumeration or machine-wide process termination is used.
internal sealed class OwnedWorkerProcess : IAsyncDisposable
{
    private Process? _linuxRoot;
    private NativeHandle? _windowsJob;
    private NativeHandle? _windowsRoot;
    private int _linuxGroup;
    private string? _bootstrapDirectory;
    private bool _disposed;

    public int ProcessId { get; private set; }
    public Stream Input { get; private set; } = Stream.Null;
    public Stream Output { get; private set; } = Stream.Null;
    public Stream Error { get; private set; } = Stream.Null;
    public Task<int> Exit { get; private set; } = Task.FromResult(-1);

    public static async Task<OwnedWorkerProcess> StartAsync(WorkerLaunch launch, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(launch.Executable) || !File.Exists(launch.Executable)
            || !Path.IsPathFullyQualified(launch.WorkingDirectory) || !Directory.Exists(launch.WorkingDirectory)
            || launch.Arguments.Count > 64 || launch.Arguments.Any(a => a.Contains('\0')))
            throw new ArgumentException("Use an existing explicit executable, working directory and bounded structured arguments.");
        cancellationToken.ThrowIfCancellationRequested();
        var owned = new OwnedWorkerProcess();
        try
        {
            if (OperatingSystem.IsWindows()) owned.StartWindows(launch);
            else if (OperatingSystem.IsLinux()) await owned.StartLinuxAsync(launch, cancellationToken).ConfigureAwait(false);
            else throw new PlatformNotSupportedException("Comparison process ownership requires Windows jobs or Linux sessions.");
            cancellationToken.ThrowIfCancellationRequested();
            return owned;
        }
        catch
        {
            await owned.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    [SupportedOSPlatform("linux")]
    private async Task StartLinuxAsync(WorkerLaunch launch, CancellationToken cancellationToken)
    {
        _bootstrapDirectory = Path.Combine(Path.GetTempPath(), "lython-worker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_bootstrapDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var pidPath = Path.Combine(_bootstrapDirectory, "pid");
        var gatePath = Path.Combine(_bootstrapDirectory, "go");
        var start = new ProcessStartInfo("/usr/bin/setsid")
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = launch.WorkingDirectory,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        // --fork --wait keeps a parent we can reap. The gated session leader
        // publishes its PID before exec; positional parameters carry all data.
        foreach (var argument in new[] { "--fork", "--wait", "/bin/sh", "-c",
            "umask 077; printf '%s\\n' \"$$\" > \"$1\"; gate=$2; shift 2; i=0; " +
            "while [ ! -f \"$gate\" ]; do i=$((i+1)); [ \"$i\" -le 500 ] || exit 124; sleep .01; done; exec \"$@\"",
            "lython-benchmark-bootstrap", pidPath, gatePath, launch.Executable }) start.ArgumentList.Add(argument);
        foreach (var argument in launch.Arguments) start.ArgumentList.Add(argument);
        _linuxRoot = Process.Start(start) ?? throw new IOException("Worker launcher did not start.");
        Input = _linuxRoot.StandardInput.BaseStream;
        Output = _linuxRoot.StandardOutput.BaseStream;
        Error = _linuxRoot.StandardError.BaseStream;
        Exit = WaitLinuxAsync(_linuxRoot);
        var ownershipDeadline = Stopwatch.StartNew();
        while (!TryOwnLinuxGroup(pidPath))
        {
            if (Exit.IsCompleted) throw new IOException("Worker bootstrap exited before establishing ownership.");
            if (ownershipDeadline.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("Worker bootstrap did not establish ownership within five seconds.");
            // Finish ownership even if cancellation arrives, then the caller's
            // catch can kill the known group before the gate is released.
            await Task.Delay(10).ConfigureAwait(false);
        }
        cancellationToken.ThrowIfCancellationRequested();
        File.WriteAllText(gatePath, "go");
        ProcessId = _linuxGroup;
    }

    private bool TryOwnLinuxGroup(string pidPath)
    {
        if (_linuxGroup != 0) return true;
        if (!File.Exists(pidPath) || !int.TryParse(File.ReadAllText(pidPath).Trim(), out var pid) || pid <= 1) return false;
        if (Native.GetProcessGroup(pid) != pid || Native.GetSession(pid) != pid) return false;
        var parentLine = File.ReadLines($"/proc/{pid}/status").FirstOrDefault(line => line.StartsWith("PPid:", StringComparison.Ordinal));
        if (parentLine is null || !int.TryParse(parentLine.AsSpan(5).Trim(), out var parent) || parent != _linuxRoot!.Id)
            throw new IOException("Worker bootstrap is not a child of the owned launcher.");
        _linuxGroup = pid;
        return true;
    }

    private static async Task<int> WaitLinuxAsync(Process process)
    {
        await process.WaitForExitAsync().ConfigureAwait(false);
        return process.ExitCode;
    }

    private void StartWindows(WorkerLaunch launch)
    {
        _windowsJob = Native.CreateJobObject(IntPtr.Zero, null);
        Check(!_windowsJob.IsInvalid);
        var limits = new JobExtendedLimits { Basic = new JobBasicLimits { LimitFlags = 0x2000 } }; // KILL_ON_JOB_CLOSE
        Check(Native.SetInformationJobObject(_windowsJob, 9, ref limits, (uint)Marshal.SizeOf<JobExtendedLimits>()));
        var input = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        Input = input;
        var output = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        Output = output;
        var error = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        Error = error;
        using var attributes = new ProcessAttributes(2);
        attributes.Add(0x00020002, input.ClientSafePipeHandle.DangerousGetHandle(),
            output.ClientSafePipeHandle.DangerousGetHandle(), error.ClientSafePipeHandle.DangerousGetHandle()); // HANDLE_LIST
        attributes.Add(0x0002000D, _windowsJob.DangerousGetHandle()); // JOB_LIST: atomic association at creation
        var startup = new StartupInfoEx
        {
            StartupInfo = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 0x100, // STARTF_USESTDHANDLES
                Input = input.ClientSafePipeHandle.DangerousGetHandle(),
                Output = output.ClientSafePipeHandle.DangerousGetHandle(), Error = error.ClientSafePipeHandle.DangerousGetHandle(),
            },
            Attributes = attributes.Pointer,
        };
        var command = new StringBuilder(string.Join(" ", new[] { launch.Executable }.Concat(launch.Arguments).Select(QuoteWindows)));
        if (command.Length >= 32767) throw new ArgumentException("Worker command line is too long.");
        Check(Native.CreateProcess(launch.Executable, command, IntPtr.Zero, IntPtr.Zero, true,
            0x08080000, IntPtr.Zero, launch.WorkingDirectory, ref startup, out var created)); // NO_WINDOW | EXTENDED_STARTUPINFO
        _windowsRoot = new NativeHandle(created.Process);
        using var thread = new NativeHandle(created.Thread);
        ProcessId = checked((int)created.ProcessId);
        // Parent copies of child handles must close, including when the root
        // exits before the supervisor gets its first response.
        input.DisposeLocalCopyOfClientHandle();
        output.DisposeLocalCopyOfClientHandle();
        error.DisposeLocalCopyOfClientHandle();
        var root = _windowsRoot;
        Exit = Task.Run(() =>
        {
            if (Native.WaitForSingleObject(root, uint.MaxValue) != 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            Check(Native.GetExitCodeProcess(root, out var code));
            return unchecked((int)code);
        });
    }

    private static string QuoteWindows(string argument)
    {
        var quoted = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            quoted.Append(character);
            slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }

    public void Kill()
    {
        if (_windowsJob is { IsInvalid: false, IsClosed: false })
            Check(Native.TerminateJobObject(_windowsJob, 137));
        if (_linuxRoot is not null)
        {
            if (_linuxGroup == 0 && _bootstrapDirectory is not null)
                TryOwnLinuxGroup(Path.Combine(_bootstrapDirectory, "pid"));
            if (_linuxGroup != 0 && Native.Kill(-_linuxGroup, 9) != 0 && Marshal.GetLastPInvokeError() != 3) // ESRCH
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            if (_linuxGroup == 0 && !_linuxRoot.HasExited) _linuxRoot.Kill();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            Kill(); // Also kills descendants after a normal root exit.
            await Exit.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (_windowsJob is not null)
            {
                var deadline = Stopwatch.StartNew();
                while (true)
                {
                    Check(Native.QueryInformationJobObject(_windowsJob, 1, out var counts,
                        (uint)Marshal.SizeOf<JobAccounting>(), IntPtr.Zero));
                    if (counts.ActiveProcesses == 0) break;
                    if (deadline.Elapsed > TimeSpan.FromSeconds(5)) throw new IOException("Owned Windows job did not become empty.");
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Input.Dispose(); Output.Dispose(); Error.Dispose();
            _windowsJob?.Dispose(); _windowsRoot?.Dispose(); _linuxRoot?.Dispose();
            if (_bootstrapDirectory is not null)
            {
                // This directory was created by this instance; remove only its
                // two known files, never recursively delete a computed path.
                File.Delete(Path.Combine(_bootstrapDirectory, "pid"));
                File.Delete(Path.Combine(_bootstrapDirectory, "go"));
                Directory.Delete(_bootstrapDirectory);
            }
        }
    }

    private static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private sealed class NativeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public NativeHandle() : base(true) { }
        public NativeHandle(IntPtr handle) : base(true) => SetHandle(handle);
        protected override bool ReleaseHandle() => Native.CloseHandle(handle);
    }

    private sealed class ProcessAttributes : IDisposable
    {
        private readonly List<IntPtr> _values = [];
        public IntPtr Pointer { get; }
        public ProcessAttributes(int count)
        {
            nuint bytes = 0;
            Native.InitializeProcThreadAttributeList(IntPtr.Zero, count, 0, ref bytes);
            Pointer = Marshal.AllocHGlobal(checked((nint)bytes));
            if (!Native.InitializeProcThreadAttributeList(Pointer, count, 0, ref bytes))
            {
                var failure = new Win32Exception(Marshal.GetLastWin32Error());
                Marshal.FreeHGlobal(Pointer);
                throw failure;
            }
        }
        public void Add(nuint attribute, params IntPtr[] handles)
        {
            var bytes = checked(handles.Length * IntPtr.Size);
            var value = Marshal.AllocHGlobal(bytes);
            _values.Add(value);
            Marshal.Copy(handles, 0, value, handles.Length);
            Check(Native.UpdateProcThreadAttribute(Pointer, 0, attribute, value, (nuint)bytes, IntPtr.Zero, IntPtr.Zero));
        }
        public void Dispose()
        {
            Native.DeleteProcThreadAttributeList(Pointer);
            foreach (var value in _values) Marshal.FreeHGlobal(value);
            Marshal.FreeHGlobal(Pointer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public ushort ShowWindow, ReservedSize;
        public IntPtr ReservedBytes, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobBasicLimits
    {
        public long ProcessUserTime, JobUserTime;
        public uint LimitFlags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass, SchedulingClass;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobExtendedLimits
    {
        public JobBasicLimits Basic;
        public IoCounters Io;
        public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct JobAccounting
    {
        public long UserTime, KernelTime, PeriodUserTime, PeriodKernelTime;
        public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
    }

    private static class Native
    {
        [DllImport("libc", EntryPoint = "getpgid", SetLastError = true)] internal static extern int GetProcessGroup(int pid);
        [DllImport("libc", EntryPoint = "getsid", SetLastError = true)] internal static extern int GetSession(int pid);
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)] internal static extern int Kill(int pid, int signal);
        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern NativeHandle CreateJobObject(IntPtr security, string? name);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetInformationJobObject(NativeHandle job, int kind, ref JobExtendedLimits information, uint bytes);
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool QueryInformationJobObject(NativeHandle job, int kind, out JobAccounting information, uint bytes, IntPtr returned);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateJobObject(NativeHandle job, uint exitCode);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern uint WaitForSingleObject(NativeHandle handle, uint milliseconds);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetExitCodeProcess(NativeHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool InitializeProcThreadAttributeList(IntPtr attributes, int count, uint flags, ref nuint bytes);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool UpdateProcThreadAttribute(IntPtr attributes, uint flags, nuint attribute, IntPtr value, nuint bytes, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(IntPtr attributes);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern bool CreateProcess(string application, StringBuilder command, IntPtr processSecurity, IntPtr threadSecurity,
            bool inherit, uint flags, IntPtr environment, string directory, ref StartupInfoEx startup, out ProcessInformation created);
    }
}
