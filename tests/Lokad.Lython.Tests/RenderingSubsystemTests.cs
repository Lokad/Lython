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

    [Fact]
    public void RuntimeRendering_DoesNotExposeClrTypeNamesForInternalObjects()
    {
        var engine = new LythonEngine();
        var script = engine.Compile("""
import os
import re
import sys

pattern = re.compile("a+")
match = pattern.search("caaab")
return "|".join([
    str(sys),
    repr(os),
    repr(sys.modules),
    str(pattern),
    repr(match),
    type(sys).__name__,
    type(pattern).__name__,
    type(match).__name__,
])
""");

        var result = script.Run(new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var rendered = Assert.IsType<string>(result.ReturnValue);
        Assert.Contains("<module 'sys'>", rendered, StringComparison.Ordinal);
        Assert.Contains("<module 'os'>", rendered, StringComparison.Ordinal);
        Assert.Contains("re.compile('a+')", rendered, StringComparison.Ordinal);
        Assert.Contains("<re.Match object; span=(1, 4), match='aaa'>", rendered, StringComparison.Ordinal);
        Assert.Contains("|module|Pattern|Match", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Lokad.", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("System.", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft.", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("Utf8Regex", rendered, StringComparison.Ordinal);
    }
}
