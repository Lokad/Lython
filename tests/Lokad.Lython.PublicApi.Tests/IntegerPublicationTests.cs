using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerPublicationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedWideHelpersReleaseWorkingStorage(bool asynchronous)
    {
        var parameters = string.Join(",", Enumerable.Range(0, 18).Select(i => "p" + i));
        var arguments = string.Join(",", Enumerable.Range(0, 18));
        var source = $"""
            def fold({parameters}):
                total=p0+p17
                total*=2
                return total
            for ordinal in range(10000):result=fold({arguments})
            return result
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new System.Numerics.BigInteger(34), result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return ["""
            def identity(value):return value
            def fold(a,b):
                total=a+b
                alias=total
                first=identity(total)
                assert alias is first and first is total
                total+=1
                assert alias==5001 and total==5002
                return first,total
            assert fold(5000,1)==(5001,5002)
            print('ok')
            """];
        yield return ["""
            def identity(*args,**kwargs):return args,kwargs
            def fold(a,b):
                total=a+b
                args,kw=identity(total,total,total=total)
                assert args[0] is args[1] and args[0] is kw['total']
                assert args[0] is total
                return args[0]
            assert fold(5000,1)==5001
            print('ok')
            """];
        yield return ["""
            def fold(a,b):
                total=a+b
                pair=(total,total)
                items=[total,total]
                assert pair[0] is items[0] and items[0] is items[1]
                assert total is pair[1]
                return pair,items
            assert fold(5000,1)==((5001,5001),[5001,5001])
            print('ok')
            """];
        yield return ["""
            def fold(a,b):
                total=a+b
                return f'{total:06d}',[total+1,total-1],(total*2,total//2,total%2)
            assert fold(5000,1)==('005001',[5002,5000],(10002,2500,1))
            print('ok')
            """];
        yield return ["""
            def fold(a,b):
                total=a+b
                try:total//0
                except ZeroDivisionError:pass
                else:assert False
                return total,total is total
            assert fold(5000,1)==(5001,True)
            print('ok')
            """];
        yield return ["""
            def fold(a,b):
                total=a+b
                return (total+1)*2*9223372036854775807
            assert fold(1,1)==55340232221128654842
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ArithmeticValuesKeepIdentityAndPythonBehaviorAtObservation(string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        using var synchronous = script.Run(new MockLythonHost());
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(synchronous.Success, synchronous.Failure?.Message);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal("ok\n", synchronous.StandardOutput);
        Assert.Equal(synchronous.StandardOutput, asynchronous.StandardOutput);
    }
}
