using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerBinarySemanticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignedArithmeticProtocolsAndAccumulatorsKeepPythonSemantics(bool asynchronous)
    {
        const string source = """
            import math
            from collections import Counter
            big=2**100+17
            for left in [-big,-7,-1,0,1,7,big]:
                for right in [-big,-3,-1,1,3,big]:
                    quotient=left//right
                    remainder=left%right
                    assert left==quotient*right+remainder
                    assert abs(remainder)<abs(right)
                    assert remainder==0 or (remainder>0)==(right>0)
                    assert (left-right)+right==left
            assert True*big==big
            assert False-big==-big
            assert big//True==big
            assert big%True==0
            assert 7.0//-3==-3.0 and 7.0%-3==-2.0
            assert math.prod([big,3])==big*3
            assert math.sumprod([big,3],[3,big])==big*6
            assert (Counter(x=big)-Counter(x=3))['x']==big-3
            events=[]
            class Reflected:
                def __rsub__(self,left):
                    events.append('sub')
                    return 11
                def __rmul__(self,left):
                    events.append('mul')
                    return 12
                def __rfloordiv__(self,left):
                    events.append('floor')
                    return 13
                def __rmod__(self,left):
                    events.append('mod')
                    return 14
            value=Reflected()
            assert (7-value,7*value,7//value,7%value)==(11,12,13,14)
            assert events==['sub','mul','floor','mod']
            values=[7]
            def index():
                events.append('index')
                return 0
            def operand():
                events.append('operand')
                return value
            values[index()]*=operand()
            assert values==[12]
            assert events[-3:]==['index','operand','mul']
            for operation in [lambda:big//0,lambda:big%0]:
                try:operation()
                except ZeroDivisionError:pass
                else:assert False
            return big*3,big*3
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var pass = 0; pass < 2; pass++)
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                    Assert.Equal("3802951800684688204490109616179", values[0]?.ToString());
                    Assert.Equal(values[0], values[1]);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
