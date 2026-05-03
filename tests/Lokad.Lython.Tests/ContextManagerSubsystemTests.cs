using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ContextManagerSubsystemTests
{
    [Fact]
    public void WithOpen_UsesContextManagerProtocolAndCleansUpHandle()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
with open("/out.txt", "w") as handle:
    handle.write("hello")
return "ok"
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", host.ReadText("/out.txt"));
        Assert.Equal("ok", result.ReturnValue);
    }

    [Fact]
    public void WithOpen_InvokesExitOnExceptionPath()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
try:
    with open("/out.txt", "w") as handle:
        handle.write("hello")
        raise ValueError("boom")
except ValueError:
    pass
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("hello", host.ReadText("/out.txt"));
    }
}
