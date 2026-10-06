using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed partial class PostponedAnnotationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultsAndDecoratorsAwaitWhileAnnotationsRemainUnevaluated(bool imported)
    {
        var source = """
            from __future__ import annotations
            def bad():
             raise ValueError('annotation was evaluated')
            def default():
             with open('/value.txt') as f:value=int(f.read())
             print('default')
             return value
            def decorate(f):
             with open('/value.txt') as stream:stream.read()
             print(f.__annotations__)
             return f
            @decorate
            def identity[T](x:bad()=default())->bad():return x
            value:bad()=7
            class C:child:bad()=8
            print(identity(),__annotations__,C.__annotations__)
            """;
        var compiled = new LythonEngine().Compile(imported ? "import helper" : source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper", "/helper.py" }
        };
        foreach (var asynchronous in new[] { false, true })
        {
            ILythonHost host = asynchronous ? new DelayedLythonHost() : new MockLythonHost();
            Action<string, string> seed = host is DelayedLythonHost delayedHost
                ? delayedHost.SeedFile : ((MockLythonHost)host).SeedFile;
            seed("/value.txt", "9");
            seed("/helper.py", source);
            var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("default\n{'x': 'bad()', 'return': 'bad()'}\n9 {'value': 'bad()'} {'child': 'bad()'}\n", result.StandardOutput);
            if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously >= 3);
        }
    }

    [Fact]
    public async Task ModuleFlagsDoNotLeakIntoImportedModules()
    {
        var compiled = new LythonEngine().Compile("""
            from __future__ import annotations
            import eager
            import postponed
            print(eager.f.__annotations__['x'] is int,postponed.f.__annotations__['x'])
            """);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "eager", "/eager.py", "postponed", "/postponed.py" }
        };
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/eager.py", "def f(x:int):pass\n");
            host.SeedFile("/postponed.py", "from __future__ import annotations\ndef f(x:Missing):pass\n");
            var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True Missing\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ReplacedClassMappingAwaitsItsItemProtocol()
    {
        var compiled = new LythonEngine().Compile("""
            from __future__ import annotations
            class Registry:
             def __setitem__(self,key,value):
              with open('/annotation.txt','w') as f:f.write(value)
              print(key,value)
            class C:
             __annotations__=Registry()
             item:Missing=7
            """);
        Assert.True(compiled.IsValid);
        var sync = compiled.Run(new MockLythonHost());
        var host = new DelayedLythonHost();
        var result = await compiled.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("item Missing\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("def f(x:Missing)->Other:pass")]
    [InlineData("def f[T](x:T)->Other:pass")]
    [InlineData("def f():pass\n unused=f.__annotations__")]
    [InlineData("f=lambda:7\n unused=f.__annotations__")]
    public async Task DroppedFunctionsReclaimAnnotationMetadata(string definition)
    {
        var source = "from __future__ import annotations\nfor i in range(5000):\n " + definition + "\nprint('done')";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("done\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedMetadataRetainsItsGovernedStorage(bool retainFunction)
    {
        var source = "from __future__ import annotations\nvalues=[]\nfor i in range(1000):\n def f(x:Missing)->Other:pass\n values.append(" +
            (retainFunction ? "f" : "f.__annotations__") + ")\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 32768 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MetadataLimitsApplyBeforeDecoratorPublication(bool collection)
    {
        var declaration = collection ? "def f(" + string.Join(',', Enumerable.Range(0, 12).Select(i => $"x{i}:T")) + "):pass"
            : "def f(x:VeryLongAnnotationName):pass";
        var source = "from __future__ import annotations\ndef deco(f):print('decorator');return f\n@deco\n" + declaration;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = collection ? new LythonRunOptions { MaxCollectionSize = 8 } : new LythonRunOptions { MaxStringLength = 16 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("print('effect')\nfrom __future__ import annotations\ndef f(x:Missing):pass")]
    [InlineData("def f():\n from __future__ import annotations")]
    [InlineData("if False:\n from __future__ import annotations")]
    [InlineData("from __future__ import annotations\nx:(value:=int)")]
    [InlineData("from __future__ import annotations\ndef f(x:(value:=int)):pass")]
    [InlineData("from __future__ import annotations\nx:(lambda a=(value:=int):a)")]
    public void InvalidPostponedScopesFailBeforeEffects(string source)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA1100");
    }

    [Fact]
    public void NestedFormattedAnnotationsHaveBoundedCanonicalText()
    {
        var expression = "Missing";
        for (var i = 0; i < 28; i++) expression = "f\"{" + expression + "}\"";
        var compiled = new LythonEngine().Compile("from __future__ import annotations\nx:" + expression);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code == "LA0005");
    }
}
