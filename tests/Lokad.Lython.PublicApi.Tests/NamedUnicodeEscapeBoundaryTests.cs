using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedUnicodeEscapeBoundaryTests
{
    [Theory]
    [InlineData("\"\\N{LATIN SMALL LETTER A}\"")]
    [InlineData("u\"\\N{LATIN SMALL LETTER A}\"")]
    [InlineData("U'\\N{LATIN SMALL LETTER A}'")]
    [InlineData("\"\"\"\\N{LATIN SMALL LETTER A}\"\"\"")]
    [InlineData("f\"\\N{LATIN SMALL LETTER A}\"")]
    [InlineData("F\"prefix \\N{LATIN SMALL LETTER A} {1}\"")]
    [InlineData("f\"{'\\N{LATIN SMALL LETTER A}'}\"")]
    [InlineData("f\"{1:\\N{GREATER-THAN SIGN}4}\"")]
    [InlineData("'prefix' u'\\N{LATIN SMALL LETTER A}'")]
    public async Task NamedEscapesFailExplicitlyBeforeEffects(string literal)
    {
        var compiled = new LythonEngine().Compile("print('effects')\nvalue = " + literal + "\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA2000" &&
            diagnostic.Message.Contains("Unsupported named Unicode escape", StringComparison.Ordinal));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Fact]
    public async Task RawBytesAndEscapedBackslashesRemainPythonCompatible()
    {
        await AssertOutput("""
            print(r"\N{LATIN SMALL LETTER A}")
            print("\\N{LATIN SMALL LETTER A}")
            print(b"\N{LATIN SMALL LETTER A}")
            print(rb"\N{LATIN SMALL LETTER A}")
            print(f"\\N{{LATIN SMALL LETTER A}}")
            print(rf"\N{{LATIN SMALL LETTER A}}")
            print(f"{r'\N{LATIN SMALL LETTER A}'}")
            """, "\\N{LATIN SMALL LETTER A}\n\\N{LATIN SMALL LETTER A}\nb'\\\\N{LATIN SMALL LETTER A}'\nb'\\\\N{LATIN SMALL LETTER A}'\n\\N{LATIN SMALL LETTER A}\n\\N{LATIN SMALL LETTER A}\n\\N{LATIN SMALL LETTER A}\n");
    }

    [Fact]
    public async Task TextAndFormattedStringsShareNumericUnicodeAndUnknownEscapeSemantics()
    {
        await AssertOutput("""
            print("a\u0062\U0001f642")
            print(f"a\u0062\U0001f642 {3}")
            print(f"\101\x42\u0043")
            print(f"\u007b\u007d")
            print(f"{1:\u003e4}")
            print(f"\q {1}")
            print(rf"\u0061 {2}")
            """, "ab🙂\nab🙂 3\nABC\n{}\n   1\n\\q 1\n\\u0061 2\n");
    }

    [Fact]
    public async Task NamedEscapePatternsFailBeforeEffects()
    {
        var compiled = new LythonEngine().Compile("""
            print("effects")
            match "a":
                case "\N{LATIN SMALL LETTER A}":
                    print("matched")
            """);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA2000");
        var result = await compiled.RunAsync(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
