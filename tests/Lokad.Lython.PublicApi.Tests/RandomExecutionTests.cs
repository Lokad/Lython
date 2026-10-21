using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RandomExecutionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BitResultTransfersItsAdmittedWorkingStorage(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import random\nreturn random.getrandbits(100000).bit_length()");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.InRange(Assert.IsType<BigInteger>(result.ReturnValue), BigInteger.One, new BigInteger(100000));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("getrandbits(2147483647)")]
    [InlineData("getrandbits(2147483648)")]
    [InlineData("randbytes(268435456)")]
    public async Task LargeRepresentableWidthsReachAdmissionWithoutAllocating(string call)
    {
        foreach (var receiver in new[] { "random", "random.Random(1)" })
        {
            using var script = new LythonEngine().Compile("import random\nreturn " + receiver + "." + call);
            Assert.True(script.IsValid);
            foreach (var asynchronous in new[] { false, true })
            {
                var budget = new LythonMemoryBudget(32768);
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                    : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                {
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                    Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BitByteDrawsAndCustomWidthProtocolsPreserveTheirOrder(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import random
            r=random.Random(123)
            for n in [0,1,7,8,9,17]:
                state=r.getstate()
                raw=r.randbytes(n)
                r.setstate(state)
                assert raw == r.getrandbits(8*n).to_bytes(n,'little')
            for k in [0,1,7,8,9,64,65]:
                assert 0 <= r.getrandbits(k) < 2**k
            events=[]
            before=r.getstate()
            class Count:
                def __mul__(self,other):
                    events.append('mul')
                    return 8
                def __index__(self):
                    events.append('index')
                    assert r.getstate() != before
                    return 2
            raw=r.randbytes(Count())
            print(events, len(raw), raw[1])
            for n in [-1, 1 << 100]:
                try: r.getrandbits(n)
                except Exception as error: print(type(error).__name__)
                try: r.randbytes(n)
                except Exception as error: print(type(error).__name__)
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['mul', 'index'] 2 0\nValueError\nValueError\nOverflowError\nOverflowError\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DrawsRemainFundedUntilBothResultsEnd(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(524288);
        using var script = new LythonEngine().Compile("import random\nreturn [random.randbytes(4096),random.getrandbits(8192)]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var first = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        using var second = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(first.ReturnValue);
        Assert.Equal(4096, Assert.IsType<byte[]>(values[0]).Length);
        Assert.InRange(Assert.IsType<BigInteger>(values[1]).GetBitLength(), 1L, 8192L);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
