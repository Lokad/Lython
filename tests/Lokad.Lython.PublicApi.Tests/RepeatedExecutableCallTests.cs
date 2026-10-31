using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RepeatedExecutableCallTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedCallsKeepLexicalOwnersCapturedCellsAndExceptionStateSeparate(bool asynchronous)
    {
        const string source = """
def make(offset):
 def run(value):
  try:
   if value < 0:raise ValueError('negative')
   with open('marker.txt') as source:source.read()
   return offset+value
  except ValueError:return -offset
  finally:print('done',offset)
 return run
def adapt(fn,items):
 return map(fn,items)
a=make(10)
b=make(20)
first=adapt(a,[1,-1,3])
second=adapt(b,[2,-1,4])
print(next(first),next(second),next(first),next(second))
return [list(first),list(second),a(5),b(6)]
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            for (var invocation = 0; invocation < 2; invocation++)
            {
                var host = new MockLythonHost(); host.SeedFile("/marker.txt", "ok");
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/marker.txt", "ok");
                using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("done 10\ndone 20\ndone 10\ndone 20\n11 22 -10 -20\ndone 10\ndone 20\ndone 10\ndone 20\n", result.StandardOutput);
                    var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
                    Assert.Equal(4, values.Count);
                    Assert.Equal(new BigInteger(13), Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(values[0])));
                    Assert.Equal(new BigInteger(24), Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<object?>>(values[1])));
                    Assert.Equal(new BigInteger(15), values[2]);
                    Assert.Equal(new BigInteger(26), values[3]);
                    Assert.True(budget.ReservedBytes > baseline);
                    if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
