using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LoopLocalValueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedHelpersReleaseWorkingStorageWithinOneExecution(bool asynchronous)
    {
        const string source = """
            def fold(values):
                total=0
                for value in values:total+=value
                return total
            for ordinal in range(20000):result=fold((1,2,3))
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
                Assert.Equal(new System.Numerics.BigInteger(6), result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return ["""
            def fold(values):
                total=1
                for value in values:total+=value
                return (total+1)*2*9223372036854775807
            assert fold((1,))==55340232221128654842
            print('ok')
            """];
        yield return ["""
            def fold(values):
                total=9223372036854775806
                first=total
                snapshots=[]
                for value in values:
                    total+=value
                    snapshots.append((total,total))
                return first,total,snapshots
            first,total,items=fold((1,1,-1))
            assert first==9223372036854775806 and total==9223372036854775807
            assert [pair[0] for pair in items]==[9223372036854775807,9223372036854775808,9223372036854775807]
            assert all(pair[0] is pair[1] for pair in items)
            print('ok')
            """];
        yield return ["""
            def identity(value):return value
            def fold(values):
                total=0
                saved=total
                for value in values:
                    total=total+value
                    saved=identity(total)
                return saved,total
            a,b=fold((1,2,3))
            assert a is b and a==6
            print('ok')
            """];
        yield return ["""
            def fold(values,initial):
                total=initial
                original=total
                for value in values:total+=value
                return original,total
            a,b=fold((1,2),True)
            assert a is True and b==4 and type(b) is int
            a,b=fold((1,2),1.5)
            assert a==1.5 and b==4.5 and type(b) is float
            source=[]
            a,b=fold(([1],[2]),source)
            assert a is b and b is source and source==[1,2]
            print('ok')
            """];
        yield return ["""
            def fold(values):
                total=17
                for value in values:
                    total+=value
                    if value==2:continue
                    if value==3:break
                else:total*=2
                return total
            assert fold(())==34 and fold((1,2,3,4))==23
            print('ok')
            """];
        yield return ["""
            def fold(values,ready):
                if ready:total=0
                for value in values:total+=value
                return total
            for values in [(),(1,)]:
                try:fold(values,False)
                except UnboundLocalError:pass
                else:assert False
            print('ok')
            """];
        yield return ["""
            def fold(n):
                total=0
                while n>0:
                    total+=n
                    n-=1
                return n,total
            assert fold(10)==(0,55)
            print('ok')
            """];
        yield return ["""
            class Number:
                def __init__(self):self.value=0
                def __iadd__(self,value):
                    self.value+=value
                    return self
            def fold(values,initial):
                total=initial
                for value in values:total+=value
                return total
            original=Number()
            result=fold((1,2,3),original)
            assert result is original and result.value==6
            print('ok')
            """];
        yield return ["""
            def fold(values,inspect):
                total=0
                snapshots=[]
                for value in values:
                    total+=value
                    snapshots.append(inspect()['total'])
                return total,snapshots
            assert fold((1,2,3),locals)==(6,[1,3,6])
            print('ok')
            """];
        var parameters = string.Join(",", Enumerable.Range(0, 18).Select(i => "p" + i));
        var arguments = string.Join(",", Enumerable.Range(0, 18));
        yield return [$"""
            def fold(values,{parameters}):
                total=0
                for value in values:total+=value
                return total,p17
            assert fold((1,2,3),{arguments})==(6,17)
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LoopValuesPreservePythonBehaviorAndReleaseTheirOwner(string source)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var pass = 0; pass < 2; pass++)
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("ok\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
