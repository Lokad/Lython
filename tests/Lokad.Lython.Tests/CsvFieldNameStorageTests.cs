using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvFieldNameStorageTests
{
    private const long Cap = 262144;
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly MethodInfo Finish = typeof(LythonRuntime)
        .GetNestedType("CsvModule", BindingFlags.NonPublic)!
        .GetMethod("FinishCsvFieldNames", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static LythonRuntime.ExecutionContext Context() => new(new MockLythonHost(),
        new LythonRunOptions { MaxExecutionMemoryBytes = Cap });

    private static PyString[] Create(LythonRuntime.ExecutionContext context, int count)
        => (PyString[])Finish.Invoke(null, [Enumerable.Range(0, count).Select(_ => PyString.FromString("name")).ToList(), context, Span])!;

    [Fact]
    public void EmptyTablesHaveIndependentStorageOwners()
    {
        var context = Context();
        var first = Create(context, 0);
        var second = Create(context, 0);
        Assert.NotSame(first, second);
        Assert.Equal(2, context.State.CallTemporaries.Count);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void DroppedTableReclaimsWhileRetainedTableStaysFunded()
    {
        var context = Context();
        var retained = Create(context, 2);
        CreateAndDrop(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        Assert.Equal(64 + ChargeReclamationPool.EntryChargeBytes, context.State.CallTemporaries.Sweep(full: true));
        Assert.Equal(before - 64 - ChargeReclamationPool.EntryChargeBytes, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.Equal("name", retained[0].AsString());
        GC.KeepAlive(retained);
    }

    [Fact]
    public void DeniedRegistrationRefundsOnlyTheUnpublishedArray()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var retained = Create(context, 2); // Warm backing; another slot needs no page growth.
        var committed = governor.CurrentCommittedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - 64 - (ChargeReclamationPool.EntryChargeBytes - 1);
        using var reservation = governor.ReserveTemporary(pressure, Span);
        var error = Assert.Throws<TargetInvocationException>(() => Create(context, 2));
        Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        Assert.Equal(1, context.State.CallTemporaries.Count);
        Assert.Equal("name", retained[0].AsString());
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndDrop(LythonRuntime.ExecutionContext context) => _ = Create(context, 2);
}
