using System.Text.Json;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonByteInputTests
{
    public sealed record Case(string Name, string Source, string Output);
    public static IEnumerable<object[]> Programs() => JsonSerializer.Deserialize<Case[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "JsonBytes", "cases.json"), Encoding.UTF8))!
        .SelectMany(c => new[] { false, true }.Select(a => new object[] { c.Name, c.Source, c.Output, a }));

    // CPython 3.13.2 goldens; normal checks need no installed Python.
    [Theory]
    [MemberData(nameof(Programs))]
    public async Task BytesUsePythonDetectionAndTheOrdinaryDecoder(string name, string source, string expected, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
