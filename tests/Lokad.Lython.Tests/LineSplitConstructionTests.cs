using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LineSplitConstructionTests
{
    private static readonly LythonSourceSpan Span = new(1, 1, 1, 1);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnicodeLineBreaksAcrossChunksKeepPythonTrailingLineRules(bool keepEnds)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            foreach (var length in new[] { 4094, 4095 })
            foreach (var separator in new[] { "\r\n", "\r", "\n", "\v", "\f", "\u001c", "\u001d", "\u001e", "\u0085", "\u2028", "\u2029" })
            {
                var prefix = new string('a', length);
                const string content = "é\u00a0\u2027😀\u001f";
                var text = prefix + separator + content + separator + separator;
                var input = PyString.FromString(text, context.MemoryGovernor, Span);
                var result = PyStringOps.SplitLines(input, keepEnds, context.MemoryGovernor, Span, context);
                var ending = keepEnds ? separator : "";
                Assert.Equal(new[] { prefix + ending, content + ending, ending }, result.Select(x => Assert.IsType<PyString>(x).AsString()));
                Assert.Equal(text, input.AsString());
                Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Empty(PyStringOps.SplitLines(PyString.Empty, keepEnds, context.MemoryGovernor, Span, context));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UnbrokenScanObservesTheActiveContextBeforeConstruction()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var input = PyString.FromString(new string('\u00a0', 32768), context.MemoryGovernor, Span);
            var held = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            cancellation.Cancel();
            using var ambient = PyStructuralGuard.PushAmbient(context, Span);
            var error = Assert.Throws<LythonRuntimeException>(() => PyStringOps.SplitLines(input, false, context.MemoryGovernor, Span));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedFinalStoragePublishesNothingAndRefundsWorkingStorage()
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            _ = PyStringOps.SplitLines(PyString.FromString("warm\nup"));
            var input = PyString.FromString("a\n" + new string('x', 262144));
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 8192, out var blocker));
            using (blocker)
            {
                var held = budget.ReservedBytes;
                var committed = context.MemoryGovernor.CurrentCommittedBytes;
                PyList? published = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                var error = Assert.Throws<LythonRuntimeException>(() => published = PyStringOps.SplitLines(input, false, context.MemoryGovernor, Span, context));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Null(published);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.InRange(allocated, 0, 32768);
                Assert.Equal(262146, input.ByteLength);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
