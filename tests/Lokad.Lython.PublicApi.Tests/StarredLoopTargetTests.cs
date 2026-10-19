using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N25: one starred name per flat loop target binds the remainder as a list,
// in leading, middle and trailing positions, across loops and comprehensions.
[Collection("RuntimeStress")]
public sealed class StarredLoopTargetTests : LifetimeScenario
{
    protected override int BatchRepetitions(int iterations) => Math.Max(8, iterations);
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            for *a, *b in [(1, 2)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1015");
    }

    [Fact]
    public void BareStarredTarget_Rejected()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            for *a in [(1, 2)]:
                pass
            """);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA1015");
    }

    [Fact]
    public void StarWithoutName_Rejected()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        var retained = Compile("""
            row = list(range(50))
            kept = []
            for a, *b in [row] * 20000:
                kept.append(b)
            return len(kept)
            """);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16000000) };
        AssertError(retained.Run(new MockLythonHost(), options), "MemoryError", "shared memory budget exceeded");
        AssertError(await retained.RunAsync(new MockLythonHost(), options), "MemoryError", "shared memory budget exceeded");
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

    [Theory]
    [InlineData("for a, *b in ([0] * 20 for _ in range(2048)):\n    pass\nreturn \"done\"\n")]
    [InlineData("d = {}\nrow = list(range(20))\nfor i in range(2048):\n    d[0], *d[1] = row\nreturn \"done\"\n")]
    public async Task DiscardedLoopAndSubscriptRemaindersCompleteAtFixedCap(string source)
    {
        await AssertBatchCompletes(source.Replace("range(2048)", "range(8)"), "done", 65536);
    }

    [Fact]
    public async Task DiscardedAssignRemainders_DoNotDeny()
        => await AssertAssignRemainders(2048, 65536);

    [Fact]
    [Trait("Category", "Stress")]
    public async Task DiscardedAssignRemaindersStress()
        => await AssertAssignRemainders(20000, 16000000);

    private static async Task AssertAssignRemainders(int iterations, long memoryLimit)
    {
        var batchSize = iterations >= 20000 ? 1000 : 8;
        await new StarredLoopTargetTests().AssertBatchCompletes($"""
            row = list(range(20))
            for i in range({batchSize}):
                a, *b = row
            return "done"
            """, "done", memoryLimit, iterations >= 20000 ? (iterations + batchSize - 1) / batchSize : 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncFileRowRemaindersStayBounded(bool capExecutionMemory)
        => await AssertAsyncFileRowRemainders(capExecutionMemory, 64, 128, 131072);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Stress")]
    public async Task AsyncFileRowRemaindersStress(bool capExecutionMemory)
        => await AssertAsyncFileRowRemainders(capExecutionMemory, 10000, 100000, 4L * 1024 * 1024);

    private static async Task AssertAsyncFileRowRemainders(bool capExecutionMemory, int smallRows, int largeRows, long memoryLimit)
    {
        var budget = new LythonMemoryBudget(Math.Max(512 * 1024, memoryLimit + 256 * 1024));
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("with open('/r.txt') as f:\n    total = 0\n    for line in f:\n        first, *rest = line.split()\n        assert first == 'k' and rest == ['v', 'w', 'x']\n        total += 1\n    return total\n", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var batchSize = largeRows >= 100000 ? 256 : 32;
        foreach (var count in new[] { smallRows, largeRows })
        {
            var total = BigInteger.Zero;
            for (var start = 0; start < count; start += batchSize)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/r.txt", string.Concat(Enumerable.Repeat("k v w x\n", Math.Min(batchSize, count - start))));
                var options = new LythonRunOptions { MemoryBudget = capExecutionMemory ? new LythonMemoryBudget(memoryLimit) : null };
                using (var result = await script.RunAsync(host, options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    total += Assert.IsType<BigInteger>(result.ReturnValue);
                    Assert.True(host.CompletedAsynchronously > 0);
                    if (capExecutionMemory) Assert.InRange(result.PeakExecutionMemoryBytes, 1, memoryLimit);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(new BigInteger(count), total);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
