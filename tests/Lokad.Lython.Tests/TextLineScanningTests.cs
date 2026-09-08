using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R14: the shared UTF-8 line-boundary scan and codec-name facts. The scanner
/// covers every newline mode and multibyte boundaries through one operation so
/// future ZIP text consumers reuse it instead of adding a third line scanner;
/// the codec facts keep static diagnostics and runtime parsing consistent
/// (including the previously flagged-as-unsupported <c>utf8</c> alias).
/// </summary>
public sealed class TextLineScanningTests
{
    [Theory]
    [InlineData("abc\ndef", "PreserveLineFeed", 0, 4)]
    [InlineData("abc\ndef", "TranslateUniversal", 0, 4)]
    [InlineData("abc\ndef", "PreserveUniversal", 0, 4)]
    [InlineData("a\r\nb", "PreserveUniversal", 0, 3)]
    [InlineData("a\r\nb", "PreserveCarriageReturnLineFeed", 0, 3)]
    [InlineData("a\r\nb", "PreserveCarriageReturn", 0, 2)]
    [InlineData("a\rb", "PreserveUniversal", 0, 2)]
    [InlineData("a\rb", "PreserveCarriageReturn", 0, 2)]
    [InlineData("a\rb", "PreserveCarriageReturnLineFeed", 0, 3)]
    [InlineData("a\rb", "TranslateUniversal", 0, 3)]
    [InlineData("a\rb", "PreserveLineFeed", 0, 3)]
    [InlineData("ab", "PreserveLineFeed", 0, 2)]
    [InlineData("", "PreserveLineFeed", 0, 0)]
    [InlineData("a\nb\n", "PreserveLineFeed", 2, 4)]
    [InlineData("\r", "PreserveUniversal", 0, 1)]
    [InlineData("\r", "PreserveCarriageReturnLineFeed", 0, 1)]
    [InlineData("\r", "PreserveCarriageReturn", 0, 1)]
    [InlineData("é\n", "PreserveLineFeed", 0, 3)]
    [InlineData("🙂\r\nx", "PreserveUniversal", 0, 6)]
    [InlineData("日本語\r\n", "PreserveCarriageReturnLineFeed", 0, 11)]
    public void LineEndOffsetsFollowModeAndBytePositions(string text, string modeName, int start, int expected)
    {
        var mode = Enum.Parse<LythonRuntime.TextNewlineMode>(modeName);
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, TextLineScanning.FindLineEndByte(bytes, start, mode));
    }

    [Fact]
    public void CodecFactsAcceptRuntimeAliases()
    {
        Assert.True(StaticTextContractFacts.IsSupportedEncodingName("utf8"));
        Assert.True(StaticTextContractFacts.IsSupportedEncodingName("UTF-8"));
        Assert.True(StaticTextContractFacts.IsSupportedEncodingName("latin1"));
        Assert.False(StaticTextContractFacts.IsSupportedEncodingName("utf-16"));
        Assert.True(StaticTextContractFacts.IsSupportedNewlineName("\r\n"));
        Assert.False(StaticTextContractFacts.IsSupportedNewlineName("\n\n"));
    }

    [Fact]
    public void Utf8AliasCompilesWithoutEncodingDiagnostic()
    {
        var source = """
handle = open("/in.txt", "r", encoding="utf8")
return handle.read()
""";
        var compiled = new LythonEngine().Compile(source);
        Assert.DoesNotContain(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA3003");
        var host = new MockLythonHost();
        host.SeedFile("/in.txt", "hello utf8");
        var result = compiled.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello utf8", result.ReturnValue);
    }
}
