using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringFormatMethodTests
{
    [Theory]
    [InlineData("\"{\".format()", "ValueError", "Single '{' encountered in format string")]
    [InlineData("\"}\".format()", "ValueError", "Single '}' encountered in format string")]
    [InlineData("\"{!}\".format(1)", "ValueError", "unmatched '{' in format spec")]
    [InlineData("\"{!x}\".format(1)", "ValueError", "Unknown conversion specifier x")]
    [InlineData("\"{0!rx}\".format(1)", "ValueError", "expected ':' after conversion specifier")]
    [InlineData("\"{0!{}}\".format(1)", "ValueError", "Unknown conversion specifier {")]
    [InlineData("\"{0{1}}\".format(1, 2)", "ValueError", "unexpected '{' in field name")]
    [InlineData("\"{0[abc}\".format([1])", "ValueError", "expected '}' before end of string")]
    [InlineData("\"{0.}\".format(1)", "ValueError", "Empty attribute in format string")]
    [InlineData("\"{0[a[b]]}\".format({\"a[b\": 1})", "ValueError", "Only '.' or '[' may follow ']' in format field specifier")]
    [InlineData("\"{0:{1:{2}}}\".format(\"ab\", 5, 6)", "ValueError", "Max string recursion exceeded")]
    [InlineData("\"{5}\".format(1)", "IndexError", "Replacement index 5 out of range for positional args tuple")]
    [InlineData("\"{}\".format()", "IndexError", "Replacement index 0 out of range for positional args tuple")]
    [InlineData("\"{5[a]b}\".format(1)", "IndexError", "Replacement index 5 out of range for positional args tuple")]
    [InlineData("\"{-1}\".format(1)", "KeyError", "-1")]
    [InlineData("\"{aX}\".format()", "KeyError", "aX")]
    [InlineData("(\"{\" + \"9\" * 19 + \"}\").format(1)", "ValueError", "Too many decimal digits in format string")]
    [InlineData("\"{0}\".format_map({})", "ValueError", "Format string contains positional fields")]
    [InlineData("\"{}\".format_map({})", "ValueError", "Format string contains positional fields")]
    [InlineData("\"{} {0}\".format(1, 2)", "ValueError", "cannot switch from automatic field numbering to manual field specification")]
    [InlineData("\"{0} {}\".format(1, 2)", "ValueError", "cannot switch from manual field specification to automatic field numbering")]
    public void InvalidFormatTemplates_ReportPythonShapedRuntimeFailures(
        string expression,
        string exceptionType,
        string message)
    {
        var source = "return str(" + expression + ")";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(message, result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMethod_ComposesConversionsNestedSpecsAndMapping()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
class Label:
    def __str__(self):
        return "label"

    def __repr__(self):
        return "Label()"

vals = []
vals.append("{:05d}|{:.2f}|{:b}".format(42, 3.14159, 10))
vals.append("{0!r}|{1!s}|{0!r:>6}".format("ab", 42))
vals.append("{:<5}|{:*^7}".format(5, 5))
vals.append("{0:{w}}".format("ab", w=4))
vals.append("{} {:{}}".format(1, 2, 3))
vals.append("{{}}|{a}|{0[a:b]}".format({"a:b": 7}, a="x"))
vals.append("{name:>{width}}".format(name="ab", width=5))
vals.append("{x:>5}".format_map({"x": 1}))
vals.append("{}|{!r}".format(Label(), Label()))
vals.append("{007}".format("a", "b", "c", "d", "e", "f", "g", "h"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "00042|3.14|1010|'ab'|42|  'ab'|5    |***5***|ab  |1   2|{}|x|7|   ab|    1|label|Label()|h",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void FormatMethodOutput_ObservesExecutionMemoryBudget()
    {
        var result = new LythonEngine().Run(
            "return \"{:1000000}\".format(\"x\")\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }
}