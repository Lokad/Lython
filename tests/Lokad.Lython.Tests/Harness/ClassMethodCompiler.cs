using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests.Harness;

internal static class ClassMethodCompiler
{
    internal static ExecutableCreateFunction Compile(string methodSource)
    {
        var indented = string.Join('\n', methodSource.Split('\n').Select(line => "    " + line));
        var frontend = LythonFrontend.Compile("class TestType:\n" + indented, regexProvider: Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var body = Assert.Single(ExecutableScript.Compile(frontend.Script.RequireNotNull()).EntryPoint.Constants.OfType<ExecutableCreateClass>()).Body;
        return Assert.Single(body.FunctionBindings());
    }
}
