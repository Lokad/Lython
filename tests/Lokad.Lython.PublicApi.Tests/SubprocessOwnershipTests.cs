using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessOwnershipTests
{
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
