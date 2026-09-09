using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG14: functools registries own their storage: partial bound-argument
/// arrays, dispatch registrations, wrapper metadata, and per-instance
/// cached_property values.
/// </summary>
public sealed class FunctoolsRegistryAccountingScenarioTests
{
    [Fact]
    public async Task ManyPartialsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            def f(a, b, c, d, e):
                return a
            ps = []
            i = 0
            while i < 2000:
                ps.append(functools.partial(f, 1, 2, 3, 4, 5))
                i = i + 1
            return len(ps)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyDispatchRegistrationsStayCharged()
    {
        // Distinct class statements isolate registry growth; dynamically built
        // type names would commit renderer charges of their own.
        var builder = new StringBuilder("import functools\ndef f(x):\n    return 0\nd = functools.singledispatch(f)\n");
        for (var i = 0; i < 1200; i++)
        {
            builder.Append("class T").Append(i).Append(":\n    pass\nd.register(T").Append(i).Append(", f)\n");
        }

        builder.Append("return 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ManyWrapperAttributesStayCharged()
    {
        var builder = new StringBuilder("import functools\ndef g(n):\n    return n\nf = functools.lru_cache(maxsize=8)(g)\n");
        for (var i = 0; i < 1500; i++)
        {
            builder.Append("f.k").Append(i).Append(" = None\n");
        }

        builder.Append("return 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CachedPropertyPerInstanceStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            class C:
                @functools.cached_property
                def prop(self):
                    return 1
            objs = []
            i = 0
            while i < 2000:
                o = C()
                objs.append(o)
                v = o.prop
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RegistryBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            def add(a, b):
                return a + b
            p = functools.partial(add, 1)
            @functools.singledispatch
            def h(x):
                return "base"
            @h.register(int)
            def _h(x):
                return "int"
            class C:
                @functools.cached_property
                def v(self):
                    return 7
            c = C()
            f = functools.lru_cache(maxsize=8)(add)
            f.tag = "t"
            return [p(2), h(1), h("s"), c.v, f.tag]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), "int", "base", new BigInteger(7), "t" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}