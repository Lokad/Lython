using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ParserCompatibilityTests
{
    [Fact]
    public void Run_NormalizesUtf8BomAndPhysicalSourceNewlines()
    {
        var source = "\uFEFFvalue = '''a\r\nb'''\r\nreturn chr(13) in value\r\n";

        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        Assert.False(Assert.IsType<bool>(result.ReturnValue));
    }

    [Fact]
    public void Run_AcceptsPythonFloatAndDecimalSeparatorSpellings()
    {
        var result = new LythonEngine().Run(
            "return [.5, 5., 1_000.5, 1.2_3, 1e1_0, 1_000, 00, 0_0]\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        Assert.Equal(
            new object?[]
            {
                0.5,
                5.0,
                1000.5,
                1.23,
                1e10,
                new System.Numerics.BigInteger(1000),
                System.Numerics.BigInteger.Zero,
                System.Numerics.BigInteger.Zero
            },
            Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void Run_AcceptsAndNormalizesPythonUnicodeIdentifiers()
    {
        var result = new LythonEngine().Run(
            "café = 3\nK = café + 1\nreturn K\n",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        Assert.Equal(new System.Numerics.BigInteger(4), result.ReturnValue);
    }

    [Fact]
    public void Run_DecodesPythonStringEscapesAndRetainsUnknownEscapes()
    {
        var result = new LythonEngine().Run(
            """
return [
    "\u0061" == "a",
    "\U0001F600" == "😀",
    "\101" == "A",
    "\a\b\f\v" == chr(7) + chr(8) + chr(12) + chr(11),
    "\q" == "\\q",
]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        Assert.All(
            Assert.IsType<List<object?>>(result.ReturnValue),
            item => Assert.True(Assert.IsType<bool>(item)));
    }

    [Fact]
    public void Compile_RejectsNonAsciiBytesSourceAndRetainsBytesUnicodeEscapes()
    {
        var invalid = new LythonEngine().Compile("return b\"é\"\n");
        var valid = new LythonEngine().Run("return b\"\\u0061\" == b\"\\\\u0061\"\n", new MockLythonHost());

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Diagnostics, d => d.Message.Contains("ASCII", StringComparison.Ordinal));
        Assert.True(valid.Success, valid.Failure?.Message);
        Assert.True(Assert.IsType<bool>(valid.ReturnValue));
    }

    [Theory]
    [InlineData("return 1__0\n")]
    [InlineData("return 1_\n")]
    [InlineData("return 01\n")]
    public void Compile_InvalidDecimalSeparatorsAndLeadingZerosAreStructuredSyntaxFailures(string source)
    {
        LythonCompiledScript? compiled = null;

        var exception = Record.Exception(() => compiled = new LythonEngine().Compile(source));

        Assert.Null(exception);
        Assert.NotNull(compiled);
        Assert.False(compiled!.IsValid);
        Assert.NotEmpty(compiled.Diagnostics);
    }

    [Fact]
    public void Compile_AcceptsOrdinarySoftKeywordContinuationAndPostfixShapes()
    {
        var compiled = new LythonEngine().Compile(
            """
match = "alpha"
case = "beta"
value = "a" + \
    "b"
items = [match, case]
line = items[0].upper().splitlines(True)[0]
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Compile_AcceptsAnnotationRichOrdinaryPythonShapes()
    {
        var compiled = new LythonEngine().Compile(
            """
from __future__ import annotations

def helper(*parts, sep="|", suffix):
    typed: list[str] = ["x"]
    return sep.join(parts) + suffix + typed[0]

result = helper("a", "b", suffix="?")
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Compile_AcceptsMixedStarredAndKeywordCallShapes()
    {
        var compiled = new LythonEngine().Compile(
            """
def helper(*parts, suffix):
    return suffix

result = helper(*["a", "b"], suffix="?")
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => d.Message)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Compile_AcceptsImplicitLineJoiningAcrossGroupedPythonSyntax()
    {
        var compiled = new LythonEngine().Compile(
            """
class Base:
    pass

class Child(
    Base,
):
    pass

def helper(
    first,
    second=[
        "b",
    ][
        0
    ],
    *,
    suffix="!",
):
    return first + second + suffix

rows = [
    (
        "a",
        "xx",
    ),
    (
        "b",
        "y",
    ),
]

items = [
    (
        name,
        len(value),
    )
    for name, value in rows
    if (
        len(value)
        > 0
    )
]

mapping = {
    "items": items,
    "unique": {
        "a",
        "b",
    },
}

result = helper(
    "a",
    suffix="?",
)

match [
    1,
    2,
]:
    case [
        first,
        *rest,
    ]:
        matched = first
    case _:
        matched = 0
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(FormatDiagnostic)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Compile_AcceptsAdjacentTextLiteralConcatenation()
    {
        var compiled = new LythonEngine().Compile(
            """
plain = "hello" " " 'world'
raw = (
    r"alpha\s+"
    r"beta"
)
triple = (
    '''left'''
    '''right'''
)
items = [
    "a"
    "b",
]
mapping = {
    "k" "ey": "v" "alue",
}
unique = {
    "a" "b",
}
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(FormatDiagnostic)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Run_FoldsAdjacentTextLiteralsBeforeExecution()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
text = (
    "hello"
    " "
    r"world"
    '''!'''
)
__lython_file = open("/out.txt", "w")
__lython_file.write(text)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello world!", host.ReadText("/out.txt"));
    }

    [Fact]
    public void Compile_FoldedAdjacentTextLiteralsStayVisibleToStaticDiagnostics()
    {
        var compiled = new LythonEngine().Compile(
            """
open("/repo/input.txt", "r" "b")
""");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA3001");
    }

    [Theory]
    [InlineData("text = 'unterminated\n", "LA0001", "Unterminated single-quoted string literal")]
    [InlineData("text = \"\"\"unterminated\nstill text\n", "LA0001", "Unterminated triple-quoted string literal")]
    [InlineData("text = \"abc\\\n", "LA0001", "Unfinished string escape")]
    [InlineData("items = [\n", "LA1021", "expected ']'")]
    [InlineData("value = (\n", "LA1008", "expected ')'")]
    [InlineData("value = 1]\n", "LA1000", "Unexpected closing delimiter ']'")]
    [InlineData("    value = 1\n", "LA1000", "Unexpected indentation")]
    [InlineData("text = \"\\xZ0\"\n", "LA1007", "Malformed \\x escape")]
    [InlineData("text = f\"{\"\n", "LA1007", "f-string")]
    [InlineData("text = u\"hello\"\n", "LA1007", "Unsupported string prefix 'u'")]
    public void Compile_CommonSyntaxFailures_ReportRepairableDiagnostics(
        string source,
        string expectedCode,
        string expectedMessageFragment)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        var diagnostic = Assert.Single(compiled.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(LythonDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(expectedMessageFragment, diagnostic.Message, StringComparison.Ordinal);
        Assert.NotNull(diagnostic.Span);
        Assert.True(diagnostic.Span!.Line > 0);
        Assert.True(diagnostic.Span.Column > 0);
    }

    private static string FormatDiagnostic(LythonDiagnostic diagnostic)
        => diagnostic.Span is null
            ? $"{diagnostic.Code}: {diagnostic.Message}"
            : $"{diagnostic.Code}: {diagnostic.Message} @ {diagnostic.Span.Line}:{diagnostic.Span.Column}";
}
