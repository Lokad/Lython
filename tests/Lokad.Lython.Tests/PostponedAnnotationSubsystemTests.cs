using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PostponedAnnotationSubsystemTests
{
    [Fact]
    public void LoweredFunctionDefinitionUsesSharedAnnotationMetadata()
    {
        var frontend = LythonFrontend.Compile("from __future__ import annotations\ndef f[T](x:Missing)->list[T]:pass\n",regexProvider:Utf8RegexProvider.Instance);
        Assert.NotNull(frontend.Script);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), null,regexProvider:Utf8RegexProvider.Instance);
        LythonRuntime.ExecuteStatements(LoweredScript.Lower(frontend.Script).Statements, context);
        var function = Assert.IsAssignableFrom<PyFunctionBase>(context.Variables["f"]);
        Assert.True(function.TryGetMember("__annotations__", out var metadata));
        var annotations = Assert.IsType<PyDict>(metadata);
        Assert.Equal("Missing", Assert.IsType<PyString>(annotations.GetItem(PyString.FromString("x"))).AsString());
        Assert.Equal("list[T]", Assert.IsType<PyString>(annotations.GetItem(PyString.FromString("return"))).AsString());
    }

}
