using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TupleIntegerTransportTests
{
    private const string RetainedSource = """
        def make(seed): return (seed+1,seed+2)
        t=make(10000)
        alias=t[0]
        with open('data.txt') as source:
            assert source.read()=='1'
        assert alias is t[0]
        return t
        """;

    [Fact]
    public async Task RetainedIntegerTuplesKeepFundingAfterCompilationEnds()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(RetainedSource, budget))
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/data.txt", "1");
            using (var first = script.Run(immediate))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var retained = budget.ReservedBytes;
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/data.txt", "1");
                using (var second = await script.RunAsync(delayed))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.True(delayed.CompletedAsynchronously > 0);
                    Assert.True(budget.ReservedBytes > retained);
                    script.Dispose();
                    Assert.True(budget.ReservedBytes > 0);
                    Assert.Equal(new System.Numerics.BigInteger(10001), Assert.IsType<object?[]>(first.ReturnValue)[0]);
                    Assert.Equal(new System.Numerics.BigInteger(10002), Assert.IsType<object?[]>(second.ReturnValue)[1]);
                }
                Assert.True(budget.ReservedBytes > 0);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationKeepsTupleUsersFundedUntilIoCleanupFinishes()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(RetainedSource, budget))
        {
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/data.txt", "1");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/data.txt");
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return ["""
            def make(seed): return (seed+1, seed+2, seed+3)
            t=make(10000)
            first=t[0]
            again=t[0]
            assert first is again and first is t[-3]
            assert t[0]+t[1]==20003
            assert first is t[0]
            alias=(t[0],t[0])
            assert alias[0] is alias[1] and alias[0] is first
            def observe(value=first): return value
            assert observe() is first
            print('ok')
            """];
        yield return ["""
            import copy
            from collections import namedtuple
            def make(seed): return (seed+1,seed+2,seed+3)
            t=make(10000)
            derived=[tuple(t),t[:],t[::-1][::-1],t+(),()+t,(t*2)[:3],copy.copy(t),copy.deepcopy(t)]
            for value in derived:
                assert value==t and value[0] is t[0]
                assert hash(value)==hash(t)
            assert repr(t)=='(10001, 10002, 10003)'
            assert list(t)==[10001,10002,10003]
            Triple=namedtuple('Triple','a b c')
            named=Triple(*t)
            assert named.a is t[0] and named==t
            print('ok')
            """];
        yield return ["""
            def make(seed): return (seed+1,seed+2)
            t=make(9223372036854775805)
            assert t[0]+2==9223372036854775808
            assert t[-1]*-2==-18446744073709551614
            events=[]
            class Index:
                def __index__(self):
                    events.append('index')
                    return -1
            assert t[Index()] is t[-1]
            try: t[3]
            except IndexError: events.append('bounds')
            try: t['bad']
            except TypeError: events.append('type')
            assert events==['index','bounds','type']
            print('ok')
            """];
        yield return ["""
            def make(seed): return (seed+1,seed+2)
            def observe(t):
                value=t[0]
                with open('data.txt') as source:
                    assert int(source.read())==1
                assert value is t[0]
                return value
            t=make(10000)
            assert observe(t) is t[0]
            def values():
                yield t[0]
                yield t[0]
            first,second=values()
            assert first is second and first is t[0]
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task IntegerTupleObservationPreservesPythonIdentityAndCleanup(string source)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/data.txt", "1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/data.txt", "1");
            using (var result = script.Run(immediate))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            if (source.Contains("open('data.txt')", StringComparison.Ordinal))
            {
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.Equal(1, delayed.ClosedFileCount);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
