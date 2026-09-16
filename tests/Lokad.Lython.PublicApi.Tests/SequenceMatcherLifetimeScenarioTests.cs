using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: matcher construction results own lifetime like the other factory results do.
public sealed class SequenceMatcherLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task MatcherCtorDiscardCompletes()
        => await AssertCompletes(
            "import difflib\na = 'x' * 100\nb = 'y' * 100\nfor i in range(20000):\n    m = difflib.SequenceMatcher(None, a, b)\nreturn 0\n", "0");

    [Fact]
    public async Task MatcherOpcodesDiscardCompletes()
        => await AssertCompletes(
            "import difflib\na = 'x' * 100\nb = 'y' * 100\nfor i in range(20000):\n    n = len(list(difflib.SequenceMatcher(None, a, b).get_opcodes()))\nreturn 0\n", "0");

    [Fact]
    public async Task MatcherReseqDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nm = difflib.SequenceMatcher(None, 'x' * 100, 'y' * 100)\nfor i in range(20000):\n    m.set_seq2('z' * 100)\n    m.set_seq2('y' * 100)\nreturn 0\n", "0");

    [Fact]
    public async Task MatcherCachesDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nm = difflib.SequenceMatcher(None, 'x' * 100, 'y' * 100)\nfor i in range(20000):\n    r = m.ratio()\n    o = m.get_opcodes()\n    b = m.b2j\nreturn 0\n", "0");

    [Fact]
    public async Task MatcherBehaves()
        => await AssertCompletes(
            "import difflib\nm = difflib.SequenceMatcher(None, 'abc', 'abd')\nreturn str(m.ratio()) + str(len(m.get_opcodes()))\n", "0.66666666666666662");

    [Fact]
    public async Task RetainedMatcherDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\na = 'x' * 100\nb = 'y' * 100\nobjs = []\ni = 0\nwhile i < 2000:\n    objs.append(difflib.SequenceMatcher(None, a, b))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
