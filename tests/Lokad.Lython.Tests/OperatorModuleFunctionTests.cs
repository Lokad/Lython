using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OperatorModuleFunctionTests
{
    [Fact]
    public void OperatorModule_IndexAndLengthHint_DispatchUserProtocols()
    {
        var result = new LythonEngine().Run(
            """
import operator

class Indexed:
    def __index__(self):
        return 7

class Hinted:
    def __length_hint__(self):
        return 9

class SizedAndHinted:
    def __len__(self):
        return 3
    def __length_hint__(self):
        return 99

print(operator.index(Indexed()))
print(operator.length_hint(Hinted()))
print(operator.length_hint(SizedAndHinted()))
print(operator.length_hint(object(), 5))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7\n9\n3\n5\n", result.StandardOutput);
    }

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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|3|12|4.0|True|True|True|True|True|True|20|2|True|('y', 'x')|7|12", host.ReadText("/out.txt"));
    }

    [Fact]
    public void OperatorModule_ExpandedHelpers_MatchDirectSyntax()
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
    def bump(self, delta=0):
        return self.inner.value + delta

def combine(a, b=0, scale=1):
    return (a + b) * scale

box = Box(7)
items = [1, 2, 2]
vals = []
vals.append(str(operator.truth([1])))
vals.append(str(operator.not_([])))
vals.append(str(operator.is_(box, box)))
vals.append(str(operator.is_not(box, Box(7))))
vals.append(str(operator.abs(-5)))
vals.append(str(operator.neg(4)))
vals.append(str(operator.pos(-4)))
vals.append(str(operator.invert(2)))
vals.append(str(operator.index(True)))
vals.append(str(operator.floordiv(7, 2)))
vals.append(str(operator.mod(7, 2)))
vals.append(str(operator.pow(2, 5)))
vals.append(str(operator.lshift(3, 2)))
vals.append(str(operator.rshift(8, 1)))
vals.append(str(operator.and_(6, 3)))
vals.append(str(operator.or_(4, 1)))
vals.append(str(operator.xor(6, 3)))
vals.append(str(operator.concat([1], [2])))
vals.append(operator.concat("a", "b"))
vals.append(str(operator.length_hint(items)))
vals.append(str(operator.length_hint(99, 5)))
vals.append(str(operator.countOf(items, 2)))
vals.append(str(operator.indexOf(items, 2)))
operator.delitem(items, 0)
operator.setitem(items, 1, 5)
same = operator.iadd(items, [9])
vals.append(str(same is items) + ":" + str(items))
vals.append(str(operator.imul([1], 3)))
vals.append(str(operator.ifloordiv(7, 2)))
vals.append(str(operator.iand(6, 3)))
vals.append(str(operator.iconcat([1], [2, 3])))
vals.append(str(operator.call(combine, 2, scale=4, b=3)))
vals.append(str(operator.call(operator.add, 4, 5)))
vals.append(str(operator.itemgetter("a", "b")({"a": 1, "b": 2})))
vals.append(str(operator.attrgetter("inner.value", "inner.value")(box)))
vals.append(str(operator.methodcaller("bump", delta=6)(box)))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "True|True|True|True|5|-4|-4|-3|1|3|1|32|12|4|2|5|5|[1, 2]|ab|3|5|2|1|True:[2, 5, 9]|[1, 1, 1]|3|2|[1, 2, 3]|20|9|(1, 2)|(7, 7)|13",
            host.ReadText("/out.txt"));
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
    [InlineData(
        """
import operator
operator.delitem((1, 2), 0)
""",
        "TypeError",
        "Tuple does not support item deletion")]
    [InlineData(
        """
import operator
operator.matmul(1, 2)
""",
        "TypeError",
        "matrix multiplication")]
    [InlineData(
        """
import operator
operator.concat(1, 2)
""",
        "TypeError",
        "sequence")]
    [InlineData(
        """
import operator
operator.length_hint(1, -1)
""",
        "ValueError",
        "non-negative")]
    [InlineData(
        """
import operator
operator.indexOf([1], 2)
""",
        "ValueError",
        "not in sequence")]
    [InlineData(
        """
import operator
operator.call(1)
""",
        "TypeError",
        "callable")]
    [InlineData(
        """
import operator
operator.attrgetter("a..b")
""",
        "TypeError",
        "non-empty")]
    public void OperatorModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure.RequireNotNull().ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
