using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class RuntimeFailureSubsystemTests
{
    [Fact]
    public void RuntimeFailureProjection_ProjectsFramesDeterministically()
    {
        var span = new LythonSourceSpan(1, 4, 1, 1);
        var ex = new LythonRuntimeException("ValueError", "bad", span);
        ex.AddFrame("helper", span);

        var failure = RuntimeFailureProjection.ToPublicFailure(ex);

        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Equal("bad", failure.Message);
        Assert.Single(failure.StackTrace);
        Assert.Equal("helper", failure.StackTrace[0].FunctionName);
    }
}
