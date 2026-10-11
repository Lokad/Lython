using System.Diagnostics;
using System.Reflection;
using Lokad.Lython.Benchmarks.Comparison;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonWorkerSupervisorTests
{
    [Theory]
    [InlineData("clear")]
    [InlineData("inherit")]
    public async Task SupervisorTieringIsRemovedOnlyFromExplicitQualificationWorkerLaunches(string mode)
    {
        if (!OperatingSystem.IsLinux()) return; // Qualification is Linux-only.
        using var fixture = new Fixture("environment-parent", mode);
        await using var worker = await fixture.StartAsync();
        var environment = worker.Identity.GetProperty("childEnvironment");
        Assert.Equal(mode == "clear" ? null : "0", environment.GetProperty("tiering").GetString());
        Assert.Equal("1", environment.GetProperty("pgo").GetString());
        await worker.CloseAsync();
        Assert.True(worker.CleanupCompleted);
        await AssertExitedAsync(fixture.RootId);
    }

    private static readonly ComparisonWorkload Case = new("fixture", "control", "control", "test", 1,
        "print('λ😀')\n", "{\"size\":1,\"text\":null}", "λ😀\n");
    private static readonly string CatalogHash = new('a', 64);

    [Theory]
    [InlineData("warm")]
    [InlineData("compile-run")]
    [InlineData("compile")]
    public async Task ValidResponsesAndStructuredArgumentsSurviveCompleteDrainAndExit(string lane)
    {
        using var fixture = new Fixture("normal", "space λ😀", "", "quote\"slash\\", "trailing\\");
        await using var worker = await fixture.StartAsync();
        Assert.Equal(new[] { "space λ😀", "", "quote\"slash\\", "trailing\\" },
            worker.Identity.GetProperty("echoedArguments").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("Equivalent", (await worker.VerifyAsync(Case)).Status);
        var sample = await worker.BatchAsync(Case, lane, 7);
        Assert.Equal(7, sample.CompletedInvocations);
        Assert.Equal(100, sample.ElapsedTicks);
        Assert.Equal(lane == "compile" ? null : Case.ExpectedOutputSha256, sample.ActualOutputSha256);
        await worker.CloseAsync();
        Assert.True(worker.CleanupCompleted);
        await AssertExitedAsync(fixture.RootId);
    }

    [Theory]
    [InlineData("wrong-version")]
    [InlineData("wrong-catalog")]
    [InlineData("wrong-pid")]
    [InlineData("wrong-clock")]
    [InlineData("oversize")]
    [InlineData("truncated")]
    public async Task StartupRejectsInvalidIdentityAndFramingAndReapsTheWorker(string mode)
    {
        using var fixture = new Fixture(mode);
        var failure = await Record.ExceptionAsync(() => fixture.StartAsync());
        Assert.NotNull(failure);
        Assert.IsNotType<TimeoutException>(failure);
        await AssertExitedAsync(fixture.RootId);
    }

    [Theory]
    [InlineData("replay")]
    [InlineData("wrong-case")]
    [InlineData("wrong-source")]
    [InlineData("wrong-count")]
    [InlineData("wrong-output")]
    [InlineData("changed-clock")]
    [InlineData("failure-with-time")]
    public async Task MalformedResultCannotBecomeASample(string mode)
    {
        using var fixture = new Fixture(mode);
        await using var worker = await fixture.StartAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.VerifyAsync(Case));
        Assert.True(worker.CleanupCompleted);
        await AssertExitedAsync(fixture.RootId);
    }

    [Fact]
    public async Task ImpossibleElapsedTimeCannotBecomeASample()
    {
        using var fixture = new Fixture("oversized-time");
        await using var worker = await fixture.StartAsync();
        Assert.Equal("Equivalent", (await worker.VerifyAsync(Case)).Status);
        await Assert.ThrowsAsync<InvalidDataException>(() => worker.BatchAsync(Case, "warm", 1));
        Assert.True(worker.CleanupCompleted);
    }

    [Fact]
    public async Task ValidFailureRemainsUntimedAndCannotAdmitABatch()
    {
        using var fixture = new Fixture("valid-failure");
        await using var worker = await fixture.StartAsync();
        var response = await worker.VerifyAsync(Case);
        Assert.Equal("Failure", response.Status);
        Assert.Null(response.ElapsedTicks);
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.BatchAsync(Case, "warm", 1));
        Assert.True(worker.CleanupCompleted);
    }

    [Fact]
    public async Task StartupWatchdogKillsAndReapsAHungWorker()
    {
        using var fixture = new Fixture("hang-startup");
        await Assert.ThrowsAsync<TimeoutException>(() => fixture.StartAsync(TimeSpan.FromSeconds(3)));
        await AssertExitedAsync(fixture.RootId);
    }

    [Theory]
    [InlineData("hang-request")]
    [InlineData("descendant-hang")]
    [InlineData("orphan-pipes")]
    public async Task RequestWatchdogCleansUpEvenWhenRootHasExitedAndDescendantsHoldPipes(string mode)
    {
        using var fixture = new Fixture(mode);
        await using var worker = await fixture.StartAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => worker.VerifyAsync(Case, deadline: TimeSpan.FromMilliseconds(250)));
        Assert.True(worker.CleanupCompleted);
        await AssertExitedAsync(fixture.RootId);
        if (fixture.ChildId is { } child) await AssertExitedAsync(child);
    }

    [Fact]
    public async Task CallerCancellationStopsOnlyItsOwnedProcessesAndJoinsPumps()
    {
        using var healthyFixture = new Fixture("normal");
        await using var healthy = await healthyFixture.StartAsync();
        using var hungFixture = new Fixture("descendant-hang");
        await using var hung = await hungFixture.StartAsync();
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hung.VerifyAsync(Case, cancel.Token));
        Assert.True(hung.CleanupCompleted);
        await AssertExitedAsync(hungFixture.RootId);
        await AssertExitedAsync(hungFixture.ChildId!.Value);
        Assert.Equal("Equivalent", (await healthy.VerifyAsync(Case)).Status);
        await healthy.CloseAsync();
    }

    [Fact]
    public async Task StderrFloodCannotBlockOrAllocateAnUnboundedCapture()
    {
        using var fixture = new Fixture("stderr-flood");
        ComparisonWorkerClient? worker = null;
        try
        {
            var failure = await Record.ExceptionAsync(async () =>
            {
                worker = await fixture.StartAsync();
                await worker.VerifyAsync(Case);
            });
            Assert.IsType<IOException>(failure);
            Assert.Contains("64 KiB", failure.Message);
        }
        finally { if (worker is not null) await worker.DisposeAsync(); }
        await AssertExitedAsync(fixture.RootId);
    }

    [Theory]
    [InlineData("extra-output")]
    [InlineData("nonzero-exit")]
    [InlineData("hang-shutdown")]
    public async Task ShutdownRequiresACompleteCleanDrainAndZeroExit(string mode)
    {
        using var fixture = new Fixture(mode);
        await using var worker = await fixture.StartAsync();
        Assert.Equal("Equivalent", (await worker.VerifyAsync(Case)).Status);
        var failure = await Record.ExceptionAsync(() => worker.CloseAsync());
        Assert.NotNull(failure);
        Assert.True(worker.CleanupCompleted);
        await AssertExitedAsync(fixture.RootId);
    }

    [Fact]
    public async Task ExitAfterOpeningProcStatDoesNotEscapeTheCleanupAssertion()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture("normal");
        await using var worker = await fixture.StartAsync();
        var reads = 0;
        await AssertExitedAsync(fixture.RootId, async id =>
        {
            reads++;
            using var reader = File.OpenText($"/proc/{id}/stat");
            await worker.CloseAsync();
            // An open proc handle can report ESRCH when its process is reaped.
            var error = Assert.Throws<IOException>(() => reader.ReadToEnd());
            throw error;
        });
        Assert.Equal(1, reads);
        Assert.True(worker.CleanupCompleted);
    }

    [Fact]
    public async Task ProcStatReadFailureDoesNotPassForALiveWorker()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture("normal");
        await using var worker = await fixture.StartAsync();
        var read = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = AssertExitedAsync(fixture.RootId, _ =>
        {
            read.TrySetResult();
            return Task.FromException<string>(new IOException("Transient proc read failure"));
        });
        try
        {
            await read.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal("Equivalent", (await worker.VerifyAsync(Case)).Status);
        }
        finally
        {
            await worker.CloseAsync();
            if (pending.IsFaulted) _ = pending.Exception;
        }
        await pending;
        Assert.True(worker.CleanupCompleted);
    }

    private static async Task AssertExitedAsync(int id, Func<int, Task<string>>? readStat = null)
    {
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                if (process.HasExited) return;
                // An orphan killed in our Linux session may briefly await PID1
                // reaping. A zombie has exited and cannot hold any pipe open.
                if (OperatingSystem.IsLinux())
                {
                    var stat = readStat is null ? File.ReadAllText($"/proc/{id}/stat") : await readStat(id);
                    if (stat.Split(')')[1].TrimStart().StartsWith('Z')) return;
                }
            }
            catch (ArgumentException) { return; }
            catch (FileNotFoundException) { return; }
            // Linux can reap a process between opening and reading its stat
            // file, producing ESRCH as IOException. Retry the exit check;
            // a read failure alone does not establish that a worker exited.
            catch (IOException) when (OperatingSystem.IsLinux()) { }
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(5), $"Owned process {id} is still running.");
            await Task.Delay(10);
        }
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "lython supervisor λ😀 " + Guid.NewGuid().ToString("N"));
        private readonly WorkerLaunch _launch;
        private readonly string _catalogHash;
        internal WorkerLaunch Launch => _launch;
        public int RootId => int.Parse(File.ReadAllText(Path.Combine(_directory, "root")));
        public int? ChildId => File.Exists(Path.Combine(_directory, "child"))
            ? int.Parse(File.ReadAllText(Path.Combine(_directory, "child"))) : null;
        public Fixture(string mode, params string[] arguments) : this(mode, CatalogHash, (IEnumerable<string>)arguments) { }
        internal Fixture(string mode, string catalogHash, IEnumerable<string> arguments)
        {
            _catalogHash = catalogHash;
            Directory.CreateDirectory(_directory);
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
            if (root is null) throw new InvalidOperationException("Checkout not found.");
            var configuration = typeof(Fixture).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var dll = Path.Combine(root.FullName, "tests", "Lokad.Lython.WorkerFixture", "bin", configuration, "net10.0", "Lokad.Lython.WorkerFixture.dll");
            Assert.True(File.Exists(dll), "Matching fixture must be built as the test project's dependency.");
            var hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(host)) host = Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator)
                .Select(path => Path.Combine(path, hostName)).First(File.Exists);
            _launch = new WorkerLaunch(Path.GetFullPath(host), new[] { dll, mode, catalogHash, _directory }.Concat(arguments).ToArray(), root.FullName);
        }
        public Task<ComparisonWorkerClient> StartAsync(TimeSpan? deadline = null) =>
            ComparisonWorkerClient.StartAsync(_launch, "Fixture", _catalogHash, startupDeadline: deadline);
        public void Dispose()
        {
            File.Delete(Path.Combine(_directory, "root"));
            File.Delete(Path.Combine(_directory, "child"));
            Directory.Delete(_directory);
        }
    }
}
