using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RenderingSubsystemTests
{
    [Fact]
    public void RuntimeRendering_SeparatesPythonAndInterpolatedRendering()
    {
        var engine = new LythonEngine();
        var script = engine.Compile("""
value = {"x": (1,), "y": {2, 1}}
return (str(value), f"{value}")
""");

        var result = script.Run(new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var tuple = Assert.IsType<object?[]>(result.ReturnValue);
        Assert.Equal("{'x': (1,), 'y': {1, 2}}", tuple[0]);
        Assert.Equal("{'x': (1,), 'y': {1, 2}}", tuple[1]);
    }

    [Fact]
    public void PyRendering_RendersNestedRuntimeValuesDirectly()
    {
        var context = new PyRenderingContext(new LythonRuntime.ExecutionContext(new MockLythonHost(), null));
        var value = new PyDict();
        value.SetItem(PyString.FromString("items"), new PyList([new BigInteger(1), PyNone.Instance]));

        var rendered = PyRendering.ToPythonPyString(value, context);

        Assert.Equal("{'items': [1, None]}", rendered.AsString());
    }
}
