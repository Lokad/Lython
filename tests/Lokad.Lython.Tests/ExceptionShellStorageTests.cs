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
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes >= PyException.ShellBytes);
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
    public void RetainedNativeDiagnosticNeedsAdmissionForItsMessage()
    {
        var context = Root();
        var exception = new PyException("ValueError", new string('x', 2048), PyNone.Instance);
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        var error = Assert.Throws<LythonRuntimeException>(() => exception.Adopt(context, Span));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Null(exception.OwnerMemoryGovernor);
        Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
    }

    [Fact]
    public void PartialChainAdmissionKeepsOnlyPublishedRootFunding()
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
        Assert.Throws<LythonRuntimeException>(() => value.Adopt(context, Span));
        Assert.Equal(rootFunding, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Null(child.OwnerMemoryGovernor);
        GC.KeepAlive(warm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCleanupDoesNotNeedRoomForAnotherGuestException(bool asynchronous)
    {
        var context = Root();
        using var manager = new CleanupManager(context.MemoryGovernor);
        context.Variables["manager"] = manager;
        var syntax = LythonFrontend.Compile("with manager:\n    1 / 0", regexProvider: Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        if (asynchronous)
            await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
        else Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context));
        Assert.True(manager.Closed);
        Assert.NotNull(manager.Exception);
        Assert.Null(manager.Exception!.OwnerMemoryGovernor);
    }

    private static LythonRuntime.ExecutionContext Root() => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4096) },regexProvider:Utf8RegexProvider.Instance);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyException> Drop(LythonRuntime.ExecutionContext context)
        => new(PyException.CreateOwned(PythonExceptionIdentity.FromRuntimeTypeName("ValueError"), "", PyNone.Instance, context, Span));

    private sealed class CleanupManager(MemoryGovernor governor) : IPyContextManager, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _pressure;
        internal bool Closed { get; private set; }
        internal PyException? Exception { get; private set; }
        public object Enter()
        {
            // Use the remaining allowance once the protected frame is ready.
            _pressure = governor.ReserveTemporary(4096 - governor.CurrentAccountedBytes, Span);
            return PyNone.Instance;
        }
        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            Closed = true;
            Exception = exceptionValue as PyException;
            return false;
        }

        public void Dispose() => _pressure?.Dispose();
    }
}
