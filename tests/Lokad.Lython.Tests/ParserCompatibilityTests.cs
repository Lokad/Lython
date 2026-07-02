using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ParserCompatibilityTests
{
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
line = items[0].upper().splitlines(keepends=True)[0]
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

    private static string FormatDiagnostic(LythonDiagnostic diagnostic)
        => diagnostic.Span is null
            ? $"{diagnostic.Code}: {diagnostic.Message}"
            : $"{diagnostic.Code}: {diagnostic.Message} @ {diagnostic.Span.Line}:{diagnostic.Span.Column}";
}
