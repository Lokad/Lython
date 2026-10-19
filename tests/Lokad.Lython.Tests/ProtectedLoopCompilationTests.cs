using Lokad.Lython.Frontend;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ProtectedLoopCompilationTests
{
    [Fact]
    public void OrdinaryFileSearchNeedsNeitherFallbackNorLocalMirroring()
    {
        var frontend = LythonFrontend.Compile("""
            with open('input.txt') as source:
                for line in source:
                    if line.startswith('NEEDLE:'):
                        break
            """);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        Assert.Empty(code.StatementFallbacks);
        Assert.Empty(code.ExpressionFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    public static IEnumerable<object[]> Programs()
        => ProtectedLoopPrograms.Cases.Select(p => new object[] { p.Name, p.Source });

    [Theory]
    [MemberData(nameof(Programs))]
    public void ProtectedLoopJumpsRemainExecutable(string name, string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.True(frontend.Diagnostics.All(d => d.Severity != LythonDiagnosticSeverity.Error),
            name + ": " + string.Join("; ", frontend.Diagnostics.Select(d => d.Message)));
        var executable = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
        var bodies = Bodies(executable.EntryPoint).ToArray();
        foreach (var body in bodies)
            Assert.DoesNotContain(body.StatementFallbacks,
                f => f is LoweredWithStatement or LoweredTryStatement);
    }

    private static IEnumerable<ExecutableCodeObject> Bodies(ExecutableCodeObject code)
    {
        yield return code;
        foreach (var function in code.Functions)
        {
            foreach (var body in Bodies(function.CodeObject)) yield return body;
        }
    }
}
