using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: a function value retains its defining context for the run. Nested
/// definitions own the context scaffolding plus one slot per captured entry,
/// while module-level definitions share the run-rooted frame and stay free.
/// </summary>
public sealed class ClosureRetentionAccountingScenarioTests
{
    // 5000 closures own 128B (value) + 512B (context) + 32B (cell) + 16B
    // (list slot) each, so they fit 1.5MB pre-fix and trip post-fix.
    private const long ClosureBudgetBytes = 1572864;

    // 3000 closures over 21 captured entries own an extra 640B each.
    private const long WideClosureBudgetBytes = 1572864;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnedLeafFunctionsKeepLiveGlobalsAndIndependentDefaults(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            shift = 1
            def factory(seed):
                def named(value=seed):
                    return value + shift
                return named if seed % 2 == 0 else (lambda value=seed: value + shift)
            funcs = [factory(seed) for seed in range(400)]
            shift = 10
            return [funcs[0](), funcs[-1](), funcs[0].__qualname__, funcs[-1].__qualname__]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?>
            {
                new BigInteger(10), new BigInteger(409), "factory.<locals>.named", "factory.<locals>.<lambda>"
            }, result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReturnedMatchBodyKeepsItsPatternNamespace()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            def factory():
                class Verdict:
                    ok = 1
                def accepts(value):
                    match value:
                        case Verdict.ok:
                            return True
                    return False
                return accepts
            accepts = factory()
            return [accepts(1), accepts(2)]
            """);
        Assert.True(script.IsValid);
        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, false }, sync.ReturnValue);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForwardedCellsStayLiveAfterFactoryRebindAndReturn(bool asynchronous)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            shift = 1
            def factory(seed):
                captured = seed
                def middle():
                    def add(value):
                        return captured + value + shift
                    return add
                add = middle()
                captured += 1
                return add
            funcs = [factory(seed) for seed in range(320)]
            shift = 10
            return [funcs[0](5), funcs[-1](5)]
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(256 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { new BigInteger(16), new BigInteger(335) }, result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task InlinedComprehensionShadowsTheSameNameInADefiningSlot()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            def factory():
                value = 1
                direct = lambda: value
                inlined = [lambda: value for value in [3, 4]]
                value = 2
                return direct, inlined
            direct, inlined = factory()
            return [direct(), inlined[0](), inlined[1]()]
            """);
        Assert.True(script.IsValid);
        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(4), new BigInteger(4) }, sync.ReturnValue);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ManyRetainedClosuresStayCharged()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            def make(n):
                x = n
                def inner():
                    return x
                return inner
            funcs = []
            i = 0
            while i < 5000:
                funcs.append(make(i))
                i = i + 1
            return len(funcs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(ClosureBudgetBytes) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ClosureBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ClosureBudgetBytes);
    }

    [Fact]
    public async Task ManyWideClosuresStayCharged()
    {
        var body = new System.Text.StringBuilder("def make(n):\n");
        var total = "n";
        for (var k = 0; k < 20; k++)
        {
            body.Append("    v").Append(k).Append(" = n\n");
            total += " + v" + k;
        }

        body.Append("    def inner():\n        return ").Append(total).Append("\n    return inner\nfuncs = []\ni = 0\nwhile i < 3000:\n    funcs.append(make(i))\n    i = i + 1\nreturn len(funcs)\n");
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(body.ToString());
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(WideClosureBudgetBytes) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ClosureIsolationAndNonlocalBehave()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            def make(n):
                x = n
                def get():
                    return x
                def bump(d):
                    nonlocal x
                    x = x + d
                    return x
                return [get, bump]
            pair = make(10)
            get = pair[0]
            bump = pair[1]
            first = get()
            after = bump(5)
            return [first, after, get()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(10), new BigInteger(15), new BigInteger(15) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
