using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CsvParserCancellationTests
{
    [Fact]
    public void NativeParsingStopsDuringFieldGrowth()
    {
        var root=new LythonMemoryBudget();using var owner=root.CreateScope();
        using var cancellation=new CancellationTokenSource();
        var budget=new CancelOnGrowth(owner,cancellation);
        var context=new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions
            {MemoryBudget=budget,CancellationToken=cancellation.Token});
        var span=new LythonSourceSpan(0,0,1,1);
        var options=new LythonRuntime.CsvOptions(PyStringOps.CommaLiteral,PyString.FromString("\""),
            LythonRuntime.CsvQuotingMode.Minimal,true,null,false,PyString.FromString("\n"),false);
        var source=new LythonRuntime.CsvRecordSource(new PyList([]),options,span,context);
        var parser=(LythonRuntime.CsvRecordParser)typeof(LythonRuntime.CsvRecordSource)
            .GetField("_parser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source)!;
        // The CLR fixture is already host-owned; signal after actual parser
        // capacity growth, rather than before line decoding or via a timer.
        var error=Assert.Throws<LythonRuntimeException>(()=>parser.Feed(new string('x',65536)));
        Assert.True(budget.Triggered);
        Assert.Contains("execution canceled",error.Message,StringComparison.Ordinal);
        Assert.False(parser.TryTakeReady(out _));
        Assert.True(parser.StorageBytes<65536);
        owner.Dispose();Assert.Equal(0,root.ReservedBytes);
    }

    private sealed class CancelOnGrowth(ILythonMemoryBudget inner,CancellationTokenSource cancellation):ILythonMemoryBudget
    {
        public bool Triggered {get;private set;}
        public ILythonMemoryScope CreateScope()=>inner.CreateScope();
        public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if(!inner.TryReserve(bytes,out var actual)){reservation=null;return false;}
            reservation=new Admission(this,actual);return true;
        }
        private void Grew(long previous,long bytes)
        {
            if(!Triggered&&bytes-previous>=8192){Triggered=true;cancellation.Cancel();}
        }
        private sealed class Admission(CancelOnGrowth outer,ILythonMemoryReservation inner):ILythonMemoryReservation
        {
            public long Bytes=>inner.Bytes;
            public bool TryResize(long bytes)
            {
                var previous=inner.Bytes;if(!inner.TryResize(bytes))return false;
                outer.Grew(previous,bytes);
                return true;
            }
            public void Dispose()=>inner.Dispose();
        }
    }
}
