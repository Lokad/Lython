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

    [Theory]
    [InlineData("format(1, \"{\")", "ValueError", "Unknown format code '{' for object of type 'int'")]
    [InlineData("format(\"a\", \"{\")", "ValueError", "Unknown format code '{' for object of type 'str'")]
    [InlineData("format(1.5, \"{\")", "ValueError", "Unknown format code '{' for object of type 'float'")]
    [InlineData("format(True, \"{\")", "ValueError", "Unknown format code '{' for object of type 'bool'")]
    [InlineData("format(1, \"{}\")", "ValueError", "Invalid format specifier '{}' for object of type 'int'")]
    [InlineData("format(\"a\", \"{}\")", "ValueError", "Invalid format specifier '{}' for object of type 'str'")]
    [InlineData("format(1, \"}\")", "ValueError", "Unknown format code '}' for object of type 'int'")]
    [InlineData("format(1, \"abc\")", "ValueError", "Invalid format specifier 'abc' for object of type 'int'")]
    [InlineData("format(1, \".\")", "ValueError", "Format specifier missing precision")]
    [InlineData("format(\"a\", \".5x\")", "ValueError", "Unknown format code 'x' for object of type 'str'")]
    [InlineData("format(1.5, \",q\")", "ValueError", "Cannot specify ',' with 'q'.")]
    [InlineData("format(1.5, \"#q\")", "ValueError", "Unknown format code 'q' for object of type 'float'")]
    [InlineData("format(1, \"#,x\")", "ValueError", "Cannot specify ',' with 'x'.")]
    [InlineData("format(\"a\", \"#q\")", "ValueError", "Unknown format code 'q' for object of type 'str'")]
    [InlineData("format(\"a\", \",s\")", "ValueError", "Cannot specify ',' with 's'.")]
    [InlineData("format(1, \".5d\")", "ValueError", "Precision not allowed in integer format specifier")]
    [InlineData("format(\"a\", \"+\")", "ValueError", "Sign not allowed in string format specifier")]
    [InlineData("format(\"a\", \" \")", "ValueError", "Space not allowed in string format specifier")]
    [InlineData("format(\"a\", \"#\")", "ValueError", "Alternate form (#) not allowed in string format specifier")]
    [InlineData("format(\"a\", \"_\")", "ValueError", "Cannot specify '_' with 's'.")]
    [InlineData("format(1.5, \"d\")", "ValueError", "Unknown format code 'd' for object of type 'float'")]
    [InlineData("format(\"a\", \"d\")", "ValueError", "Unknown format code 'd' for object of type 'str'")]
    [InlineData("format(1.5, \"s\")", "ValueError", "Unknown format code 's' for object of type 'float'")]
    [InlineData("format(1, \"s\")", "ValueError", "Unknown format code 's' for object of type 'int'")]
    [InlineData("format(True, \"s\")", "ValueError", "Unknown format code 's' for object of type 'bool'")]
    [InlineData("f\"{1.5:s}\"", "ValueError", "Unknown format code 's' for object of type 'float'")]
    [InlineData("\"{:s}\".format(1.5)", "ValueError", "Unknown format code 's' for object of type 'float'")]
    public void InvalidFormatSpecs_ReportPythonShapedRuntimeFailures(
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

    [Theory]
    [InlineData("format(None, \"5\")", "NoneType")]
    [InlineData("format(None, \">10\")", "NoneType")]
    [InlineData("format([1, 2], \">10\")", "list")]
    [InlineData("format((1,), \"d\")", "tuple")]
    [InlineData("format({1: 2}, \".2f\")", "dict")]
    [InlineData("format(b\"a\", \"5\")", "bytes")]
    public void ExoticValuesWithNonEmptySpecs_ReportUnsupportedFormat(string expression, string typeName)
    {
        var result = new LythonEngine().Run("return str(" + expression + ")", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains("unsupported format string passed to " + typeName + ".__format__", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("format(Decimal(\"1.5\"), \".2f\")", "1.50")]
    [InlineData("format(Decimal(\"1.5\"), \"f\")", "1.5")]
    [InlineData("format(Decimal(\"1.5\"), \"e\")", "1.5e+0")]
    [InlineData("format(Decimal(\"1.5\"), \".2e\")", "1.50e+0")]
    [InlineData("format(Decimal(\"1.5\"), \"g\")", "1.5")]
    [InlineData("format(Decimal(\"1E+20\"), \"g\")", "1e+20")]
    [InlineData("format(Decimal(\"1E+20\"), \"G\")", "1E+20")]
    [InlineData("format(Decimal(\"1E+20\"), \"f\")", "100000000000000000000")]
    [InlineData("format(Decimal(\"1E+20\"), \"e\")", "1e+20")]
    [InlineData("format(Decimal(\"0.1\"), \"e\")", "1e-1")]
    [InlineData("format(Decimal(\"0.0001\"), \"g\")", "0.0001")]
    [InlineData("format(Decimal(\"-2.675\"), \".2f\")", "-2.68")]
    [InlineData("format(Decimal(\"0\"), \"f\")", "0")]
    [InlineData("format(Decimal(\"0\"), \".2e\")", "0.00e+2")]
    [InlineData("format(Decimal(\"-0.0\"), \"e\")", "-0e-1")]
    [InlineData("format(Decimal(\"0E+5\"), \"g\")", "0e+5")]
    [InlineData("format(Decimal(\"0.00\"), \".1g\")", "0.00")]
    [InlineData("format(Decimal(\"0\"), \".2g\")", "0")]
    [InlineData("format(Decimal(\"1.5\"), \"%\")", "150%")]
    [InlineData("format(Decimal(\"1.5\"), \".1%\")", "150.0%")]
    [InlineData("format(Decimal(\"1.5\"), \"#.0f\")", "2.")]
    [InlineData("format(Decimal(\"100\"), \"#g\")", "100.")]
    [InlineData("format(Decimal(\"100\"), \"#f\")", "100.")]
    [InlineData("format(Decimal(\"1E+20\"), \"#\")", "1.E+20")]
    [InlineData("format(Decimal(\"1500.5\"), \",.2f\")", "1,500.50")]
    [InlineData("format(Decimal(\"150.0\"), \",.0%\")", "15,000%")]
    [InlineData("format(Decimal(\"1234567.891\"), \",g\")", "1,234,567.891")]
    [InlineData("format(Decimal(\"1234567.891\"), \",e\")", "1.234567891e+6")]
    [InlineData("format(Decimal(\"1.5\"), \"n\")", "1.5")]
    [InlineData("format(Decimal(\"1234567\"), \"n\")", "1234567")]
    [InlineData("format(Decimal(\"1.5\"), \"=10\")", "       1.5")]
    [InlineData("format(Decimal(\"-2.675\"), \"=10\")", "-    2.675")]
    [InlineData("format(Decimal(\"1.5\"), \"010.2f\")", "0000001.50")]
    [InlineData("format(Decimal(\"0.1234567890123456789012345678\"), \"e\")", "1.234567890123456789012345678e-1")]
    [InlineData("format(Decimal(\"999\"), \".2g\")", "1.0e+3")]
    [InlineData("format(Decimal(\"9.999\"), \".3g\")", "10.0")]
    [InlineData("format(Decimal(\"150\"), \".1g\")", "2e+2")]
    [InlineData("format(Decimal(\"0.000012345\"), \".3g\")", "0.0000123")]
    [InlineData("format(Decimal(\"1e-7\"), \".3g\")", "1e-7")]
    [InlineData("format(Decimal(\"1.5E+3\"), \".5g\")", "1.5e+3")]
    [InlineData("format(Decimal(\"1E+20\"), \".25g\")", "1e+20")]
    [InlineData("format(Decimal(\"2.6750\"), \"g\")", "2.6750")]
    [InlineData("format(Decimal(\"1.50\"), \"g\")", "1.50")]
    [InlineData("format(Decimal(\"1.500\"), \".3g\")", "1.50")]
    [InlineData("format(Decimal(\"2.5\"), \".0f\")", "2")]
    [InlineData("format(Decimal(\"0.125\"), \".2f\")", "0.12")]
    [InlineData("format(Decimal(\"1.5\"), \".0e\")", "2e+0")]
    [InlineData("format(Decimal(\"5\"), \".1e\")", "5.0e+0")]
    [InlineData("format(Decimal(\"1.5\"), \".28e\")", "1.5000000000000000000000000000e+0")]
    [InlineData("format(Decimal(\"100\"), \".1g\")", "1e+2")]
    [InlineData("format(Decimal(\"0.9\"), \".0g\")", "0.9")]
    public void DecimalFormatSpecs_RenderPythonShapedOutput(string expression, string expected)
    {
        var source = "from decimal import Decimal\nreturn str(" + expression + ")";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<string>(result.ReturnValue));
    }

    [Theory]
    [InlineData("format(Decimal(\"1.5\"), \"d\")")]
    [InlineData("format(Decimal(\"1.5\"), \"s\")")]
    [InlineData("format(Decimal(\"1.5\"), \"_\")")]
    [InlineData("format(Decimal(\"1.5\"), \",n\")")]
    [InlineData("format(Decimal(\"1.5\"), \"_.2f\")")]
    [InlineData("format(Decimal(\"1.5\"), \".2q\")")]
    [InlineData("format(Decimal(\"1\"), \"x\")")]
    public void InvalidDecimalSpecs_ReportInvalidFormatString(string expression)
    {
        var source = "from decimal import Decimal\nreturn str(" + expression + ")";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Contains("invalid format string", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DecimalFormatSpecs_HonorAmbientRounding()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
from decimal import Decimal, ROUND_DOWN, ROUND_UP, getcontext
context = getcontext()
vals = []
vals.append(format(Decimal("2.675"), ".2f"))
context.rounding = ROUND_DOWN
vals.append(format(Decimal("2.675"), ".2f"))
vals.append(format(Decimal("2.675"), ".2e"))
vals.append(format(Decimal("-2.675"), ".2f"))
context.rounding = ROUND_UP
vals.append(format(Decimal("2.611"), ".2f"))
vals.append(format(Decimal("2.675"), ".2g"))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2.68|2.67|2.67e+0|-2.67|2.62|2.7", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("format(1.0, \"#.0f\")", "1.")]
    [InlineData("format(1.5, \"#.0f\")", "2.")]
    [InlineData("format(100.0, \"#.0f\")", "100.")]
    [InlineData("format(1.0, \"#.0e\")", "1.e+00")]
    [InlineData("format(1.5, \"#.0E\")", "2.E+00")]
    [InlineData("format(1.0, \"#.0F\")", "1.")]
    [InlineData("format(1.5, \"#.0%\")", "150.%")]
    [InlineData("format(1.0, \"#\")", "1.0")]
    [InlineData("format(100.0, \"#\")", "100.0")]
    [InlineData("format(0.0, \"#\")", "0.0")]
    [InlineData("format(1e-5, \"#\")", "1.e-05")]
    [InlineData("format(1.5e-7, \"#\")", "1.5e-07")]
    [InlineData("format(1, \"#.0f\")", "1.")]
    [InlineData("format(1, \"#.0e\")", "1.e+00")]
    [InlineData("format(150.0, \"#,.0f\")", "150.")]
    [InlineData("format(1500.0, \"#,.0f\")", "1,500.")]
    [InlineData("format(1.0, \">#10.0f\")", "        1.")]
    [InlineData("f\"{1.0:#.0f}\"", "1.")]
    [InlineData("\"{:#.0%}\".format(1.5)", "150.%")]
    public void FloatAlternateSpecs_ForceDecimalPoint(string expression, string expected)
    {
        var result = new LythonEngine().Run("return str(" + expression + ")", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<string>(result.ReturnValue));
    }

    [Theory]
    [InlineData("format(10**400, \".2e\")")]
    [InlineData("format(10**400, \".2f\")")]
    [InlineData("format(10**400, \"e\")")]
    [InlineData("format(-(10**400), \".2E\")")]
    [InlineData("format(10**400, \"g\")")]
    [InlineData("format(10**400, \"%\")")]
    public void OversizedIntegersForFloatCodes_RaiseOverflow(string expression)
    {
        var result = new LythonEngine().Run("return str(" + expression + ")", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("OverflowError", result.Failure?.ExceptionType);
        Assert.Contains("int too large to convert to float", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("format(1.5, \".2e\")", "1.50e+00")]
    [InlineData("format(1.5, \".2E\")", "1.50E+00")]
    [InlineData("format(1.5, \"e\")", "1.500000e+00")]
    [InlineData("format(0.0, \".2e\")", "0.00e+00")]
    [InlineData("format(-1.5, \".2e\")", "-1.50e+00")]
    [InlineData("format(1e100, \".2e\")", "1.00e+100")]
    [InlineData("format(1e-5, \".2e\")", "1.00e-05")]
    [InlineData("format(1e20, \"g\")", "1e+20")]
    [InlineData("format(1e20, \"G\")", "1E+20")]
    [InlineData("format(0.0001, \"g\")", "0.0001")]
    [InlineData("format(123456.0, \"g\")", "123456")]
    [InlineData("format(float(\"inf\"), \".2f\")", "inf")]
    [InlineData("format(float(\"-inf\"), \".2e\")", "-inf")]
    [InlineData("format(float(\"nan\"), \".2f\")", "nan")]
    [InlineData("format(float(\"inf\"), \"F\")", "INF")]
    [InlineData("format(float(\"nan\"), \"E\")", "NAN")]
    [InlineData("format(float(\"-inf\"), \"G\")", "-INF")]
    [InlineData("format(float(\"inf\"), \"+\")", "+inf")]
    [InlineData("format(float(\"inf\"), \"%\")", "inf%")]
    [InlineData("format(1, \".2e\")", "1.00e+00")]
    [InlineData("format(1, \".2E\")", "1.00E+00")]
    [InlineData("format(42, \"e\")", "4.200000e+01")]
    [InlineData("format(-42, \".1E\")", "-4.2E+01")]
    [InlineData("format(0, \"e\")", "0.000000e+00")]
    [InlineData("format(True, \".2e\")", "1.00e+00")]
    [InlineData("format(False, \"E\")", "0.000000E+00")]
    [InlineData("format(1000000000000000000000000000000, \".2e\")", "1.00e+30")]
    [InlineData("format(123456789012345678901234567890, \".2e\")", "1.23e+29")]
    [InlineData("format(10, \".3g\")", "10")]
    [InlineData("format(1e308, \"%\")", "inf%")]
    [InlineData("format(-1e308, \"%\")", "-inf%")]
    [InlineData("format(10**308, \"%\")", "inf%")]
    public void FloatFormatSpecs_RenderPythonShapedOutput(string expression, string expected)
    {
        var result = new LythonEngine().Run("return str(" + expression + ")", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<string>(result.ReturnValue));
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