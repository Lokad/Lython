using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Investigation #2: Compile retains every representation and reuses them
// across invocations; per-invocation host analysis is shared per capability
// fingerprint instead of re-walking the tree. Hosts that gain capabilities
// between runs re-evaluate instead of serving stale denials.
public sealed class CompileOnceRunManyTests
{
    [Fact]
    public void MutatingHostCapabilitiesReevaluatePerInvocation()
    {
        const string code = """
            x = input()
            return x
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var host = new MockLythonHost();

        var denied = script.Run(host);
        Assert.False(denied.Success);
        Assert.Null(denied.Failure);
        Assert.Contains(denied.Diagnostics, diagnostic => diagnostic.Code == "LA3040");

        host.SeedStandardInput("hi\n");
        var allowed = script.Run(host);
        Assert.True(allowed.Success, allowed.Failure?.Message);
        Assert.Equal("hi", allowed.ReturnValue);
    }

    [Fact]
    public void RepeatedRunsShareHostAnalysis()
    {
        var script = new LythonEngine().Compile(LargeSource());
        var host = new MockLythonHost();
        for (var i = 0; i < 3; i++)
        {
            var result = script.Run(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(3998), result.ReturnValue);
        }
    }

    [Fact]
    public void WarmRunsAvoidPerInvocationAnalysisAllocations()
    {
        // The 2000-statement walk costs ~1 MB per invocation uncached; shared
        // analysis keeps three warm runs far below one walk.
        var script = new LythonEngine().Compile(LargeSource());
        var host = new MockLythonHost();
        script.Run(host);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        script.Run(host);
        script.Run(host);
        script.Run(host);
        Assert.True(
            GC.GetAllocatedBytesForCurrentThread() - before < 500000,
            $"warm allocation exceeded shared-analysis budget");
    }

    private static string LargeSource()
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < 2000; i++)
        {
            builder.Append("value_");
            builder.Append(i);
            builder.Append(" = ");
            builder.Append(i);
            builder.Append(" * 2\n");
        }

        builder.Append("return value_1999\n");
        return builder.ToString();
    }
}
