using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinObjectExceptionFunctionTests
{
    [Fact]
    public void BuiltinTypesAndLocalNamespacesAreInspectable()
    {
        var result = new LythonEngine().Run(
            """
x = 3
names = dir()
local_values = vars()
return type(1).__name__ + "|" + str(issubclass(bool, int)) + "|" + str(issubclass(int, object)) + "|" + str("x" in names) + "|" + str(local_values["x"]) + "|" + str("upper" in dir("x")) + "|" + str("append" in dir([]))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("int|True|True|True|3|True|True", result.ReturnValue);
    }

    [Fact]
    public void GlobalsAndLocalsExposeScopeNamespaces()
    {
        var result = new LythonEngine().Run(
            """
x = 1
def f(a, b=2):
    y = a + b
    return (sorted(globals().keys()), sorted(locals().keys()))
g = sorted(globals().keys())
l = sorted(locals().keys())
fg, fl = f(10)
return "|".join([";".join(g), ";".join(l), ";".join(fg), ";".join(fl)])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("__name__;f;x|__name__;f;g;x|__name__;f;g;l;x|a;b;y", result.ReturnValue);
    }

    [Fact]
    public void IdExposesStablePerRunObjectIdentity()
    {
        var result = new LythonEngine().Run(
            """
x = object()
y = object()
return str(isinstance(id(x), int)) + "|" + str(id(x) == id(x)) + "|" + str(id(x) != id(y)) + "|" + str(id(None) == id(None))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True", result.ReturnValue);
    }

    [Fact]
    public void IdIsDeterministicAcrossRuns()
    {
        const string source = "x = object()\nreturn str(id(x))\n";
        var first = new LythonEngine().Run(source, new MockLythonHost());
        var second = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(first.ReturnValue, second.ReturnValue);
    }

    [Fact]
    public void DictViewsExposeDirNames()
    {
        var result = new LythonEngine().Run(
            "return str(dir({1: 2}.keys())) + \"|\" + str(dir({1: 2}.items())) + \"|\" + str(dir({1: 2}.values())) + \"|\" + str(\"isdisjoint\" in dir({}.keys()))\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['isdisjoint']|['isdisjoint']|[]|True", result.ReturnValue);
    }

    [Fact]
    public void InstancesDispatchTruthLengthAndCallProtocols()
    {
        var result = new LythonEngine().Run(
            """
class Empty:
    def __bool__(self):
        return False

class Sized:
    def __len__(self):
        return 0

class AddOne:
    def __call__(self, value):
        return value + 1

f = AddOne()
return str(bool(Empty())) + "|" + str(bool(Sized())) + "|" + str(len(Sized())) + "|" + str(callable(f)) + "|" + str(callable(Empty())) + "|" + str(f(3))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("False|False|0|True|False|4", result.ReturnValue);
    }

    [Fact]
    public void InstancesDispatchIterationContainmentAndSubscriptionProtocols()
    {
        var result = new LythonEngine().Run(
            """
class Counter:
    def __init__(self):
        self.value = 0
    def __iter__(self):
        return self
    def __next__(self):
        if self.value == 3:
            raise StopIteration()
        value = self.value
        self.value += 1
        return value

class Box:
    def __init__(self):
        self.value = 0
    def __contains__(self, value):
        return value == 3
    def __getitem__(self, key):
        return key + 10 + self.value
    def __setitem__(self, key, value):
        self.value = key + value
    def __delitem__(self, key):
        self.value = -key

box = Box()
box[2] = 3
assigned = box[2]
del box[4]
return str(list(Counter())) + "|" + str(3 in box) + "|" + str(assigned) + "|" + str(box[2])
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 1, 2]|True|17|8", result.ReturnValue);
    }

    [Fact]
    public void InstancesAndContainersDispatchPythonRenderingProtocols()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    def __repr__(self):
        return "Box!"
    def __str__(self):
        return "box"
    def __format__(self, spec):
        return "formatted:" + spec

box = Box()
return repr(box) + "|" + str(box) + "|" + format(box, "x") + "|" + str(["a", box]) + "|" + str({"k": "v"})
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Box!|box|formatted:x|['a', Box!]|{'k': 'v'}", result.ReturnValue);
    }

    [Fact]
    public void InstancesDispatchRichComparisonAndHashProtocols()
    {
        var result = new LythonEngine().Run(
            """
class Box:
    def __init__(self, value):
        self.value = value
    def __eq__(self, other):
        return self.value == other
    def __lt__(self, other):
        return self.value < other
    def __gt__(self, other):
        return self.value > other
    def __hash__(self):
        return 7

x = Box(3)
return str(x == 3) + "|" + str(x != 4) + "|" + str(x < 4) + "|" + str(2 < x) + "|" + str(hash(x))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|True|True|7", result.ReturnValue);
    }

    [Fact]
    public void InstancesDispatchNumericConversionAndOperatorProtocols()
    {
        var result = new LythonEngine().Run(
            """
class Seven:
    def __int__(self):
        return 7
    def __float__(self):
        return 7.5
    def __index__(self):
        return 1
    def __neg__(self):
        return -7
    def __abs__(self):
        return 7
    def __add__(self, other):
        return 7 + other
    def __radd__(self, other):
        return other + 7
    def __iadd__(self, other):
        return 70 + other

x = Seven()
left = x + 2
right = 2 + x
x += 3
return str(int(Seven())) + "|" + str(float(Seven())) + "|" + str([10, 20][Seven()]) + "|" + str(-Seven()) + "|" + str(abs(Seven())) + "|" + str(left) + "|" + str(right) + "|" + str(x)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7|7.5|20|-7|7|9|9|73", result.ReturnValue);
    }

    [Fact]
    public void ExceptionsExposePythonArgsTextAndClearHandlerAliases()
    {
        var result = new LythonEngine().Run(
            """
e = ValueError("bad", 3)
values = [str(e), repr(e), str(e.args), str(hasattr(e, "message"))]
try:
    raise e
except ValueError as error:
    values.append("caught")
try:
    error
except NameError:
    values.append("missing")
return "|".join(values)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("('bad', 3)|ValueError('bad', 3)|('bad', 3)|False|caught|missing", result.ReturnValue);
    }

    [Fact]
    public async Task RaisedExceptionArgsSurviveHandlerRewrap()
    {
        const string source = """
            values = []
            try:
                raise ValueError("x", 1)
            except ValueError as e:
                values.append(str(e.args))
                values.append(repr(e))
            try:
                raise ValueError(("x", 1))
            except ValueError as e:
                values.append(str(e.args))
            try:
                raise ValueError("solo")
            except ValueError as e:
                values.append(str(e.args))
            try:
                raise ValueError()
            except ValueError as e:
                values.append(str(e.args))
            try:
                raise ValueError("x", 1)
            except ValueError as e:
                e.args = (9,)
                values.append(str(e.args))
                try:
                    raise e
                except ValueError as e2:
                    values.append(str(e2.args))
            return "|".join(values)
            """;
        const string expected = "('x', 1)|ValueError('x', 1)|(('x', 1),)|('solo',)|()|(9,)|(9,)";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }







    [Fact]
    public void ObjectHelpersAndExceptionCategories_MatchPythonShapedCoreBehavior()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
values = []

class Box:
    def __init__(self):
        self.value = 3

    def method(self):
        return self.value

box = Box()
values.append(str(getattr(box, "value")))
setattr(box, "name", "alpha")
values.append(getattr(box, "name"))
values.append(str(vars(box)["value"]))
values.append(str("value" in dir(box)))
values.append(str("method" in dir(box)))
values.append(str("__class__" in dir(box)))
values.append(str(hasattr(box, "missing")))
values.append(str(getattr(box, "missing", 42)))
delattr(box, "name")
values.append(str(hasattr(box, "name")))

try:
    raise ModuleNotFoundError("missing")
except ImportError as ex:
    values.append(ex.type)

try:
    raise KeyError("k")
except LookupError as ex:
    values.append(ex.type)

try:
    1 / 0
except ArithmeticError as ex:
    values.append(ex.type)

try:
    raise UnicodeDecodeError("bad")
except ValueError as ex:
    values.append(ex.type)

try:
    raise FileNotFoundError("path")
except IOError as ex:
    values.append(ex.type)

try:
    raise SystemExit(5)
except BaseException as ex:
    values.append(ex.type + ":" + str(ex.code))

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(values))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(
            "3|alpha|3|True|True|True|False|42|False|ModuleNotFoundError|KeyError|ZeroDivisionError|UnicodeDecodeError|FileNotFoundError|SystemExit:5",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void SystemExit_IsNotCaughtByExceptionCategory()
    {
        var result = new LythonEngine().Run(
            """
try:
    raise SystemExit(5)
except Exception:
    pass
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("SystemExit", result.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData("getattr(object(), 1)\n", "TypeError", "name")]
    [InlineData("getattr(object(), 'missing')\n", "AttributeError", "missing")]
    [InlineData("setattr(1, 'x', 2)\n", "AttributeError", "no __dict__ for setting new attributes")]
    [InlineData("delattr(object(), 'x')\n", "AttributeError", "x")]
    [InlineData("vars(1)\n", "TypeError", "must have __dict__ attribute")]
    public void ObjectHelpers_ReportExplicitFailures(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task NoneComparisonDundersAdvanceLikeCpython()
    {
        // None slots take None only and decline everything else with
        // NotImplemented on every dunder, ordering included, exactly like
        // CPython; unrelated members keep the house AttributeError.
        var script = new LythonEngine().Compile("""
def call2(f, a, b):
    return f(a, b)
results = []
results.append(str(None.__eq__(None)))
results.append(str(None.__ne__(None)))
results.append(str(None.__eq__(False)))
results.append(str(None.__ne__(0)))
results.append(str(None.__lt__(None)))
results.append(str(None.__le__(1)))
results.append(str(None.__gt__(None)))
results.append(str(None.__ge__("x")))
try:
    None.foo
except AttributeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
results.append(str(None == None))
results.append(str(None != None))
results.append(str(None == 1))
results.append(str(None != 1))
for (f, a, b) in [(None.__eq__, 1, 2), (None.__ne__, 1, 2), (None.__lt__, 1, 2), (None.__le__, 1, 2), (None.__gt__, 1, 2), (None.__ge__, 1, 2)]:
    try:
        call2(f, a, b)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(None, "__eq__")))
results.append(str(hasattr(None, "__lt__")))
results.append(str(hasattr(None, "__rlt__")))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "False",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "NotImplemented",
            "AttributeError",
            "'NoneType' object has no attribute 'foo'",
            "True",
            "False",
            "False",
            "True",
            "TypeError",
            "Method 'None.__eq__' received too many positional arguments.",
            "TypeError",
            "Method 'None.__ne__' received too many positional arguments.",
            "TypeError",
            "Method 'None.__lt__' received too many positional arguments.",
            "TypeError",
            "Method 'None.__le__' received too many positional arguments.",
            "TypeError",
            "Method 'None.__gt__' received too many positional arguments.",
            "TypeError",
            "Method 'None.__ge__' received too many positional arguments.",
            "True",
            "True",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    [Fact]
    public async Task BoolDundersAdvanceLikeCpython()
    {
        // __bool__ members delegate to each type truthiness core (ints by
        // nonzero, floats including nan, ranges and Decimals by their own
        // IsTruthy), exactly like CPython.
        var script = new LythonEngine().Compile("""
from decimal import Decimal
def call1(f, a):
    return f(a)
results = []
results.append(str((1).__bool__()))
results.append(str((0).__bool__()))
results.append(str((-5).__bool__()))
results.append(str(True.__bool__()))
results.append(str(False.__bool__()))
results.append(str((10 ** 30).__bool__()))
results.append(str((0.0).__bool__()))
results.append(str((-0.0).__bool__()))
results.append(str(float("nan").__bool__()))
results.append(str((2.5).__bool__()))
results.append(str(None.__bool__()))
results.append(str(range(0).__bool__()))
results.append(str(range(5).__bool__()))
results.append(str(range(5, 0).__bool__()))
results.append(str(Decimal("0").__bool__()))
results.append(str(Decimal("1.5").__bool__()))
results.append(str(Decimal("-0.0").__bool__()))
for f in [(1).__bool__, (1.0).__bool__, None.__bool__, range(3).__bool__, Decimal("1").__bool__]:
    try:
        call1(f, 1)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(1, "__bool__")))
results.append(str(hasattr(1.0, "__bool__")))
results.append(str(hasattr(None, "__bool__")))
results.append(str(hasattr(range(3), "__bool__")))
results.append(str(hasattr("", "__bool__")))
results.append(str(hasattr([], "__bool__")))
results.append(str(bool(0)))
results.append(str(bool([])))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "False",
            "True",
            "True",
            "False",
            "True",
            "False",
            "False",
            "True",
            "True",
            "False",
            "False",
            "True",
            "False",
            "False",
            "True",
            "False",
            "TypeError",
            "int.__bool__() expects no arguments.",
            "TypeError",
            "float.__bool__() expects no arguments.",
            "TypeError",
            "None.__bool__() expects no arguments.",
            "TypeError",
            "range.__bool__() expects no arguments.",
            "TypeError",
            "Decimal.__bool__() expects no arguments.",
            "True",
            "True",
            "True",
            "True",
            "False",
            "False",
            "False",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    [Fact]
    public async Task HashDundersAdvanceLikeCpython()
    {
        // hashable __hash__ members funnel through the same core as the hash builtin (self-consistency only, since house hashes stay CLR-shaped), with range hashing made equality-consistent.
        var script = new LythonEngine().Compile("""
from decimal import Decimal
def call1(f, a):
    return f(a)
results = []
results.append(str((5).__hash__() == hash(5)))
results.append(str((-3).__hash__() == hash(-3)))
results.append(str((2 ** 100).__hash__() == hash(2 ** 100)))
results.append(str(True.__hash__() == hash(True)))
results.append(str((1.5).__hash__() == hash(1.5)))
results.append(str(None.__hash__() == hash(None)))
results.append(str("ab".__hash__() == hash("ab")))
results.append(str(b"ab".__hash__() == hash(b"ab")))
results.append(str((1, 2).__hash__() == hash((1, 2))))
results.append(str(().__hash__() == hash(())))
results.append(str((1, (2, 3)).__hash__() == hash((1, (2, 3)))))
results.append(str(Decimal("1.5").__hash__() == hash(Decimal("1.5"))))
results.append(str(Decimal("0").__hash__() == hash(Decimal("0"))))
results.append(str(range(3).__hash__() == hash(range(3))))
results.append(str(range(0, 1).__hash__() == hash(range(0, 1, 2))))
results.append(str(range(5, 5).__hash__() == hash(range(0, 0))))
results.append(str(len({range(3), range(3)})))
results.append(str({range(0, 1): "x"}[range(0, 1, 2)]))
try:
    (1, [2]).__hash__()
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
try:
    hash((1, [2]))
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
for f in [(5).__hash__, (1.5).__hash__, "ab".__hash__, b"ab".__hash__, (1, 2).__hash__, None.__hash__, range(3).__hash__, Decimal("1").__hash__]:
    try:
        call1(f, 1)
    except TypeError as e:
        results.append(type(e).__name__)
        results.append(str(e))
results.append(str(hasattr(5, "__hash__")))
results.append(str(hasattr("ab", "__hash__")))
results.append(str(hasattr((1,), "__hash__")))
results.append(str(hasattr(None, "__hash__")))
results.append(str(hasattr(range(3), "__hash__")))
results.append(str(hasattr(b"ab", "__hash__")))
results.append(str(hasattr(5, "__rhash__")))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "1",
            "x",
            "TypeError",
            "unhashable type: 'tuple'",
            "TypeError",
            "unhashable type: 'tuple'",
            "TypeError",
            "int.__hash__() expects no arguments.",
            "TypeError",
            "float.__hash__() expects no arguments.",
            "TypeError",
            "str.__hash__() expects no arguments.",
            "TypeError",
            "bytes.__hash__() expects no arguments.",
            "TypeError",
            "tuple.__hash__() expects no arguments.",
            "TypeError",
            "None.__hash__() expects no arguments.",
            "TypeError",
            "range.__hash__() expects no arguments.",
            "TypeError",
            "Decimal.__hash__() expects no arguments.",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task UnhashableHashIsNoneLikeCpython()
    {
        // unhashable containers and views serve __hash__ as None like CPython, so hasattr holds and calls fail with the house not-callable error.
        var script = new LythonEngine().Compile("""
from collections import defaultdict, Counter, deque, ChainMap
results = []
results.append(str([].__hash__))
results.append(str({}.__hash__))
results.append(str(set().__hash__))
results.append(str(hasattr([1], "__hash__")))
results.append(str(hasattr({}, "__hash__")))
results.append(str(hasattr({1}, "__hash__")))
results.append(str(hasattr(defaultdict(list), "__hash__")))
results.append(str(hasattr(Counter(), "__hash__")))
results.append(str(hasattr(deque(), "__hash__")))
results.append(str(hasattr(ChainMap({}), "__hash__")))
d = {}
cm = ChainMap({})
results.append(str(hasattr(d.keys(), "__hash__")))
results.append(str(hasattr(d.values(), "__hash__")))
results.append(str(hasattr(d.items(), "__hash__")))
results.append(str(hasattr(cm.keys(), "__hash__")))
results.append(str(hasattr(cm.values(), "__hash__")))
results.append(str(hasattr(cm.items(), "__hash__")))
try:
    [].__hash__()
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
try:
    {}.__hash__()
except TypeError as e:
    results.append(type(e).__name__)
    results.append(str(e))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "None",
            "None",
            "None",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "True",
            "TypeError",
            "Object is not callable.",
            "TypeError",
            "Object is not callable.",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    [Fact]
    public async Task ExceptionHashDundersAdvanceLikeCpython()
    {
        // Exceptions hash by identity through the shared core, so direct
        // calls agree with hash() and set/dict behavior, exactly like
        // CPython; mutation never moves a live key.
        var script = new LythonEngine().Compile("""
def call1(f, a):
    return f(a)
results = []
e = ValueError("x")
results.append(str(e.__hash__() == hash(e)))
results.append(str(hasattr(e, "__hash__")))
results.append(str(len({e, e})))
d = {}
d[e] = 1
results.append(str(d[e]))
results.append(str(e == e))
e1 = ValueError("x")
e2 = ValueError("x")
results.append(str(len({e1, e2})))
results.append(str(e1 == e2))
eu = ValueError([1])
results.append(str(eu.__hash__() == hash(eu)))
results.append(str(len({eu})))
em = ValueError("x")
h1 = hash(em)
em.args = ("y",)
results.append(str(hash(em) == h1))
results.append(str(em == em))
try:
    raise KeyError("k")
except KeyError as e:
    results.append(str(e.__hash__() == hash(e)))
    results.append(str(hasattr(e, "__hash__")))
try:
    em.__hash__(1)
except TypeError as ex:
    results.append(type(ex).__name__)
    results.append(str(ex))
results.append(str(hasattr(em, "__rhash__")))
results.append(str(hasattr(em, "__len__")))
return results
""");
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "True",
            "True",
            "1",
            "1",
            "True",
            "2",
            "False",
            "True",
            "1",
            "True",
            "True",
            "True",
            "True",
            "TypeError",
            "BaseException.__hash__() expects no arguments.",
            "False",
            "False",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
