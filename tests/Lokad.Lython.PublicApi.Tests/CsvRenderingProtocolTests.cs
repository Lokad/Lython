using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvRenderingProtocolTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "escape-roundtrip", "import csv,io\ns=io.StringIO()\ncsv.writer(s,escapechar='\\\\',lineterminator='\\n').writerow(['a\\\\b'])\nprint(repr(s.getvalue()))\nprint(list(csv.reader([s.getvalue()],escapechar='\\\\')))", "'a\\\\\\\\b\\n'\n[['a\\\\b']]\n" };
        yield return new object[] { "escape-quoted", "import csv,io\ns=io.StringIO()\ncsv.writer(s,escapechar='\\\\',quoting=csv.QUOTE_ALL,lineterminator='\\n').writerow(['a\\\\b'])\nprint(repr(s.getvalue()))", "'\"a\\\\\\\\b\"\\n'\n" };
        yield return new object[] { "custom-terminator", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator=';').writerow(['a;b'])\nprint(repr(s.getvalue()))", "'\"a;b\";'\n" };
        yield return new object[] { "bool-nonnumeric", "import csv,io\ns=io.StringIO()\ncsv.writer(s,quoting=csv.QUOTE_NONNUMERIC,lineterminator='\\n').writerow([True,False])\nprint(repr(s.getvalue()))", "'True,False\\n'\n" };
        yield return new object[] { "str-cell", "import csv,io\nclass Cell:\n def __str__(self):return 'cell'\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([Cell()])\nprint(repr(s.getvalue()))", "'cell\\n'\n" };
        yield return new object[] { "bytes-cell", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([b'abc'])\nprint(repr(s.getvalue()))", "\"b'abc'\\n\"\n" };
        yield return new object[] { "none-cell-control", "import csv,io\ns=io.StringIO()\ncsv.writer(s,lineterminator='\\n').writerow([None,2,2.5])\nprint(repr(s.getvalue()))", "',2,2.5\\n'\n" };
        yield return new object[] { "writer-error-order", "import csv,io\ndef row():\n yield 'a,b'\n print('tail')\n yield 'later'\ntry:csv.writer(io.StringIO(),quoting=csv.QUOTE_NONE).writerow(row())\nexcept csv.Error:print('Error')", "Error\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RenderingMatchesIndependentPythonGoldens(string label, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, label+": "+string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
        var budget = new LythonMemoryBudget(262144);
        foreach (var asynchronous in new[]{false,true})
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget})
                : script.Run(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget}))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(expected,result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidBufferedRowNeverEntersHistory(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import csv
            w=csv.writer(quoting=csv.QUOTE_NONE,lineterminator='\n')
            try:w.writerow(['a,b'])
            except csv.Error:pass
            w.writerow(['ok'])
            print(repr(w.getvalue()))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success,result.Failure?.Message);
        Assert.Equal("'ok'\n",result.StandardOutput);
    }

    [Theory]
    [InlineData("import csv\nnext(csv.reader([value]))")]
    [InlineData("import csv\nw=csv.writer(lineterminator=value)\nw.writerow([])")]
    public async Task MajorWorkingStorageRefusesAndBudgetCanBeReused(string operation)
    {
        var source = "value='"+new string('x',65536)+"'\ntry:\n "+operation.Replace("\n","\n ",StringComparison.Ordinal)+"\nexcept MemoryError:print('caught')\nprint('resumed')";
        using var large = new LythonEngine().Compile(source);
        using var small = new LythonEngine().Compile("import csv\nw=csv.writer(lineterminator='\\n')\nw.writerow(['ok'])\nreturn w.getvalue()");
        var budget = new LythonMemoryBudget(32768);
        foreach(var asynchronous in new[]{false,true})
        {
            using(var result=asynchronous ? await large.RunAsync(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget})
                : large.Run(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget}))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
            using(var result=asynchronous ? await small.RunAsync(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget})
                : small.Run(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget}))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal("ok",result.ReturnValue);
                Assert.True(budget.ReservedBytes>0);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInterruptsEnteredNativeRendering(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import csv\nvalue='"+new string('x',65536)+"'\ncsv.writer().writerow([value])\nprint('finished')");
        using var cancellation = new CancellationTokenSource();
        var budget = new CancelOnCapacity(cancellation);
        using(var result=asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget},cancellation.Token)
            : script.Run(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget,CancellationToken=cancellation.Token}))
        {
            Assert.True(budget.Triggered);
            Assert.Equal(LythonFailureReason.Cancelled,result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            // A complete output would require at least 64 KiB; growth stopped early.
            Assert.True(budget.Root.PeakBytes<65536);
        }
        Assert.Equal(0,budget.Root.ReservedBytes);
    }

    private sealed class CancelOnCapacity(CancellationTokenSource cancellation):ILythonMemoryBudget
    {
        public LythonMemoryBudget Root {get;}=new();
        public bool Triggered {get;private set;}
        public ILythonMemoryScope CreateScope()=>new Owner(this,Root.CreateScope());
        public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)=>Reserve(Root,bytes,out reservation);
        private bool Reserve(ILythonMemoryBudget owner,long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if(!owner.TryReserve(bytes,out var inner)){reservation=null;return false;}
            reservation=new Admission(this,inner);return true;
        }
        private void Grew(long previous,long next)
        {
            if(Triggered||next-previous<8192)return;
            Triggered=true;cancellation.Cancel();
        }
        private sealed class Owner(CancelOnCapacity outer,ILythonMemoryScope inner):ILythonMemoryScope
        {
            public ILythonMemoryScope CreateScope()=>new Owner(outer,inner.CreateScope());
            public bool TryReserve(long bytes,[NotNullWhen(true)] out ILythonMemoryReservation? reservation)=>outer.Reserve(inner,bytes,out reservation);
            public void Dispose()=>inner.Dispose();
        }
        private sealed class Admission(CancelOnCapacity outer,ILythonMemoryReservation inner):ILythonMemoryReservation
        {
            public long Bytes=>inner.Bytes;
            public bool TryResize(long bytes){var previous=inner.Bytes;if(!inner.TryResize(bytes))return false;outer.Grew(previous,bytes);return true;}
            public void Dispose()=>inner.Dispose();
        }
    }

}
