using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Lokad.Lython.Tests.Harness;

// Test-only consolidated subprocess runner (T01): both output streams drain
// concurrently under a single deadline, so a child that fills the pipe nobody
// reads cannot deadlock the test host. Launch failure, timeout and crash stay
// distinguishable: a missing executable throws InvalidOperationException, an
// expired deadline throws TimeoutException (after killing the owned child
// process tree), and any other completion returns its exit code with both
// captured streams for the caller to assert on.
internal static class SubprocessProbeRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public sealed record RunResult(int ExitCode, string StandardOutput, string StandardError);

    public static RunResult Run(
        string executable,
        IEnumerable<string> arguments,
        string? standardInput = null,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        if (standardInput is not null)
        {
            // The encoding property requires a redirected pipe.
            startInfo.StandardInputEncoding = new UTF8Encoding(false);
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"Could not start '{executable}': {exception.Message}", exception);
        }

        using (process)
        {
            // Start both drains and construct the deadline BEFORE delivering
            // stdin: a child that never reads a pipe-sized input must trip the
            // deadline instead of wedging a synchronous write issued first,
            // and a child that writes heavily before reading must find a
            // reader already draining. Write/close/wait/drain share the one
            // bounded operation below; expiry reaps the owned child tree.
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            var budget = timeout ?? DefaultTimeout;
            var deadline = DateTime.UtcNow + budget;
            var stdinTask = standardInput is not null
                ? WriteAndCloseStandardInputAsync(process, standardInput)
                : null;

            if (!process.WaitForExit(RemainingMs(deadline)))
            {
                KillProcessTree(process);
                throw new TimeoutException(
                    $"'{executable} {string.Join(" ", arguments)}' did not complete within {budget.TotalSeconds} seconds.");
            }

            Task[] drainTasks = stdinTask is null
                ? [stdoutTask, stderrTask]
                : [stdoutTask, stderrTask, stdinTask];
            if (!Task.WaitAll(drainTasks, RemainingMs(deadline)))
            {
                KillProcessTree(process);
                throw new TimeoutException(
                    $"'{executable} {string.Join(" ", arguments)}' output drain did not complete within {budget.TotalSeconds} seconds.");
            }

            process.WaitForExit();
            return new RunResult(process.ExitCode, stdoutTask.Result, stderrTask.Result);
        }
    }

    // Locates the probe built from this checkout, preferring the configuration
    // the tests themselves were built with: a Debug test run must exercise the
    // Debug probe (with its pairing asserts), not whichever was built first.
    public static string FindProbeDll()
    {
        var matching = typeof(SubprocessProbeRunner).Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
        var ordered = matching == "Release"
            ? new[] { "Release", "Debug" }
            : new[] { "Debug", "Release" };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var configuration in ordered)
            {
                var candidate = Path.Combine(
                    directory.FullName, "tools", "LythonProbe", "bin", configuration, "net10.0", "LythonProbe.dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "LythonProbe.dll was not found; build it first, e.g. `dotnet build tools/LythonProbe/LythonProbe.csproj -c "
            + (matching ?? "Release") + "`.");
    }

    // Asynchronous stdin delivery under the caller's deadline. A child may
    // exit (or be reaped on timeout) without consuming all input, so a broken
    // pipe surfaces here as a quiet completion: the exit code and captured
    // streams carry the outcome instead. The task never faults, keeping the
    // bounded drain wait above a pure completion wait.
    private static async Task WriteAndCloseStandardInputAsync(Process process, string standardInput)
    {
        try
        {
            await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
        {
        }
        finally
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
            }
        }
    }

    private static int RemainingMs(DateTime deadline)
        => (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }

        process.WaitForExit(5000);
    }
}
