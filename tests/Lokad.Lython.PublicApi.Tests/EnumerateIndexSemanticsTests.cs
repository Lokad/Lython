using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class EnumerateIndexSemanticsTests
{
    // Independent CPython 3.13 values, including the signed-magnitude boundary.
    [Theory]
    [InlineData("0", "[(0, 1), (1, 2)]", "(0, 1)")]
    [InlineData("18446744073709551615", "[(18446744073709551615, 1), (18446744073709551616, 2)]", "(18446744073709551615, 1)")]
    [InlineData("-18446744073709551617", "[(-18446744073709551617, 1), (-18446744073709551616, 2)]", "(-18446744073709551617, 1)")]
    public async Task NativeIndicesPreserveValuesAfterActualSourceIo(string start, string expected, string first)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile($$"""
            class Source:
                def __init__(self):
                    self.count = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    if self.count == 2:
                        raise StopIteration
                    with open('ready.txt') as stream:
                        stream.read()
                    self.count += 1
                    return self.count
            rows = list(enumerate(Source(), {{start}}))
            print(rows)
            return str(rows[0])
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                ILythonHost host;
                if (asynchronous)
                {
                    var delayed = new DelayedLythonHost();
                    delayed.SeedFile("/ready.txt", "ready");
                    host = delayed;
                }
                else
                {
                    var immediate = new MockLythonHost();
                    immediate.SeedFile("/ready.txt", "ready");
                    host = immediate;
                }
                using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected + "\n", result.StandardOutput);
                    Assert.Equal(first, result.ReturnValue);
                    Assert.True(budget.ReservedBytes > baseline);
                    if (asynchronous) Assert.True(((DelayedLythonHost)host).CompletedAsynchronously > 0);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
