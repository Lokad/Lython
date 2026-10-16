using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SliceValueDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TupleSliceConstructionRetainsRawBoundsWithoutCallingIndex(bool asynchronous)
    {
        const string source = """
            events=[]
            class Bound:
             def __index__(self):
              events.append('index')
              raise AssertionError('premature coercion')
            start=Bound()
            class Receiver:
             def __getitem__(self,key):
              events.append('get')
              return (key[0].start is start,key[0].stop,key[0].step,key[1])
            print(Receiver()[start:5:2,9],events)
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(True, 5, 2, 9) ['get']\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
