using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class HtmlEscapeAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(8)]
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
    public void CollectedOutputStaysOwnedUntilBatchCleanup()
    {
        var budget = new LythonMemoryBudget(100000);
        using var owner = budget.CreateScope();
        var context = Context(100000, owner);
        var weak = Abandon(context, out var charge);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(before, budget.ReservedBytes);
        Assert.True(before >= charge);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> Abandon(LythonRuntime.ExecutionContext context, out long charge)
    {
        var result = LythonRuntime.HtmlModule.EscapeText(PyString.FromString("é😀&<>\"'"), true, Span, context.Services);
        charge = result.CommittedOwnedBytes;
        return new WeakReference<PyString>(result);
    }

    private static LythonRuntime.ExecutionContext Context(long cap, ILythonMemoryBudget? budget = null)
        => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget ?? new LythonMemoryBudget(cap) });
}
