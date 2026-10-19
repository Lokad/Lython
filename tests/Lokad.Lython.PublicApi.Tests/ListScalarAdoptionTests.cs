using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Governed lists fund possible boxes through retained backing capacity.
// Aliases keep Python identity; removal/refill retains capacity funding.
public sealed class ListScalarAdoptionTests
{
    private const long ThreeMib = 3145728;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task DistinctPopulationDeniedBeforeRetention()
    {
        // 100k distinct small ints retain ~4.4 MB; coupons (~6.4 MB) deny at
        // 3 MiB before the retention escapes the budget.
        var script = Compile("return len([i for i in range(100000)])\n");
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(ThreeMib) };
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
    public async Task AliasPopulationUsesConservativeSlotAllowances()
    {
        // Intentional admission change: the original program/cap are retained.
        // 100k numeric slots now need 6.4 MB before backing, even for one box.
        var script = Compile("return len([0] * 100000)\n");
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(ThreeMib) };
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
    public async Task SliceRefillReusesRetainedCapacity()
    {
        // Ten bounded refills reuse the same backing, independent of value identity.
        var script = Compile("""
            lst = []
            for c in range(10):
                del lst[:]
                for i in range(256):
                    lst.append(i + c * 256)
            return len(lst)
            """);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var expected = new BigInteger(256);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AliasIdentitySurvivesAdoption()
    {
        // Adoption is charge-only: aliases keep identity, removal keeps the
        // surviving reference observable.
        var script = Compile("""
            x = 12345
            lst = [x, x, 1]
            first = lst[0] is lst[1]
            del lst[0]
            return [first, lst[0] is x, len(lst)]
            """);
        var expected = new List<object?> { true, true, new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
