using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedTargetRetentionTests
{
    public static IEnumerable<object[]> SliceCases()
    {
        foreach (var (expression, bounds) in new[]
        {
            ("target[1:3]", "1 3 None"),
            ("target[:]", "None None None"),
            ("target[1:5:2]", "1 5 2")
        })
        foreach (var method in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
            yield return [expression, bounds, method, asynchronous];
    }

    [Theory]
    [MemberData(nameof(SliceCases))]
    public async Task UserSlicesGetAndStoreRawKeysInBothModes(string expression, string bounds, bool method, bool asynchronous)
    {
        var operation = method
            ? $"class Runner:\n def run(self):{expression} += [2]\nRunner().run()\n"
            : $"{expression} += [2]\n";
        var source = """
            events=[]
            class Target:
             def __getitem__(self,key):
              self.key=key
              events.append('get')
              return [1]
             def __setitem__(self,key,value):
              assert key == self.key
              events.append('set')
              print(key.start,key.stop,key.step,value)
            target=Target()
            """ + "\n" + operation + "print(events)\n";
        await AssertOutput(source, $"{bounds} [1, 2]\n['get', 'set']\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SliceBoundsStayRawAndRetainIdentityAcrossTheRhs(bool asynchronous)
    {
        await AssertOutput("""
            events=[]
            class Bound:
             def __init__(self,name):self.name=name
             def __index__(self):raise AssertionError('user keys must stay raw')
            start=Bound('start')
            stop=Bound('stop')
            step=Bound('step')
            class Target:
             def __getitem__(self,key):
              assert key.start is start and key.stop is stop and key.step is step
              self.key=key
              events.append('get')
              return [1]
             def __setitem__(self,key,value):
              assert key.start is start and key.stop is stop and key.step is step
              assert key is self.key
              events.append(('set',key.start.name,value))
            target=Target()
            def rhs():
             events.append('rhs')
             start.name='changed'
             return [2]
            class Runner:
             def run(self):target[start:stop:step] += rhs()
            Runner().run()
            print(events)
            """, "['get', 'rhs', ('set', 'changed', [1, 2])]\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiverAndMutableIndexSurviveRhsRebinding(bool asynchronous)
    {
        await AssertOutput("""
            events=[]
            key=[]
            class Target:
             def __init__(self,name):self.name=name
             def __getitem__(self,index):
              events.append(('get',self.name,index is key,list(index)))
              return 1
             def __setitem__(self,index,value):
              events.append(('set',self.name,index is key,list(index),value))
            active=Target('first')
            def receiver():
             events.append('receiver')
             return active
            def index():
             events.append('index')
             return key
            def rhs():
             global active
             events.append('rhs')
             key.append('changed')
             active=Target('replacement')
             return 2
            class Runner:
             def run(self):receiver()[index()] += rhs()
            Runner().run()
            print(events)
            """, "['receiver', 'index', ('get', 'first', True, []), 'rhs', ('set', 'first', True, ['changed'], 3)]\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DescriptorReplacementDuringRhsChangesTheStore(bool asynchronous)
    {
        await AssertOutput("""
            events=[]
            class Descriptor:
             def __init__(self,name):self.name=name
             def __get__(self,obj,owner):
              events.append(('get',self.name))
              return obj._value
             def __set__(self,obj,value):
              events.append(('set',self.name,value))
              obj._value=value
            class Target:
             value=Descriptor('original')
            target=Target()
            target._value=1
            def rhs():
             events.append('rhs')
             Target.value=Descriptor('replacement')
             return 2
            class Runner:
             def run(self):target.value += rhs()
            Runner().run()
            print(events,target._value)
            """, "[('get', 'original'), 'rhs', ('set', 'replacement', 3)] 3\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UserRetainedSliceKeysConsumeTheExecutionBudget(bool asynchronous)
    {
        var script = new LythonEngine().Compile("""
            keep=[]
            class Target:
             def __getitem__(self,key):keep.append(key);return 0
             def __setitem__(self,key,value):pass
            target=Target()
            class Runner:
             def run(self):
              for i in range(400):target[::1] += 1
            Runner().run()
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 32768 };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    private static async Task AssertOutput(string source, string expected, bool asynchronous)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new MockLythonHost();
        var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }
}
