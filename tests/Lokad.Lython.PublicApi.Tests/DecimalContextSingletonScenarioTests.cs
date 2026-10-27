using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Decimal templates have stable identity within an execution; fresh contexts
/// and other executions keep their mutable state separate.
/// </summary>
public sealed class DecimalContextSingletonScenarioTests
{
    [Fact]
    public async Task SharedContextsKeepIdentity()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import decimal
            checks = [decimal.DefaultContext is decimal.DefaultContext]
            checks.append(decimal.BasicContext is decimal.BasicContext)
            checks.append(decimal.ExtendedContext is decimal.ExtendedContext)
            checks.append(decimal.Context() is decimal.Context())
            checks.append(decimal.DefaultContext.prec)
            checks.append(decimal.BasicContext.prec)
            checks.append(decimal.ExtendedContext.prec)
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true, false };
        expected.Add(new BigInteger(28));
        expected.Add(new BigInteger(9));
        expected.Add(new BigInteger(9));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RepeatedTemplateAliasesRemainBoundedAndReleaseTheirOwner()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import decimal
            ds = []
            i = 0
            while i < 256:
                ds.append(decimal.DefaultContext)
                i = i + 1
            return len(ds)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(131072) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(256), result.ReturnValue);
            }
            Assert.Equal(0, ((LythonMemoryBudget)options.MemoryBudget).ReservedBytes);
        }
    }
}
