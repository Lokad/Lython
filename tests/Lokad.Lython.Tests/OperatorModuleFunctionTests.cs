using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OperatorModuleFunctionTests
{
    [Fact]
    public void OperatorModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import operator

class Inner:
    def __init__(self, value):
        self.value = value

class Box:
    def __init__(self, value):
        self.inner = Inner(value)
    def bump(self, delta):
        return self.inner.value + delta

box = Box(7)
vals = []
vals.append(str(operator.add(2, 3)))
vals.append(str(operator.sub(5, 2)))
vals.append(str(operator.mul(4, 3)))
vals.append(str(operator.truediv(8, 2)))
vals.append(str(operator.eq(1, 1)))
vals.append(str(operator.ne(1, 2)))
vals.append(str(operator.lt(1, 2)))
vals.append(str(operator.le(2, 2)))
vals.append(str(operator.gt(3, 2)))
vals.append(str(operator.ge(3, 3)))
vals.append(str(operator.getitem([10, 20], 1)))
data = {"a": 1}
operator.setitem(data, "b", 2)
vals.append(str(data["b"]))
vals.append(str(operator.contains([1, 2, 3], 2)))
vals.append(str(operator.itemgetter(1, 0)(["x", "y"])))
vals.append(str(operator.attrgetter("inner.value")(box)))
vals.append(str(operator.methodcaller("bump", 5)(box)))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|3|12|4|True|True|True|True|True|True|20|2|True|(y, x)|7|12", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
import operator
operator.attrgetter(1)
""",
        "TypeError",
        "string")]
    [InlineData(
        """
import operator
operator.methodcaller(1)
""",
        "TypeError",
        "method name string")]
    [InlineData(
        """
import operator
operator.setitem((1, 2), 0, 9)
""",
        "TypeError",
        "Tuple does not support item assignment")]
    public void OperatorModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
