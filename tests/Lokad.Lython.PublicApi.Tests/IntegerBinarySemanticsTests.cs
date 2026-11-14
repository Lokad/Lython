using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerBinarySemanticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MachineBoundaryArithmeticPromotesExactlyAndPreservesPublishedAliases(bool asynchronous)
    {
        const string source = """
            def calculate(left, right):
                addition = left + right
                aliases = [addition, addition]
                assert aliases[0] is aliases[1]
                difference = left - right
                product = left * right
                left += right
                assert left == addition
                if right:
                    return addition, difference, product, (left-right)//right, (left-right)%right
                return addition, difference, product
            return [calculate(left, right) for left in values for right in values]
            """;
        System.Numerics.BigInteger[] values = [
            (System.Numerics.BigInteger)long.MinValue - 1, long.MinValue, long.MinValue + 1,
            -4294967297L, -7, -1, 0, 1, 7, 4294967297L,
            long.MaxValue - 1, long.MaxValue, (System.Numerics.BigInteger)long.MaxValue + 1
        ];
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["values"] = values.Select(value => (object)value).ToArray() } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var rows = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                Assert.Equal(values.Length * values.Length, rows.Count);
                var index = 0;
                foreach (var left in values)
                foreach (var right in values)
                {
                    var row = Assert.IsAssignableFrom<IReadOnlyList<object?>>(rows[index++]);
                    Assert.Equal(left + right, row[0]);
                    Assert.Equal(left - right, row[1]);
                    Assert.Equal(left * right, row[2]);
                    Assert.Equal(right.IsZero ? 3 : 5, row.Count);
                    if (right.IsZero) continue;
                    var quotient = System.Numerics.BigInteger.DivRem(left, right, out var remainder);
                    if (!remainder.IsZero && (left.Sign < 0) != (right.Sign < 0)) { quotient--; remainder += right; }
                    Assert.Equal(quotient, row[3]);
                    Assert.Equal(remainder, row[4]);
                }
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

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
