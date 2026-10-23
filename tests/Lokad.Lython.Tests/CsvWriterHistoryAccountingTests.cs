using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvWriterHistoryAccountingTests
{
    private static readonly LythonSourceSpan Span = new(0,0,1,1);
    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget budget)
        => new(new MockLythonHost(), new LythonRunOptions {MemoryBudget=budget});
    private static LythonRuntime.CsvWriterObject Writer(LythonRuntime.ExecutionContext context, bool unquoted=false)
        => new(new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral,PyString.FromString("\""),
            unquoted ? LythonRuntime.CsvQuotingMode.None : LythonRuntime.CsvQuotingMode.Minimal,
            true,null,false,PyString.FromString("\n"),false),null,context.MemoryGovernor,Span);

    [Fact]
    public void DeniedRenderingNeverPublishesHistory()
    {
        var context=Context(new LythonMemoryBudget(32768));
        var writer=Writer(context);
        var error=Assert.Throws<LythonRuntimeException>(()=>LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,
            new PyList([PyString.FromString(new string('x',65536))]),Span,context));
        Assert.Equal("MemoryError",error.ExceptionType);
        Assert.Empty(writer.Rows);
        Assert.Equal(128,context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void RenderingErrorLeavesPriorHistoryIntactAndWriterUsable()
    {
        var context=Context(new LythonMemoryBudget());var writer=Writer(context,true);
        LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,new PyList([PyString.FromString("first")]),Span,context);
        var funded=context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Throws<LythonRuntimeException>(()=>LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,
            new PyList([PyString.FromString("a,b")]),Span,context));
        Assert.Single(writer.Rows);
        Assert.Equal(funded,context.MemoryGovernor.CurrentCommittedBytes);
        LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,new PyList([PyString.FromString("last")]),Span,context);
        Assert.Equal(new[]{"first\n","last\n"},writer.Rows.Select(s=>s.AsString()));
    }

    [Fact]
    public void RetainedRowsAndCapacityStayFundedThroughDenialAndCleanup()
    {
        var budget=new LythonMemoryBudget(8192);using var owner=budget.CreateScope();
        var context=Context(owner);var writer=Writer(context);var row=new PyList([PyString.FromString("value")]);
        var denied=false;
        for(var i=0;i<100;i++)
        {
            var count=writer.Rows.Count;
            try { LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,row,Span,context); }
            catch(LythonRuntimeException error)
            {
                Assert.Equal("MemoryError",error.ExceptionType);Assert.Equal(count,writer.Rows.Count);denied=true;break;
            }
        }
        Assert.True(denied);Assert.NotEmpty(writer.Rows);
        Assert.True(budget.ReservedBytes>=128+8L*writer.Rows.Capacity+writer.Rows.Sum(s=>s.CommittedOwnedBytes));
        Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();Assert.Equal(0,budget.ReservedBytes);
    }

    [Fact]
    public void OversizedIntegerConversionRefusesBeforePublication()
    {
        var context=Context(new LythonMemoryBudget(2130));var writer=Writer(context);
        var error=Assert.Throws<LythonRuntimeException>(()=>LythonRuntime.CsvWriterMembers.WriteRowFromValue(writer,
            new PyList([BigInteger.Pow(10,2000)]),Span,context));
        Assert.Equal("MemoryError",error.ExceptionType);Assert.Empty(writer.Rows);
        Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
    }
}
