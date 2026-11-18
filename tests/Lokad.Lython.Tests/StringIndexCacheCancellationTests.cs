using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringIndexCacheCancellationTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void FailedCacheConstructionKeepsInputAndReturnsWorkingAllowance(bool refuse, bool native = false, bool warmCache = false)
    {
        var root = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = root.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var input = PyString.FromString("α😀β", context.MemoryGovernor, Span);
            Assert.Equal(3, input.Length);
            if (warmCache)
            {
                using var preparation = PyStructuralGuard.PushAmbient(context, Span);
                Assert.Equal(2, input.GetByteIndexForRuneBoundary(1));
            }
            var outer = native ? new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { CancellationToken = new CancellationToken(true) }) : context;
            using var ambient = PyStructuralGuard.PushAmbient(outer, Span);
            var before = root.ReservedBytes;
            if (refuse) hook.RefuseGrowth = _ => true;
            else hook.AfterGrowth = _ => cancellation.Cancel();

            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (native) PyIndexing.TryReadSequenceIndex(input, 1, Span, context, out _);
                else input.GetByteIndexForRuneBoundary(1);
            });
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                error.Attribution?.Reason);
            if (refuse) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            else Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(Span, error.Span);
            Assert.Same(outer, PyStructuralGuard.AmbientContext);
            Assert.Equal(before, root.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal("α😀β", input.AsString());
        }
        Assert.Equal(0, root.ReservedBytes);
        Assert.True(root.TryReserve(root.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }
}
