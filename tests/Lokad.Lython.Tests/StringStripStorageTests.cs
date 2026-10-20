using System.Text;
using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringStripStorageTests
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData("strip")]
    [InlineData("lstrip")]
    [InlineData("rstrip")]
    public void FreshConstantSlicePreflightsThroughTheActiveOwner(string method)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var source = PyString.FromString(" word ");
            var baseline = budget.ReservedBytes;
            using (context.MemoryGovernor.ReserveTemporary(limit - baseline, Span))
            using (PyStructuralGuard.PushAmbient(context, Span))
            {
                var error = Assert.Throws<LythonRuntimeException>(() => method switch
                {
                    "strip" => PyStringOps.Strip(source),
                    "lstrip" => PyStringOps.LStrip(source),
                    _ => PyStringOps.RStrip(source),
                });
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(Span, error.Span);
                Assert.Equal(" word ", source.AsString());
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("strip")]
    [InlineData("lstrip")]
    [InlineData("rstrip")]
    public void CustomSetNeedsActiveAllowanceEvenWhenTheSourceDoesNotChange(string method)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        using (var sourceOwner = budget.CreateScope())
        {
            var sourceGovernor = new MemoryGovernor(sourceOwner);
            var source = PyString.FromString("word", sourceGovernor, Span);
            var chars = PyString.FromString("x", sourceGovernor, Span);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var callable = Resolve(source, method, context);
            var sourceFunding = sourceGovernor.CurrentAccountedBytes;
            var baseline = context.MemoryGovernor.CurrentAccountedBytes;
            using (context.MemoryGovernor.ReserveTemporary(limit - budget.ReservedBytes, Span))
            {
                // An empty set borrows the unchanged source without working storage.
                Assert.Same(source, callable.Invoke([CallArgumentValue.Positional(PyString.Empty)], Span, context));
                var error = Assert.Throws<LythonRuntimeException>(() =>
                    callable.Invoke([CallArgumentValue.Positional(chars)], Span, context));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(Span, error.Span);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal("word", source.AsString());
                Assert.Equal(sourceFunding, sourceGovernor.CurrentAccountedBytes);
            }
            Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("strip")]
    [InlineData("lstrip")]
    [InlineData("rstrip")]
    public void RepeatedUnchangedResultsReturnScratchImmediately(string method)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var source = PyString.FromString("word");
            var chars = PyString.FromString(string.Concat(Enumerable.Repeat("é😀", 128)));
            var callable = Resolve(source, method, context);
            var baseline = budget.ReservedBytes;
            for (var repeat = 0; repeat < 3; repeat++)
            {
                Assert.Same(source, callable.Invoke([CallArgumentValue.Positional(chars)], Span, context));
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Null(PyStructuralGuard.AmbientContext);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("strip")]
    [InlineData("lstrip")]
    [InlineData("rstrip")]
    public void InvalidCustomUtf8StillFailsAndReleasesScratch(string method)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var callable = Resolve(PyString.Empty, method, context);
            var malformed = PyString.FromOwnedUtf8([(byte)'x', 0xff]);
            var baseline = budget.ReservedBytes;
            Assert.Throws<DecoderFallbackException>(() => callable.Invoke([CallArgumentValue.Positional(malformed)], Span, context));
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Null(PyStructuralGuard.AmbientContext);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("left")]
    [InlineData("right")]
    [InlineData("whitespace-left")]
    [InlineData("whitespace-right")]
    public void PreparationAndBoundaryLoopsCheckCancellation(string phase)
    {
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { CancellationToken = cancellation.Token });
        var padding = new string(phase.StartsWith("whitespace", StringComparison.Ordinal) ? ' ' : 'x', 256);
        var source = PyString.FromString(phase == "set" ? "word" : phase.EndsWith("right", StringComparison.Ordinal) ? "word" + padding : padding + "word");
        var chars = PyString.FromString(phase == "set" ? padding : "x");
        using var ambient = PyStructuralGuard.PushAmbient(context, Span);
        cancellation.Cancel();
        // Invoke the primitive itself: caller entry/exit checks cannot make this pass.
        var error = Assert.Throws<LythonRuntimeException>(() => phase switch
        {
            "set" => PyStringOps.Strip(source, chars),
            "left" => PyStringOps.LStrip(source, chars),
            "right" => PyStringOps.RStrip(source, chars),
            "whitespace-left" => PyStringOps.LStrip(source),
            _ => PyStringOps.RStrip(source),
        });
        Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
    }

    [Theory]
    [InlineData("strip", "set")]
    [InlineData("lstrip", "left")]
    [InlineData("rstrip", "right")]
    [InlineData("strip", "left")]
    [InlineData("strip", "right")]
    public void CancellationAfterMethodEntryReleasesAdmittedScratch(string method, string phase)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var supplied = new CancelOnGrowthBudget(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = supplied, CancellationToken = cancellation.Token });
            var padding = new string('x', 256);
            var source = PyString.FromString(phase == "set" ? "word" : phase == "left" ? padding + "word" : "word" + padding);
            var chars = PyString.FromString(phase == "set" ? padding : "x");
            var callable = Resolve(source, method, context);
            var baseline = budget.ReservedBytes;
            // The budget cancels only after the method's scratch is admitted,
            // past callable entry. The actual preparation/boundary loop must stop.
            supplied.Armed = true;
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([CallArgumentValue.Positional(chars)], Span, context));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(PyStructuralGuard.AmbientContext);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRuntime.ICallable Resolve(PyString source, string method, LythonRuntime.ExecutionContext context)
    {
        Assert.True(PyMemberAccess.TryResolve(source, method, context, Span, out var member));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
    }

    private sealed class CancelOnGrowthBudget(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        internal bool Armed;
        private void Cancel() { Armed = false; cancellation.Cancel(); }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var storage)) { reservation = null; return false; }
            reservation = new Reservation(storage, this);
            return true;
        }
        private sealed class Reservation(ILythonMemoryReservation inner, CancelOnGrowthBudget owner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var growth = bytes > inner.Bytes;
                if (!inner.TryResize(bytes)) return false;
                if (growth && owner.Armed) owner.Cancel();
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
