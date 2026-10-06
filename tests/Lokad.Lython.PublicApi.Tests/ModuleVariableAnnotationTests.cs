using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ModuleVariableAnnotationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnnotationsFollowDelayedValuesAndSettersInEntryAndImportedModules(bool imported)
    {
        var source = """
            class Box:
             @property
             def item(self):return self._value
             @item.setter
             def item(self,value):
              with open('/setter.txt','w') as f:f.write(str(value))
              self._value=value
              print('setter')
            def value():
             with open('/value.txt') as f:result=int(f.read())
             print('value')
             return result
            def annotation():
             with open('/value.txt') as f:f.read()
             print('annotation')
             return int
            box=Box()
            box.item:annotation()=value()
            result:annotation()=value()
            print(list(__annotations__),__annotations__['result'] is int,box.item)
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
            seed("/value.txt", "7");
            seed("/helper.py", source);
            var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("value\nsetter\nannotation\nvalue\nannotation\n['result'] True 7\n", result.StandardOutput);
            if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously >= 5);
        }
    }

    [Fact]
    public async Task ReplacedAnnotationMappingAwaitsItsItemProtocol()
    {
        var compiled = new LythonEngine().Compile("""
            class Registry:
             def __setitem__(self,key,value):
              with open('/annotation.txt','w') as f:f.write(key)
              print(key,value is int)
            __annotations__=Registry()
            item:int=7
            """);
        Assert.True(compiled.IsValid);
        var sync = compiled.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var host = new DelayedLythonHost();
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("item True\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnnotationMetadataObeysCollectionAndMemoryLimits(bool collection)
    {
        var source = string.Join('\n', Enumerable.Range(0, 1000).Select(i => $"value{i}:int"));
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = collection
            ? new LythonRunOptions { MaxCollectionSize = 8 }
            : new LythonRunOptions { MaxExecutionMemoryBytes = 32768 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal(collection ? "RuntimeError" : "MemoryError", result.Failure?.ExceptionType);
            if (collection) Assert.Contains("collection size", result.Failure?.Message);
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "basic","x:int=7\nprint(list(__annotations__),__annotations__[\"x\"] is int,x)","['x'] True 7\n" };
        yield return new object[] { "annotation_only","x:int\nprint(__annotations__[\"x\"] is int)\ntry:print(x)\nexcept NameError:print(\"unbound\")","True\nunbound\n" };
        yield return new object[] { "early_setup","print(__annotations__)\nif False:x:int","{}\n" };
        yield return new object[] { "parenthesized_only","print(__annotations__)\n(x):int=7\nprint(x,__annotations__)","{}\n7 {}\n" };
        yield return new object[] { "nested_parentheses","((x)):int=7\nprint(x,__annotations__)","7 {}\n" };
        yield return new object[] { "conditional","if False:x:int\nif True:y:str=\"value\"\nprint(list(__annotations__),__annotations__[\"y\"] is str)","['y'] True\n" };
        yield return new object[] { "ordering","def value():print(\"value\");return 7\ndef ann():print(\"annotation\",x);return int\nx:ann()=value()\nprint(__annotations__[\"x\"] is int)","value\nannotation 7\nTrue\n" };
        yield return new object[] { "alias_identity","T=int\nx:T=7\nprint(__annotations__[\"x\"] is T)","True\n" };
        yield return new object[] { "mapping_mutation","x:int=7\n__annotations__[\"other\"]=\"value\"\ny:str=\"a\"\nx:bool\nprint(list(__annotations__),__annotations__[\"x\"] is bool,__annotations__[\"other\"])","['x', 'other', 'y'] True value\n" };
        yield return new object[] { "mapping_replacement","__annotations__={}\nx:int=7\nprint(list(__annotations__))","['x']\n" };
        yield return new object[] { "mapping_bad_type","__annotations__=[]\ntry:x:int=7\nexcept TypeError:print(\"bad mapping\",x)","bad mapping 7\n" };
        yield return new object[] { "mapping_deleted","del __annotations__\ntry:x:int=7\nexcept NameError:print(\"deleted\",x)","deleted 7\n" };
        yield return new object[] { "annotation_replaces_mapping","def ann():\n global __annotations__\n __annotations__={}\n return int\nx:ann()=7\nprint(__annotations__[\"x\"] is int)","True\n" };
        yield return new object[] { "annotation_failure","def ann():raise ValueError(\"annotation failure\")\ntry:x:ann()=7\nexcept ValueError:print(x,__annotations__)","7 {}\n" };
        yield return new object[] { "rhs_failure","def value():raise ValueError(\"value failure\")\ndef ann():print(\"annotation\");return int\ntry:x:ann()=value()\nexcept ValueError:print(__annotations__)","{}\n" };
        yield return new object[] { "member_only","class C:pass\ndef obj():print(\"target\");return C()\ndef ann():print(\"annotation\");return int\nobj().x:ann()\nprint(__annotations__)","target\nannotation\n{}\n" };
        yield return new object[] { "item_only","def obj():print(\"target\");return []\ndef key():print(\"index\");return 0\ndef ann():print(\"annotation\");return int\nobj()[key()]:ann()\nprint(__annotations__)","target\nindex\nannotation\n{}\n" };
        yield return new object[] { "member_value","class C:pass\nx=C()\nx.y:int=7\nprint(x.y,__annotations__)","7 {}\n" };
        yield return new object[] { "item_value","x=[0]\nx[0]:int=7\nprint(x,__annotations__)","[7] {}\n" };
        yield return new object[] { "missing_member_receiver","def ann():print(\"annotation\");return int\ntry:missing.x:ann()\nexcept NameError:print(\"missing\",__annotations__)","missing {}\n" };
        yield return new object[] { "local_unevaluated","def f():\n x:Missing\n (y):Missing=7\n return y\nprint(f())\ntry:print(__annotations__)\nexcept NameError:print(\"no module annotations\")","7\nno module annotations\n" };
        yield return new object[] { "local_target_reads","def f():\n def obj():print(\"target\");return []\n obj()[0]:Missing\nf()","target\n" };
        yield return new object[] { "nested_class_isolation","class C:x:int=7\ntry:print(__annotations__)\nexcept NameError:print(\"no module annotations\")\nprint(list(C.__annotations__))","no module annotations\n['x']\n" };
        yield return new object[] { "loop","for i in range(3):x:int=i\nprint(list(__annotations__),x)","['x'] 2\n" };
        yield return new object[] { "custom_mapping","class M:\n def __setitem__(self,key,value):print(key,value is int)\n__annotations__=M()\nx:int=7","x True\n" };
        yield return new object[] { "unicode_annotation","x:chr(233)=7\nprint(ascii(__annotations__[\"x\"]))","'\\xe9'\n" };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CompatibleBindings(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

}
