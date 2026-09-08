using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening5RegressionTests
{
    private const string RepeatedBomb = """
import difflib
matcher = difflib.SequenceMatcher(None, "x" * 500, "x" * 500, autojunk=False)
return matcher.ratio()
""";

    [Fact]
    public void RepeatedBombRespectsStepBudget()
    {
        var result = new LythonEngine().Run(
            RepeatedBomb,
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionSteps = 50
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedBombRespectsStepBudgetAsync()
    {
        var result = await new LythonEngine().RunAsync(
            RepeatedBomb,
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxExecutionSteps = 50
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum execution step count exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MatchingHonorsMidRunCancellation()
    {
        // 16M inner match iterations keep the run alive far past the 50ms
        // cancel point; the per-64 work checks observe the token deterministically.
        using var cts = new CancellationTokenSource();
        var runTask = Task.Run(() => new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, "x" * 4000, "x" * 4000, autojunk=False)
return matcher.ratio()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            }));

        await Task.Delay(50);
        cts.Cancel();

        var result = await runTask;
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UniqueSequenceGrowthRespectsCollectionLimit()
    {
        var result = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, list(range(3000)), list(range(3000)))
return matcher.ratio()
""",
            new MockLythonHost(),
            new LythonRunOptions
            {
                MaxCollectionSize = 100
            });

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UniqueSequenceGrowthRespectsMemoryBudget()
    {
        var options = new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 327680
        };
        var baseline = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, list(range(3000)), list(range(3000)))
return matcher.ratio()
""",
            new MockLythonHost());

        Assert.True(baseline.Success, baseline.Failure?.Message);
        Assert.Equal(1.0, baseline.ReturnValue);

        var result = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, list(range(3000)), list(range(3000)))
return matcher.ratio()
""",
            new MockLythonHost(),
            options);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.Contains("execution memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DissimilarSequencesCompareCorrectly()
    {
        var result = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, "a" * 2000, "b" * 2000)
return [matcher.ratio(), matcher.quick_ratio(), matcher.find_longest_match().size]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void DirectMatchQueriesRespectStepBudget()
    {
        foreach (var query in new[] { "matcher.find_longest_match()", "matcher.get_opcodes()", "matcher.get_matching_blocks()" })
        {
            var result = new LythonEngine().Run(
                "import difflib\nmatcher = difflib.SequenceMatcher(None, \"x\" * 500, \"x\" * 500, autojunk=False)\nreturn " + query + "\n",
                new MockLythonHost(),
                new LythonRunOptions
                {
                    MaxExecutionSteps = 50
                });

            Assert.False(result.Success);
            Assert.NotNull(result.Failure);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("maximum execution step count exceeded", result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SetSeqInvalidationUpdatesRatios()
    {
        var result = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, "abcd", "abXcd")
before = matcher.ratio()
matcher.set_seq2("abcd")
exact = matcher.ratio()
matcher.set_seq1("zzzz")
after = matcher.ratio()
matcher.set_seqs("same", "same")
renewed = matcher.ratio()
return [before < 1.0, exact, after, renewed]
""",
            new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(true, values[0]);
        Assert.Equal(1.0, values[1]);
        Assert.Equal(0.0, values[2]);
        Assert.Equal(1.0, values[3]);
    }
}


