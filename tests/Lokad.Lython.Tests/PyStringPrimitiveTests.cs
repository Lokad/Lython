using Lokad.Lython.Tests.Harness;
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
    [Fact]
    public void RuneOffsetCacheIsChargedOnceAcrossAliases()
    {
        // MG06: the rune-offset table is per-string state shared by every
        // alias. Indexing one alias commits it once; indexing another alias
        // reuses it instead of charging again (ASCII results are cached
        // singletons, so the table is the only charge here).
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var text = PyString.FromString("abé", context.MemoryGovernor, span);
        var alias = text;

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal("a", text.Index(0).AsString());
        Assert.Equal(32 + (4 * 4), context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
        Assert.Equal("b", alias.Index(1).AsString());
        Assert.Equal(32 + (4 * 4), context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }

    [Fact]
    public void DecodedReadsChargeNothing()
    {
        // Full decodes stay transient: repeated reads return equal values
        // without committing or retaining anything.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var text = PyString.FromString("héllo wörld", context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal("héllo wörld", text.AsString());
        Assert.Equal("héllo wörld", text.AsString());
        Assert.Equal(committedBefore, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void NoRetainedDecodedStringField()
    {
        // Decodes stay transient by construction: no instance string field
        // may retain a UTF-16 copy behind a long-lived value. The rune tables
        // (arrays) are the only retained caches and stay governed.
        var retained = typeof(PyString)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Where(static field => field.FieldType == typeof(string))
            .Select(static field => field.Name)
            .ToArray();
        Assert.Empty(retained);
    }


    [Fact]
    public void RuneTableDeniedWithoutBudget()
    {
        // The surviving cache still denies: a 4-byte string owns 132B, so a
        // 48B rune table does not fit a 150B budget.
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var pinned = new MemoryGovernor(150);
        var probe = PyString.FromString("abé", pinned, span);
        var failure = Assert.Throws<LythonRuntimeException>(() => probe.Index(1));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }
}
