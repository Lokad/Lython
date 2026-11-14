using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RangeHelperExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def fold():\n total=0\n for item in range(-5,8,3):total+=item\n return total\nprint(fold())", "5\n" },
        { "def make():return range(True)\nprint(list(make()))", "[0]\n" },
        { "def make():return range(1<<100,(1<<100)+3)\nprint([x-(1<<100) for x in make()])", "[0, 1, 2]\n" },
        { "def make(values):return range(*values)\nprint(list(make((7,-4,-3))))", "[7, 4, 1, -2]\n" },
        { "original=range\ndef make(factory):return factory(3)\nrange=lambda count:('custom',)\nprint(list(make(original)),make(range))", "[0, 1, 2] ('custom',)\n" },
        { "def range(count):return (count+10,)\ndef make():return range(3)\nprint(make())", "(13,)\n" },
        { "events=[]\nclass Bound:\n def __init__(self,value):self.value=value\n def __index__(self):\n  events.append(self.value)\n  return self.value\ndef make():return range(Bound(1),Bound(8),Bound(3))\nprint(list(make()),events)", "[1, 4, 7] [1, 8, 3]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ResultsAndOwnershipMatchBothApis(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
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
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("", "TypeError")]
    [InlineData("1,2,3,4", "TypeError")]
    [InlineData("1,2,0", "ValueError")]
    [InlineData("stop=3", "TypeError")]
    [InlineData("1.5", "TypeError")]
    public Task ErrorsDoNotPublishARange(string arguments, string error)
        => ResultsAndOwnershipMatchBothApis($"def make():return range({arguments})\ntry:\n make()\nexcept {error}:print('caught')", "caught\n");

    private const string ReadingLoop = """
        def read_value():
            with open('input.txt') as source:return int(source.read())
        def fold(count):
            total=0
            for item in range(count):
                total+=item
                if item==3:total+=read_value()
            return total,item
        print(fold(6))
        """;

    [Fact]
    public async Task RangeCursorTransfersOnceThroughRealIo()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(ReadingLoop, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using (var result = await script.RunAsync(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("(22, 5)\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(1, host.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsideRangeLoopRemainsCancellable(bool asynchronous)
    {
        const string source = "def fold(count):\n total=0\n for item in range(count):total+=item\n return total\nreturn fold(1<<100)";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions { CancellationToken = cancellation.Token };
            var pending = asynchronous ? script.RunAsync(new MockLythonHost(), options)
                : Task.Run(() => script.Run(new MockLythonHost(), options));
            try
            {
                await Task.Delay(50);
                Assert.False(pending.IsCompleted);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(LythonFailureReason.Cancelled, joined.FailureAttribution?.Reason);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
