using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamespaceViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedModuleNamespacesReuseTheirActualStorage(bool asynchronous)
    {
        const string source = """
            import helper,math
            d=vars(helper)
            print(d is helper.__dict__)
            d['value']=8
            helper.extra=9
            print(helper.value,d['extra'])
            del d['value']
            print(hasattr(helper,'value'))
            native=vars(math)
            print(native is math.__dict__,native['sqrt'] is math.sqrt)
            try:native['sqrt']=0
            except TypeError:print('readonly')
            """;
        ILythonHost host;
        if (asynchronous)
        {
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/helper.py", "value=1");
            host = delayed;
        }
        else
        {
            var immediate = new MockLythonHost();
            immediate.SeedFile("/helper.py", "value=1");
            host = immediate;
        }
        using var script = new LythonEngine().Compile(source);
        var options = new LythonRunOptions { SourcePath = "/main.py", AllowedLocalModules = new HashSet<string> { "helper" } };
        using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.True(result.Success, result.Failure?.Message ?? string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal("True\n8 9\nFalse\nTrue True\nreadonly\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObjectNamespacesAreLiveAndClassViewsAreReadOnly(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            class C:class_value=1
            c=C()
            c.x=2
            d=vars(c)
            print(d is c.__dict__)
            d['x']=7
            print(c.x)
            c.y=3
            print(d['y'])
            iterator=iter(d)
            next(iterator)
            c.z=4
            try:next(iterator)
            except RuntimeError:print('changed')
            d.clear()
            print(hasattr(c,'x'))
            view=vars(C)
            print(type(view).__name__,isinstance(view,dict),view is C.__dict__)
            C.class_value=9
            print(view['class_value'],view==dict(view))
            try:view['x']=7
            except TypeError:print('readonly')
            try:c.__dict__={}
            except NotImplementedError:print('replace unsupported')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\n7\n3\nchanged\nFalse\nmappingproxy False True\n9 True\nreadonly\nreplace unsupported\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FunctionAndWrapperDictionariesHaveCanonicalCustomAttributes(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import partial,lru_cache
            def f():pass
            for value in (f,partial(f),lru_cache()(f)):
                value.tag=1
                d=vars(value)
                d['tag']=2
                print(d is value.__dict__,value.tag)
            old=vars(f)
            f.__dict__={'tag':3}
            old['tag']=4
            print(f.tag,old['tag'])
            f.__dict__['__name__']='shadow'
            print(f.__name__)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True 2\nTrue 2\nTrue 2\n3 4\nf\n", result.StandardOutput);
    }

    [Fact]
    public async Task DictionaryDescriptorsAwaitIoAndUnwindCancellation()
    {
        using var script = new LythonEngine().Compile("""
            class C:
                @property
                def __dict__(self):
                    with open('/namespace') as source:return {'value':source.read()}
            print(vars(C())['value'])
            """);
        var budget = new LythonMemoryBudget(262144);
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost(); host.SeedFile("/namespace", "value");
            var entered = cancel ? host.PauseReadUntilCancellation("/namespace") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.False(pending.IsCompleted); cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else { Assert.True(result.Success, result.Failure?.Message); Assert.Equal("value\n", result.StandardOutput); Assert.True(host.CompletedAsynchronously > 0); }
            }
            finally { cancellation.Cancel(); using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadOnlyNamespacesProjectToFundedIndependentResults(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("class C:value=7\nreturn vars(C)");
        var budget = new LythonMemoryBudget(262144);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsType<Dictionary<object, object?>>(result.ReturnValue);
            Assert.Equal(new System.Numerics.BigInteger(7), values["value"]);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModuleAndClassLocalsAreLiveButFunctionLocalsAreSnapshots(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            x=1
            module=locals()
            x=2
            print(module is globals(),module is vars(),module['x'])
            module['dynamic']=7
            print(dynamic)
            del module['dynamic']
            print('dynamic' in globals())
            class C:
                saved=locals()
                x=2
                print(saved is locals(),saved['x'])
                saved['y']=3
                print(y)
                del saved['y']
                print('y' in locals())
            C.saved['x']=9
            print(C.x)
            def f():
                x=1
                saved=locals()
                x=2
                print(saved['x'],locals()['x'],saved is locals())
            f()
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True True 2\n7\nFalse\nTrue 2\n3\nFalse\n2\n1 2 False\n", result.StandardOutput);
    }
}
