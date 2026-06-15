using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class HostStreamsScenarioTests
{
    [Fact]
    public void Input_AndSysStreams_AreHostMediated()
    {
        var host = new MockLythonHost();
        host.SeedStandardInput("typed line\n");

        var result = new LythonEngine().Run(
            """
import sys
value = input("prompt> ")
sys.stdout.write("|tail")
sys.stdout.flush()
sys.stderr.write("warn")
__lython_file = open("/out.txt", "w")
__lython_file.write(value)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("typed line", host.ReadText("/out.txt"));
        Assert.Equal("prompt> |tail", result.StandardOutput);
        Assert.Equal("warn", result.StandardError);
        Assert.Equal("prompt> |tail", host.CapturedStandardOutput());
        Assert.Equal("warn", host.CapturedStandardError());
    }

    [Fact]
    public void Input_FailsCleanlyWhenStandardInputIsUnavailable()
    {
        var result = new LythonEngine().Run("input()\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3040");
    }
}
