using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HtmlUnescapeAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(8)]
    [InlineData(150)]
    public void DeniedUnescapeReleasesScratchAndFreshOutput(long cap)
    {
        var context = Context(cap);
        var input = PyString.FromString("&lt;é");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.HtmlModule.UnescapeText(input, Span, context.Services));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal("&lt;é", input.AsString());
    }

    [Fact]
    public void InvalidLongReferenceAfterAnEarlierReplacementReleasesScratch()
    {
        var context = Context(100000);
        var input = PyString.FromString("&lt;&#" + new string('1', 4301) + ";");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.HtmlModule.UnescapeText(input, Span, context.Services));
            Assert.Equal("ValueError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void AbandonedDecodedOutputReclaimsItsExactOwnership()
    {
        var context = Context(100000);
        var weak = Abandon(context, out var charge);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        context.Services.State.CallTemporaries.Sweep(full: true);
        Assert.Equal(before - charge - ChargeReclamationPool.EntryChargeBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> Abandon(LythonRuntime.ExecutionContext context, out long charge)
    {
        var result = LythonRuntime.HtmlModule.UnescapeText(PyString.FromString("é😀&amp;&NotEqualTilde;"), Span, context.Services);
        charge = result.CommittedOwnedBytes;
        return new WeakReference<PyString>(result);
    }

    private static LythonRuntime.ExecutionContext Context(long cap)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
}
