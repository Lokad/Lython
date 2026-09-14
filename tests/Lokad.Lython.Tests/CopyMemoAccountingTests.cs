using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG20: internal copy memos cover each entry with transient scratch and
/// durable view storage; an unexposed view refunds deterministically on
/// disposal, while user-supplied memo dicts keep their own ownership.
/// </summary>
public sealed class CopyMemoAccountingTests
{
    private static object NewInternalMemo(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var type = typeof(LythonRuntime).GetNestedType("CopyMemo", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CopyMemo not found.");
        var ctor = type.GetConstructors().Single(static c => c.GetParameters().Length == 2);
        return ctor.Invoke([context, span]);
    }

    private static void Remember(object memo, object original, object copied)
    {
        var method = memo.GetType().GetMethod("Remember", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Remember not found.");
        method.Invoke(memo, [original, copied]);
    }

    [Fact]
    public void InternalMemoEntriesOwnDurableStorageUntilDispose()
    {
        // Transient scratch rides reserved; durable memo entries commit; each
        // fresh original also mints one governed id-registry entry (64 B value
        // plus 64 B pool entry) shared with id(). The live memo pins its
        // originals, so their id boxes release only after the memo drops.
        var (context, reserved, committed, afterDispose) = RememberThreeAndDispose();
        Assert.Equal(3L * 128L, reserved);
        Assert.Equal(3L * 128L + 3L * 128L, committed);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(3L * 128L, afterDispose);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        context.Services.State.CallTemporaries.Sweep();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (LythonRuntime.ExecutionContext Context, long Reserved, long Committed, long AfterDispose) RememberThreeAndDispose()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var memo = NewInternalMemo(context, span);
        Remember(memo, new object[] { 1 }, new object[] { 1 });
        Remember(memo, new object[] { 2 }, new object[] { 2 });
        Remember(memo, new object[] { 3 }, new object[] { 3 });
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        ((IDisposable)memo).Dispose();
        return (context, reserved, committed, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ScalarCopiesHoldNoMemoScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var memo = NewInternalMemo(context, span);
        ((IDisposable)memo).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
    }
}
