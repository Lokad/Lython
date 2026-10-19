using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Enumerate results preserve value/alias semantics and own conservative storage
// under the execution batch; later batches reuse the allowance immediately.
[Collection("AllocationSensitive")]
public sealed class IteratorItemAccountingTests
{
    [Fact]
    public void EnumerateItemsKeepTheirStorageFunded()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(30000000) },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList(new object[] { new BigInteger(10), new BigInteger(20) }, context.MemoryGovernor, span);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;

        var iterator = new PyEnumerateIterator(source, BigInteger.Zero, span, context);
        Assert.True(iterator.TryMoveNext(out var first));
        Assert.True(iterator.TryMoveNext(out var second));
        Assert.Equal(new BigInteger(0), ((PyTuple)first)[0]);
        Assert.Equal(new BigInteger(1), ((PyTuple)second)[0]);
        Assert.Equal(committedBefore + 128L + ((PyTuple)first).CommittedStorageBytes +
            ((PyTuple)second).CommittedStorageBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(iterator);
        GC.KeepAlive(source);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void EnumerateReturnsTheActualYieldedTuple()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(30000000) },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList(new object[] { new BigInteger(10) }, context.MemoryGovernor, span);
        var iterator = new PyEnumerateIterator(source, BigInteger.Zero, span, context);
        Assert.True(iterator.TryMoveNext(out var first));

        GC.KeepAlive(iterator);
        GC.KeepAlive(source);
        GC.KeepAlive(first);
    }

    [Fact]
    public void EnumerateBatchesReturnAllowanceAfterTheirUsersFinish()
    {
        var budget = new LythonMemoryBudget(16384);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        for (var batch = 0; batch < 8; batch++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
                AbandonEnumeratePulls(context, span);
                Assert.True(budget.ReservedBytes > 0);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    // All pulled tuples and the iterator die with this frame, so no test slots root them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonEnumeratePulls(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var range = new PyRange(BigInteger.Zero, new BigInteger(16), BigInteger.One);
        var iterator = new PyEnumerateIterator(range, BigInteger.Zero, span, context);
        for (var i = 0; i < 16; i++)
        {
            Assert.True(iterator.TryMoveNext(out _));
        }
    }
}
