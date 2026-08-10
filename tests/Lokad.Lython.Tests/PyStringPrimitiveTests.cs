using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PyStringPrimitiveTests
{
    [Fact]
    public void LengthIndexAndSlice_AreRuneBased()
    {
        var text = PyString.FromString("a😀bé");

        Assert.Equal(4, text.Length);
        Assert.Equal("😀", text.Index(1).AsString());
        Assert.Equal("é", text.Index(3).AsString());
        Assert.Equal("😀é", text.Slice([1, 3]).AsString());
    }

    [Fact]
    public void SteppedUnicodeSlices_HandleReverseAndExtremeSteps()
    {
        var text = PyString.FromString("aβ😀é");

        Assert.Equal("é😀βa", text.Slice(new PyIndexing.SliceBounds(3, -1, -1)).AsString());
        Assert.Equal("é", text.Slice(new PyIndexing.SliceBounds(3, -1, int.MinValue)).AsString());
    }

    [Fact]
    public void FindCountReplaceAndCompare_FollowUtf8Semantics()
    {
        var text = PyString.FromString("é😀é😀");
        var eAcute = PyString.FromString("é");
        var emoji = PyString.FromString("😀");

        Assert.True(text.StartsWith(eAcute));
        Assert.True(text.EndsWith(emoji));
        Assert.True(text.Contains(PyString.FromString("😀é")));
        Assert.Equal(1, text.Find(emoji));
        Assert.Equal(2, text.Count(eAcute));
        Assert.Equal("-😀-😀", text.Replace(eAcute, PyString.FromString("-")).AsString());
        Assert.True(PyString.CompareOrdinal(PyString.FromString("abc"), PyString.FromString("abd")) < 0);
    }

    [Fact]
    public void RepeatEnumerateAndEquality_WorkAsExpected()
    {
        var rune = PyString.FromString("β");
        var repeated = rune.Repeat(3);

        Assert.Equal("βββ", repeated.AsString());
        Assert.Equal(["β", "β", "β"], repeated.EnumerateRunes().Select(r => r.AsString()).ToArray());
        Assert.Equal(PyString.FromString("βββ"), repeated);
        Assert.Equal(repeated.GetHashCode(), PyString.FromString("βββ").GetHashCode());
    }
}
