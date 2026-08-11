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
        Assert.False(compiled.RequireNotNull().IsValid);
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
    public void Run_AcceptsContinuationIndentationThatClosesInsideCompoundHeaders()
    {
        var result = new LythonEngine().Run(
            """
values = []
for table in ["A4",
              "A5",
              "A6"]:
    values.append(table)

if all([True,
        True]):
    values.append("if")

while any([False,
           False]):
    values.append("never")

return values
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        Assert.Equal(new object?[] { "A4", "A5", "A6", "if" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void Compile_ContinuationIndentationDoesNotHideRealSuiteDedents()
    {
        var compiled = new LythonEngine().Compile(
            """
if all([True,
        True]):
    values = [1,
              2]
after = 3
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(FormatDiagnostic)));
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Run_AcceptsUnparenthesizedTupleExpressionLists()
    {
        var result = new LythonEngine().Run(
            """
events = []

def mark(value):
    events.append(value)
    return value

def choose(flag):
    return 10 if flag else 20, 30

a, b = mark(1), mark(2)
single = mark(3),
left = right = mark(4), mark(5)
mark(6), mark(7)
a, b

seen = []
for item in mark(8), mark(9):
    seen.append(item)

return a, b, single, left is right, events, seen, choose(True), choose(False)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic)));
        var returned = Assert.IsType<object?[]>(result.ReturnValue);
        Assert.Equal(new System.Numerics.BigInteger(1), returned[0]);
        Assert.Equal(new System.Numerics.BigInteger(2), returned[1]);
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(3) }, Assert.IsType<object?[]>(returned[2]));
        Assert.True(Assert.IsType<bool>(returned[3]));
        Assert.Equal(Enumerable.Range(1, 9).Select(value => (object?)new System.Numerics.BigInteger(value)), Assert.IsType<List<object?>>(returned[4]));
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(8), new System.Numerics.BigInteger(9) }, Assert.IsType<List<object?>>(returned[5]));
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(10), new System.Numerics.BigInteger(30) }, Assert.IsType<object?[]>(returned[6]));
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(20), new System.Numerics.BigInteger(30) }, Assert.IsType<object?[]>(returned[7]));
    }

    [Fact]
    public void Compile_RejectsMissingTupleExpressionListItemsPrecisely()
    {
        var compiled = new LythonEngine().Compile("return 1, , 2\n");

        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic =>
            diagnostic.Code == "LA1004" && diagnostic.Message.Contains("after ','", StringComparison.Ordinal));
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
        Assert.True(diagnostic.Span.RequireNotNull().Line > 0);
        Assert.True(diagnostic.Span.Column > 0);
    }

    [Fact]
    public void Compile_RejectsSourceBeyondTheContainedFrontendLimit()
    {
        var source = "value = 1\n#" + new string('x', LythonEngine.MaxSourceLength);

        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        var diagnostic = Assert.Single(compiled.Diagnostics);
        Assert.Equal("LA0002", diagnostic.Code);
        Assert.Contains(LythonEngine.MaxSourceLength.ToString(), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_RejectsExcessiveNestingBeforeParsing()
    {
        var nesting = LythonEngine.MaxSyntaxNesting + 1;
        var source = "value = " + new string('(', nesting) + "0" + new string(')', nesting);

        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        var diagnostic = Assert.Single(compiled.Diagnostics);
        Assert.Equal("LA0003", diagnostic.Code);
        Assert.Contains(LythonEngine.MaxSyntaxNesting.ToString(), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_NestingLimitIgnoresStringAndCommentText()
    {
        var delimiters = new string('(', LythonEngine.MaxSyntaxNesting + 1);
        var source = $"text = '{delimiters}'\n# {delimiters}\n";

        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(FormatDiagnostic)));
    }

    [Fact]
    public void Compile_RejectsExcessiveUnaryOperatorNestingBeforeRecursiveLowering()
    {
        var source = "value = " + string.Concat(Enumerable.Repeat("not ", LythonEngine.MaxUnaryOperatorNesting + 1)) + "False\n";

        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        var diagnostic = Assert.Single(compiled.Diagnostics);
        Assert.Equal("LA0004", diagnostic.Code);
        Assert.Contains(LythonEngine.MaxUnaryOperatorNesting.ToString(), diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_AcceptsUnaryOperatorNestingAtTheContainedFrontendLimit()
    {
        var source = "value = " + string.Concat(Enumerable.Repeat("not ", LythonEngine.MaxUnaryOperatorNesting)) + "False\n";

        var compiled = new LythonEngine().Compile(source);

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(FormatDiagnostic)));
    }

    private static string FormatDiagnostic(LythonDiagnostic diagnostic)
        => diagnostic.Span is null
            ? $"{diagnostic.Code}: {diagnostic.Message}"
            : $"{diagnostic.Code}: {diagnostic.Message} @ {diagnostic.Span.Line}:{diagnostic.Span.Column}";
}
