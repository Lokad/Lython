using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AdjacentBytesLiteralTests
{
    [Fact]
    public async Task AdjacentLiteralsCombineQuotingPrefixesAndEscapes()
    {
        await AssertOutput("""
            value = b"a" B'b' br"\n" RB'\x41' b"\x42\103"
            print(value, len(value))
            print(b"" b"" b"x")
            """, "b'ab\\\\n\\\\x41BC' 10\nb'x'\n");
    }

    [Fact]
    public async Task ImplicitAndExplicitLineJoiningConcatenateBytes()
    {
        await AssertOutput(""""
            value = (
                b"a" # a piece
                b'''b
            c''' b"d"
            )
            other = b"e" \
                B"f"
            print(value, other)
            """", "b'ab\\ncd' b'ef'\n");
    }

    [Fact]
    public async Task LiteralAndMappingPatternsUseTheCombinedConstant()
    {
        await AssertOutput("""
            match b"ab":
                case b"a" B"b":
                    print("literal")
            match {b"ab": 3, b"extra": 4}:
                case {b"a" b"b": value, **rest}:
                    print(value, rest)
            match b"ab":
                case b"c" b"d" | b"a" b"b":
                    print("alternative")
            """, "literal\n3 {b'extra': 4}\nalternative\n");
    }

    [Fact]
    public async Task AdjacentConstantsWorkInsideFormattedFieldsAndClassBodies()
    {
        await AssertOutput("""
            print(f"{b'a' b'b'}")
            class C:
                value = b"a" b"b"
                print(value)
            print(C.value)
            """, "b'ab'\nb'ab'\nb'ab'\n");
    }

    [Theory]
    [InlineData("b")]
    [InlineData("B")]
    [InlineData("br")]
    [InlineData("Br")]
    [InlineData("bR")]
    [InlineData("BR")]
    [InlineData("rb")]
    [InlineData("rB")]
    [InlineData("Rb")]
    [InlineData("RB")]
    public async Task EverySupportedBytesPrefixParticipates(string prefix)
    {
        await AssertOutput("print(b'a' " + prefix + "'b' b'c')\n", "b'abc'\n");
    }

    [Fact]
    public async Task CombinedBytesRenderControlCharactersAndQuotesLikePython()
    {
        await AssertOutput("""
            print(b"\t" b"\n" b"\r" b"\x00")
            print(b"'" b"a")
            print(b"'" b'"')
            print(b'"' b"a")
            """, "b'\\t\\n\\r\\x00'\nb\"'a\"\nb'\\'\"'\nb'\"a'\n");
    }

    [Theory]
    [InlineData("b'a' 'b'")]
    [InlineData("'a' b'b'")]
    [InlineData("b'a' u'b'")]
    [InlineData("b'a' f'{1}'")]
    [InlineData("b'a' r'b'")]
    [InlineData("br'a' U'b'")]
    [InlineData("b 'a'")]
    [InlineData("b'a' RB 'b'")]
    [InlineData("b'a' b'é'")]
    public async Task InvalidAdjacencyFailsBeforeEffects(string expression)
    {
        var compiled = new LythonEngine().Compile("print('effects')\nvalue = " + expression + "\n");
        Assert.False(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Fact]
    public async Task ManyPiecesFoldToOneSourceBoundedConstant()
    {
        var source = "print(len(" + string.Concat(Enumerable.Repeat("b'x' ", 20000)) + "))\n";
        await AssertOutput(source, "20000\n");
    }

    [Fact]
    public void SourceLimitRejectsOversizedLiteralSequencesBeforeParsing()
    {
        var source = "value = " + string.Concat(Enumerable.Repeat("b'x' ", LythonEngine.MaxSourceLength / 5 + 1)) + "\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA0002");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombinedConstantsRespectExecutionMemoryLimits(bool classBody)
    {
        var pieces = string.Concat(Enumerable.Repeat("b'x' ", 20000));
        var source = classBody ? "class C:\n    value = " + pieces + "\n" : "print(len(" + pieces + "))\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16384 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
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
