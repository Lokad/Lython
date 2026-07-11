using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

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
        Assert.Equal("SystemExit", result.Failure!.ExceptionType);
    }

    [Theory]
    [InlineData("getattr(object(), 1)\n", "TypeError", "name")]
    [InlineData("getattr(object(), 'missing')\n", "AttributeError", "missing")]
    [InlineData("setattr(1, 'x', 2)\n", "AttributeError", "writable")]
    [InlineData("delattr(object(), 'x')\n", "AttributeError", "x")]
    [InlineData("vars(1)\n", "TypeError", "attribute dictionary")]
    public void ObjectHelpers_ReportExplicitFailures(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure!.ExceptionType);
        Assert.Contains(messageFragment, result.Failure.Message, StringComparison.Ordinal);
    }
}
