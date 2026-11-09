using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeCallbackArgumentsTests
{
    private const string DefaultLookupSource = """
        class Key:
            def __hash__(self):return hash('suffix')
            def __eq__(self,other):
                with open('default.txt') as source:return source.read()==other
        def target(value, *, suffix='?'):return value+suffix
        target.__kwdefaults__={Key():'!'}
        return list(map(target, ['first','second']))
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BindingAndReentrantCallbacksPreserveValuesAndOrder(bool asynchronous)
    {
        const string source = """
            def collect(first, /, *rest, suffix='!'):
                return str(first)+str(rest)+suffix
            class Prefix:
                def __init__(self, prefix):self.prefix=prefix
                def add(self, value):return self.prefix+value
                def __call__(self, left, right):return self.prefix+left+right
            prefix=Prefix('x')
            print(list(map(collect, [1,2], [3,4])))
            print(list(map(prefix.add, ['a','b'])))
            print(list(map(prefix, ['a','b'], ['c','d'])))
            print(list(map(str, [1,2])))
            print(list(map(lambda a,b,c,d:(a,b,c,d), [1], [2], [3], [4])))
            def nested(value):
                return value+sum(map(lambda x:x+1, [3,4]))
            print(list(map(nested, [1,2])))
            def required(value, *, missing):return value
            try:list(map(required, [1]))
            except TypeError:print('required')
            print(list(filter(lambda x:x%2, [1,2,3])))
            """;
        const string expected = "['1(3,)!', '2(4,)!']\n['xa', 'xb']\n['xac', 'xbd']\n['1', '2']\n[(1, 2, 3, 4)]\n[10, 11]\nrequired\n[1, 3]\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackArgumentsSurviveSuspendingDefaultLookup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(DefaultLookupSource, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var mock = new MockLythonHost(); mock.SeedFile("/default.txt", "suffix");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/default.txt", "suffix");
            using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(mock))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { "first!", "second!" }, result.ReturnValue);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancelledDefaultLookupAwaitsCleanupBeforeReturningAllowance()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(DefaultLookupSource, budget);
        Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/default.txt", "suffix");
        var reading = host.PauseReadUntilCancellation("/default.txt");
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeFileClose = () => { closing.TrySetResult(); return new ValueTask(release.Task); };
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
            cancellation.Cancel(); release.TrySetResult();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
