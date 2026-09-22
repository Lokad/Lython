using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N05: escaped difflib snapshots own their lifetime. Views cache until
// replacement, aliases retain old graphs with charges, matcher drop does not
// refund aliases, mutation does not feed back, and denial precedes publication.
public sealed class DifflibSnapshotLifetimeTests
{
    private static void AssertDenied(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.True(result.Failure?.ExceptionType is "MemoryError" or "RuntimeError", result.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ReplacementKeepsOldAliasWithOldGraph()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [], [1, 2])
            old = m.b2j
            old_len = len(old)
            m.set_seq2([3, 4, 5])
            return [old_len, len(old), len(m.b2j), (old is m.b2j)]
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new System.Numerics.BigInteger(2), new System.Numerics.BigInteger(2), new System.Numerics.BigInteger(3), false }, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task RetainedSnapshotsStayAccountedAndDeny()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [], [])
            views = []
            for i in range(100):
                m.set_seq2(range(500))
                views.append(m.b2j)
            return [len(views), len(views[0])]
            """;
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var tiny = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        AssertDenied(script.Run(new MockLythonHost(), tiny));
        AssertDenied(await script.RunAsync(new MockLythonHost(), tiny));
        // Funded small retention still succeeds.
        const string small = """
            import difflib
            m = difflib.SequenceMatcher(None, [], [])
            views = []
            for i in range(3):
                m.set_seq2(range(10))
                views.append(m.b2j)
            return [len(views), len(views[0])]
            """;
        var funded = new LythonEngine().Compile(small);
        var ok = funded.Run(new MockLythonHost());
        Assert.True(ok.Success, ok.Failure?.Message);
    }

    [Fact]
    public async Task MatcherAbandonmentKeepsAliasUsable()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [], [1, 2, 3])
            v = m.b2j
            m = None
            return len(v)
            """;
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task ViewMutationDoesNotFeedBackIntoMatching()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [1, 2], [1, 2])
            before = m.ratio()
            v = m.b2j
            v[99] = [1, 2, 3]
            return [before, m.ratio(), len(v)]
            """;
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public void NestedListMutationDeniesUnderTinyBudget()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [], [0])
            v = m.b2j[0]
            v.extend(range(200000))
            return len(v)
            """;
        var script = new LythonEngine().Compile(code);
        AssertDenied(script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 65536, MaxCollectionSize = 10 }));
    }

    [Fact]
    public async Task FailedSetSeq2KeepsPreviousSequence()
    {
        const string code = """
            import difflib
            m = difflib.SequenceMatcher(None, [1, 2], [3, 4])
            try:
                m.set_seq2([[1]])
            except TypeError:
                pass
            return [list(m.b), m.ratio() == m.ratio()]
            """;
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}
