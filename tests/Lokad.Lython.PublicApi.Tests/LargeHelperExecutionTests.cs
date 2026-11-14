using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LargeHelperExecutionTests
{
    private static string Locals(string name, string tail, string parameter = "seed")
        => "def " + name + "(" + parameter + "):\n" + string.Concat(Enumerable.Range(0, 20)
            .Select(i => $"    a{i}=seed+{i}\n")) + tail;

    private static readonly string Sum = string.Join("+", Enumerable.Range(0, 20).Select(i => "a" + i));

    [Fact]
    public async Task LargeLocalsKeepValuesAndReturnAliases()
    {
        var source = Locals("many", "    total=" + Sum + "\n    alias=total\n    return total,alias is total\n") + "print(many(3))\n";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("(250, True)\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(20)]
    [InlineData(80)]
    public async Task LargeArgumentListsUseAdmittedBindingAndLiveKeywordDefaults(int count)
    {
        var names = string.Join(",", Enumerable.Range(0, count).Select(i => "a" + i));
        var groups = Enumerable.Range(0, count).Chunk(40).ToArray();
        var body = string.Concat(groups.Select((group, i) => $"    part{i}=" + string.Join("+", group.Select(n => "a" + n)) + "\n"));
        var sum = string.Join("+", Enumerable.Range(0, groups.Length).Select(i => "part" + i));
        var source = $"def many({names},*,scale=2,offset=3):\n" + body + $"    return ({sum})*scale+offset\n" + """
            def call(function,args,options): return function(*args,**options)
            print(call(many,tuple(range(COUNT)),{}))
            print(call(many,tuple(range(COUNT)),{'scale':3,'offset':1}))
            many.__kwdefaults__['scale']=4
            print(call(many,tuple(range(COUNT)),{}))
            for args,options in [(tuple(range(COUNT-1)),{}),(tuple(range(COUNT+1)),{}),(tuple(range(COUNT)),{'unknown':1})]:
                try: call(many,args,options)
                except TypeError: print('TypeError')
            """.Replace("COUNT", count.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var expectedSum = count * (count - 1) / 2;
        var expected = $"{expectedSum * 2 + 3}\n{expectedSum * 3 + 1}\n{expectedSum * 4 + 3}\nTypeError\nTypeError\nTypeError\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static string ReadingSource => """
        def read_value():
            with open('input.txt') as source: return int(source.read())

        """ + Locals("many", "    total=" + Sum + "\n    return total+read_value()\n") + "print(many(1))\n";

    [Fact]
    public async Task LargeOwnedStateTransfersOnceThroughActualIo()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingSource, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "7");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "7");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("217\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("217\n", result.StandardOutput);
            }
            Assert.Equal(1, immediate.OpenFileCount);
            Assert.Equal(1, immediate.ClosedFileCount);
            Assert.Equal(1, delayed.ClosedFileCount);
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancelledIoKeepsTheLargeBindingFundedUntilCleanupFinishes()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingSource, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/input.txt");
            var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.BeforeFileClose = () => { closing.TrySetResult(); return new ValueTask(release.Task); };
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            LythonExecutionResult? completed = null;
            try
            {
                try
                {
                    await started.WaitAsync(cancellation.Token);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                    await closing.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                finally { cancellation.Cancel(); release.TrySetResult(); completed = await pending; }
                Assert.False(completed.Success);
                Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
                Assert.Equal("", completed.StandardOutput);
            }
            finally { completed?.Dispose(); }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RetainedLargeHelperResultsShareAnAllowanceWithoutRefundingAliases()
    {
        var source = Locals("many", "    text=f'{a0}:{a19}:é'\n    alias=text\n    return text,alias\n") + "return many(1)\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var first = script.Run(new MockLythonHost()))
            {
                Assert.True(first.Success, first.Failure?.Message);
                Assert.Equal(new object[] { "1:20:é", "1:20:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(first.ReturnValue));
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                using (var second = await script.RunAsync(new MockLythonHost()))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.Equal(new object[] { "1:20:é", "1:20:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(second.ReturnValue));
                    Assert.Equal(new object[] { "1:20:é", "1:20:é" }, Assert.IsAssignableFrom<IEnumerable<object>>(first.ReturnValue));
                    Assert.True(budget.ReservedBytes > retained);
                }
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyLoopsInLargeHelpersRemainCancellable(bool asynchronous)
    {
        var source = Locals("many", "    while flag: pass\n    return " + Sum + "\n", "seed,flag") + "return many(0,flag)\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var warm = script.Run(new MockLythonHost(), new LythonRunOptions { Globals = new Dictionary<string, object?> { ["flag"] = false } }))
            {
                Assert.True(warm.Success, warm.Failure?.Message);
                Assert.Equal(new BigInteger(190), warm.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions { CancellationToken = cancellation.Token, Globals = new Dictionary<string, object?> { ["flag"] = true } };
            var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options) : Task.Run(() => script.Run(new MockLythonHost(), options));
            LythonExecutionResult? completed = null;
            try
            {
                try { await Task.Delay(50); Assert.False(pending.IsCompleted); }
                finally { cancellation.Cancel(); completed = await pending.WaitAsync(TimeSpan.FromSeconds(10)); }
                Assert.False(completed.Success);
                Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
            }
            finally { completed?.Dispose(); }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task TheLargeBindingMustBeAdmittedBeforeBodyEffects()
    {
        var source = "import time\n" + Locals("many", "    print('must not run')\n    return " + Sum + "\n") + """
            time.time()
            try: many(1)
            except MemoryError: print('must not suppress')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            using (var result = asynchronous ? await script.RunAsync(new AdmissionGateHost(budget, 0), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new AdmissionGateHost(budget, 0), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialFormattingInLargeStorageSurvivesAsyncPromotionAndCleanup(bool cancel)
    {
        var source = """
            class Value:
                def __format__(self,spec):
                    with open('input.txt') as source: return source.read()

            """ + Locals("many", "    text=f'{a0}:{value}'\n    return text,a19\n", "seed,value") + "print(many(1,Value()))\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "kept");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = cancel ? host.PauseReadUntilCancellation("/input.txt") : Task.CompletedTask;
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            LythonExecutionResult? completed = null;
            try
            {
                if (cancel)
                {
                    try
                    {
                        await started.WaitAsync(cancellation.Token);
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > baseline);
                    }
                    finally { cancellation.Cancel(); completed = await pending; }
                    Assert.False(completed.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
                    Assert.Equal("", completed.StandardOutput);
                }
                else
                {
                    completed = await pending;
                    Assert.True(completed.Success, completed.Failure?.Message);
                    Assert.Equal("('1:kept', 20)\n", completed.StandardOutput);
                }
            }
            finally { completed?.Dispose(); }
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
