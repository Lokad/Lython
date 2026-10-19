using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FloatSumCompatibilityTests
{
    public static IEnumerable<object[]> Programs()
    {
        var cases = new (string Name, string Source, string Expected)[]
        {
            ("compensated-cancellation", "print(sum([1e16,1.0,-1e16]))", "1.0\n"),
            ("repeated-decimal", "print(sum([0.1]*10))", "1.0\n"),
            ("explicit-float-start", "print(sum([1e16,1.0,-1e16],start=2.0))", "3.0\n"),
            ("guest-reverse-add-sees-compensated-total", "class Box:\n def __radd__(self,total):\n  print(total)\n  return 7\nprint(sum([1e16,1.0,-1e16,Box()]))", "1.0\n7\n"),
            ("bool-start-keeps-general-phase", "print(sum([1e16,1.0,-1e16],True))", "0.0\n"),
            ("small-int-in-float-phase", "print(sum([1e16,1,-1e16]))", "0.0\n"),
            ("negative-zero", "import math\nprint(math.copysign(1.0,sum([],start=-0.0)),math.copysign(1.0,sum([-0.0],start=-0.0)))", "-1.0 -1.0\n"),
            ("nonfinite-and-overflow", "print(sum([1e308,1e308,-1e308]),sum([float('inf'),float('-inf')]))\ntry:sum([1.0,1<<10000])\nexcept OverflowError:print('overflow')", "inf nan\noverflow\n"),
            ("integer-phase-guest-double-enters-compensation", "class Box:\n def __radd__(self,total):\n  print(total)\n  return 1e16\nprint(sum([Box(),1.0,-1e16]))", "0\n1.0\n"),
            ("float-phase-guest-double-stays-general", "class Box:\n def __radd__(self,total):\n  print(total)\n  return 1e16\nprint(sum([0.0,Box(),1.0,-1e16]))", "0.0\n0.0\n"),
            ("integer-phase-guest-int-stays-general", "class Box:\n def __radd__(self,total):return 0\nprint(sum([Box(),1e16,1.0,-1e16]))", "0.0\n"),
            ("wider-integer-start-stays-general", "print(sum([-(1<<80),1e16,1.0,-1e16],1<<80))", "0.0\n"),
            ("integer-prefix-overflow-stays-general", "print(sum([1<<62,1<<62,-(1<<63),1e16,1.0,-1e16]))", "0.0\n"),
            ("float-phase-wide-int-stays-general", "print(sum([1e16,1.0,-1e16,1<<80,-(1<<80),1e16,1.0,-1e16]))", "0.0\n"),
            ("retained-iterator-after-type-error", "values=iter([1e16,1.0,-1e16,'bad',5])\ntry:sum(values)\nexcept TypeError:print(next(values))", "5\n"),
            ("guest-memory-error-remains-catchable", "class Stop:\n def __radd__(self,total):\n  print(total)\n  raise MemoryError('guest')\ntry:sum([1e16,1.0,-1e16,Stop()])\nexcept MemoryError:print('caught')", "1.0\ncaught\n"),
            // Lython's documented fixed 64-bit policy is independent of C long.
            // CPython builds with narrower native conversions may print 0.0.
            ("portable-64-bit-integer-prefix", "print(sum([1<<40,-(1<<40),1e16,1.0,-1e16]))", "1.0\n"),
        };
        foreach (var item in cases)
        foreach (var asynchronous in new[] { false, true })
            yield return [item.Name, asynchronous, item.Source, item.Expected];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FloatSummationPreservesPythonNumericAndGuestTransitions(
        string name, bool asynchronous, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompensatedPrefixReachesAwaitedGuestAdditionAndCancellation(bool cancel)
    {
        const string source = """
class Finish:
 def __radd__(self,total):
  print(total)
  with open('sum.txt','a') as target:target.write(str(total))
  return 7
print(sum([1e16,1.0,-1e16,Finish()]))
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/sum.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/sum.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Guest addition did not reach the pending write.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "1.0\n" : "1.0\n7\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "before1.0", host.ReadText("/sum.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedGuestAllocationAfterCompensationRemainsTerminal(bool asynchronous)
    {
        const string source = """
class Finish:
 def __radd__(self,total):
  print(total)
  payload='x'*2000000
  return 7
try:print(sum([1e16,1.0,-1e16,Finish()]))
except MemoryError:print('caught')
print('after')
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(4, result.Failure?.Span?.Line);
            Assert.Equal("1.0\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
