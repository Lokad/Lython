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
        var code = ExecutableScript.Compile(frontend.Script!).EntryPoint;
        Assert.Equal(crossesBoundary, code.Instructions.Any(i => i.OpCode == ExecutableOpCode.AbruptJump));
    }

    [Fact]
    public void OrdinaryFileSearchNeedsNoLocalMirroring()
    {
        var frontend = LythonFrontend.Compile("""
            with open('input.txt') as source:
                for line in source:
                    if line.startswith('NEEDLE:'):
                        break
            """,regexProvider:Utf8RegexProvider.Instance);
        var code = ExecutableScript.Compile(frontend.Script!).EntryPoint;
        Assert.False(code.RequiresLocalVariableMirroring);
    }

}
