using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeListConcatenationTests
{
    private static readonly LythonSourceSpan Span = new(5, 7, 2, 3);
    private static readonly Func<object, object, Context, LythonSourceSpan, string?, object> Add =
        typeof(LythonRuntime).GetMethod("EvaluateAdd", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<object, object, Context, LythonSourceSpan, string?, object>>();

    [Fact]
    public void PrepaidViewConcatenationFundsNewStorageWithoutChangingSourceCharges()
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var context = NewContext(owner);
        var source = PrepaidView(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var result = Assert.IsType<PyList>(Add(source, source, context, Span, null));
        Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
        Assert.Equal(before + result.CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(result.CommittedStorageBytes > 0);
        Assert.Equal(source.Count * 2, result.Count);
        Assert.NotSame(source, result);
        for (var i = 0; i < source.Count; i++)
        {
            Assert.Same(source[i], result[i]);
            Assert.Same(source[i], result[i + source.Count]);
        }
        Assert.Null(source.OwnerMemoryGovernor);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void RefusedPrepaidViewCopyPreservesSourceFundingAndAdmissionOrigin()
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        var context = NewContext(owner);
        var source = PrepaidView(context);
        var first = source[0];
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        using (var pressure = context.MemoryGovernor.ReserveTemporary(65536 - root.ReservedBytes, Span))
        {
            var reserved = context.MemoryGovernor.CurrentReservedBytes;
            var failure = Assert.Throws<LythonRuntimeException>(() => Add(source, source, context, Span, null));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(Span, failure.Span);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(reserved, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(65536, root.ReservedBytes);
            Assert.Equal(16, source.Count);
            Assert.Same(first, source[0]);
        }
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void DestinationStorageOutlivesRelinquishedSourceBacking()
    {
        var root = new LythonMemoryBudget(65536);
        using var destinationOwner = root.CreateScope();
        var context = NewContext(destinationOwner);
        PyList result;
        using (var sourceOwner = root.CreateScope())
        {
            var sourceGovernor = new MemoryGovernor(sourceOwner);
            var source = new PyList([PyNone.Instance], sourceGovernor, Span);
            var sourceCharge = sourceGovernor.CurrentCommittedBytes;
            result = Assert.IsType<PyList>(Add(source, source, context, Span, null));
            Assert.Equal(sourceCharge, sourceGovernor.CurrentCommittedBytes);
        }
        Assert.Equal(2, result.Count);
        Assert.Same(PyNone.Instance, result[0]);
        Assert.Same(context.MemoryGovernor, result.OwnerMemoryGovernor);
        Assert.True(result.CommittedStorageBytes > 0);
        Assert.Equal(context.MemoryGovernor.CurrentAccountedBytes, root.ReservedBytes);
        Assert.True(root.ReservedBytes >= result.CommittedStorageBytes);
        destinationOwner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void CancellationAfterAdmissionRefundsOnlyUnpublishedDestination()
    {
        var root = new LythonMemoryBudget(65536);
        using var owner = root.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var budget = new AdmissionCallbackBudget(owner);
        var context = NewContext(budget, cancellation.Token);
        var source = new PyList([PyNone.Instance, PyNone.Instance], context.MemoryGovernor, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var held = root.ReservedBytes;
        budget.AfterGrowth = cancellation.Cancel;
        var failure = Assert.Throws<LythonRuntimeException>(() => Add(source, source, context, Span, null));
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(held, root.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(2, source.Count);
        Assert.Same(PyNone.Instance, source[0]);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    private static Context NewContext(ILythonMemoryBudget budget, CancellationToken token = default)
        => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, CancellationToken = token });

    private static PyList PrepaidView(Context context)
    {
        var matcher = new LythonRuntime.DifflibSequenceMatcherObject(null, Array.Empty<object>(),
            Enumerable.Repeat<object>(BigInteger.Zero, 16).ToArray(), false, Span, context);
        Assert.True(matcher.TryGetMember("b2j", out var view));
        var source = Assert.IsType<PyList>(Assert.IsType<PyDict>(view).GetItem(BigInteger.Zero));
        Assert.Null(source.OwnerMemoryGovernor); // Exercises the actual prepaid producer.
        return source;
    }

    private sealed class AdmissionCallbackBudget(ILythonMemoryBudget inner) : ILythonMemoryBudget
    {
        internal Action? AfterGrowth;
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new Admission(this, admitted);
            return true;
        }
        private sealed class Admission(AdmissionCallbackBudget budget, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var before = inner.Bytes;
                var accepted = inner.TryResize(bytes);
                if (accepted && bytes > before && budget.AfterGrowth is { } callback)
                {
                    budget.AfterGrowth = null;
                    callback();
                }
                return accepted;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}
