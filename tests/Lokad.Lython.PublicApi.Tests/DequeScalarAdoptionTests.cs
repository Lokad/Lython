using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Governed deques fund nodes and retained-value headroom together. Aliases pay
// per node; eviction and indexed replacement transfer the funded storage.
public sealed class DequeScalarAdoptionTests
{
    private const long ThreeMib = 3145728;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task DistinctAppendsDeniedBeforeRetention()
    {
        var script = Compile("""
            import collections
            d = collections.deque()
            for i in range(100000):
                d.append(i)
            return len(d)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ThreeMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task AliasAppendsPreserveValuesWithinTheirFundedBudget()
    {
        // Repeated aliases preserve Python identity but each node now includes
        // its own retained-value allowance. The execution budget stays 3 MiB;
        // the former <=1 MiB peak oracle no longer describes this contract.
        var script = Compile("""
            import collections
            d = collections.deque()
            x = 0
            for i in range(10000):
                d.append(x)
            return len(d)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var expected = new BigInteger(10000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.InRange(sync.PeakExecutionMemoryBytes, 1280000, ThreeMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.InRange(asyncResult.PeakExecutionMemoryBytes, 1280000, ThreeMib);
    }

    [Fact]
    public async Task MaxlenRotationStaysBounded()
    {
        var script = Compile("""
            import collections
            d = collections.deque(maxlen=3)
            for i in range(100000):
                d.append(i)
            return list(d)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(99997), new BigInteger(99998), new BigInteger(99999) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MutationTurnoverBehaves()
    {
        var script = Compile("""
            import collections
            d = collections.deque([1, 2, 3])
            out = []
            d.appendleft(0)
            d.insert(2, 99)
            d[0] = 7
            out.append([d.pop(), d.popleft(), len(d)])
            d.remove(99)
            d.extend([8, 9])
            out.append(list(d))
            d.clear()
            out.append(len(d))
            return out
            """);
        var expected = new List<object?>
        {
            new List<object?> { new BigInteger(3), new BigInteger(7), new BigInteger(3) },
            new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(8), new BigInteger(9) },
            new BigInteger(0),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
