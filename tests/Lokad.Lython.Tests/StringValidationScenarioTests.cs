using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringValidationScenarioTests
{
    [Theory]
    [InlineData("\"hello\".find()\n", "str.find(sub[, start[, end]]) expects one to three arguments.")]
    [InlineData("\"hello\".find(1)\n", "str.find(sub[, start[, end]]) expects one string argument plus optional integer bounds.")]
    [InlineData("\"hello\".replace(\"h\")\n", "str.replace(old, new[, count]) expects two or three arguments.")]
    [InlineData("\"hello\".replace(1, \"x\")\n", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.")]
    [InlineData("\"hello\".replace(\"h\", \"x\", \"1\")\n", "expects count to be an integer.")]
    [InlineData("\"hello\".startswith()\n", "str.startswith(prefix[, start[, end]]) expects one to three arguments.")]
    [InlineData("\"hello\".startswith(1)\n", "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.")]
    [InlineData("\"hello\".endswith()\n", "str.endswith(suffix[, start[, end]]) expects one to three arguments.")]
    [InlineData("\"hello\".endswith(1)\n", "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.")]
    [InlineData("\"hello\".capitalize(1)\n", "str.capitalize() expects no arguments.")]
    [InlineData("\"hello\".lower(1)\n", "str.lower() expects no arguments.")]
    [InlineData("\"hello\".islower(1)\n", "str.islower() expects no arguments.")]
    [InlineData("\"hello\".isupper(1)\n", "str.isupper() expects no arguments.")]
    [InlineData("\"hello\".isalpha(1)\n", "str.isalpha() expects no arguments.")]
    [InlineData("\"hello\".isdigit(1)\n", "str.isdigit() expects no arguments.")]
    [InlineData("\"hello\".isalnum(1)\n", "str.isalnum() expects no arguments.")]
    [InlineData("\"hello\".isspace(1)\n", "str.isspace() expects no arguments.")]
    [InlineData("\"hello\".upper(1)\n", "str.upper() expects no arguments.")]
    [InlineData("\"hello\".swapcase(1)\n", "str.swapcase() expects no arguments.")]
    [InlineData("\"hello\".title(1)\n", "str.title() expects no arguments.")]
    [InlineData("\"hello\".split(1)\n", "str.split([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.")]
    [InlineData("\"hello\".split(\"a\", 1, 2)\n", "str.split([separator[, maxsplit]]) expects zero to two arguments.")]
    [InlineData("\"hello\".split(\",\", \"1\")\n", "expects maxsplit to be an integer.")]
    [InlineData("\"hello\".rsplit(1)\n", "str.rsplit([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.")]
    [InlineData("\"hello\".rsplit(\",\", \"1\")\n", "expects maxsplit to be an integer.")]
    [InlineData("\"hello\".splitlines(1)\n", "str.splitlines([keepends]) expects zero or one bool argument.")]
    [InlineData("\"hello\".strip(1)\n", "str.strip([chars]) expects zero or one string argument.")]
    [InlineData("\"hello\".lstrip(1)\n", "str.lstrip([chars]) expects zero or one string argument.")]
    [InlineData("\"hello\".rstrip(1)\n", "str.rstrip([chars]) expects zero or one string argument.")]
    [InlineData("\" \".join()\n", "str.join(iterable) expects one argument.")]
    [InlineData("\" \".join(7)\n", "str.join(iterable) expects an iterable of strings.")]
    [InlineData("\"hello\".count()\n", "str.count(sub[, start[, end]]) expects one to three arguments.")]
    [InlineData("\"hello\".count(1)\n", "str.count(sub[, start[, end]]) expects one string argument plus optional integer bounds.")]
    [InlineData("\"hello\".find(\"e\", \"1\")\n", "slice indices must be integers or None or have an __index__ method")]
    [InlineData("\"hello\".count(\"l\", 1, \"4\")\n", "slice indices must be integers or None or have an __index__ method")]
    [InlineData("\"hello\".removeprefix()\n", "str.removeprefix(prefix) expects one argument.")]
    [InlineData("\"hello\".removesuffix()\n", "str.removesuffix(suffix) expects one argument.")]
    [InlineData("\"hello\".partition()\n", "str.partition(sep) expects one argument.")]
    [InlineData("\"hello\".rpartition()\n", "str.rpartition(sep) expects one argument.")]
    [InlineData("\"hello\".startswith((1,))\n", "tuple for startswith must only contain str, not int")]
    [InlineData("\"hello\".endswith((1,))\n", "tuple for endswith must only contain str, not int")]
    [InlineData("\"hello\".index(1)\n", "str.index(sub[, start[, end]]) expects one string argument plus optional integer bounds.")]
    [InlineData("\"hello\".rfind(1)\n", "str.rfind(sub[, start[, end]]) expects one string argument plus optional integer bounds.")]
    [InlineData("\"hello\".rindex(1)\n", "str.rindex(sub[, start[, end]]) expects one string argument plus optional integer bounds.")]
    [InlineData("\"hello\".index(\"e\", \"1\")\n", "slice indices must be integers or None or have an __index__ method")]
    [InlineData("\"hello\".rfind(\"e\", \"1\")\n", "slice indices must be integers or None or have an __index__ method")]
    [InlineData("\"hello\".rindex(\"e\", \"1\")\n", "slice indices must be integers or None or have an __index__ method")]
    [InlineData("\"hello\".center()\n", "str.center(width[, fillchar]) expects one or two arguments.")]
    [InlineData("\"hello\".ljust()\n", "str.ljust(width[, fillchar]) expects one or two arguments.")]
    [InlineData("\"hello\".rjust()\n", "str.rjust(width[, fillchar]) expects one or two arguments.")]
    [InlineData("\"hello\".zfill()\n", "str.zfill(width) expects one argument.")]
    [InlineData("\"hello\".expandtabs(1, 2)\n", "str.expandtabs([tabsize]) expects zero or one argument.")]
    [InlineData("\"hello\".center(5, \"..\")\n", "The fill character must be exactly one character long")]
    [InlineData("\"hello\".ljust(5, 1)\n", "expects fillchar to be a string.")]
    [InlineData("\"hello\".rjust(5, \"..\")\n", "The fill character must be exactly one character long")]
    [InlineData("\"hello\".zfill(\"5\")\n", "expects width to be an integer.")]
    [InlineData("\"a\\tb\".expandtabs(\"2\")\n", "expects tabsize to be an integer.")]
    [InlineData("\"{}\".format()\n", "Replacement index 0 out of range for positional args tuple")]
    [InlineData("\"{0} {}\".format(\"x\", \"y\")\n", "cannot switch from manual field specification to automatic field numbering")]
    [InlineData("\"{foo}\".format()\n", "foo")]
    [InlineData("\"{foo}\".format_map([])\n", "expects one dictionary argument.")]
    [InlineData("\"{p.name}\".format()\n", "p")]
    [InlineData("\"{user[name}\".format(user={\"name\": \"x\"})\n", "Invalid format field.")]
    public void StringMethodContractFailure_ReportsTypeError(string source, string message)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        if (result.Failure is null)
        {
            Assert.Contains(result.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(result.Failure.ExceptionType, new[] { "TypeError", "ValueError", "IndexError", "KeyError" });
            Assert.Contains(message, result.Failure.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StringPartition_EmptySeparator_ReportsValueError()
    {
        var result = new LythonEngine().Run("\"hello\".partition(\"\")\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("empty separator", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringSplit_EmptySeparator_ReportsValueError()
    {
        var result = new LythonEngine().Run("\"hello\".split(\"\")\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("empty separator", result.Failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"hello\".index(\"zz\")\n")]
    [InlineData("\"hello\".rindex(\"zz\")\n")]
    public void StringIndexFamily_MissingSubstring_ReportsValueError(string source)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Contains("substring not found", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StringMethods_HandleReplaceAndMissingFind()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append("banana".replace("na", "X"))
vals.append(str("banana".find("zz")))
vals.append(str("".split(",")))
vals.append(str("a\n".splitlines()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("baXX|-1|[]|[a]", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StringMethods_HandleEmptyNeedlesWhitespaceAndCharacterJoin()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str("abc".find("")))
vals.append(str("".startswith("")))
vals.append(str("".endswith("")))
vals.append(str("   \t\n".split()))
vals.append("-".join("ab"))
vals.append(str(" \t\nabc \r".strip()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("0|True|True|[]|a-b|abc", host.ReadText("/out.txt"));
    }

    [Fact]
    public void StringMethods_HandleIsLowerWithPythonShapedSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
vals.append(str("alpha".islower()))
vals.append(str("Alpha".islower()))
vals.append(str("alpha42".islower()))
vals.append(str("42!?".islower()))
vals.append(str("a😀".islower()))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("True|False|True|False|True", host.ReadText("/out.txt"));
    }
}
