using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalSignalValidationTests
{
    [Fact]
    public async Task MalformedSignalKeysFailWithoutRunningGuestEquality()
    {
        const string source = """
            from decimal import Decimal,getcontext,Inexact
            context=getcontext()
            left=Decimal(1)
            right=Decimal(2)
            events=[]
            class BadKey:
                def __hash__(self): return hash(Inexact)
                def __eq__(self,other): events.append('equality');return False
            del context.flags[Inexact]
            context.flags[BadKey()]=False
            events.clear()
            try: left+right
            except KeyError: print('invalid signal dict')
            print(events)
            """;
        var root = new LythonMemoryBudget(2*1024*1024);
        using (var script = new LythonEngine().Compile(source,root))
        {
            Assert.True(script.IsValid,string.Join("; ",script.Diagnostics.Select(d=>d.Message)));
            var baseline = root.ReservedBytes;
            foreach (var asynchronous in new[] {false,true})
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success,result.Failure?.Message);
                    Assert.Equal("invalid signal dict\n[]\n",result.StandardOutput);
                }
                Assert.Equal(baseline,root.ReservedBytes);
            }
        }
        Assert.Equal(0,root.ReservedBytes);
    }
}
