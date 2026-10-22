using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class EnvironmentProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ViewsFollowMutationsAndExplicitListsAreSnapshots(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import os
            e=os.environ
            e.clear()
            e['X']='old'
            keys=e.keys();values=e.values();items=e.items()
            snapshot=list(items)
            e['X']='new'
            print(list(values),list(items),snapshot)
            e['Y']='added'
            print(list(keys),len(keys),sorted(keys|{'Z'}))
            iterator=iter(keys)
            print(next(iterator))
            del e['Y']
            try:next(iterator)
            except RuntimeError:print('changed')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("['new'] [('X', 'new')] [('X', 'old')]\n['X', 'Y'] 2 ['X', 'Y', 'Z']\nX\nchanged\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidWritesDoNotPublishAndAllMutationRoutesAreContained(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import os
            e=os.environ
            e.clear();e['X']='old'
            for key,value in [('X=Y','bad'),('X','bad\0value'),('bad\0key','bad')]:
                try:e[key]=value
                except ValueError:print('invalid')
            try:e.update({'X':'bad\0value'})
            except ValueError:print('invalid update')
            try:os.putenv('X=Y','bad')
            except ValueError:print('invalid putenv')
            print(e['X'],list(e.keys()),os.getenv('X=Y','missing'))
            os.putenv('X','new')
            print(os.getenv('X'))
            os.unsetenv('X');os.unsetenv('X')
            print(os.getenv('X','missing'))
            """);
        var original = new Dictionary<string, string> { ["HOST"] = "preserved" };
        var options = new LythonRunOptions { Environment = original };
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, Describe(result));
        Assert.Equal("invalid\ninvalid\ninvalid\ninvalid update\ninvalid putenv\nold ['X'] missing\nnew\nmissing\n", result.StandardOutput);
        Assert.Equal("preserved", Assert.Single(original).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeMutationRefusesBeforeLaterEffectsAndReturnsAllowance(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import os\nos.environ['X']=arg\nprint('stored')");
        var budget = new LythonMemoryBudget(100000);
        var original = new Dictionary<string, string> { ["X"] = "old" };
        var options = new LythonRunOptions { MemoryBudget = budget, Environment = original,
            Globals = new Dictionary<string, object?> { ["arg"] = new string('x', 65536) } };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("old", original["X"]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ViewsDoNotDecodeUnconsumedHostValues(bool asynchronous)
    {
        using var view = new LythonEngine().Compile("import os\ne=os.environ\nk=e.keys();v=e.values();i=e.items()\nreturn len(k)+len(v)+len(i)");
        using var snapshot = new LythonEngine().Compile("import os\nreturn list(os.environ.values())");
        var budget = new LythonMemoryBudget(16384);
        var options = new LythonRunOptions { MemoryBudget = budget, Environment = new Dictionary<string, string> { ["X"] = new string('x', 65536) } };
        using (var result = asynchronous ? await view.RunAsync(new MockLythonHost(), options) : view.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal(new System.Numerics.BigInteger(3), result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var result = asynchronous ? await snapshot.RunAsync(new MockLythonHost(), options) : snapshot.Run(new MockLythonHost(), options))
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join("; ", result.Diagnostics.Select(x => x.Message));
}
