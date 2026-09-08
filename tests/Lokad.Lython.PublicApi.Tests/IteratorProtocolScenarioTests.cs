using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R08: user iterators keep identity through iter(), validate eagerly, advance
/// through next() directly, and await real suspension for __iter__/__next__
/// callbacks on delayed hosts.
/// </summary>
public sealed class IteratorProtocolScenarioTests
{
    [Fact]
    public async Task IterRetainsIdentityForSelfIterators()
    {
        var script = new LythonEngine().Compile(
            """
            class Counter:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    if self.n >= 2:
                        raise StopIteration()
                    self.n = self.n + 1
                    return self.n
            first = Counter()
            second = Counter()
            return [iter(first) is first, list(first), list(second)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, new List<object?> { new BigInteger(1), new BigInteger(2) }, new List<object?> { new BigInteger(1), new BigInteger(2) } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task IterReturnsFreshIteratorsWithoutIdentity()
    {
        var script = new LythonEngine().Compile(
            """
            class Fresh:
                def __iter__(self):
                    return FreshOnce()
            class FreshOnce:
                def __init__(self):
                    self.done = False
                def __iter__(self):
                    return self
                def __next__(self):
                    if self.done:
                        raise StopIteration()
                    self.done = True
                    return 7
            source = Fresh()
            return [iter(source) is source, list(source), list(source)]
            """);
        Assert.True(script.IsValid);
        var seven = new List<object?> { new BigInteger(7) };
        var expected = new List<object?> { false, seven, seven };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task DirectNextHonorsDefaultAndExhaustion()
    {
        var script = new LythonEngine().Compile(
            """
            class Counter:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    if self.n >= 1:
                        raise StopIteration()
                    self.n = self.n + 1
                    return self.n
            it = iter(Counter())
            results = [next(it), next(it, "dflt")]
            try:
                next(it)
                results.append("no-error")
            except StopIteration:
                results.append("StopIteration")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), "dflt", "StopIteration" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task InvalidIteratorsFailEagerly()
    {
        var script = new LythonEngine().Compile(
            """
            results = []
            class BadResult:
                def __iter__(self):
                    return 5
            try:
                iter(BadResult())
                results.append("badresult-no-error")
            except TypeError:
                results.append("badresult-TypeError")
            class NoNext:
                def __iter__(self):
                    return self
            try:
                iter(NoNext())
                results.append("nonext-no-error")
            except TypeError:
                results.append("nonext-TypeError")
            try:
                next(5)
                results.append("noniter-no-error")
            except TypeError:
                results.append("noniter-TypeError")
            class Plain:
                pass
            try:
                next(Plain())
                results.append("plain-no-error")
            except TypeError:
                results.append("plain-TypeError")
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "badresult-TypeError", "nonext-TypeError", "noniter-TypeError", "plain-TypeError" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task GeneratorReturningIterStaysLazy()
    {
        var script = new LythonEngine().Compile(
            """
            log = []
            class Gen:
                def __iter__(self):
                    log.append("iter")
                    return (x * 2 for x in [1, 2, 3])
            source = Gen()
            created = list(log)
            return [created, list(source)]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Empty(Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(4), new BigInteger(6) }, Assert.IsType<List<object?>>(values[1]));
        var delayed = new DelayedLythonHost("/");
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Empty(Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(4), new BigInteger(6) }, Assert.IsType<List<object?>>(asyncValues[1]));
    }

    [Fact]
    public async Task EagerConsumersAwaitDelayedHostCallbacks()
    {
        const string source = """
            class R:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    self.n = self.n + 1
                    if self.n > 2:
                        raise StopIteration()
                    return open("/data.txt").read()
            total = []
            for value in R():
                total.append(value)
            total.append(list(R()))
            return total
            """;
        var host = new DelayedLythonHost("/");
        host.SeedFile("/data.txt", "hello");
        var result = await new LythonEngine().RunAsync(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "hello", "hello", new List<object?> { "hello", "hello" } }, values);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task LazyProtocolAwaitDelayedHostCallbacks()
    {
        const string source = """
            class R:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    self.n = self.n + 1
                    if self.n > 2:
                        raise StopIteration()
                    return open("/data.txt").read()
            it = iter(R())
            first = next(it)
            rest = list(map(lambda value: value, it))
            return [first, rest]
            """;
        var host = new DelayedLythonHost("/");
        host.SeedFile("/data.txt", "hello");
        var result = await new LythonEngine().RunAsync(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal("hello", values[0]);
        Assert.Equal(new List<object?> { "hello" }, Assert.IsType<List<object?>>(values[1]));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task IterBuiltinItselfAwaitsDelayedResolution()
    {
        const string source = """
            class H:
                def __init__(self):
                    self.n = 0
                def __iter__(self):
                    open("/mark.txt").read()
                    return self
                def __next__(self):
                    self.n = self.n + 1
                    if self.n > 1:
                        raise StopIteration()
                    return self.n
            it = iter(H())
            return [it is not None, next(it), next(it, "done")]
            """;
        var host = new DelayedLythonHost("/");
        host.SeedFile("/mark.txt", "x");
        var result = await new LythonEngine().RunAsync(source, host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(1), "done" }, Assert.IsType<List<object?>>(result.ReturnValue));
        Assert.True(host.CompletedAsynchronously > 0);
    }
}

