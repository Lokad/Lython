using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class ExceptionShellStorageTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void CollectedShellsRemainBatchOwnedWhileRetainedAliasesStayUsable()
    {
        var context = Root();
        var retained = PyException.CreateOwned(PythonExceptionIdentity.FromRuntimeTypeName("ValueError"), "", PyNone.Instance, context, Span);
        var weak = Drop(context);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        retained.Adopt(context, Span);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(retained.TrySnapshotOwnership(out var bytes));
        Assert.Equal(PyException.ShellBytes, bytes);
        GC.KeepAlive(retained);
    }

    [Fact]
    public void DeniedShellAdmissionKeepsExistingFunding()
    {
        var context = Root();
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        using var pressure = context.MemoryGovernor.ReserveTemporary(4096 - baseline - PyException.ShellBytes + 1, Span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Throws<LythonRuntimeException>(() => PyException.CreateOwned(
            PythonExceptionIdentity.FromRuntimeTypeName("ValueError"), "", PyNone.Instance, context, Span));
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void PartialChainAdmissionRetriesWithoutChargingTheRootAgain()
    {
        var context = Root();
        var warm = PyException.CreateOwned(PythonExceptionIdentity.FromRuntimeTypeName("ValueError"), "", PyNone.Instance, context, Span);
        var child = new PyException("ValueError", "", PyNone.Instance);
        var value = new PyException("ValueError", "", PyNone.Instance) { Cause = child };
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        using (var pressure = context.MemoryGovernor.ReserveTemporary(4096 - baseline -
            PyException.ShellBytes - 1, Span))
        {
            Assert.Throws<LythonRuntimeException>(() => value.Adopt(context, Span));
            Assert.Null(child.OwnerMemoryGovernor);
        }
        var rootFunding = context.MemoryGovernor.CurrentCommittedBytes;
        value.Adopt(context, Span);
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= rootFunding + PyException.ShellBytes);
        var complete = context.MemoryGovernor.CurrentCommittedBytes;
        value.Adopt(context, Span);
        Assert.Equal(complete, context.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(warm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCleanupDoesNotNeedRoomForAnotherGuestException(bool asynchronous)
    {
        var context = Root();
        using var pressure = context.MemoryGovernor.ReserveTemporary(4096 - context.MemoryGovernor.CurrentAccountedBytes, Span);
        var manager = new CleanupManager();
        var syntax = LythonFrontend.Compile("1 / 0").Script!;
        var body = LoweredScript.Lower(syntax).Statements;
        if (asynchronous)
            await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await PyContextManagers.ExecuteWithAsync(manager, null, body, Span, Span, context));
        else Assert.Throws<LythonRuntimeException>(() => PyContextManagers.ExecuteWith(manager, null, body, Span, Span, context));
        Assert.True(manager.Closed);
        Assert.NotNull(manager.Exception);
        Assert.Null(manager.Exception!.OwnerMemoryGovernor);
    }

    private static LythonRuntime.ExecutionContext Root() => new(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 4096 });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyException> Drop(LythonRuntime.ExecutionContext context)
        => new(PyException.CreateOwned(PythonExceptionIdentity.FromRuntimeTypeName("ValueError"), "", PyNone.Instance, context, Span));

    private sealed class CleanupManager : IPyContextManager
    {
        internal bool Closed { get; private set; }
        internal PyException? Exception { get; private set; }
        public object Enter() => PyNone.Instance;
        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            Closed = true;
            Exception = exceptionValue as PyException;
            return false;
        }
    }
}
