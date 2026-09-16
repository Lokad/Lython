using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: collection wrappers own shell plus backing with reclamation.
public sealed class WrapperLifetimeScenarioTests
{    private const long ThreeMib = 3145728;

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
    public async Task CounterDiscardCompletes()
        => await AssertCompletes(
            "from collections import Counter\nfor i in range(100000):\n    x = Counter(a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task DefaultdictKwargsDiscardCompletes()
        => await AssertCompletes(
            "from collections import defaultdict\nfor i in range(100000):\n    x = defaultdict(int, a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task DictKwargsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = dict(a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task DefaultdictDiscardCompletes()
        => await AssertCompletes(
            "from collections import defaultdict\nfor i in range(100000):\n    x = defaultdict(list)\nreturn 0\n", "0");
    [Fact]
    public async Task FromKeysDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = dict.fromkeys(['a', 'b'], 0)\nreturn 0\n", "0");

    [Fact]
    public async Task FromKeysNoDefaultDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = {}.fromkeys(['a'])\nreturn 0\n", "0");

    [Fact]
    public async Task FromKeysBehaves()
        => await AssertCompletes(
            "return str(dict.fromkeys(['a', 'b'], 0))\n", "{'a': 0, 'b': 0}");

    [Fact]
    public async Task RetainedFromKeysDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(dict.fromkeys(['a', 'b'], 0))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
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
            "from collections import defaultdict\nfor i in range(50000):\n    d = defaultdict(int)\n    d.update(a=1)\nreturn 0\n", "0");

    [Fact]
    public async Task DefaultdictUpdateKwargsBehaves()
        => await AssertCompletes(
            "from collections import defaultdict\nd = defaultdict(int)\nd.update({'a': 1}, b=2)\nd.update()\nreturn str(dict(d))\n", "{'a': 1, 'b': 2}");

    [Fact]
    public async Task RetainedDefaultdictUpdateDenied()
    {
        var script = new LythonEngine().Compile(
            "from collections import defaultdict\nobjs = []\ni = 0\nwhile i < 20000:\n    d = defaultdict(int)\n    d.update(a=1)\n    objs.append(d)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
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
            "for i in range(100000):\n    d = dict.fromkeys(['a', 'b'], 0)\n    e = d\n    x = (d, e)\nreturn 0\n", "0");

    [Fact]
    public async Task FromKeysDeniedRecoversWhenFunded()
    {
        var script = new LythonEngine().Compile(
            "return str(dict.fromkeys(['a', 'b'], 0))\n");
        Assert.True(script.IsValid);
        var denied = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 100 });
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
        var script = new LythonEngine().Compile(
            "from collections import defaultdict\nd = defaultdict(int)\nd.update(a=1)\nreturn str(dict(d))\n");
        Assert.True(script.IsValid);
        var denied = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 100 });
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal("{'a': 1}", funded.ReturnValue?.ToString());

        var fundedAsync = await script.RunAsync(new MockLythonHost());
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal("{'a': 1}", fundedAsync.ReturnValue?.ToString());
    }}
