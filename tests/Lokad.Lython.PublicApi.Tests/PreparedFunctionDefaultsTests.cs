using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedFunctionDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefinitionDefaultsSurviveTransferDuringKeywordEvaluation(bool asynchronous)
    {
        const string source = """
            inspect_scope = locals
            def step(value):
                return value + 1
            def read(value):
                with open('/pause') as stream:
                    stream.read()
                return value
            def factory(seed):
                def prior(value=step(seed + 1), *, named=5):
                    return value, named
                def pending(value=seed + 3, *, named=read(seed)):
                    return value, named
                a = 0; b = 0; c = 0; d = 0; e = 0; f = 0; g = 0; h = 0
                i = 0; j = 0; k = 0; l = 0; m = 0; n = 0; o = 0; p = 0
                return inspect_scope()
            saved = factory(10)
            print(saved['prior'](), saved['pending']())
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        if (asynchronous)
        {
            var paused = new DelayedLythonHost();
            paused.SeedFile("/pause", "content");
            var started = paused.PauseReadUntilCancellation("/pause");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(paused, new LythonRunOptions
            {
                MemoryBudget = budget, CancellationToken = cancellation.Token
            });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > 0);
            }
            finally
            {
                cancellation.Cancel();
                using var interrupted = await pending;
                Assert.Equal(LythonFailureReason.Cancelled, interrupted.FailureAttribution?.Reason);
                Assert.Empty(interrupted.StandardOutput);
            }
            Assert.Equal(1, paused.ClosedFileCount);
            Assert.Equal(0, budget.ReservedBytes);
        }

        var host = new MockLythonHost();
        host.SeedFile("/pause", "content");
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(12, 5) (13, 10)\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultCanObserveTheParameterThatItsDefinitionReplaces(bool asynchronous)
    {
        const string source = """
            inspect_scope = locals
            def factory(target):
                def target(value=inspect_scope(), *, named=5):
                    return value, named
                a = 0; b = 0; c = 0; d = 0; e = 0; f = 0; g = 0; h = 0
                i = 0; j = 0; k = 0; l = 0; m = 0; n = 0; o = 0; p = 0
                return inspect_scope()
            saved = factory('old')
            before, named = saved['target']()
            print(before['target'], named)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var host = new MockLythonHost();
        using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("old 5\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IteratorProtocolCanObserveAnEarlierDefinitionThroughNext(bool asynchronous)
    {
        const string source = """
            class Observe:
                def __iter__(self): return self
                __next__ = staticmethod(locals)

            def step(value): return value + 1

            def factory(seed, observer):
                def saved(value=step(seed), *, factor=step(seed + 1)):
                    return value, factor
                a = 0; b = 0; c = 0; d = 0; e = 0; f = 0; g = 0; h = 0
                i = 0; j = 0; k = 0; l = 0; m = 0; n = 0; o = 0; p = 0
                return next(observer)

            snapshot = factory(10, Observe())
            saved = snapshot['saved']
            print(saved(), saved.__defaults__, saved.__kwdefaults__['factor'])
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var host = new MockLythonHost();
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(11, 12) (11,) 12\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 10)]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    public async Task EarlierDefinitionsRemainVisibleToAnAliasedLocalsDefault(bool asynchronous, int offset)
    {
        const string source = """
            lookup = locals

            def step(value): return value + 1

            def factory(offset):
                def earlier(value=12, *, factor=step(offset + 2)):
                    return value + factor
                assigned = lambda value=step(offset + 3), *, factor=step(offset + 4): value + factor
                a = 0; b = 0; c = 0; d = 0; e = 0; f = 0; g = 0; h = 0
                i = 0; j = 0; k = 0; l = 0; m = 0; n = 0; o = 0; p = 0
                guard = 1 // offset
                def capture(snapshot=lookup()):
                    return snapshot
                return capture()

            saved = factory(input_offset)
            first = saved['earlier']
            second = saved['assigned']
            print(first(), second(), first.__defaults__, second.__kwdefaults__['factor'], 'capture' in saved)
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new MockLythonHost();
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["input_offset"] = offset } };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                if (offset != 0)
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("25 29 (12,) 15 False\n", result.StandardOutput);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal("ZeroDivisionError", result.Failure?.ExceptionType);
                    Assert.Empty(result.StandardOutput);
                }
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiteralTupleDefaultsShareIdentityAcrossDefinitions(bool asynchronous)
    {
        const string source = """
            def read(seed):
                with open('/base.txt') as stream:
                    return int(stream.read()) + seed
            def make(seed):
                def evaluate(value=(7), /, name='base', *, scale=read(seed)):
                    return (value * scale, name)
                return evaluate
            first = make(2)
            second = make(3)
            defaults = first.__defaults__
            print(defaults is second.__defaults__, first.__kwdefaults__ is second.__kwdefaults__)
            print(first(), second())
            first.__defaults__ = (11, 'other')
            first.__kwdefaults__['scale'] = 5
            print(first(), second(), defaults is second.__defaults__)
            def factory():
                return lambda n=2: n
            print(factory().__defaults__ is factory().__defaults__)
            """;
        const string expected = "True False\n(28, 'base') (35, 'base')\n(55, 'other') (35, 'base') True\nTrue\n";
        var compilation = new LythonMemoryBudget(1024 * 1024);
        var execution = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, compilation);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var before = compilation.ReservedBytes;
        var host = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        host.SeedFile("/base.txt", "2");
        delayed.SeedFile("/base.txt", "2");
        var options = new LythonRunOptions { MemoryBudget = execution };
        using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(0, execution.ReservedBytes);
        Assert.Equal(before, compilation.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, compilation.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LiteralKeywordDefaultsKeepIndependentDictionariesAcrossSuspendingPositionalDefaults(bool asynchronous)
    {
        const string source = """
            def read(seed):
                with open('/base.txt') as stream:
                    return int(stream.read()) + seed
            def make(seed):
                return lambda value=read(seed), *, scale=(7), unit='base': (value * scale, unit)
            first = make(2)
            second = make(3)
            assert first() == (28, 'base') and second() == (35, 'base')
            defaults = first.__kwdefaults__
            assert defaults is first.__kwdefaults__ and defaults is not second.__kwdefaults__
            defaults['scale'] = 5
            assert first() == (20, 'base') and second() == (35, 'base')
            first.__kwdefaults__ = {'scale': 3, 'unit': 'other'}
            assert first() == (12, 'other') and defaults['unit'] == 'base'
            return second()
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        host.SeedFile("/base.txt", "2");
        delayed.SeedFile("/base.txt", "2");
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(2, asynchronous ? delayed.ClosedFileCount : host.ClosedFileCount);
        }
        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeywordDefaultCanBeADictionaryAndKeepItsIdentity(bool asynchronous)
    {
        const string source = """
            def factory(value):
                def read(*, required, item=value):
                    return item
                return read
            payload = {'item': 7}
            first = factory(payload)
            second = factory(payload)
            assert first(required=None) is payload
            observed = first.__kwdefaults__
            assert observed is first.__kwdefaults__
            assert observed['item'] is payload
            first.__kwdefaults__ = {'item': 9}
            assert first(required=None) == 9
            assert observed['item'] is second(required=None)
            second.__kwdefaults__ = payload
            assert second(required=None) == 7
            """;
        var budget = new LythonMemoryBudget();
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalFactoryDefaultsStayOwnedAcrossSuspension(bool cancel)
    {
        const string source = """
def default_value(seed):
    with open('/events.txt', 'a') as events:
        events.write(str(seed))
    return [seed]
def make(seed):
    def call(value=default_value(seed), *, flag=seed + 1) -> tuple:
        return value, flag
    return call
call = make(2)
value, flag = call()
print(value is call.__defaults__[0], flag, call.__annotations__['return'] is tuple)
return value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/events.txt", "before:");
            var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }

                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                if (cancel)
                {
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.Empty(result.StandardOutput);
                    Assert.Equal("before:", host.ReadText("/events.txt"));
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True 3 True\n", result.StandardOutput);
                    Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal("before:2", host.ReadText("/events.txt"));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.True(host.CompletedAsynchronously > 0);
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedDefinitionsKeepDefaultsAndClosuresIndependent(bool asynchronous)
    {
        const string source = """
def make(seed):
    def call(value: int = seed, /, *extra, flag: int = seed, **named) -> tuple:
        return value, extra, flag, named, seed
    def stream(value=seed, *, flag=seed):
        yield value, flag, seed
    return call, stream, lambda value=seed, *, flag=seed: (value, flag, seed)
first, first_stream, first_lambda = make(2)
second, second_stream, second_lambda = make(7)
first.__defaults__ = (11,)
first.__kwdefaults__['flag'] = 13
first_stream.__kwdefaults__['flag'] = 17
first_lambda.__defaults__ = (19,)
first.__annotations__['return'] = list
del first.__annotations__['flag']
assert second.__annotations__ == {'value': int, 'flag': int, 'return': tuple}
print(first())
print(second(23, 29, value=31, flag=37))
print(list(first_stream()), list(second_stream()))
print(first_lambda(), second_lambda())
print(second.__defaults__, second.__kwdefaults__)
""";
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        var executionBudget = new LythonMemoryBudget(16 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = executionBudget };
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using (var result = asynchronous
                    ? await compiled.RunAsync(new MockLythonHost(), options)
                    : compiled.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("(11, (), 13, {}, 2)\n(23, (29,), 37, {'value': 31}, 7)\n" +
                        "[(2, 17, 2)] [(7, 7, 7)]\n(19, 2, 2) (7, 7, 7)\n(7,) {'flag': 7}\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, executionBudget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProjectedDefinitionRecordsSurviveCompilationDisposal(bool asynchronous)
    {
        const string source = """
            from __future__ import annotations
            def make(value):
                def read(*, clé: int = value):
                    return clé
                return read
            first = make(2)
            second = make(7)
            first.__kwdefaults__['clé'] = 3
            first.__annotations__['clé'] = 'other'
            return [second.__kwdefaults__, second.__annotations__]
            """;
        var compilation = new LythonMemoryBudget(2 * 1024 * 1024);
        var execution = new LythonMemoryBudget(2 * 1024 * 1024);
        var projection = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, compilation);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var options = new LythonRunOptions { MemoryBudget = execution, ResultMemoryBudget = projection };
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0, execution.ReservedBytes);

        script.Dispose();
        Assert.Equal(0, compilation.ReservedBytes);
        Assert.True(projection.ReservedBytes > 0);
        var records = Assert.IsType<List<object?>>(result.ReturnValue);
        var defaults = Assert.IsType<Dictionary<object, object?>>(records[0]);
        var annotations = Assert.IsType<Dictionary<object, object?>>(records[1]);
        Assert.Equal("7", defaults["clé"]?.ToString());
        Assert.Equal("int", annotations["clé"]);
        result.Dispose();
        Assert.Equal(0, projection.ReservedBytes);
    }
}
