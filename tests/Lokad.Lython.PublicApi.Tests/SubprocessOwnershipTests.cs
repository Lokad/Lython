using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessOwnershipTests
{
    [Fact]
    public async Task ReturnedCaptureSurvivesHostCompletionDisposalAndSequentialReuse()
    {
        var budget = new LythonMemoryBudget(262144);
        LythonSubprocessResult? captured = null;
        var host = new MockLythonHost();
        host.UseSubprocessHandler(async (request, token) =>
        {
            using var acquisition = request.MemoryBudget!.CreateScope();
            Assert.True(acquisition.TryReserve(4096 + 128, out _));
            var bytes = new byte[4096];
            Array.Fill(bytes, (byte)'x');
            return captured = await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0, bytes,
                ReadOnlyMemory<byte>.Empty, static (_, _) => ValueTask.CompletedTask, static (_, _) => ValueTask.CompletedTask, token);
        });
        using (var script = new LythonEngine().Compile("import subprocess\nreturn subprocess.run(['produce'],capture_output=True).stdout", budget))
        {
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using (var result = await script.RunAsync(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var bytes = Assert.IsType<byte[]>(result.ReturnValue);
                    Assert.Equal(4096, bytes.Length);
                    Assert.All(bytes, value => Assert.Equal((byte)'x', value));
                    Assert.Throws<ObjectDisposedException>(() => captured!.StandardOutputUtf8);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedProducerPipeKeepsItsCompletionOwnedUntilTheActualConsumerFinishes(bool refuse)
    {
        var budget = new LythonMemoryBudget(262144);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        LythonSubprocessResult? producer = null;
        var host = new MockLythonHost();
        host.UseSubprocessHandler(async (request, token) =>
        {
            if (request.Args[0] == "produce")
            {
                using var acquisition = request.MemoryBudget!.CreateScope();
                Assert.True(acquisition.TryReserve(4096 + 128, out _));
                var bytes = new byte[4096];
                Array.Fill(bytes, (byte)'x');
                return producer = await LythonSubprocessCompletion.CompleteBufferedAsync(request, 0, bytes,
                    ReadOnlyMemory<byte>.Empty, UnexpectedWrite, UnexpectedWrite, token);
            }

            entered.TrySetResult();
            await release.Task; // Cancellation does not end this active input borrow.
            Assert.Equal(4096, request.StandardInputUtf8.Length);
            Assert.Equal((byte)'x', request.StandardInputUtf8.Span[^1]);
            Assert.Equal(4096, producer!.StandardOutputUtf8.Length);
            if (refuse)
            {
                Assert.False(request.MemoryBudget!.TryReserve(1_000_000, out _));
                throw new LythonHostException(LythonHostFailureKind.MemoryAdmission, "consumer admission refused", requestedBytes: 1_000_000);
            }
            throw new OperationCanceledException(token);
        });
        using var script = new LythonEngine().Compile("""
            import subprocess
            up=subprocess.Popen(['produce'],stdout=subprocess.PIPE)
            down=subprocess.Popen(['consume'],stdin=up.stdout,stdout=subprocess.PIPE)
            up.stdout.close()
            return down.communicate()[0]
            """);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
        LythonFailureReason? reason = null;
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.NotNull(producer);
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes >= 4096);
            Assert.Equal(4096, producer.StandardOutputUtf8.Length);
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            reason = result.FailureAttribution?.Reason;
        }
        Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled, reason);
        Assert.Throws<ObjectDisposedException>(() => producer!.StandardOutputUtf8);
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value, out var reused));
        reused.Dispose();

        static ValueTask UnexpectedWrite(ReadOnlyMemory<byte> _, CancellationToken __) => throw new InvalidOperationException("Unexpected inherited output.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownRequestStorageRefusesBeforeDispatch(bool asynchronous)
    {
        foreach (var call in new[] { "subprocess.run([arg])", "subprocess.run(['tool'],env={'X':arg})", "subprocess.Popen([arg])" })
        {
            using var script = new LythonEngine().Compile("import subprocess\n" + call);
            var budget = new LythonMemoryBudget(100000);
            var host = new MockLythonHost(); host.EnableSubprocess();
            using (var result = await Run(script, host, budget, asynchronous, new string('x', 65536)))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Empty(host.SubprocessRequests);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedPreparationCannotBeSuppressed(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import subprocess
            def execute():
                try:subprocess.run([arg])
                finally:return 'forbidden'
            try:execute()
            except MemoryError:print('caught')
            print('resumed')
            """);
        var budget = new LythonMemoryBudget(100000);
        var host = new MockLythonHost(); host.EnableSubprocess();
        using (var result = await Run(script, host, budget, asynchronous, new string('x', 65536)))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Empty(host.SubprocessRequests);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalArgsKeepTheirIdentityAndNativeRequestsKeepTheirSnapshot(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import subprocess
            args=['tool']
            completed=subprocess.run(args)
            pending=subprocess.Popen(args)
            print(completed.args is args,pending.args is args)
            args.append('later')
            print(completed.args,pending.args,pending.wait())
            """);
        var budget = new LythonMemoryBudget(262144);
        for (var i = 0; i < 2; i++)
        {
            var host = new MockLythonHost(); host.EnableSubprocess(); host.SeedSubprocessResult(["tool"], 0);
            using (var result = await Run(script, host, budget, asynchronous))
            {
                Assert.True(result.Success, Describe(result));
                Assert.Equal("True True\n['tool', 'later'] ['tool', 'later'] 0\n", result.StandardOutput);
                Assert.All(host.SubprocessRequests, request => Assert.Equal(new[] { "tool" }, request.Args));
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CancellationKeepsRequestBorrowFundedUntilHostCleanupFinishes()
    {
        var budget = new LythonMemoryBudget(262144);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        long retained = 0;
        var host = new MockLythonHost();
        host.UseSubprocessHandler(async (request, token) =>
        {
            retained = budget.ReservedBytes;
            entered.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) { cancelling.SetResult(); await cleanup.Task; }
            Assert.Equal(16384, request.Args[1].Length);
            throw new OperationCanceledException(token);
        });
        using var script = new LythonEngine().Compile("import subprocess\nsubprocess.run(['hold',arg])");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["arg"] = new string('x', 16384) } }, cancellation.Token);
        LythonFailureReason? reason = null;
        try
        {
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(retained >= 3L * 16384);
            cancellation.Cancel();
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(retained, budget.ReservedBytes);
        }
        finally
        {
            cancellation.Cancel(); cleanup.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            reason = result.FailureAttribution?.Reason;
        }
        Assert.Equal(LythonFailureReason.Cancelled, reason);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandLineResultIsFundedAndBudgetCanBeReused(bool asynchronous)
    {
        using var large = new LythonEngine().Compile("import subprocess\nvalue=subprocess.list2cmdline([arg])\nreturn len(value)");
        using var small = new LythonEngine().Compile("import subprocess\nreturn subprocess.list2cmdline(['small'])");
        var budget = new LythonMemoryBudget(100000);
        var host = new MockLythonHost(); host.EnableSubprocess();
        using (var result = await Run(large, host, budget, asynchronous, new string('x', 65536)))
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Equal(0, budget.ReservedBytes);
        using (var result = await Run(small, host, budget, asynchronous))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal("small", result.ReturnValue);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("a b", "\"a b\"")]
    [InlineData("a\"b", "a\\\"b")]
    [InlineData("x\tz", "\"x\tz\"")]
    [InlineData("a\nb", "a\nb")]
    [InlineData("x y\\", "\"x y\\\\\"")]
    [InlineData("snow☃", "snow☃")]
    public async Task CommandLineQuotingFollowsPython(string value, string expected)
    {
        using var script = new LythonEngine().Compile("import subprocess\nreturn subprocess.list2cmdline([arg])");
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost(); host.EnableSubprocess();
            using var result = await Run(script, host, new LythonMemoryBudget(), asynchronous, value);
            Assert.True(result.Success, Describe(result));
            Assert.Equal(expected, result.ReturnValue);
        }
    }

    private static Task<LythonExecutionResult> Run(LythonCompiledScript script, ILythonHost host,
        LythonMemoryBudget budget, bool asynchronous, string? argument = null)
    {
        var options = new LythonRunOptions { MemoryBudget = budget,
            Globals = argument is null ? null : new Dictionary<string, object?> { ["arg"] = argument } };
        return asynchronous ? script.RunAsync(host, options) : Task.FromResult(script.Run(host, options));
    }
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join("; ", result.Diagnostics.Select(x => x.Message));
}
