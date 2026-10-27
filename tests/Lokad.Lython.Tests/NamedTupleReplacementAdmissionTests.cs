using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NamedTupleReplacementAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    private static LythonRuntime.ICallable Replace(PyNamedTupleObject value)
    {
        Assert.True(value.TryGetMember("_replace", out var member));
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusalOrCancellationPrecedesTheValueArrayCopyAndPreservesTheSource(bool cancelAfterAdmission)
    {
        const int count = 8192;
        var inputs = new LythonMemoryBudget();
        var operation = cancelAfterAdmission ? new LythonMemoryBudget() : new LythonMemoryBudget(32768);
        using var cancellation = new CancellationTokenSource();
        using (var inputOwner = inputs.CreateScope())
        using (var owner = operation.CreateScope())
        {
            var governor = new MemoryGovernor(inputOwner);
            var budget = new CancelOnBackingAdmission(owner, cancellation);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            {
                MemoryBudget = cancelAfterAdmission ? budget : owner,
                CancellationToken = cancellation.Token,
            });
            // Warm the callable outside the allocation window with a small record.
            var smallType = new PyNamedTupleType("Small", ["x"]);
            _ = Replace(new PyNamedTupleObject(smallType, [BigInteger.One])).Invoke([], Span,
                new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions()));
            var type = new PyNamedTupleType("Large", Enumerable.Range(0, count).Select(i => "f" + i).ToArray(), null, governor, Span);
            var source = new PyNamedTupleObject(type, Enumerable.Repeat<object>(BigInteger.One, count).ToArray(), governor, Span);
            var replace = Replace(source);
            var inputBaseline = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => replace.Invoke([], Span, context));
#if !DEBUG
            // One old reference-array clone alone exceeds this broad bound.
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < count * IntPtr.Size);
#endif
            if (cancelAfterAdmission)
            {
                Assert.True(budget.Triggered);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
            }
            else
            {
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            }
            Assert.Equal(BigInteger.One, source[0]);
            Assert.Equal(BigInteger.One, source[count - 1]);
            Assert.Equal(inputBaseline, inputs.ReservedBytes);
            Assert.Equal(0, operation.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }

    [Fact]
    public void ReplacementRetainsAliasesAndFundingWithoutMutatingTheSource()
    {
        var root = new LythonMemoryBudget(65536);
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var type = new PyNamedTupleType("P", ["x", "y"], null, context.MemoryGovernor, Span);
            var shared = new PyList([BigInteger.One], context.MemoryGovernor, Span);
            var values = new object[] { shared, new BigInteger(2) };
            var source = new PyNamedTupleObject(type, values, context.MemoryGovernor, Span);
            values[1] = new BigInteger(99); // Construction still copies caller-owned arrays.
            var baseline = root.ReservedBytes;
            var result = Assert.IsType<PyNamedTupleObject>(Replace(source).Invoke([CallArgumentValue.Keyword("y", new BigInteger(3))], Span, context));
            Assert.Same(type, result.Type);
            Assert.Same(shared, result[0]);
            Assert.Same(shared, source[0]);
            Assert.Equal(new BigInteger(2), source[1]);
            Assert.Equal(new BigInteger(3), result[1]);
            Assert.True(root.ReservedBytes > baseline);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class CancelOnBackingAdmission(ILythonMemoryBudget inner, CancellationTokenSource cancellation) : ILythonMemoryBudget
    {
        public bool Triggered { get; private set; }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Admission(this, actual);
            Admitted(bytes);
            return true;
        }
        private void Admitted(long bytes)
        {
            if (bytes >= 8192 * IntPtr.Size) { Triggered = true; cancellation.Cancel(); }
        }
        private sealed class Admission(CancelOnBackingAdmission owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                if (!inner.TryResize(bytes)) return false;
                owner.Admitted(bytes);
                return true;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
