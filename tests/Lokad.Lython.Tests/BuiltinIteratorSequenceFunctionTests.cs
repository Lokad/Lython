using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BuiltinIteratorSequenceFunctionTests
{
    [Fact]
    public void IteratorAndSequenceBuiltins_MatchPythonShapedCoreBehavior()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []

it = iter([1, 2])
values.append(str(next(it)))
values.append(str(next(it)))
values.append(str(next(it, 99)))
values.append(str(iter(it) is it))

source = [1, 2, 0, 3]
def take():
    return source.pop(0)

values.append(str(list(iter(take, 0))))

def double(x):
    return x * 2

def add(left, right):
    return left + right

def odd(x):
    return x % 2

values.append(str(list(map(double, [1, 2, 3]))))
values.append(str(list(map(add, [1, 2, 3], [10, 20]))))
values.append(str(list(filter(odd, [1, 2, 3, 4]))))
values.append(str(list(filter(None, [0, 1, False, True]))))

values.append(str(list(reversed([1, 2, 3]))))
values.append("".join(reversed("ab😀")))
values.append(str(list(reversed(bytes([1, 2, 3])))))

class Custom:
    def __reversed__(self):
        return iter([7, 8])

values.append(str(list(reversed(Custom()))))

s = slice(1, 5, 2)
values.append(str(s.start) + ":" + str(s.stop) + ":" + str(s.step))
values.append(str([0, 1, 2, 3, 4, 5][s]))
values.append("abcdef"[slice(2)])
values.append(str(slice(None)))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "1|2|99|True|[1, 2]|[2, 4, 6]|[11, 22]|[1, 3]|[1, True]|[3, 2, 1]|😀ba|[3, 2, 1]|[7, 8]|1:5:2|[1, 3]|ab|slice(None, None, None)",
            host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("iter(1)\n", "TypeError", "not iterable")]
    [InlineData("iter(1, 0)\n", "TypeError", "callable")]
    [InlineData("reversed(1)\n", "TypeError", "reversible")]
    [InlineData("map(1, [1])\n", "TypeError", "callable")]
    [InlineData("map(lambda x: x)\n", "TypeError", "at least one iterable")]
    [InlineData("filter(1, [1])\n", "TypeError", "callable or None")]
    [InlineData("slice()\n", "TypeError", "expects one to three arguments")]
    [InlineData("[1, 2][slice(0, 2, 0)]\n", "ValueError", "slice step cannot be zero")]
    public void IteratorAndSequenceBuiltins_ReportExplicitFailures(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
