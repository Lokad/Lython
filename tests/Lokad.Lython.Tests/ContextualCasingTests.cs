using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ContextualCasingTests
{
    // Independent CPython 3.13.2 outputs; includes original-input sigma context,
    // first-scalar capitalization, titlecase digraphs and full expansions.
    [Theory]
    [InlineData("\u039f\u03a3", "\u039f\u03c2", "\u039f\u03c2", "\u03bf\u03c2")]
    [InlineData("\u039f\u03a3\u0391", "\u039f\u03c3\u03b1", "\u039f\u03c3\u03b1", "\u03bf\u03c3\u03b1")]
    [InlineData("\u03a3", "\u03a3", "\u03a3", "\u03c3")]
    [InlineData("A\u03a3\u0301", "A\u03c2\u0301", "A\u03c2\u0301", "a\u03c2\u0301")]
    [InlineData("A\u03a3\u0301A", "A\u03c3\u0301a", "A\u03c3\u0301A", "a\u03c3\u0301a")]
    [InlineData("A\u03a3.A", "A\u03c3.a", "A\u03c3.A", "a\u03c3.a")]
    [InlineData("A\u03a3/A", "A\u03c2/a", "A\u03c2/A", "a\u03c2/a")]
    [InlineData("\u02b0\u03a3", "\u02b0\u03c3", "\u02b0\u03c3", "\u02b0\u03c3")]
    [InlineData("A\u02b0\u03a3", "A\u02b0\u03c2", "A\u02b0\u03c2", "a\u02b0\u03c2")]
    [InlineData("!AB\u03a3", "!ab\u03c2", "!Ab\u03c2", "!ab\u03c2")]
    [InlineData("\u0301\u039f\u03a3", "\u0301\u03bf\u03c2", "\u0301\u039f\u03c2", "\u0301\u03bf\u03c2")]
    [InlineData("\u0000A\u03a3", "\u0000a\u03c2", "\u0000A\u03c2", "\u0000a\u03c2")]
    [InlineData("\u01c4", "\u01c5", "\u01c5", "\u01c6")]
    [InlineData("\u01c5", "\u01c5", "\u01c5", "\u01c5")]
    [InlineData("\u01c6", "\u01c5", "\u01c5", "\u01c4")]
    [InlineData("\u01c7", "\u01c8", "\u01c8", "\u01c9")]
    [InlineData("\u01c8", "\u01c8", "\u01c8", "\u01c8")]
    [InlineData("\u01c9", "\u01c8", "\u01c8", "\u01c7")]
    [InlineData("\u01ca", "\u01cb", "\u01cb", "\u01cc")]
    [InlineData("\u01cb", "\u01cb", "\u01cb", "\u01cb")]
    [InlineData("\u01cc", "\u01cb", "\u01cb", "\u01ca")]
    [InlineData("\u01f1", "\u01f2", "\u01f2", "\u01f3")]
    [InlineData("\u01f2", "\u01f2", "\u01f2", "\u01f2")]
    [InlineData("\u01f3", "\u01f2", "\u01f2", "\u01f1")]
    [InlineData("\u00df", "Ss", "Ss", "SS")]
    [InlineData("\ufb03", "Ffi", "Ffi", "FFI")]
    [InlineData("\u0130", "\u0130", "\u0130", "i\u0307")]
    [InlineData("\ud801\udc00", "\ud801\udc00", "\ud801\udc00", "\ud801\udc28")]
    [InlineData("\ud801\udc28AB\u03a3", "\ud801\udc00ab\u03c2", "\ud801\udc00ab\u03c2", "\ud801\udc00ab\u03c2")]
    [InlineData("Stra\u00dfe \u0130 \ufb03 \u03a3", "Stra\u00dfe i\u0307 \ufb03 \u03c3", "Stra\u00dfe \u0130 Ffi \u03a3", "sTRASSE i\u0307 FFI \u03c3")]
    [InlineData("\u0345A\u03a3", "\u0399a\u03c2", "\u0399a\u03c2", "\u0399a\u03c2")]
    [InlineData("\u1f88", "\u1f88", "\u1f88", "\u1f88")]
    [InlineData("", "", "", "")]
    public void CaseMethodsFollowPython(string source, string capitalized, string title, string swapped)
    {
        var value = PyString.FromString(source);
        Assert.Equal(Encoding.UTF8.GetBytes(capitalized), PyStringOps.Capitalize(value).Utf8Bytes.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(title), PyStringOps.Title(value).Utf8Bytes.ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes(swapped), PyStringOps.SwapCase(value).Utf8Bytes.ToArray());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public void CasingUsesBulkStorage(int operation, bool unicode)
    {
        var warm = PyString.FromString("CAFÉΔ😀𐐀");
        _ = Apply(operation, warm);
        var input = PyString.FromString(unicode ? string.Concat(Enumerable.Repeat("CAFÉΔ😀𐐀", 512)) : new string('A', 4096));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = Apply(operation, input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // Scratch and final output plus existing fixed bookkeeping; the old
        // per-scalar CLR strings exceed this by an order of magnitude.
        Assert.True(allocated <= 3L * input.Utf8Bytes.Length + 4096, $"Casing allocated {allocated} bytes.");
        GC.KeepAlive(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FinalCopyRefusalReturnsScratchWithoutChangingInput(int operation)
    {
        const string text = "ABCD";
        var bytes = Encoding.UTF8.GetByteCount(text);
        var governor = new MemoryGovernor(new LythonMemoryBudget(2 * PyString.EstimateApproximateBytes(bytes) + bytes - 1));
        var input = PyString.FromString(text, governor, null);
        var baseline = governor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => Apply(operation, input));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Equal(baseline, governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Equal(text, input.AsString());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AbortedCasingUnwindsScratch(int operation)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
        });
        var governor = context.MemoryGovernor;
        var input = PyString.FromString(new string('A', 4096), governor, null);
        var baseline = governor.CurrentCommittedBytes;
cancellation.Cancel();
        var span = new LythonSourceSpan(0, 1, 1, 1);
        using (PyStructuralGuard.PushAmbient(context, span))
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => Apply(operation, input));
            Assert.Equal(LythonFailureReason.Cancelled , failure.Attribution?.Reason);
            Assert.Equal(span, failure.Span);
        }
        Assert.Equal(baseline, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(new string('A', 4096), input.AsString());
    }

    private static PyString Apply(int operation, PyString value) => operation switch
    {
        0 => PyStringOps.Capitalize(value),
        1 => PyStringOps.Title(value),
        _ => PyStringOps.SwapCase(value),
    };
}
