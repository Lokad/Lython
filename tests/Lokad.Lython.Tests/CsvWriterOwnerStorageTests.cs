using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CsvWriterOwnerStorageTests
{
    private const long Cap = 262144;
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);
    private static readonly Type Module = typeof(LythonRuntime).GetNestedType("CsvModule", BindingFlags.NonPublic)!;
    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget? budget = null) => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget ?? new LythonMemoryBudget(Cap) },regexProvider:Utf8RegexProvider.Instance);
    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, PyString.FromString("\""),
        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);
    private static LythonRuntime.CsvWriterObject Create(LythonRuntime.ExecutionContext context)
        => (LythonRuntime.CsvWriterObject)Module.GetMethod("CreateWriter", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [Options(), null, Span, context])!;

    [Fact]
    public void WriterHistoryRemainsFundedUntilItsBatchEnds()
    {
        var budget = new LythonMemoryBudget(Cap);
        using var owner = budget.CreateScope();
        var context = Context(owner);
        var retained = Create(context);
        LythonRuntime.CsvWriterMembers.WriteRowFromValue(retained, new PyList([PyString.Empty]), Span, context);
        CreateAndDrop(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before, budget.ReservedBytes);
        GC.Collect();
        Assert.Single(retained.Rows);
        GC.KeepAlive(retained);
        retained = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeniedShellAdmissionPreservesTheRetainedWriter()
    {
        var context = Context();
        var retained = Create(context);
        var governor = context.MemoryGovernor;
        var committed = governor.CurrentCommittedBytes;
        var pressure = Cap - governor.CurrentAccountedBytes - 128 - -1;
        using var reservation = governor.ReserveTemporary(pressure, Span);
        var error = Assert.Throws<TargetInvocationException>(() => Create(context));
        Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
        Assert.Equal(committed, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
    }

    [Fact]
    public void DeniedDictionaryWrapperPreservesTheParentWriter()
    {
        var context=Context();var writer=Create(context);var governor=context.MemoryGovernor;
        using var pressure=governor.ReserveTemporary(Cap-governor.CurrentAccountedBytes-63,Span);
        var before=governor.CurrentCommittedBytes;
        var error=Assert.Throws<LythonRuntimeException>(()=>new LythonRuntime.CsvDictWriterObject(writer,
            PyNone.Instance,PyNone.Instance,PyString.FromString("ignore"),Span));
        Assert.Equal("MemoryError",error.ExceptionType);Assert.Equal(before,governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndDrop(LythonRuntime.ExecutionContext context)
    {
        var writer = Create(context);
        LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer, new PyList([PyString.Empty]), Span, context);
    }

}
