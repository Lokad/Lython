using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: constructed iterator objects own their storage like other
/// constructed values; each live iterator commits one unit.
/// </summary>
public sealed class IteratorValueAccountingTests
{
    private sealed class StubCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            return PyNone.Instance;
        }
    }

    [Fact]
    public async Task IteratorGraphsRemainFundedUntilTheirOwnerEnds()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList([PyNone.Instance]);
        var map = await PyMapIterator.CreateAsync([new StubCallable(), source], context, span, false);
        var funded = budget.ReservedBytes;
        Assert.True(funded > PyIteratorBase.IteratorValueBytes);
        Assert.True(map.TryMoveNext(out _));
        Assert.True(budget.ReservedBytes >= funded);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(map);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void YieldedTuplesCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var source = new PyList([PyNone.Instance]);
        var zip = new PyZipIterator([source], false, span, context);
        Assert.True(zip.TryMoveNext(out _));
        var afterZip = context.MemoryGovernor.CurrentCommittedBytes;
        var enumerate = new PyEnumerateIterator(source, new System.Numerics.BigInteger(0), span, context);
        Assert.True(enumerate.TryMoveNext(out _));
        var afterEnumerate = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(afterZip > 128L);
        Assert.True(afterEnumerate > afterZip + 128L);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedReversedStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        _ = new PyReversedIterator(1, static i => (object)PyNone.Instance);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
