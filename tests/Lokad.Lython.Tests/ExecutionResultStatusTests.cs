using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionResultStatusTests
{
    [Theory]
    [InlineData("value =\n")]
    [InlineData("1 / 0\n")]
    [InlineData("open('/missing.txt')\n")]
    public void UnsuccessfulExecutionsCarryProcessCompatibleExitCode(string source)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
    }

    [Theory]
    [InlineData("value =\n")]
    [InlineData("1 / 0\n")]
    [InlineData("open('/missing.txt')\n")]
    public async Task UnsuccessfulAsyncExecutionsCarryProcessCompatibleExitCode(string source)
    {
        var result = await new LythonEngine().RunAsync(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void SuccessfulExecutionKeepsImplicitExitDistinctFromSystemExit()
    {
        var success = new LythonEngine().Run("return 1\n", new MockLythonHost());
        var explicitExit = new LythonEngine().Run("import sys\nsys.exit()\n", new MockLythonHost());

        Assert.True(success.Success);
        Assert.Null(success.ExitCode);
        Assert.False(explicitExit.Success);
        Assert.Equal(0, explicitExit.ExitCode);
    }
}
