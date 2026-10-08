using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinIterationCancellationTests
{
    public static TheoryData<bool, string> Consumers => new()
    {
        { false, "sum(range(n))" },
        { true, "sum(range(n))" },
        { false, "min(range(n))" },
        { true, "min(range(n))" },
        { false, "max(range(n))" },
        { true, "max(range(n))" },
        { false, "sum(itertools.repeat(1, n))" },
        { true, "sum(itertools.repeat(1, n))" },
        { false, "any(itertools.repeat(False, n))" },
        { true, "any(itertools.repeat(False, n))" },
        { false, "all(itertools.repeat(True, n))" },
        { true, "all(itertools.repeat(True, n))" },
        { false, "sum(Values())" },
        { true, "sum(Values())" },
    };

    [Theory]
    [MemberData(nameof(Consumers))]
    public async Task CancellationDuringPrimitiveAggregationStopsTheActualRun(bool asynchronous, string expression)
    {
        var script = new LythonEngine().Compile("n = 100000000\nimport itertools\nclass Values:\n"
            + "    def __iter__(self):\n        return iter(range(n))\n"
            + "print('started')\nprint(" + expression + ")\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Message)));
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MockLythonHost { OnStandardOutputWrite = () => entered.TrySetResult(true) };
        var options = new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
        };
        var pending = asynchronous
            ? Task.Run(() => script.RunAsync(host, options, cancellation.Token))
            : Task.Run(() => script.Run(host, options));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(100);
            Assert.False(pending.IsCompleted, pending.IsCompleted
                ? "The aggregate ended before cancellation: " + (await pending).Failure?.Message
                : "The aggregate must still be executing when canceled.");
            cancellation.Cancel();
            var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
            Assert.Equal("started\n", result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel();
            // A timeout must not masquerade as cancellation while leaving a
            // background interpreter running. Always join the actual run.
            await pending;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryAggregatesAndPrimitiveUserIteratorsKeepTheirResults(bool asynchronous)
    {
        const string source = """
import itertools
class Values:
    def __iter__(self):
        return iter(range(100))
print(sum(range(100)), sum(Values()), sum([1, 2, 3], 4))
print(min(range(100)), max(range(100)))
print(any(itertools.repeat(False, 100)), all(itertools.repeat(True, 100)))
print(sum([]), any([]), all([]), min([], default=42), max([], default=-1))
print(list(zip(range(3), range(4))), list(enumerate(range(3), 5)))
""";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var result = asynchronous ? await script.RunAsync(new MockLythonHost())
                : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("4950 4950 10\n0 99\nFalse True\n0 False True 42 -1\n"
                + "[(0, 0), (1, 1), (2, 2)] [(5, 0), (6, 1), (7, 2)]\n", result.StandardOutput);
        }
    }
}
