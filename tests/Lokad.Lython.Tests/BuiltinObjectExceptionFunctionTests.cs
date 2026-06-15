using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BuiltinObjectExceptionFunctionTests
{
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
    [InlineData("dir()\n", "TypeError", "without an object")]
    [InlineData("dir(1)\n", "TypeError", "not supported")]
    [InlineData("vars()\n", "TypeError", "without an object")]
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
