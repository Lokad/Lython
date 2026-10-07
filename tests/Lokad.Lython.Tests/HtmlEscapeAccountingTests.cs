using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HtmlEscapeAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(8)]
    [InlineData(150)]
    public void DeniedEscapeReleasesScratchAndFreshOutput(long cap)
    {
        var context = Context(cap);
        var input = PyString.FromString("<");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.HtmlModule.EscapeText(input, true, Span, context.Services));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal("<", input.AsString());
    }

    [Fact]
    public void UnchangedEscapeRetainsItsOwnedInputWithoutNewCharges()
    {
        var context = Context(512);
        var input = PyString.FromString("plain é😀", context.MemoryGovernor, Span);
        context.Services.State.CallTemporaries.TrackFreshString(input, Span);
        context.Services.State.CallTemporaries.Sweep(full: true);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Assert.Same(input, LythonRuntime.HtmlModule.EscapeText(input, true, Span, context.Services));
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(input);
    }

    [Fact]
    public void AbandonedOutputReclaimsItsExactOwnership()
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
        var result = LythonRuntime.HtmlModule.EscapeText(PyString.FromString("é😀&<>\"'"), true, Span, context.Services);
        charge = result.CommittedOwnedBytes;
        return new WeakReference<PyString>(result);
    }

    private static LythonRuntime.ExecutionContext Context(long cap)
        => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = cap });
}
