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

    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget? budget = null) => new(new MockLythonHost(),
        new LythonRunOptions { MemoryBudget = budget ?? new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);

    private static PyString[] Create(LythonRuntime.ExecutionContext context, int count)
        => (PyString[])Finish.Invoke(null, [Enumerable.Range(0, count).Select(_ => PyString.FromString("name")).ToList(), context, Span])!;

    [Fact]
    public void EmptyTablesHaveIndependentStorageOwners()
    {
        var context = Context();
        var first = Create(context, 0);
        var second = Create(context, 0);
        Assert.NotSame(first, second);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [Fact]
    public void TablesRemainBatchOwnedUntilExplicitCleanup()
    {
        var budget = new LythonMemoryBudget(Cap);
        using var owner = budget.CreateScope();
        var context = Context(owner);
        var retained = Create(context, 2);
        CreateAndDrop(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before, budget.ReservedBytes);
        GC.Collect();
        Assert.Equal("name", retained[0].AsString());
        GC.KeepAlive(retained);
        retained = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeniedArrayAdmissionPreservesRetainedTables()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        var retained = Create(context, 2); // Keep an earlier table live through refusal.
        var committed = governor.CurrentCommittedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - 64 - -1;
        using var reservation = governor.ReserveTemporary(pressure, Span);
        var error = Assert.Throws<TargetInvocationException>(() => Create(context, 2));
        Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        Assert.Equal("name", retained[0].AsString());
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndDrop(LythonRuntime.ExecutionContext context) => _ = Create(context, 2);
}
