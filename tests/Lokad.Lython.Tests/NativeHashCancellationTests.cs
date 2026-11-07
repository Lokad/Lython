using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class NativeHashCancellationTests
{
    private static readonly LythonSourceSpan Span = new(13, 4, 2, 6);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdKeysUseTheirCurrentCallerWithoutAmbientState(bool binary)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            object value = binary ? new PyBytes("native"u8.ToArray(), context.MemoryGovernor, Span)
                : PyString.FromString("native", context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            Assert.Null(PyStructuralGuard.AmbientContext);
            cancellation.Cancel();
            var sync = Assert.Throws<LythonRuntimeException>(() => PyHashProtocols.GetProtocolHash(value, context, Span));
            var async = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await PyHashProtocols.GetProtocolHashAsync(value, context, Span));
            Assert.Equal(LythonFailureReason.Cancelled, sync.Attribution?.Reason);
            Assert.Equal(LythonFailureReason.Cancelled, async.Attribution?.Reason);
            Assert.Equal(Span, sync.Span);
            Assert.Equal(Span, async.Span);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(PyStructuralGuard.AmbientContext);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CachedHashesDoNotRetainAnEarlierCallersToken(bool binary)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var firstCancellation = new CancellationTokenSource())
        using (var secondCancellation = new CancellationTokenSource())
        {
            var first = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = firstCancellation.Token });
            var second = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = secondCancellation.Token });
            object value = binary ? new PyBytes("cached"u8.ToArray(), first.MemoryGovernor, Span)
                : PyString.FromString("cached", first.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            var expected = PyHashProtocols.GetProtocolHash(value, first, Span);
            firstCancellation.Cancel();
            Assert.Equal(expected, PyHashProtocols.GetProtocolHash(value, second, Span));
            secondCancellation.Cancel();
            var failure = Assert.Throws<LythonRuntimeException>(() => PyHashProtocols.GetProtocolHash(value, second, Span));
            Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            Assert.Equal(baseline, budget.ReservedBytes);
            first.State.CloseResources();
            second.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DirectNativeReadAndWriteObserveCancellationBeforeMutation()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var dictionary = new PyDict(context.MemoryGovernor, Span);
            var stored = PyString.FromString("key", context.MemoryGovernor, Span);
            dictionary.SetItem(stored, PyNone.Instance);
            var requested = PyString.FromString("key", context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            cancellation.Cancel();
            var read = Assert.Throws<LythonRuntimeException>(() =>
                dictionary.TryReadNativeStringValue(requested, context, Span, out _));
            var write = Assert.Throws<LythonRuntimeException>(() =>
                dictionary.TryWriteKeyWithoutAdmission(requested, true, context, Span));
            Assert.Equal(LythonFailureReason.Cancelled, read.Attribution?.Reason);
            Assert.Equal(LythonFailureReason.Cancelled, write.Attribution?.Reason);
            Assert.Single(dictionary);
            Assert.True(dictionary.TryGetValue(stored, out var value));
            Assert.Same(PyNone.Instance, value);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstRefusedAdmissionRetainsPriorityOverHashCancellation(bool binary)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            object value = binary ? new PyBytes("key"u8.ToArray(), context.MemoryGovernor, Span)
                : PyString.FromString("key", context.MemoryGovernor, Span);
            var origin = new LythonSourceSpan(17, 8, 2, 5);
            var refusal = Assert.Throws<LythonRuntimeException>(() =>
                context.MemoryGovernor.Reserve(budget.LimitBytes!.Value + 1, origin));
            cancellation.Cancel();
            var failure = Assert.Throws<LythonRuntimeException>(() => PyHashProtocols.GetProtocolHash(value, context, Span));
            Assert.Same(refusal, failure);
            Assert.Equal(origin, failure.Span);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveNativeHashStopsBeforePublishingItsCache(bool binary)
    {
        const int length = 64 * 1024 * 1024;
        var budget = new LythonMemoryBudget(96 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var nextCaller = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var bytes = binary ? PyBytes.EstimateApproximateBytes(length) : PyString.EstimateApproximateBytes(length);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, Span);
            var input = new byte[length];
            Array.Fill(input, (byte)'a');
            object value = binary ? new PyBytes(input, context.MemoryGovernor, Span, storage)
                : PyString.FromOwnedUtf8(input, context.MemoryGovernor, Span, storage);
            object small = binary ? new PyBytes("warm"u8.ToArray()) : PyString.FromString("warm");
            var warm = HashMember(small);
            _ = warm.Invoke([], Span, context);
            var callable = HashMember(value);
            var baseline = budget.ReservedBytes;
            using var started = new ManualResetEventSlim();
            var run = Task.Factory.StartNew(() =>
            {
                started.Set();
                return callable.Invoke([], Span, context);
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            try
            {
                Assert.True(started.Wait(TimeSpan.FromSeconds(10)));
                // Warmed native entry, then cancel while the substantial scan
                // remains active. No interpreter exit can supply the failure.
                Thread.SpinWait(10_000);
                Assert.False(run.IsCompleted);
                cancellation.Cancel();
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                    await run.WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                Assert.Equal(Span, failure.Span);
            }
            finally
            {
                cancellation.Cancel();
                try { await run.WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (LythonRuntimeException failure) when (failure.Attribution?.Reason == LythonFailureReason.Cancelled) { }
            }
            // A completed cache would bypass the scan even for this token. A
            // refusal here proves that cancellation did not finish/cache it.
            var retry = Assert.Throws<LythonRuntimeException>(() =>
            {
                _ = value is PyString text ? text.GetHashCode(cancellation.Token, Span)
                    : ((PyBytes)value).GetHashCode(cancellation.Token, Span);
            });
            Assert.Equal(LythonFailureReason.Cancelled, retry.Attribution?.Reason);
            var completed = callable.Invoke([], Span, nextCaller);
            Assert.Equal(completed, callable.Invoke([], Span, nextCaller));
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, nextCaller.State.CurrentInterpreterDepth);
            Assert.Null(PyStructuralGuard.AmbientContext);
            Assert.Equal((byte)'a', input[0]);
            context.State.CloseResources();
            nextCaller.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8195)]
    public void ChunkedNativeHashesPreserveWholeBufferValues(int length)
    {
        var input = new byte[length];
        for (var index = 0; index < input.Length; index++) input[index] = (byte)('a' + index % 26);
        var expected = new HashCode();
        expected.AddBytes(input);
        Assert.Equal(expected.ToHashCode(), new PyBytes(input).GetPyHashCode());
        Assert.Equal(expected.ToHashCode(), PyString.FromOwnedUtf8(input).GetPyHashCode());
    }

    private static LythonRuntime.ICallable HashMember(object value)
    {
        object? member;
        var found = value is PyBytes bytes
            ? LythonRuntime.BytesMembers.TryGetMember(bytes, "__hash__", out member)
            : LythonRuntime.StringMembers.TryGetMember((PyString)value, "__hash__", out member);
        Assert.True(found);
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
    }
}
