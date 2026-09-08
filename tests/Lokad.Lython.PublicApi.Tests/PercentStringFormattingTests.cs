using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PercentStringFormattingTests
{
    private const string CompatibilitySource = """
class Label:
    def __str__(self):
        return "label"

    def __repr__(self):
        return "Label()"

mapping = {"name": "lython", "count": 7}
value = "changed=%s"
value %= True
return "|".join([
    "%s\t%s" % ("left", "right"),
    "%(name)s:%(count)04d" % mapping,
    "%+08.2f:%#x:%#o" % (1.25, 31, 8),
    "%-5.3s:%%" % "abcdef",
    "%*.*f" % (8, 3, 1.25),
    "%r:%a:%c" % ("é", "é", 128512),
    "%e:%g:%#.3g" % (1.2, 1e20, 1.2),
    "%s:%r" % (Label(), Label()),
    "%s" % ((1, 2),),
    "%s %(name)s" % mapping,
    value
])
""";

    private const string Expected = "left\tright|lython:0007|+0001.25:0x1f:0o10|abc  :%|   1.250|'é':'\\xe9':😀|1.200000e+00:1e+20:1.20|label:Label()|(1, 2)|{'name': 'lython', 'count': 7} lython|changed=True";

    [Fact]
    public void PercentFormatting_CoversTupleMappingFlagsWidthsAndConversions()
    {
        var result = new LythonEngine().Run(CompatibilitySource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(Expected, result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_PercentFormattingUsesTheSameRuntimeCore()
    {
        var result = await new LythonEngine().RunAsync(CompatibilitySource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(Expected, result.ReturnValue);
    }

    [Fact]
    public void OperatorMod_UsesStringFormattingAndKeepsNumericModulo()
    {
        var result = new LythonEngine().Run(
            """
import operator
return operator.mod("%03d", 7) + "|" + str(operator.mod(-7, 3))
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("007|2", result.ReturnValue);
    }

    [Theory]
    [InlineData("%s %s", "(1,)", "TypeError", "not enough arguments")]
    [InlineData("%s", "(1, 2)", "TypeError", "not all arguments")]
    [InlineData("%(name)s", "({'name': 'x'},)", "TypeError", "requires a mapping")]
    [InlineData("%q", "1", "ValueError", "unsupported format character")]
    [InlineData("%", "1", "ValueError", "incomplete format")]
    [InlineData("%x", "1.5", "TypeError", "integer")]
    [InlineData("%c", "'xy'", "TypeError", "requires int or char")]
    [InlineData("%(missing)s", "{'name': 'x'}", "KeyError", "missing")]
    public void InvalidPercentFormats_ReportPythonShapedRuntimeFailures(
        string format,
        string argumentExpression,
        string exceptionType,
        string message)
    {
        var source = $$"""
def apply_percent(template, value):
    return template % value

return apply_percent({{ToPythonLiteral(format)}}, {{argumentExpression}})
""";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(message, result.Failure?.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PercentFormatOutput_ObservesExecutionMemoryBudget()
    {
        var result = new LythonEngine().Run(
            "return \"%1000000s\" % \"x\"\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void StaticContractsAcceptPlausibleFormatsAndRejectProvableInvalidLiterals()
    {
        var valid = new LythonEngine().Compile(
            """
text = "%s:%04d" % ("x", 7)
mapped = "%(name)s" % {"name": "x"}
dynamic = "%s" % object()
""");

        Assert.True(valid.IsValid, string.Join(" | ", valid.Diagnostics.Select(d => d.Message)));

        var invalid = new LythonEngine().Compile(
            """
plain = "plain" % 1
too_many = "%s" % (1, 2)
bad_conversion = "%q" % 1
""");

        Assert.False(invalid.IsValid);
        Assert.Equal(3, invalid.Diagnostics.Count(d => d.Code == "LA3141"));
    }

    private static string ToPythonLiteral(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

