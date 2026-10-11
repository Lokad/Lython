using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorAugmentedSliceBoundaryTests
{
    [Fact]
    public async Task ExplicitStepKeyAndReceiverSurviveRhsSuspension()
    {
        await AssertOutput("""
            class Target:
             def __init__(self):self.value=1;self.same=False
             def __getitem__(self,key):self.key=key;return self.value
             def __setitem__(self,key,value):self.same=key is self.key;self.value=value
            target=Target()
            original=target
            def generate():
             target[::1]+=yield 'rhs'
             yield 'done'
            g=generate()
            print(next(g))
            target=Target()
            print(g.send(3))
            print(original.same,original.value,target.value)
            """, "rhs\ndone\nTrue 4 1\n");
    }

    [Fact]
    public async Task SuspendedOperandsKeepRawBoundsAndSingleUserKey()
    {
        await AssertOutput("""
            class Bound:
             def __index__(self):raise AssertionError('user slice bounds stay raw')
            start=Bound()
            stop=Bound()
            step=Bound()
            class Target:
             def __getitem__(self,key):self.key=key;return 1
             def __setitem__(self,key,value):self.same=key is self.key;self.value=value
            box=Target()
            def generate():
             (yield 'receiver')[(yield 'start'):(yield 'stop'):(yield 'step')]+=yield 'rhs'
             yield box.same,box.value,box.key.start is start,box.key.stop is stop,box.key.step is step
            g=generate()
            print(next(g),g.send(box),g.send(start),g.send(stop),g.send(step))
            print(g.send(3))
            """, "receiver start stop step rhs\n(True, 4, True, True, True)\n");
    }

    [Fact]
    public async Task GetterFailureStopsBeforeRhsSuspension()
    {
        await AssertOutput("""
            events=[]
            class Target:
             def __getitem__(self,key):events.append('get');raise ValueError('stop')
             def __setitem__(self,key,value):events.append('store')
            box=Target()
            def generate():
             box[::1]+=yield 'rhs'
            try:next(generate())
            except ValueError as error:print(str(error))
            print(events)
            """, "stop\n['get']\n");
    }

    [Fact]
    public async Task ThrowDuringRhsUnwindsWithoutStore()
    {
        await AssertOutput("""
            events=[]
            class Target:
             def __getitem__(self,key):events.append('get');return 1
             def __setitem__(self,key,value):events.append('store')
            box=Target()
            def generate():
             try:box[::1]+=yield 'rhs'
             finally:events.append('finally')
            g=generate()
            print(next(g))
            try:g.throw(ValueError('stop'))
            except ValueError as error:print(str(error))
            print(events)
            """, "rhs\nstop\n['get', 'finally']\n");
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
