using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class StackSafetySubsystemTests
{
    [Fact]
    public void ModuleAnnotationOnlyAssignmentUsesStatementFallback()
    {
        var frontend = LythonFrontend.Compile("x: int\ndef f():\n return f()\nf()\n");
        var lowered = LoweredScript.Lower(frontend.Script.RequireNotNull());

        var executable = ExecutableScript.Compile(lowered);
        Assert.IsType<LoweredAnnotatedAssignmentStatement>(Assert.Single(executable.EntryPoint.StatementFallbacks).Statement);
    }
}
