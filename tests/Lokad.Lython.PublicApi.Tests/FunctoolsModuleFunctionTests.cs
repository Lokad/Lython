using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("5|5|10|base|True|plus_two|6|[3, 2, 1]|True|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void FunctoolsModule_ExpandedHelpers_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
from functools import (
    WRAPPER_ASSIGNMENTS,
    WRAPPER_UPDATES,
    Placeholder,
    cache,
    cached_property,
    lru_cache,
    partial,
    partialmethod,
    recursive_repr,
    singledispatch,
    singledispatchmethod,
    update_wrapper,
    wraps,
)

vals = []
vals.append(str("__name__" in WRAPPER_ASSIGNMENTS))
vals.append(str("__dict__" in WRAPPER_UPDATES))

def base():
    return 1
base.custom = "copied"

def wrapper():
    return 2

update_wrapper(wrapper, base, assigned=("__name__", "custom"), updated=())
vals.append(wrapper.__name__)
vals.append(wrapper.custom)
vals.append(str(wrapper.__wrapped__ is base))

def decorate(fn):
    @wraps(fn, assigned=("custom",), updated=())
    def inner():
        return 3
    return inner

wrapped = decorate(base)
vals.append(wrapped.custom)
vals.append(str(wrapped.__wrapped__ is base))

calls = []
@lru_cache(maxsize=2, typed=True)
def plus_ten(x):
    calls.append(x)
    return x + 10

vals.append(str(plus_ten(1)))
vals.append(str(plus_ten(1)))
vals.append(str(plus_ten(True)))
info = plus_ten.cache_info()
vals.append(str(info.hits) + "/" + str(info.misses) + "/" + str(info.maxsize) + "/" + str(info.currsize))
params = plus_ten.cache_parameters()
vals.append(str(params["typed"]) + "/" + str(params["maxsize"]))
plus_ten.cache_clear()
vals.append(str(plus_ten.cache_info().currsize))

cached_calls = []
@cache
def twice(x):
    cached_calls.append(x)
    return x * 2
vals.append(str(twice(4)))
vals.append(str(twice(4)))
vals.append(str(len(cached_calls)))

class Box:
    def __init__(self):
        self.calls = 0
    @cached_property
    def value(self):
        self.calls += 1
        return 7

box = Box()
vals.append(str(box.value))
vals.append(str(box.value))
vals.append(str(box.calls))
box.value = 9
vals.append(str(box.value))

def combine(a, b=0, c=0):
    return a * 100 + b * 10 + c
p = partial(combine, 1, b=2)
vals.append(str(p(c=3)))
vals.append(str(p(b=4, c=5)))
vals.append(str("functools.partial" in repr(p)))

class Adder:
    def add(self, a, b, c=0):
        return a + b + c
    inc = partialmethod(add, 1, c=3)
vals.append(str(Adder().inc(2)))

@singledispatch
def describe(value):
    return "object"

@describe.register(int)
def _(value):
    return "int"

describe.register(str, lambda value: "str")
vals.append(describe(1))
vals.append(describe("x"))
vals.append(describe([1]))
vals.append(describe.dispatch(int)(5))

class Handler:
    @singledispatchmethod
    def handle(self, value):
        return "object"
    @handle.register(int)
    def _(self, value):
        return "int" + str(value)

handler = Handler()
vals.append(handler.handle(3))
vals.append(handler.handle("x"))

@recursive_repr(fillvalue="<rec>")
def show(value):
    return show(value)
vals.append(show(object()))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "True|True|base|copied|True|copied|True|11|11|11|1/2/2/2|True/2|0|8|8|1|7|7|1|9|123|145|True|6|int|str|object|int|int3|object|<rec>",
            host.ReadText("/out.txt"));
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
    [InlineData(
        """
from functools import partial, Placeholder
def add(a, b):
    return a + b
partial(add, Placeholder, 1)
""",
        "NotImplementedError",
        "Placeholder")]
    [InlineData(
        """
from functools import lru_cache
@lru_cache(maxsize="many")
def f(x):
    return x
""",
        "TypeError",
        "maxsize")]
    [InlineData(
        """
from functools import singledispatch
@singledispatch
def f(x):
    return x
f.register(1, f)
""",
        "TypeError",
        "supported class")]
    public void FunctoolsModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SingleDispatch_SelectsNearestRegisteredMroType()
    {
        var result = new LythonEngine().Run(
            """
from functools import singledispatch

class Base: pass
class Child(Base): pass

@singledispatch
def f(value): return "object"

@f.register(Child)
def child(value): return "child"

@f.register(Base)
def base(value): return "base"

return f(Child()) + "|" + f.dispatch(Child)(Child())
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("child|child", result.ReturnValue);
    }

    [Fact]
    public void LruCache_MaintainsConstantTimeRecencyAndSkipsItWhenUnbounded()
    {
        var result = new LythonEngine().Run(
            """
from functools import cache, lru_cache

bounded_calls = []
@lru_cache(maxsize=2)
def bounded(value):
    bounded_calls.append(value)
    return value

bounded(1)
bounded(2)
bounded(1)
bounded(3)
bounded(1)
bounded(2)
bounded_info = bounded.cache_info()

unbounded_calls = []
@cache
def unbounded(value):
    unbounded_calls.append(value)
    return value * value

for value in range(1000):
    unbounded(value)
for value in range(1000):
    unbounded(value)
unbounded_info = unbounded.cache_info()

return "|".join([
    str(bounded_calls),
    str(bounded_info.hits), str(bounded_info.misses), str(bounded_info.currsize),
    str(len(unbounded_calls)),
    str(unbounded_info.hits), str(unbounded_info.misses), str(unbounded_info.currsize),
])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[1, 2, 3, 2]|2|4|2|1000|1000|1000|1000", result.ReturnValue);
    }

    [Fact]
    public void SingleDispatch_PublicDispatchUsesBuiltinMroResolution()
    {
        var result = new LythonEngine().Run(
            """
from functools import singledispatch

@singledispatch
def f(value): return "base"

@f.register(int)
def integer(value): return "int"

return f(True) + "|" + f.dispatch(bool)(True)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("int|int", result.ReturnValue);
    }

    [Fact]
    public void CmpToKey_WrappersExposeComparatorBackedRichComparisons()
    {
        var result = new LythonEngine().Run(
            """
from functools import cmp_to_key

key = cmp_to_key(lambda a, b: (a > b) - (a < b))
one = key(1)
two = key(2)
return "|".join([
    str(one < two), str(one <= two), str(one == key(1)),
    str(two > one), str(two >= one), str(one != two),
    str(one < two < key(3)),
])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|True|True|True", result.ReturnValue);
    }
}
