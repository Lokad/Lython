using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExecutionResultStatusTests
{
    [Theory]
    [InlineData("value =\n")]
    [InlineData("1 / 0\n")]
    [InlineData("open('/missing.txt')\n")]
    public void UnsuccessfulExecutionsCarryProcessCompatibleExitCode(string source)
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("value =\n")]
    [InlineData("1 / 0\n")]
    [InlineData("open('/missing.txt')\n")]
    public async Task UnsuccessfulAsyncExecutionsCarryProcessCompatibleExitCode(string source)
    {
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void SuccessfulExecutionKeepsImplicitExitDistinctFromSystemExit()
    {
        var success = new LythonEngine(Utf8RegexProvider.Instance).Run("return 1\n", new MockLythonHost());
        var explicitExit = new LythonEngine(Utf8RegexProvider.Instance).Run("import sys\nsys.exit()\n", new MockLythonHost());

        Assert.True(success.Success);
        Assert.Null(success.ExitCode);
        Assert.False(explicitExit.Success);
        Assert.Equal(0, explicitExit.ExitCode);
    }
}

