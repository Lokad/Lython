using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedFunctionDefaultsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LocalFactoryDefaultsStayOwnedAcrossSuspension(bool cancel)
    {
        const string source = """
def default_value(seed):
    with open('/events.txt', 'a') as events:
        events.write(str(seed))
    return [seed]
def make(seed):
    def call(value=default_value(seed), *, flag=seed + 1) -> tuple:
        return value, flag
    return call
call = make(2)
value, flag = call()
print(value is call.__defaults__[0], flag, call.__annotations__['return'] is tuple)
return value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/events.txt", "before:");
            var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }

                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                if (cancel)
                {
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.Empty(result.StandardOutput);
                    Assert.Equal("before:", host.ReadText("/events.txt"));
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True 3 True\n", result.StandardOutput);
                    Assert.IsType<List<object?>>(result.ReturnValue);
                    Assert.Equal("before:2", host.ReadText("/events.txt"));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.True(host.CompletedAsynchronously > 0);
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedDefinitionsKeepDefaultsAndClosuresIndependent(bool asynchronous)
    {
        const string source = """
def make(seed):
    def call(value: int = seed, /, *extra, flag: int = seed, **named) -> tuple:
        return value, extra, flag, named, seed
    def stream(value=seed, *, flag=seed):
        yield value, flag, seed
    return call, stream, lambda value=seed, *, flag=seed: (value, flag, seed)
first, first_stream, first_lambda = make(2)
second, second_stream, second_lambda = make(7)
first.__defaults__ = (11,)
first.__kwdefaults__['flag'] = 13
first_stream.__kwdefaults__['flag'] = 17
first_lambda.__defaults__ = (19,)
first.__annotations__['return'] = list
del first.__annotations__['flag']
assert second.__annotations__ == {'value': int, 'flag': int, 'return': tuple}
print(first())
print(second(23, 29, value=31, flag=37))
print(list(first_stream()), list(second_stream()))
print(first_lambda(), second_lambda())
print(second.__defaults__, second.__kwdefaults__)
""";
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        var executionBudget = new LythonMemoryBudget(16 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = executionBudget };
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using (var result = asynchronous
                    ? await compiled.RunAsync(new MockLythonHost(), options)
                    : compiled.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("(11, (), 13, {}, 2)\n(23, (29,), 37, {'value': 31}, 7)\n" +
                        "[(2, 17, 2)] [(7, 7, 7)]\n(19, 2, 2) (7, 7, 7)\n(7,) {'flag': 7}\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, executionBudget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
