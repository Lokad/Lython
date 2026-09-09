using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: constructed tables, table styles and validations commit object
/// storage, and their worksheet registries commit per entry. Registries have
/// no guest delete path, so nothing is released. Copies carry registry
/// totals; ungoverned sheets stay free.
/// </summary>
public sealed class TableValidationAccountingTests
{
    private static object Create(string name, object[] arguments, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        return method.Invoke(null, [arguments, span, context])
            ?? throw new InvalidOperationException(name + " returned null.");
    }

    private static object Call(LythonRuntime.OpenPyxlWorksheet sheet, LythonRuntime.ExecutionContext context, LythonSourceSpan span, string member, params object[] arguments)
    {
        if (!PyMemberAccess.TryResolve(sheet, member, context, span, out var found) ||
            found is not LythonRuntime.ICallable callable)
        {
            throw new InvalidOperationException($"Member {member} not found.");
        }

        var bound = new CallArgumentValue[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            bound[i] = CallArgumentValue.Positional(arguments[i]);
        }

        return callable.Invoke(bound, span, context);
    }

    [Fact]
    public void ConstructedValuesCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = Create("CreateTable", [], context, span);
        _ = Create("CreateTableStyleInfo", [], context, span);
        _ = Create("CreateDataValidation", [], context, span);
        Assert.Equal(3L * 128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ContextFreeValuesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        _ = Create("CreateTable", [], null, null);
        _ = Create("CreateTableStyleInfo", [], null, null);
        _ = Create("CreateDataValidation", [], null, null);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void RegistryEntriesCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        _ = Call(sheet, context, span, "add_table", new LythonRuntime.OpenPyxlTable("T1", "A1:B2"));
        var validation = (LythonRuntime.OpenPyxlDataValidation)Create("CreateDataValidation", [], null, null);
        _ = Call(sheet, context, span, "add_data_validation", validation);
        _ = Call(sheet, context, span, "add_data_validation", validation);
        Assert.Equal(3L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CopyCarriesRegistryTotals()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        sheet.AttachMemoryGovernor(context.MemoryGovernor, span);
        _ = Call(sheet, context, span, "add_table", new LythonRuntime.OpenPyxlTable("T1", "A1:B2"));
        var validation = (LythonRuntime.OpenPyxlDataValidation)Create("CreateDataValidation", [], null, null);
        _ = Call(sheet, context, span, "add_data_validation", validation);
        Assert.Equal(2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        _ = sheet.Copy("C");
        Assert.Equal(4L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedRegistriesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = Call(sheet, context, span, "add_table", new LythonRuntime.OpenPyxlTable("T1", "A1:B2"));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}