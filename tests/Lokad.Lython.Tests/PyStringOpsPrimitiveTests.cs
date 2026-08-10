using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PyStringOpsPrimitiveTests
{
    [Fact]
    public void SplitLines_IsUtf8Native()
    {
        var lines = PyStringOps.SplitLines(PyString.FromString("a\nb\nc\n"));

        Assert.Equal(["a", "b", "c"], lines.Select(item => ((PyString)item).AsString()).ToArray());
    }

    [Fact]
    public void SplitWhitespaceAndSplit_WithSeparator_WorkAsExpected()
    {
        var whitespace = PyStringOps.SplitWhitespace(PyString.FromString(" α\tβ  😀 "));
        var separated = PyStringOps.Split(PyString.FromString("a::b::"), PyString.FromString("::"));

        Assert.Equal(["α", "β", "😀"], whitespace.Select(item => ((PyString)item).AsString()).ToArray());
        Assert.Equal(["a", "b", ""], separated.Select(item => ((PyString)item).AsString()).ToArray());
    }

    [Fact]
    public void StripVariants_RemoveUnicodeWhitespace()
    {
        var text = PyString.FromString("\u2003é😀\u00A0");

        Assert.Equal("é😀", PyStringOps.Strip(text).AsString());
        Assert.Equal("é😀\u00A0", PyStringOps.LStrip(text).AsString());
        Assert.Equal("\u2003é😀", PyStringOps.RStrip(text).AsString());
    }

    [Fact]
    public void FormatAndEscapeRegex_HandleRuntimeValues()
    {
        var formatted = PyStringOps.Format(
            PyString.FromString("value={0}, none={1}, braces={{ok}}"),
            [new BigInteger(12), PyNone.Instance]);
        var escaped = PyStringOps.EscapeRegex(PyString.FromString("a+b?(é)"));

        Assert.Equal("value=12, none=None, braces={ok}", formatted.AsString());
        Assert.Equal(@"a\+b\?\(é\)", escaped.AsString());
    }
}
