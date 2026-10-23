using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvHeaderOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingHeaderAliasStaysFundedAfterInputExhaustion(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
            import csv
            r=csv.DictReader(['a,b','1,2'])
            names=r.fieldnames
            list(r)
            names.append('kept')
            assert r.fieldnames is names
            return names
            """,budget);
        Assert.True(script.IsValid);
        var baseline=budget.ReservedBytes;
        for(var i=0;i<2;i++)
        {
            using(var result=asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success,result.Failure?.Message);
                Assert.Equal(new List<object?> {"a","b","kept"},result.ReturnValue);
                Assert.True(budget.ReservedBytes>baseline);
                Assert.False(budget.TryReserve(262144,out _));
            }
            Assert.Equal(baseline,budget.ReservedBytes);
        }
        script.Dispose();Assert.Equal(0,budget.ReservedBytes);
    }

    [Theory]
    [InlineData("reader")]
    [InlineData("writer")]
    public async Task DeniedHeaderWorkCannotResumeOrPublish(string kind)
    {
        var source="import csv\nvalue='"+new string('x',65536)+"'\n"+
            (kind=="reader" ? "r=csv.DictReader([value])\ntry:r.fieldnames\n"
                : "class Sink:\n def write(self,value):print('published')\nw=csv.DictWriter(Sink(),[value])\ntry:w.writeheader()\n")+
            "except MemoryError:print('caught')\nprint('resumed')";
        using var script=new LythonEngine().Compile(source);Assert.True(script.IsValid);
        var budget=new LythonMemoryBudget(32768);
        foreach(var asynchronous in new[]{false,true})
        {
            using(var result=asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget})
                : script.Run(new MockLythonHost(),new LythonRunOptions {MemoryBudget=budget}))
            {
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
}
