using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N25: one starred name per flat loop target binds the remainder as a list,
// in leading, middle and trailing positions, across loops and comprehensions.
public sealed class StarredLoopTargetTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static void AssertError(LythonExecutionResult result, string type, string fragment)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(fragment, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrailingStar_BindsRemainderList()
    {
        var script = Compile("""
            for first, *rest in [(1, 2, 3)]:
                return [first, rest]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new List<object?> { new BigInteger(2), new BigInteger(3) } },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task LeadingStar_BindsRemainderList()
    {
        var script = Compile("""
            for *rest, b, last in [(1, 2, 3)]:
                return [rest, b, last]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new List<object?> { new BigInteger(1) }, new BigInteger(2), new BigInteger(3) },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MiddleStar_BindsRemainderList()
    {
        var script = Compile("""
            for a, *rest, last in [(1, 2, 3, 4)]:
                return [a, rest, last]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new List<object?> { new BigInteger(2), new BigInteger(3) }, new BigInteger(4) },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EmptyRemainder_BindsEmptyList()
    {
        var script = Compile("""
            for a, *rest in [(1,)]:
                return [a, rest]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new List<object?>() },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task GeneratorRowSums_MatchCpython()
    {
        var script = Compile("""
            rows = [(1, 2, 3), (4, 5, 6)]
            return [sum(q > 0 for _, q, *tail in rows), sum(b for *rest, b, last in rows)]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(2), new BigInteger(7) },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ComprehensionKinds_BindRemainders()
    {
        var script = Compile("""
            rows = [(1, 2), (3,)]
            r1 = [y for x, *y in rows]
            r2 = {x for x, *y in rows}
            r3 = {x: y for x, *y in [(1, 2, 3)]}
            return [r1, len(r2), 1 in r2, 3 in r2, r3[1]]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.Equal(
            new List<object?>
            {
                new List<object?> { new List<object?> { new BigInteger(2) }, new List<object?>() },
                new BigInteger(2),
                true,
                true,
                new List<object?> { new BigInteger(2), new BigInteger(3) },
            },
            sync.ReturnValue);
    }

    [Fact]
    public async Task DynamicShortRow_RaisesValueError()
    {
        var script = Compile("""
            def f(n):
                rows = [(i,) for i in range(n)]
                for a, *b, c in rows:
                    pass
                return 1
            return f(1)
            """);
        AssertError(script.Run(new MockLythonHost()), "ValueError", "expected at least 2");
        AssertError(await script.RunAsync(new MockLythonHost()), "ValueError", "expected at least 2");
    }

    [Fact]
    public void LiteralShortRow_RejectsAtCompileTime()
    {
        var script = new LythonEngine().Compile("""
            for a, *b, c in [(1,)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3030");
    }

    [Fact]
    public async Task DynamicNonIterableRow_RaisesTypeError()
    {
        var script = Compile("""
            def f(n):
                rows = [x for x in range(n)]
                for a, *b in rows:
                    pass
                return 1
            return f(1)
            """);
        AssertError(script.Run(new MockLythonHost()), "TypeError", "cannot unpack non-iterable");
        AssertError(await script.RunAsync(new MockLythonHost()), "TypeError", "cannot unpack non-iterable");
    }

    [Fact]
    public void MultipleStars_Rejected()
    {
        var script = new LythonEngine().Compile("""
            for *a, *b in [(1, 2)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1015");
    }

    [Fact]
    public void BareStarredTarget_Rejected()
    {
        var script = new LythonEngine().Compile("""
            for *a in [(1, 2)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1015");
    }

    [Fact]
    public void StarWithoutName_Rejected()
    {
        var script = new LythonEngine().Compile("""
            for *, b in [(1, 2)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1015");
    }

    [Fact]
    public async Task ParenthesizedStar_BindsRemainderList()
    {
        var script = Compile("""
            for (*a, b) in [(1, 2, 3)]:
                return [a, b]
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(
            new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(2) }, new BigInteger(3) },
            sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DroppedRemainders_RespectMemoryBudget()
    {
        var script = Compile("""
            row = list(range(50))
            for a, *b in [row] * 20000:
                pass
            return "done"
            """);
        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal("done", funded.ReturnValue);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16000000 };
        AssertError(script.Run(new MockLythonHost(), options), "MemoryError", "execution memory budget exceeded");
        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        AssertError(await script.RunAsync(new MockLythonHost(), options), "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public async Task DelayedAsyncFileRows_UnpackThroughSuspension()
    {
        var script = Compile("""
            with open("/r.txt") as f:
                total = 0
                for first, *rest in [line.split() for line in f]:
                    total = total + int(first) + len(rest)
            return total
            """);
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "1 a b\n2 c\n");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(6), result.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
