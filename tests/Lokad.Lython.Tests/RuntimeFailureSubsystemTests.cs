using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RuntimeFailureSubsystemTests
{
    [Fact]
    public void RuntimeFailureProjection_ProjectsFramesDeterministically()
    {
        var span = new LythonSourceSpan(1, 4, 1, 1);
        var ex = new LythonRuntimeException("ValueError", "bad", span);
        ex.AddFrame("inner", span);
        ex.AddFrame("outer", span);

        var failure = RuntimeFailureProjection.ToPublicFailure(ex, new ProjectionBudget(null));

        Assert.Equal("ValueError", failure.ExceptionType);
        Assert.Equal("bad", failure.Message);
        Assert.Collection(
            failure.StackTrace,
            frame => Assert.Equal("outer", frame.FunctionName),
            frame => Assert.Equal("inner", frame.FunctionName));
    }

    [Fact]
    public void RuntimeFailureProjection_RendersKeyErrorPayloads()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);

        var failure = RuntimeFailureProjection.ToPublicFailure(
            new LythonRuntimeException("KeyError", "Key was not found.", span, null, PyString.FromString("x")),
            new ProjectionBudget(null),
            context);

        Assert.Equal("KeyError", failure.ExceptionType);
        Assert.Equal("'x'", failure.Message);
    }

    [Fact]
    public void RuntimeFailureProjection_KeepsStoredMessageWithoutContext()
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);

        var failure = RuntimeFailureProjection.ToPublicFailure(
            new LythonRuntimeException("KeyError", "Key was not found.", span, null, PyString.FromString("x")),
            new ProjectionBudget(null));

        Assert.Equal("KeyError", failure.ExceptionType);
        Assert.Equal("Key was not found.", failure.Message);
    }
}
