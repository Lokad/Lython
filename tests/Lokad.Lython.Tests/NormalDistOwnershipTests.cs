using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NormalDistOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData("construct"), InlineData("sum"), InlineData("negate"), InlineData("from-samples")]
    public void FreshModelsRemainFundedWhileAliasesAddNoStorage(string operation)
    {
        var root = new LythonMemoryBudget();
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var source = LythonRuntime.StatisticsModule.NormalDistType.Invoke([], Span, context);
            Assert.True(LythonRuntime.StatisticsModule.NormalDistType.TryGetMember("from_samples", out var method));
            var samples = Assert.IsAssignableFrom<LythonRuntime.ICallable>(method);
            var inputs = new PyList([BigInteger.One, new BigInteger(2), new BigInteger(3)], context.MemoryGovernor, Span);
            var arguments = new[] { CallArgumentValue.Positional(inputs) };
            object Create()
            {
                if (operation == "construct") return LythonRuntime.StatisticsModule.NormalDistType.Invoke([], Span, context);
                if (operation == "from-samples") return samples.Invoke(arguments, Span, context);
                object? value;
                var success = operation == "sum"
                    ? LythonRuntime.StatisticsModule.TryAddNormalDist(source, BigInteger.One, Span, context, out value)
                    : LythonRuntime.StatisticsModule.TryUnaryNormalDist(source, true, context, Span, out value);
                Assert.True(success); return value!;
            }
            _ = Create(); // Prepare metadata before observing retained model storage.
            var baseline = root.ReservedBytes;
            var models = new object[8];
            for (var i = 0; i < models.Length; i++) models[i] = Create();
            Assert.All(models, value => Assert.IsType<LythonRuntime.StatisticsModule.PyNormalDist>(value));
            Assert.True(root.ReservedBytes > baseline);
            var retained = root.ReservedBytes;
            Array.Fill(models, source);
            // Replacing test aliases does not finish the explicit execution owner.
            Assert.Equal(retained, root.ReservedBytes);
            for (var i = 0; i < models.Length; i++) Assert.Same(source, models[i]);
            Assert.Equal(retained, root.ReservedBytes);
            Array.Clear(models);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void SampleCapacityAdmissionPrecedesRandomDrawsAndUnwinds(bool cancel)
    {
        var root = new LythonMemoryBudget();
        using (var owner = root.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var budget = new MajorAdmission(owner, cancellation, cancel);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = budget, CancellationToken = cancellation.Token });
            var model = Assert.IsType<LythonRuntime.StatisticsModule.PyNormalDist>(
                LythonRuntime.StatisticsModule.NormalDistType.Invoke([], Span, context));
            Assert.True(model.TryGetMember("samples", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            _ = callable.Invoke([CallArgumentValue.Positional(BigInteger.One)], Span, context);
            var arguments = new[] { CallArgumentValue.Positional(new BigInteger(8192)) };
            var state = context.State.RandomState.Snapshot();
            var baseline = root.ReservedBytes;
            budget.Armed = true;
            var allocation = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(arguments, Span, context));
#if !DEBUG
            if (!cancel) Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocation < 8192 * IntPtr.Size);
#endif
            Assert.True(budget.Triggered);
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Equal(state, context.State.RandomState.Snapshot());
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    [Fact]
    public void FailedQuantileConstructionReleasesUnpublishedCapacity()
    {
        var root = new LythonMemoryBudget();
        using (var owner = root.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var model = Assert.IsType<LythonRuntime.StatisticsModule.PyNormalDist>(LythonRuntime.StatisticsModule.NormalDistType.Invoke(
                [CallArgumentValue.Positional(BigInteger.Zero), CallArgumentValue.Positional(BigInteger.Zero)], Span, context));
            Assert.True(model.TryGetMember("quantiles", out var member));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
            var baseline = root.ReservedBytes;
            var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke([CallArgumentValue.Positional(new BigInteger(1024))], Span, context));
            Assert.Equal("StatisticsError", error.ExceptionType);
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }

    private sealed class MajorAdmission(ILythonMemoryBudget inner, CancellationTokenSource cancellation, bool cancel) : ILythonMemoryBudget
    {
        public bool Armed { get; set; }
        public bool Triggered { get; private set; }
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        private bool Admit(long previous, long bytes)
        {
            if (!Armed || bytes - previous < 8192) return true;
            Triggered = true;
            if (cancel) cancellation.Cancel();
            return cancel;
        }
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!Admit(0, bytes) || !inner.TryReserve(bytes, out var actual)) { reservation = null; return false; }
            reservation = new Admission(this, actual); return true;
        }
        private sealed class Admission(MajorAdmission owner, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes) => owner.Admit(inner.Bytes, bytes) && inner.TryResize(bytes);
            public void Dispose() => inner.Dispose();
        }
    }
}
