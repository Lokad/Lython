using System.Threading.Tasks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening5RegressionTests
{
    [Fact]
    public void MatchingHonorsMidRunCancellation()
    {
        // Fully deterministic rendezvous: wall-clock pins flipped per host
        // (a 50ms delay raced a 140ms shape on ubuntu, then an 840ms shape
        // on windows), and output polling starved under full-suite load, so
        // no timing constant is safe. The hook below cancels synchronously
        // on the run thread inside the third stdout write while 27 matching
        // rounds remain; the engine observes it at CheckExecution
        // sites inside the matcher loops. Null hook by default elsewhere.
        using var cts = new CancellationTokenSource();
        var host = new MockLythonHost();
        var writes = 0;
        host.OnStandardOutputWrite = () =>
        {
            if (++writes == 3)
            {
                cts.Cancel();
            }
        };
        var result = new LythonEngine().Run(
            """
import difflib
matcher = difflib.SequenceMatcher(None, "x" * 4000, "x" * 4000, autojunk=False)
i = 0
while i < 30:
    matcher.ratio()
    print("round-" + str(i))
    i = i + 1
return i
""",
            host,
            new LythonRunOptions
            {
                CancellationToken = cts.Token
            });
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
            MemoryBudget = new LythonMemoryBudget(327680)
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
        Assert.Contains("shared memory budget exceeded", result.Failure?.Message, StringComparison.Ordinal);
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


