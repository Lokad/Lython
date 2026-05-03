using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

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
    public void Print_FlushKeyword_RemainsUnsupported()
    {
        var result = new LythonEngine().Run("print(\"x\", flush=True)\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("print(..., flush=...) is not supported by Lython", result.Failure.Message, StringComparison.Ordinal);
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
