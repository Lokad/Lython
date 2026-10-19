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
    private static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, null,
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
        LythonRuntime.CsvWriterMembers.WriteRow(retained, [new(PyString.Empty, LythonRuntime.CsvCellKind.Text)], Span, context, 0);
        Assert.Equal(208, retained.OwnedBytes);
        CreateAndDrop(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before, budget.ReservedBytes);
        GC.Collect();
        Assert.Single(retained.Rows);
        Assert.Equal(208, retained.OwnedBytes);
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
    public void ChildDenialDoesNotCommitAnUnconstructedDictionaryWrapper()
    {
        var context = Context();
        var governor = context.MemoryGovernor;
        using var pressure = governor.ReserveTemporary(Cap - governor.CurrentAccountedBytes - 127, Span);
        var before = governor.CurrentCommittedBytes;
        var method = Module.GetMethod("CreateDictWriter", BindingFlags.NonPublic | BindingFlags.Static)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
            [new object[] { PyNone.Instance, PyNone.Instance }, new Write(), new[] { PyString.FromString("a") }, LythonRuntime.CsvExtrasAction.Ignore, Span, context]));
        Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(Cap - 127, governor.CurrentReservedBytes);
    }

    [Fact]
    public void DeniedWrapperAdmissionPreservesItsFundedChild()
    {
        var context = Context();
        var retained = Create(context);
        var governor = context.MemoryGovernor;
        var committed = governor.CurrentCommittedBytes;
        var child = 128;
        var pressure = Cap - governor.CurrentAccountedBytes - child - 64 - -1;
        using var reservation = governor.ReserveTemporary(pressure, Span);
        var method = Module.GetMethod("CreateDictWriter", BindingFlags.NonPublic | BindingFlags.Static)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
            [new object[] { PyNone.Instance, PyNone.Instance }, new Write(), new[] { PyString.FromString("a") }, LythonRuntime.CsvExtrasAction.Ignore, Span, context]));
        Assert.Equal("MemoryError", Assert.IsType<LythonRuntimeException>(error.InnerException).ExceptionType);
        Assert.Equal(committed + child, governor.CurrentCommittedBytes);
        Assert.Equal(pressure, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndDrop(LythonRuntime.ExecutionContext context)
    {
        var writer = Create(context);
        LythonRuntime.CsvWriterMembers.WriteRow(writer, [new(PyString.Empty, LythonRuntime.CsvCellKind.Text)], Span, context, 0);
    }

    private sealed class Write : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => PyNone.Instance;
    }
}
