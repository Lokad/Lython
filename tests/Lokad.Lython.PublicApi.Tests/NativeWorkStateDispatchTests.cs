using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeWorkStateDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeHelpersPreserveBranchesUnicodeAndListTupleLoopControl(bool asynchronous)
    {
        const string source = """
            def words(line):
                if line.isspace():
                    return []
                return line.lower().rsplit()
            def total(items):
                result = 0
                for item in items:
                    if item < 0:
                        continue
                    if item > 9:
                        break
                    result += item
                return result
            def nested(items):
                result = 0
                for row in items:
                    for item in row:
                        result += item
                return result
            print(words(''), words(' \t\n'), words('ÉCOLE 世界\tHELLO\n'), words('ΟΣ'))
            print(total([1,-1,2,20,100]),total((3,4)),nested([(1,2),(3,)]))
            print('é'.lower(*()), 'a b'.rsplit(**{}))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[] [] ['école', '世界', 'hello'] ['ος']\n3 7 6\né ['a', 'b']\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeDictionaryViewsRetainAliasesAndEscapingStorage(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            def select(mapping):
                if mapping:
                    return mapping.values()
                return mapping.keys()
            mapping = {'a':[1]}
            view = select(mapping)
            mapping['a'].append(2)
            assert list(view)[0] is mapping['a']
            assert list(select({})) == []
            return list(view)
            """, budget);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        script.Dispose();
        Assert.True(budget.ReservedBytes > 0);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NativeLoopTransfersItsBorrowedStateBeforeGuestIoAndCancellation(bool tuple, bool cancel)
    {
        var source = """
            def load(item):
                with open('/gate','a') as output:
                    output.write(item)
                return item
            def mapped(items):
                result = []
                for item in items:
                    result.append(load(item))
                return result
            """ + "\nprint(mapped(" + (tuple ? "('a','b')" : "['a','b']") + "))";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate", "before:");
        var entered = cancel ? host.PauseWriteUntilCancellation("/gate") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Guest call did not suspend.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "['a', 'b']\n", result.StandardOutput);
            Assert.Equal(cancel ? "before:" : "before:ab", host.ReadText("/gate"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeHelperRefusalRemainsTerminalThroughFinallyReturn(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            def expand(text):
                return text.expandtabs()
            text = '\t' * 131072
            print('ready')
            try:
                expand(text)
            except MemoryError:
                print('hidden')
            finally:
                return 'hidden'
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("ready\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonAllocatingNativeLoopObservesHostCancellationAndReleasesWorkingStorage(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("def spin(items):\n while True:\n  for item in items:pass\nspin([1,2])", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var options = new LythonRunOptions { CancellationToken = cancellation.Token };
        var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
            : Task.Run(() => script.Run(new MockLythonHost(), options));
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
