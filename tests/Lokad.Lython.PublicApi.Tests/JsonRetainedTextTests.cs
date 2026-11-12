using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonRetainedTextTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedDecodedAliasesShareTheHostAllowanceUntilResultsEnd(bool asynchronous)
    {
        const string source = """
            import json
            with open('/input.json') as source:
                value = json.load(source)
            aliases = [value, value]
            print(aliases[0] is aliases[1])
            return aliases
            """;
        const string expected = "decoded café😀 text";
        var document = new string(' ', 32768) + "\"" + expected + "\"" + new string(' ', 32768);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                MemoryBudget = budget,
            };
            for (var round = 0; round < 3; round++)
            {
                using (var first = await Run())
                {
                    Check(first);
                    var retained = budget.ReservedBytes;
                    // Projection owns independent CLR strings. Guest input may
                    // retire after its users finish; retained projections stay funded.
                    Assert.True(retained > baseline);
                    using (var second = await Run())
                    {
                        Check(second);
                        Assert.True(budget.ReservedBytes > retained);
                    }
                    Assert.Equal(retained, budget.ReservedBytes);
                    Check(first);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Task<LythonExecutionResult> Run()
            {
                var host = new MockLythonHost();
                host.WriteText("/input.json", document);
                return asynchronous ? script.RunAsync(host, options)
                    : Task.FromResult(script.Run(host, options));
            }
        }
        Assert.Equal(0, budget.ReservedBytes);

        static void Check(LythonExecutionResult result)
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n", result.StandardOutput);
            Assert.Equal(new List<object?> { expected, expected }, result.ReturnValue);
        }
    }
}
