using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExactIntegerOperatorTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "print(13-4,13*4,13/4,13//4,13%4,13**4)", "9 52 3.25 3 1 28561\n" },
        { "print(-13//4,-13%4,13//-4,13%-4,2**-3)", "-4 3 -4 -3 0.125\n" },
        { "print(13|4,13^4,13&4,13<<4,13>>4,-13>>4)", "13 9 4 208 0 -1\n" },
        { "print(13<4,13<=13,13>4,13>=13,13==4,13!=4)", "False True True True False True\n" },
        { "x=1<<100\nprint(x is x,x is not x,x==int(str(x)),(x*x)//x==x)", "True False True True\n" },
        { "print((2**63)*2,(2**64)-1,(-(2**64))>>63,(-1)<<(65))", "18446744073709551616 18446744073709551615 -2 -36893488147419103232\n" },
        { "print(True-1,True*4,4/True,False|1,13-0.5,2*1.5)", "0 4 4.0 1 12.5 3.0\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ResultsAndCleanupMatchBothExecutionApis(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("/", "0", "ZeroDivisionError")]
    [InlineData("//", "0", "ZeroDivisionError")]
    [InlineData("%", "0", "ZeroDivisionError")]
    [InlineData("<<", "-1", "ValueError")]
    [InlineData(">>", "-1", "ValueError")]
    [InlineData("@", "2", "TypeError")]
    public async Task FailedAugmentedOperationsDoNotAssign(string token, string right, string error)
    {
        await ResultsAndCleanupMatchBothExecutionApis($"value=13\noriginal=value\ntry:\n value {token}= {right}\nexcept {error}:\n print(value is original,value)\n", "True 13\n");
    }

    [Theory]
    [MemberData(nameof(AugmentedProtocolFallbackTests.Operators), MemberType = typeof(AugmentedProtocolFallbackTests))]
    public async Task DynamicOperatorsStillAwaitEachFallbackOnce(string token, string method)
    {
        var source = $$"""
def mark(value):
    with open('events.txt','a') as target: target.write(value+'\n')
class Left:
    def __i{{method}}__(self,other):
        mark('in-place')
        return NotImplemented
    def __{{method}}__(self,other):
        mark('left')
        return NotImplemented
class Right:
    def __r{{method}}__(self,other):
        mark('right')
        return 42
value=Left()
value {{token}}= Right()
return value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
            Assert.Equal("in-place\nleft\nright\n", host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("seed*seed")]
    [InlineData("seed<<64")]
    [InlineData("seed-1")]
    public async Task EscapingMagnitudeResultsRemainFunded(string expression)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile($"return {expression}", budget);
        var baseline = budget.ReservedBytes;
        var seed = System.Numerics.BigInteger.One << 10000;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["seed"] = seed } };
        using var first = script.Run(new MockLythonHost(), options);
        using var second = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal(first.ReturnValue, second.ReturnValue);
        first.Dispose();
        Assert.True(budget.ReservedBytes > baseline);
        second.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReflectedSubtractionCanSuspendAndCancelWithoutPublishing()
    {
        const string source = """
class Right:
    def __rsub__(self,other):
        with open('events.txt','a') as target: target.write('changed')
        return 42
value=13
value-=Right()
return value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/events.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            Assert.Equal("before", host.ReadText("/events.txt"));
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/events.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
