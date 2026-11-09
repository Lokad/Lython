using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FnmatchSuspendedIterationTests
{
    private const string Source = """
        import fnmatch
        def names():
            with open('/names') as source:
                for line in source:
                    yield line.strip()
        return fnmatch.filter(names(), '*.txt')
        """;

    public static TheoryData<string> Sources => new()
    {
        Source,
        """
        import fnmatch
        class Names:
            def __iter__(self):
                with open('/names') as source:
                    return iter(source.read().splitlines())
        return fnmatch.filter(Names(), '*.txt')
        """,
        """
        import fnmatch
        class Names:
            def __init__(self):
                self.values=None
                self.index=0
            def __iter__(self):return self
            def __next__(self):
                if self.values is None:
                    with open('/names') as source:
                        self.values=source.read().splitlines()
                if self.index==len(self.values):raise StopIteration
                value=self.values[self.index]
                self.index+=1
                return value
        return fnmatch.filter(Names(), '*.txt')
        """,
    };

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task FileBackedNamesUseTheActiveExecutionApiAndReturnBudget(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var synchronous = new MockLythonHost();
        synchronous.SeedFile("/names", "alpha.txt\nbeta.md\né😀.txt\n");
        using (var result = script.Run(synchronous))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { "alpha.txt", "é😀.txt" }, result.ReturnValue);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var host = new DelayedLythonHost();
        host.SeedFile("/names", "alpha.txt\nbeta.md\né😀.txt\n");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { "alpha.txt", "é😀.txt" }, result.ReturnValue);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task CancellationAwaitsFileCleanupAndKeepsAllowanceFunded(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/names", "alpha.txt\nbeta.md\né😀.txt\n");
        var reading = host.PauseReadUntilCancellation("/names");
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeFileClose = () =>
        {
            closing.TrySetResult();
            return new ValueTask(release.Task);
        };
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            await reading.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            await closing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            release.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidItemStopsAtThatItemAndLeavesBorrowedGeneratorUsable(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import fnmatch
            events=[]
            def names():
                events.append('a')
                yield 'alpha.txt'
                events.append('bad')
                yield 7
                events.append('later')
                yield 'later.txt'
            values=names()
            try:fnmatch.filter(values,'*.txt')
            except TypeError:events.append('type')
            print(events[:],next(values))
            name='é😀.txt'
            matched=fnmatch.filter((value for value in [name]),'*.txt')
            print(matched[0] is name)
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['a', 'bad', 'type'] later.txt\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalIsTerminalAndTheSameAllowanceCanBeReused(bool asynchronous)
    {
        var compiledBudget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import fnmatch
            def names():
                with open('/names') as source:
                    for line in source:yield line.strip()
            try:values=fnmatch.filter(names(),'*.txt')
            except MemoryError:pass
            with open('/out','w') as output:output.write('changed')
            """, compiledBudget);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var baseline = compiledBudget.ReservedBytes;
        var allowance = new LythonMemoryBudget(131072);
        var options = new LythonRunOptions { MemoryBudget = allowance };
        foreach (var retained in new[] { true, false })
        {
            var host = new MockLythonHost();
            host.SeedFile("/names", retained ? string.Concat(Enumerable.Repeat("é😀.txt\n", 2048)) : "one.txt\n");
            host.SeedFile("/out", "before");
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.Equal(!retained, result.Success);
                if (retained)
                {
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Equal("before", host.ReadText("/out"));
                }
                else Assert.Equal("changed", host.ReadText("/out"));
            }
            Assert.Equal(0, allowance.ReservedBytes);
            Assert.Equal(baseline, compiledBudget.ReservedBytes);
        }
    }
}
