using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// Collection wrappers fund shell and backing within finite host batches.
public class WrapperLifetimeScenarioTests : LifetimeScenario
{    private long StreamingBudget => Budget(3145728);

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task CounterDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\nfor i in range(" + BatchIterations(100000) + "):\n    x = Counter(a=1)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task DefaultdictKwargsDiscardCompletes()
        => await AssertCompletes(
            "from collections import defaultdict\nfor i in range(" + BatchIterations(100000) + "):\n    x = defaultdict(int, a=1)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task DictKwargsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = dict(a=1)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task DefaultdictDiscardCompletes()
        => await AssertCompletes(
            "from collections import defaultdict\nfor i in range(" + BatchIterations(100000) + "):\n    x = defaultdict(list)\nreturn 0\n", "0", Iterations(100000));
    [Fact]
    public async Task FromKeysDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = dict.fromkeys(['a', 'b'], 0)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task FromKeysNoDefaultDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = {}.fromkeys(['a'])\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task FromKeysBehaves()
        => await AssertCompletes(
            "return str(dict.fromkeys(['a', 'b'], 0))\n", "{'a': 0, 'b': 0}");

    [Fact]
    public async Task RetainedFromKeysDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(dict.fromkeys(['a', 'b'], 0))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= 1048576);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= 1048576);
    }

    [Fact]
    public async Task DefaultdictUpdateKwargsDiscardCompletes()
        => await AssertCompletes(
            "from collections import defaultdict\nfor i in range(" + BatchIterations(50000) + "):\n    d = defaultdict(int)\n    d.update(a=1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DefaultdictUpdateKwargsBehaves()
        => await AssertCompletes(
            "from collections import defaultdict\nd = defaultdict(int)\nd.update({'a': 1}, b=2)\nd.update()\nreturn str(dict(d))\n", "{'a': 1, 'b': 2}");

    [Fact]
    public async Task RetainedDefaultdictUpdateDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "from collections import defaultdict\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    d = defaultdict(int)\n    d.update(a=1)\n    objs.append(d)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= 1048576);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= 1048576);
    }

    [Fact]
    public async Task FromKeysAliasedDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    d = dict.fromkeys(['a', 'b'], 0)\n    e = d\n    x = (d, e)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task FromKeysDeniedRecoversWhenFunded()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "return str(dict.fromkeys(['a', 'b'], 0))\n");
        Assert.True(script.IsValid);
        var denied = script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(100) });
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal("{'a': 0, 'b': 0}", funded.ReturnValue?.ToString());

        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal("{'a': 0, 'b': 0}", fundedAsync.ReturnValue?.ToString());
    }

    [Fact]
    public async Task DefaultdictUpdateKwargsDeniedRecoversWhenFunded()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "from collections import defaultdict\nd = defaultdict(int)\nd.update(a=1)\nreturn str(dict(d))\n");
        Assert.True(script.IsValid);
        var denied = script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(100) });
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal("{'a': 1}", funded.ReturnValue?.ToString());

        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal("{'a': 1}", fundedAsync.ReturnValue?.ToString());
    }}
