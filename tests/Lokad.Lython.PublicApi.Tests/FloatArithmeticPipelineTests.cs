using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FloatArithmeticPipelineTests
{
    public static IEnumerable<object[]> Programs()
    {
        yield return ["""
            def identity(value):return value
            def compute(a,b):
                value=a-b
                alias=value
                assert alias is value and identity(value) is value
                pair=(value,value)
                values=[value,value]
                assert pair[0] is values[1] and pair[1] is alias
                value*=2.0
                assert alias==1.5 and value==3.0
                assert +alias is alias
                return value,pair,values
            assert compute(2.0,0.5)==(3.0,(1.5,1.5),[1.5,1.5])
            print('ok')
            """];
        yield return ["""
            import math
            def compute(a,b):
                negative=a*b
                assert math.copysign(1.0,negative)==-1.0
                assert math.copysign(1.0,-negative)==1.0
                assert math.isinf(1e308*1e308)
                assert math.isnan(float('inf')+float('-inf'))
                return negative
            assert math.copysign(1.0,compute(-0.0,2.0))==-1.0
            print('ok')
            """];
        yield return ["""
            def compute(a,b):
                difference=a-b
                return difference*difference
            assert sum(compute(value,0.5) for value in [0.0,1.0,2.0])==2.75
            def keep(value):
                result=value*2.0
                yield result
                yield result
            values=list(keep(1.25))
            assert values[0] is values[1] and values[0]==2.5
            print('ok')
            """];
        yield return ["""
            def compute(value):return value*1.0
            assert sum(compute(value) for value in [1e16,1.0,-1e16])==1.0
            assert sum((compute(value) for value in [1e16,1.0,-1e16]),True)==0.0
            class Finish:
                def __radd__(self,total):
                    assert total==1.0
                    return 7
            def values():
                for value in [1e16,1.0,-1e16]:yield compute(value)
                yield Finish()
            assert sum(values())==7
            print('ok')
            """];
        yield return ["""
            saved=[]
            def compute(a,b):
                result=a*b
                try:return result
                finally:saved.append(result)
            value=compute(1.5,2.0)
            assert value is saved[0]
            def closed(a,b):
                result=a-b
                def read():return result
                assert read() is result
                return read()
            assert closed(3.0,0.5)==2.5
            print('ok')
            """];
        yield return ["""
            def compute(value):
                result=value*2.0
                assert result+2==5.0 and result*True==3.0
                try:result+(1<<10000)
                except OverflowError:pass
                else:assert False
                try:result/0.0
                except ZeroDivisionError:pass
                else:assert False
                return result
            class Right:
                def __radd__(self,left):return (left,)
            result=compute(1.5)
            assert (result+Right())[0] is result
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task FloatArithmeticPreservesPublicationAndNumericSemantics(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var synchronous = script.Run(new MockLythonHost()))
            using (var asynchronous = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(synchronous.Success, synchronous.Failure?.Message);
                Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
                Assert.Equal("ok\n", synchronous.StandardOutput);
                Assert.Equal(synchronous.StandardOutput, asynchronous.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingIoKeepsValuesFundedAndCancellationJoinsCleanup(bool cancel)
    {
        const string source = """
            def compute(a,b):
                value=a-b
                alias=value
                pending=value*3.0
                with open('input.txt') as source:source.read()
                assert value is alias
                return value,value*2.0,pending
            return compute(3.0,0.5)
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "data");
            using (var retained = script.Run(immediate))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "data");
                using var cancellation = new CancellationTokenSource();
                var entered = cancel ? host.PauseReadUntilCancellation("/input.txt") : null;
                var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
                try
                {
                    if (entered is not null)
                    {
                        await entered.WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > held);
                        cancellation.Cancel();
                    }
                    using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(!cancel, result.Success);
                    if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    else Assert.True(host.CompletedAsynchronously > 0);
                }
                finally
                {
                    cancellation.Cancel();
                    using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                }
                Assert.Equal(new object[] { 2.5, 5.0, 7.5 }, Assert.IsAssignableFrom<IReadOnlyList<object>>(retained.ReturnValue));
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
