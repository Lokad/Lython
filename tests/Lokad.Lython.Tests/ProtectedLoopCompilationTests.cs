using Lokad.Lython.Frontend;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ProtectedLoopCompilationTests
{
    [Theory]
    [InlineData("with open('input.txt') as source:\n    for line in source:\n        if line:\n            continue\n        break", false)]
    [InlineData("try:\n    while True:\n        if False:\n            continue\n        break\nfinally:\n    pass", false)]
    [InlineData("for i in range(2):\n    with open('input.txt') as source:\n        break", true)]
    [InlineData("while True:\n    try:\n        continue\n    finally:\n        pass", true)]
    public void OnlyCrossingProtectedSuitesNeedsAbruptRouting(string source, bool crossesBoundary)
    {
        var frontend = LythonFrontend.Compile(source, regexProvider: Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        Assert.Equal(crossesBoundary, code.Instructions.Any(i => i.OpCode == ExecutableOpCode.AbruptJump));
    }

    [Fact]
    public void OrdinaryFileSearchNeedsNeitherFallbackNorLocalMirroring()
    {
        var frontend = LythonFrontend.Compile("""
            with open('input.txt') as source:
                for line in source:
                    if line.startswith('NEEDLE:'):
                        break
            """,regexProvider:Utf8RegexProvider.Instance);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        Assert.Empty(code.StatementFallbacks);
        Assert.False(code.RequiresLocalVariableMirroring);
    }

    public static IEnumerable<object[]> Programs()
        => ProtectedLoopPrograms.Cases.Select(p => new object[] { p.Name, p.Source });

    [Theory]
    [MemberData(nameof(Programs))]
    public void ProtectedLoopJumpsRemainExecutable(string name, string source)
    {
        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
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
