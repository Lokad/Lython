using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctionDefinitionIsolationTests
{
    [Fact]
    public void RefusedCompilationAvoidsLargeFrontendAllocation()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        using var warm = engine.Compile("pass");
        var source = "def f(" + string.Join(',', Enumerable.Range(0, 4096).Select(i => "p" + i)) + "):\n    pass\n";
        var budget = new LythonMemoryBudget(32768);
        var before = GC.GetAllocatedBytesForCurrentThread();
        using var refused = engine.Compile(source, budget);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(refused.IsValid);
        Assert.Contains(refused.Diagnostics, d => d.Code == "LA0008");
        Assert.True(allocated < 262144, $"Refused compilation allocated {allocated} bytes before returning.");
        refused.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedDefinitionsKeepDefaultsAndClosuresIndependent(bool asynchronous)
    {
        const string source = """
def factory(tag):
    def f(a=[], /, b=2, *args, c={'n': 3}, **kw):
        a.append(tag)
        c['n'] += b
        return [a[:], c['n'], args, kw]
    return f
g = factory(1)
h = factory(2)
print(g(), h(), g())
g.__defaults__ = ([9], 4)
g.__kwdefaults__['c'] = {'n': 10}
print(g(), h())
def gen(a=[], *, n=1):
    a.append(n)
    yield a[:]
print(list(gen()), list(gen(n=2)))
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        for (var i = 0; i < 2; i++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[[1], 5, (), {}] [[2], 5, (), {}] [[1, 1], 7, (), {}]\n" +
                    "[[9, 1], 14, (), {}] [[2, 2], 7, (), {}]\n[[1]] [[1, 2]]\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ConcurrentExecutionsShareCompiledStorageAndRetainOnlyTheirOwnDefaults()
    {
        const string source = "import time\ndef f(a=[tag], *, b={'x': tag}): return [a, b]\ntime.sleep(1)\nprint(f())\nreturn f()";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var gates = new[] { new HeldTiming(), new HeldTiming() };
        var calls = gates.Select((gate, i) =>
        {
            var host = new MockLythonHost();
            host.SetTiming(gate);
            return script.RunAsync(host, new LythonRunOptions { Globals = new Dictionary<string, object?> { ["tag"] = i + 1 } });
        }).ToArray();
        LythonExecutionResult[]? results = null;
        try
        {
            await Task.WhenAll(gates.Select(g => g.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(budget.ReservedBytes > baseline);
            gates[0].Release.SetResult();
            var first = await calls[0].WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal("[[1], {'x': 1}]\n", first.StandardOutput);
            Assert.False(calls[1].IsCompleted);
            gates[1].Release.SetResult();
            results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(results[1].Success, results[1].Failure?.Message);
            Assert.Equal("[[2], {'x': 2}]\n", results[1].StandardOutput);
            Assert.True(budget.ReservedBytes > baseline);
            results[0].Dispose();
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            foreach (var gate in gates) gate.Release.TrySetResult();
            results ??= await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var result in results) result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RepeatedMetadataDictionariesKeepTheirValuesAndMutationOrder()
    {
        const string source = """
            def create(seed):
                def rule(*, value: seed = seed, scale: int = 2) -> seed:
                    return value * scale
                return rule

            first, second, third = create(3), create(5), create(7)
            defaults = second.__kwdefaults__
            annotations = second.__annotations__
            del defaults['value']
            defaults['value'] = 11
            del annotations['value']
            annotations['value'] = 13
            fourth = create(17)
            print(list(defaults), list(annotations))
            print(list(first.__kwdefaults__), list(third.__annotations__), list(fourth.__kwdefaults__))
            print(first(), second(), third(), fourth())
            print(first.__annotations__['value'], annotations['value'], fourth.__annotations__['value'])
            return [defaults, fourth.__kwdefaults__]
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compiled = new LythonEngine().Compile(source, budget);
        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("['scale', 'value'] ['scale', 'return', 'value']\n" +
                    "['value', 'scale'] ['value', 'scale', 'return'] ['value', 'scale']\n" +
                    "6 22 14 34\n3 13 17\n", result.StandardOutput);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        compiled.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task FactoryTransfersPreparedDefaultsAtSuspension()
    {
        const string source = """
            events = []
            def read():
                events.append('read')
                with open('/value.txt') as stream:
                    return int(stream.read())
            def factory(seed):
                def f(items=[seed], *, value=read()):
                    return [items, value]
                return f
            first = factory(7)
            print(first(), first.__defaults__[0] is first()[0], events)
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var paused = new DelayedLythonHost();
        paused.SeedFile("/value.txt", "21");
        var entered = paused.PauseReadUntilCancellation("/value.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(paused, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(entered, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            cancellation.Cancel();
            using var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(LythonFailureReason.Cancelled, cancelled.FailureAttribution?.Reason);
            Assert.Empty(cancelled.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);

        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/value.txt", "21");
        using (var result = await script.RunAsync(fresh))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[[7], 21] True ['read']\n", result.StandardOutput);
            Assert.True(fresh.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Entered.SetResult();
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
