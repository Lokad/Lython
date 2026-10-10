using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ModuleFunctionSuperBoundaryTests
{
    [Fact]
    public async Task OwnerBindingAfterDefinitionKeepsMissingClassCellError()
    {
        var compiled = new LythonEngine().Compile("""
            def external(self): return super()
            class Base: pass
            class C(Base): method=external
            for call in [lambda:C().method(),lambda:external(C())]:
             try: call()
             except RuntimeError as error: print(str(error))
            """);
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("super(): __class__ cell not found\nsuper(): __class__ cell not found\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ImportedFunctionsKeepTheirGlobalsAndReceiverLayouts()
    {
        const string module = """
            value=17
            def positional(receiver, /, offset=2): return value+receiver+offset
            def named(*, receiver=3): return value+receiver
            def variadic(*args, offset=1): return value+sum(args)+offset
            def keywords(**kwargs): return value+sum(kwargs.values())
            def no_receiver(): return value
            """;
        var compiled = new LythonEngine().Compile("""
            import helper
            value=1000
            print(helper.positional(3),helper.positional(3,offset=4),helper.named(),helper.named(receiver=5))
            print(helper.variadic(2,3,offset=4),helper.keywords(a=2,b=3),helper.no_receiver())
            """);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        var syncHost = new MockLythonHost(); syncHost.SeedFile("/helper.py", module);
        var asyncHost = new MockLythonHost(); asyncHost.SeedFile("/helper.py", module);
        foreach (var result in new[] { compiled.Run(syncHost, options), await compiled.RunAsync(asyncHost, options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("22 24 20 22\n26 22 17\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ImportedFunctionOwnerBindingKeepsFailureSourceAndSpan()
    {
        var compiled = new LythonEngine().Compile("""
            import helper
            class C: method=helper.external
            C().method()
            """);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        var syncHost = new MockLythonHost(); syncHost.SeedFile("/helper.py", "def external(self):\n    return super()\n");
        var asyncHost = new MockLythonHost(); asyncHost.SeedFile("/helper.py", "def external(self):\n    return super()\n");
        foreach (var result in new[] { compiled.Run(syncHost, options), await compiled.RunAsync(asyncHost, options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Equal("super(): __class__ cell not found", result.Failure?.Message);
            Assert.Equal("/helper.py", result.Failure?.SourcePath);
            Assert.Equal(2, result.Failure?.Span?.Line);
        }
    }
}
