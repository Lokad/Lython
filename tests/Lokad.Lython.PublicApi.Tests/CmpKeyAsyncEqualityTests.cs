using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CmpKeyAsyncEqualityTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var expression in new[] { "a==b", "a!=b", "a in [b]", "[a]<[b]" })
        foreach (var flag in new[] { false, true })
            yield return [expression, flag];
    }

    private static string Source(string expression) =>
        "from functools import cmp_to_key\ndef compare(a,b):\n with open('events.txt','a') as target:target.write('seen')\n return (a>b)-(a<b)\nkey=cmp_to_key(compare)\na=key(1)\nb=key(1)\nprint(" + expression + ")";

    private static string Expected(string expression) => expression is "a!=b" or "[a]<[b]" ? "False\n" : "True\n";

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryWrappedEqualityPreservesResultsAndEffects(string expression, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(Source(expression), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost(); host.SeedFile("/events.txt", "before");
        using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(Expected(expression), result.StandardOutput);
            Assert.Equal("beforeseen", host.ReadText("/events.txt"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task WrappedEqualityAwaitsIOAndCancellation(string expression, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(Source(expression), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/events.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Comparator did not reach the pending write.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : Expected(expression), result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeseen", host.ReadText("/events.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RefusedComparatorAllocationRemainsTerminal(string expression, bool asynchronous)
    {
        var body = Source(expression).Replace("with open('events.txt','a') as target:target.write('seen')", "print('entered');payload='x'*2000000");
        var source = "try:\n" + string.Join("\n", body.Split('\n').Select(line => " " + line)) +
            "\nexcept MemoryError:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
