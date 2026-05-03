using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctoolsModuleFunctionTests
{
    [Fact]
    public void FunctoolsModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from functools import cmp_to_key, partial, reduce, total_ordering, update_wrapper, wraps

def add(a, b):
    return a + b

inc = partial(add, 1)
override = partial(add, b=3)

def base():
    return 1

def wrapper():
    return 2

update_wrapper(wrapper, base)

def decorate(fn):
    @wraps(fn)
    def inner(value):
        return fn(value) + 1
    return inner

@decorate
def plus_two(value):
    return value + 2

def reverse_cmp(left, right):
    if left < right:
        return 1
    if left > right:
        return -1
    return 0

@total_ordering
class Score:
    def __init__(self, value):
        self.value = value
    def __eq__(self, other):
        return self.value == other.value
    def __lt__(self, other):
        return self.value < other.value

low = Score(1)
high = Score(3)

vals = []
vals.append(str(inc(4)))
vals.append(str(override(2)))
vals.append(str(reduce(add, [1, 2, 3], 4)))
vals.append(str(wrapper.__name__))
vals.append(str(wrapper.__wrapped__ is base))
vals.append(str(plus_two.__name__))
vals.append(str(plus_two(3)))
vals.append(str(sorted([1, 3, 2], key=cmp_to_key(reverse_cmp))))
vals.append(str(low.__le__(high)))
vals.append(str(high.__gt__(low)))
vals.append(str(high.__ge__(high)))
write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|5|10|base|True|plus_two|6|[3, 2, 1]|True|True|True", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(
        """
from functools import total_ordering
@total_ordering
class Bad:
    pass
""",
        "TypeError",
        "requires __eq__")]
    [InlineData(
        """
from functools import partial
partial(1)
""",
        "TypeError",
        "callable")]
    [InlineData(
        """
from functools import cmp_to_key
sorted([1, 2], key=cmp_to_key(lambda a, b: "oops"))
""",
        "TypeError",
        "must return an integer")]
    public void FunctoolsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
