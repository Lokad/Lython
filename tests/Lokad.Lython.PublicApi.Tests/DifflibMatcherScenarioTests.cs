using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R03: exposed matcher state keeps Python identity with invalidation on
/// sequence change, and matching scratch stays governed on repeated and
/// unique inputs without relying on the popular-character fast path.
/// </summary>
public sealed class DifflibMatcherScenarioTests
{
    [Fact]
    public async Task ExposedViewsKeepIdentityUntilSeq2()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            matcher = difflib.SequenceMatcher(None, "qabxcd", "abycdf", autojunk=False)
            results = []
            results.append(matcher.b2j is matcher.b2j)
            results.append(matcher.bjunk is matcher.bjunk)
            results.append(matcher.bpopular is matcher.bpopular)
            before = matcher.b2j
            matcher.set_seq1("qabxcd")
            results.append(matcher.b2j is before)
            matcher.set_seq2("abycdf")
            results.append(matcher.b2j is before)
            results.append(str(matcher.get_matching_blocks()))
            return results
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true,
            true,
            true,
            true,
            false,
            "[Match(a=1, b=0, size=2), Match(a=4, b=3, size=2), Match(a=6, b=6, size=0)]",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task AutojunkFalseMatchesCPython()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            parts = []
            repeated = difflib.SequenceMatcher(None, "ab" * 300, "ab" * 300, autojunk=False)
            parts.append(str(repeated.get_matching_blocks()))
            parts.append(str(repeated.ratio()))
            unique = difflib.SequenceMatcher(None, list(range(500)), list(range(250, 750)), autojunk=False)
            parts.append(str(unique.get_matching_blocks()))
            parts.append(str(unique.ratio()))
            small = difflib.SequenceMatcher(None, "qabxcd", "abycdf", autojunk=False)
            parts.append(str(small.get_matching_blocks()))
            parts.append(str(small.get_opcodes()))
            return "|".join(parts)
            """);
        Assert.True(script.IsValid);
        const string expected = "[Match(a=0, b=0, size=600), Match(a=600, b=600, size=0)]|1.0|[Match(a=250, b=0, size=250), Match(a=500, b=500, size=0)]|0.5|[Match(a=1, b=0, size=2), Match(a=4, b=3, size=2), Match(a=6, b=6, size=0)]|[('delete', 0, 1, 0, 0), ('equal', 1, 3, 0, 2), ('replace', 3, 4, 2, 3), ('equal', 4, 6, 3, 5), ('insert', 6, 6, 5, 6)]";
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MatcherViewsStayBoundedUnderSmallBudgets()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            matcher = difflib.SequenceMatcher(None, list(range(2000)), list(range(1000, 3000)), autojunk=False)
            views = [matcher.b2j, matcher.bjunk, matcher.bpopular]
            return [len(views[0]), str(matcher.ratio())]
            """);
        Assert.True(script.IsValid);
        var tiny = new LythonRunOptions { MaxExecutionMemoryBytes = 32768 };
        var sync = script.Run(new MockLythonHost(), tiny);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost(), tiny);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task UniqueInputsStayGovernedWithoutPopularFastPath()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            matcher = difflib.SequenceMatcher(None, list(range(2000)), list(range(1000, 3000)), autojunk=False)
            return [str(matcher.get_matching_blocks()), str(matcher.ratio())]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "[Match(a=1000, b=0, size=1000), Match(a=2000, b=2000, size=0)]", "0.5" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}

