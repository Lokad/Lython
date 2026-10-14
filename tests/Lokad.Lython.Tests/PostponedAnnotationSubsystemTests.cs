using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class PostponedAnnotationSubsystemTests
{
    [Fact]
    public void RawFunctionDefinitionUsesSharedAnnotationMetadata()
    {
        var frontend = LythonFrontend.Compile("from __future__ import annotations\ndef f[T](x:Missing)->list[T]:pass\n");
        Assert.NotNull(frontend.Script);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), null);
        var execute = typeof(LythonRuntime).GetMethod("ExecuteStatement", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var statement in frontend.Script.Statements) execute.Invoke(null, [statement, context]);
        var function = Assert.IsAssignableFrom<PyFunctionBase>(context.Variables["f"]);
        Assert.True(function.TryGetMember("__annotations__", out var metadata));
        var annotations = Assert.IsType<PyDict>(metadata);
        Assert.Equal("Missing", Assert.IsType<PyString>(annotations.GetItem(PyString.FromString("x"))).AsString());
        Assert.Equal("list[T]", Assert.IsType<PyString>(annotations.GetItem(PyString.FromString("return"))).AsString());
    }

}
