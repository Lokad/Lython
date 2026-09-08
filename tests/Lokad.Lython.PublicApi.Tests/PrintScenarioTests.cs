using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PrintScenarioTests
{
    [Fact]
    public void Print_CapturesStandardOutputWithDefaults()
    {
        var result = new LythonEngine().Run(
            """
print("alpha", 2, True)
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha 2 True\n", result.StandardOutput);
    }

    [Fact]
    public void Print_SupportsSepAndEndKeywords()
    {
        var result = new LythonEngine().Run(
            """
print("a", "b", sep=":", end="!")
print("tail")
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("a:b!tail\n", result.StandardOutput);
    }

    [Fact]
    public void Print_SupportsSysStderr()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import sys
print("x", file=sys.stderr)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal("x\n", result.StandardError);
        Assert.Equal(string.Empty, host.CapturedStandardOutput());
        Assert.Equal("x\n", host.CapturedStandardError());
    }

    [Fact]
    public void Print_SupportsFlushKeyword()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
print("x", flush=True)
print("y", flush=False)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("x\ny\n", result.StandardOutput);
        Assert.Equal("x\ny\n", host.CapturedStandardOutput());
    }

    [Fact]
    public void Print_SupportsWritableTextFileHandle()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
with open("/out.txt", "w") as f:
    print("alpha", file=f)
    print("x", "y", sep=":", end="!", file=f)
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha\nx:y!", host.ReadText("/out.txt"));
        Assert.Equal(string.Empty, result.StandardOutput);
    }
}

