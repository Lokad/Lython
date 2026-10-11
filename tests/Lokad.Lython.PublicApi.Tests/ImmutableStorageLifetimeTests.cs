using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImmutableStorageLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedLargePayloadIsNotChargedForEveryRecordSlot(bool named)
    {
        var declaration = named ? "from collections import namedtuple\nRow = namedtuple('Row', 'a b c')\n" : "";
        var value = named ? "Row(large, large, large)" : "(large, large, large)";
        var script = new LythonEngine().Compile(declaration +
            $"large = 1 << 8192\nrecords = [{value} for _ in range(1000)]\nreturn [len(records), records[-1][0] is records[-1][1]]");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1024 * 1024 };
        foreach (var asyncRun in new[] { false, true })
        {
            var result = asyncRun ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { new BigInteger(1000), true }, result.ReturnValue);
            Assert.True(result.PeakExecutionMemoryBytes <= options.MaxExecutionMemoryBytes!.Value.Bytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedRowsStreamBeyondCumulativeBudgetWhileRetainedRowsDeny(bool named)
    {
        var declaration = named ? "from collections import namedtuple\nRow = namedtuple('Row', 'a b c')\n" : "";
        var value = named ? "Row(i, i, i + 1)" : "(i, i, i + 1)";
        var streaming = new LythonEngine().Compile(declaration +
            $"total = 0\nfor i in range(5000):\n row = {value}\n assert row[0] is row[1]\n total += row[0]\nreturn total\n");
        var retaining = new LythonEngine().Compile(declaration +
            $"rows = []\nfor i in range(5000):\n rows.append({value})\nreturn len(rows)\n");
        Assert.True(streaming.IsValid);
        Assert.True(retaining.IsValid);
        const long budget = 1024 * 1024;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        foreach (var asyncRun in new[] { false, true })
        {
            var streamed = asyncRun ? await streaming.RunAsync(new MockLythonHost(), options) : streaming.Run(new MockLythonHost(), options);
            Assert.True(streamed.Success, streamed.Failure?.Message);
            Assert.Equal(new BigInteger(12497500), streamed.ReturnValue);
            Assert.True(streamed.PeakExecutionMemoryBytes <= budget);
            var retained = asyncRun ? await retaining.RunAsync(new MockLythonHost(), options) : retaining.Run(new MockLythonHost(), options);
            Assert.False(retained.Success);
            Assert.Equal("MemoryError", retained.Failure?.ExceptionType);
            Assert.True(retained.PeakExecutionMemoryBytes <= budget);
        }
    }

    [Fact]
    public async Task AdequatelyFundedAliasesKeepTheirIdentity()
    {
        var script = new LythonEngine().Compile("x = 12345\nt = (x,) * 100000\nreturn [len(t), t[0] is t[-1]]");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 * 1024 };
        var expected = new List<object?> { new BigInteger(100000), true };
        var sync = script.Run(new MockLythonHost(), options);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
            Assert.True(result.PeakExecutionMemoryBytes <= options.MaxExecutionMemoryBytes!.Value.Bytes);
        }
    }
}
