using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextFileOwnerLifetimeTests : LifetimeScenario
{
    [Theory]
    [InlineData("r", false)]
    [InlineData("w", false)]
    [InlineData("a", false)]
    [InlineData("r", true)]
    [InlineData("w", true)]
    [InlineData("a", true)]
    public async Task ClosedHandleBatchesFinishWhileRetainedHandlesReject(string mode, bool retained)
    {
        var source = "kept = []\nfor i in range(5000):\n with open('/file', '" + mode + "') as f:\n  pass\n" +
            (retained ? " kept.append(f)\n" : "") + " f = None\nreturn i\n";
        if (!retained)
        {
            await AssertBatchCompletes(source.Replace("range(5000)", "range(16)"), new BigInteger(15), 262144,
                hostFactory: () => { var h = new MockLythonHost(); h.SeedFile("/file", ""); return h; });
            return;
        }
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        using var result = script.Run(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) });
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Fact]
    public void CapturedReadMethodsKeepClosedHandlesLive()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("kept = []\nfor i in range(5000):\n with open('/file') as f:\n  pass\n kept.append(f.read)\n f = None\nreturn i\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var host = new MockLythonHost(); host.SeedFile("/file", "");
        var result = script.Run(host, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) });
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }

    [Fact]
    public async Task EscapedReadValueSurvivesClosedHandlesWithinEachBatch()
    {
        await AssertBatchCompletes("with open('/line') as f:\n first = f.readline()\nf = None\nfor i in range(16):\n with open('/file', 'w') as f:\n  pass\n f = None\nreturn i == 15 and first == 'café 😀' * 2048 + '\\n'\n", true, 262144,
            hostFactory: () => { var h = new MockLythonHost(); h.SeedFile("/line", string.Concat(Enumerable.Repeat("café 😀", 2048)) + "\n"); return h; });
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task SingleAsciiValuesShareIdentityAcrossTextReads(string newline)
    {
        var budget = new LythonMemoryBudget(256 * 1024);
        using var script = new LythonEngine().Compile("""
            with open('/line', encoding='utf8') as stream:
                first = next(stream)
                second = stream.readline()
                letter = stream.read(1)
                third = stream.read(1)
                repeated = stream.read(1)
                fourth = stream.read(1)
            with open('/last', encoding='utf8') as stream:
                last = stream.readline()
            print(first == '\n', letter == 'A')
            print(first is second, first is third, first is fourth,
                  letter is repeated, letter is last)
            """, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var input = newline + newline + "A" + newline + "A" + newline;
        var sync = new MockLythonHost();
        sync.SeedFile("/line", input);
        sync.SeedFile("/last", "A");
        using (var result = script.Run(sync))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True\nTrue True True True True\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);

        var asyncHost = new DelayedLythonHost();
        asyncHost.SeedFile("/line", input);
        asyncHost.SeedFile("/last", "A");
        using (var result = await script.RunAsync(asyncHost))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True\nTrue True True True True\n", result.StandardOutput);
        }
        Assert.True(asyncHost.CompletedAsynchronously > 0);
        Assert.Equal(2, asyncHost.ClosedFileCount);
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
