using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CallableInstanceProtocolTests
{
    [Fact]
    public async Task InstanceAttributesDoNotCreateOrReplaceTheCallSlot()
    {
        await AssertBothModes("""
            class Missing:
                pass
            missing = Missing()
            missing.__call__ = lambda: 'own'
            print(callable(missing))
            try:
                missing()
            except TypeError:
                print('not callable')
            class Base:
                def __call__(self, value):
                    return 'type:' + value
            class Derived(Base):
                pass
            value = Derived()
            value.__call__ = lambda text: 'own:' + text
            print(callable(value), value('x'), value.__call__('x'))
            Base.__call__ = lambda self, text: 'new:' + text
            print(value('y'))
            """, "False\nnot callable\nTrue type:x own:x\nnew:y\n");
    }

    [Fact]
    public async Task CallabilityDoesNotRunAttributeHooksOrCallDescriptors()
    {
        await AssertBothModes("""
            events = []
            class Hook:
                def __getattribute__(self, name):
                    events.append(name)
                    return object.__getattribute__(self, name)
                @property
                def __call__(self):
                    events.append('descriptor')
                    return lambda: 'called'
            value = Hook()
            print(callable(value), events)
            print(value(), events)
            class Bad:
                __call__ = None
            print(callable(Bad()))
            try:
                Bad()()
            except TypeError:
                print('bad slot')
            """, "True []\ncalled ['descriptor']\nTrue\nbad slot\n");
    }

    [Fact]
    public async Task LambdasBindAsOrdinaryFunctionDescriptors()
    {
        await AssertBothModes("""
            class Callback:
                method = lambda self, value: self.prefix + value
                __call__ = lambda self, value: self.method(value)
                unbound = staticmethod(lambda value: value + '!')
                class_bound = classmethod(lambda cls: cls.__name__)
                def __init__(self):
                    self.prefix = 'type:'
            callback = Callback()
            callback.method = lambda value: 'own:' + value
            print(Callback.method(callback, 'a'))
            print(callback('b'), callback.unbound('c'), callback.class_bound())
            saved = Callback().method
            print(saved('d'), saved.__self__.prefix, saved.__func__ is Callback.method)
            """, "type:a\nown:b c! Callback\ntype:d type: True\n");
    }

    [Fact]
    public async Task AsyncCallAwaitsTheTypeSlotDescriptor()
    {
        const string source = """
            class Callback:
                @property
                def __call__(self):
                    with open('/marker') as marker:
                        value = marker.read()
                    return lambda: value
            callback = Callback()
            callback.__getattribute__ = lambda name: None
            print(callable(callback))
            print(callback())
            """;
        var script = Compile(source);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/marker", "ready");
        var synchronous = script.Run(syncHost);
        Assert.True(synchronous.Success, synchronous.Failure?.Message);
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/marker", "ready");
        var asynchronous = await script.RunAsync(delayedHost);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal("True\nready\n", synchronous.StandardOutput);
        Assert.Equal(synchronous.StandardOutput, asynchronous.StandardOutput);
        Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = Compile(source);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
