using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class StackSafetySubsystemTests
{
    [Fact]
    public void AnnotationOnlyAssignmentForcesLoweredExecution()
    {
        var frontend = LythonFrontend.Compile("x: int\ndef f():\n return f()\nf()\n");
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());

        var failure = Assert.Throws<ExecutableLoweringFallbackException>(() => ExecutableScript.Compile(lowered));
        Assert.Contains("annotation-only", failure.Message, StringComparison.Ordinal);
    }
}
