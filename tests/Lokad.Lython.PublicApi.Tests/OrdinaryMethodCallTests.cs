using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OrdinaryMethodCallTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["inherited defaults and exposed identity", "class Base:\n def f(self,value=2,*,scale=3):return value*scale\nclass Child(Base):pass\nobj=Child()\nprint(obj.f(),obj.f(4,scale=5),Child.f(obj,3),obj.f.__self__ is obj,obj.f.__func__ is Base.f,obj.f is obj.f)\n", "6 20 9 True True False\n"];
        yield return ["binding precedes arguments", "class C:\n def run(self,value):return 'old'+value\ndef replacement(self,value):return 'new'+value\nc=C()\ndef change():\n C.run=replacement\n c.run=lambda value:'own'+value\n return ':value'\nprint(c.run(change()),c.run(':later'),C.run(c,':class'))\n", "old:value own:later new:class\n"];
        yield return ["custom attribute lookup", "class C:\n def __getattribute__(self,name):\n  if name=='run':\n   print('lookup')\n   return lambda value:'hook'+value\n  return object.__getattribute__(self,name)\n def run(self,value):return 'unreachable'\nprint(C().run(':ok'))\n", "lookup\nhook:ok\n"];
        yield return ["descriptor before argument", "class D:\n def __get__(self,obj,owner):\n  print('get')\n  return lambda value:'descriptor'+value\nclass C:run=D()\ndef argument():print('argument');return ':ok'\nprint(C().run(argument()))\n", "get\nargument\ndescriptor:ok\n"];
        yield return ["static class and own functions", "class C:\n @staticmethod\n def static(value=2):return value+1\n @classmethod\n def classed(cls,value=3):return cls.__name__,value\n def run(self,value):return value\nc=C()\nc.run=lambda value:value+7\nprint(c.static(),c.classed(value=8),c.run(3))\n", "3 ('C', 8) 10\n"];
        yield return ["missing attribute fallback", "class C:\n def __getattr__(self,name):return lambda value:name+value\nprint(C().missing(':ok'))\n", "missing:ok\n"];
        yield return ["argument failure prevents invocation", "class C:\n def run(self,value):print('called')\ndef argument():raise ValueError('bad')\ntry:C().run(argument())\nexcept ValueError as error:print(str(error))\n", "bad\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task CallsPreservePythonBindingAndEvaluation(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationDuringArgumentReadAwaitsCleanupAndReusesBudget()
    {
        const string source = """
            class Receiver:
                def consume(self,value):print('called');return value
            receiver=Receiver()
            def argument():
                global receiver
                receiver=None
                with open('/pending.txt') as source:return source.read()
            receiver.consume(argument())
            print('after')
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/pending.txt", "value");
        using var cancellation = new CancellationTokenSource();
        var started = host.PauseReadUntilCancellation("/pending.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally { cancellation.Cancel(); }
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(30)))
        {
            Assert.False(result.Success);
            Assert.Contains("execution canceled", result.Failure?.Message);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var nextHost = new MockLythonHost();
        nextHost.SeedFile("/pending.txt", "value");
        using (var result = await script.RunAsync(nextHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("called\nafter\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedArgumentNeverInvokesSelectedMethod(bool asynchronous)
    {
        const string source = """
            class Receiver:
                def consume(self,value):print('called')
            def argument():
                print('argument')
                try:payload='x'*1000000
                except MemoryError:return 'suppressed'
                finally:return 'finally'
            Receiver().consume(argument())
            print('after')
            """;
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal("argument\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
