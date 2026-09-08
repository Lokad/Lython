using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TracebackScenarioTests
{
    [Fact]
    public void NestedRuntimeFailure_CapturesScriptFrames()
    {
        const string script = """
def outer():
    inner()

def inner():
    raise RuntimeError("boom")

outer()
""";

        var result = new LythonEngine().Run(script, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure.ExceptionType);
        Assert.Collection(
            result.Failure.StackTrace,
            frame => Assert.Equal("outer", frame.FunctionName),
            frame => Assert.Equal("inner", frame.FunctionName));
    }
}
