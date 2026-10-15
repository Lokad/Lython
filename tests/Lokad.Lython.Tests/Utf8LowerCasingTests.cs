using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class Utf8LowerCasingTests
{
    [Theory]
    [InlineData("ASCII NUL\0ÉΔ😀𐐀", "ascii nul\0éδ😀𐐨")]
    [InlineData("ȺKİΟΣ", "ⱥki\u0307ος")]
    [InlineData("İΣ́AΣ́", "i\u0307σ́aς́")]
    [InlineData("", "")]
    public void ScalarWidthsExpansionAndSigmaKeepTheirUtf8Result(string source, string expected)
    {
        var lowered = PyStringOps.Lower(PyString.FromString(source));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), lowered.Utf8Bytes.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryLoweringDoesNotAllocatePerScalarStrings(bool unicode)
    {
        _ = PyStringOps.Lower(PyString.FromString("CAFÉΔ😀𐐀"));
        var text = unicode ? string.Concat(Enumerable.Repeat("CAFÉΔ😀𐐀", 512)) : new string('A', 4096);
        var input = PyString.FromString(text);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = PyStringOps.Lower(input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // Input-sized scratch plus owned output and fixed bookkeeping. The
        // former per-scalar CLR strings exceed this by an order of magnitude.
        Assert.True(allocated <= 3L * input.Utf8Bytes.Length + 4096, $"Lower allocated {allocated} bytes.");
        GC.KeepAlive(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbortedLoweringReleasesScratchAndPreservesInput(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MaxExecutionSteps = cancel ? null : 1,
        });
        var governor = context.MemoryGovernor;
        var input = PyString.FromString(new string('A', 4096), governor, null);
        var before = governor.CurrentCommittedBytes;
        if (cancel) cancellation.Cancel();
        var span = new LythonSourceSpan(0, 1, 1, 1);
        using (PyStructuralGuard.PushAmbient(context, span))
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => PyStringOps.Lower(input));
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
            Assert.Equal(span, failure.Span);
        }
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(new string('A', 4096), input.AsString());
    }
}
