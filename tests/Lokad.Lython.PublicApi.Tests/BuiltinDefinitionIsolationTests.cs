using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinDefinitionIsolationTests
{
    private const string Source = """
import builtins
assert not hasattr(object, 'run_marker'), 'object mutation leaked'
class Marker:
    run_marker = tag
assert Marker.__mro__[1] is object, 'guest base identity'
for name in ['len', 'repr', 'ascii', 'abs', 'pow', 'round', 'divmod', 'bin', 'oct', 'hex', 'chr', 'ord', 'format', 'sorted']:
    function = getattr(builtins, name)
    assert function.__self__ is builtins, name + ' module identity'
    assert function.__name__ == name, name + ' name'
    assert function.__module__ == 'builtins', name + ' module name'
    assert type(function).__name__ == 'builtin_function_or_method', name + ' type'
    assert hash(function) == hash(function)
    assert function.__new__ is type(function).__new__, name + ' inherited new'
    for attribute in ['__bases__', '__mro__', 'append', 'real']:
        assert not hasattr(function, attribute), name + ' ' + attribute
    try:
        setattr(function, 'run_marker', tag)
    except (AttributeError, TypeError):
        pass
    else:
        assert False, name + ' allowed mutation'
assert sorted([3, 1, 2]) == [1, 2, 3], 'ordinary sorted result'
with open('input.txt') as source:
    text = source.read()
values = [repr(text), ascii(text)]
assert len(text) == 2, 'input length'
assert Marker.run_marker == tag, 'guest marker'
with open('output.txt', 'w') as output:
    output.write(tag)
return values
""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SequentialExecutionsKeepPrivateTypesModulesAndRetainedResults(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(Source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var firstHost = Host("aé");
        var secondHost = Host("bà");
        using (var first = asynchronous
            ? await script.RunAsync(firstHost, Options("first")) : script.Run(firstHost, Options("first")))
        {
            AssertResult(first, "'aé'", "'a\\xe9'");
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = asynchronous
                ? await script.RunAsync(secondHost, Options("second")) : script.Run(secondHost, Options("second")))
            {
                AssertResult(second, "'bà'", "'b\\xe0'");
                AssertResult(first, "'aé'", "'a\\xe9'");
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal("first", firstHost.ReadText("/output.txt"));
        Assert.Equal("second", secondHost.ReadText("/output.txt"));
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SuspendedExecutionKeepsItsOwnerWhileAnotherExecutionUsesTheSameDefinitions()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(Source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var pausedHost = new DelayedLythonHost();
        pausedHost.SeedFile("/input.txt", "aé");
        pausedHost.SeedFile("/output.txt", "before");
        var entered = pausedHost.PauseWriteUntilCancellation("/output.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(pausedHost, Options("paused"), cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Execution finished before controlled host suspension.");
            }
            Assert.False(pending.IsCompleted);
            var suspended = budget.ReservedBytes;
            Assert.True(suspended > baseline);
            var otherHost = Host("bà");
            using (var other = await script.RunAsync(otherHost, Options("other")))
            {
                AssertResult(other, "'bà'", "'b\\xe0'");
                Assert.Equal("other", otherHost.ReadText("/output.txt"));
                Assert.True(budget.ReservedBytes > suspended);
            }
            Assert.Equal(suspended, budget.ReservedBytes);
            cancellation.Cancel();
            using var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(cancelled.Success);
            Assert.Equal(LythonFailureReason.Cancelled, cancelled.FailureAttribution?.Reason);
            Assert.Equal("before", pausedHost.ReadText("/output.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShadowedNamesDoNotReplaceTheNextRunsDefinitions(bool asynchronous)
    {
        const string source = """
import builtins
if replace:
    len = lambda value: 91
    repr = lambda value: 'changed'
    ascii = lambda value: 'changed'
    return [len('x'), repr('x'), ascii('x')]
return [builtins.len('xy'), builtins.repr('é'), builtins.ascii('é')]
""";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        foreach (var replace in new[] { true, false })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["replace"] = replace } };
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>().ToArray();
            Assert.Equal(replace ? new object?[] { new System.Numerics.BigInteger(91), "changed", "changed" }
                : [new System.Numerics.BigInteger(2), "'é'", "'\\xe9'"], values);
        }
    }

    private static MockLythonHost Host(string text)
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", text);
        return host;
    }

    private static LythonRunOptions Options(string tag)
        => new() { Globals = new Dictionary<string, object?> { ["tag"] = tag } };

    private static void AssertResult(LythonExecutionResult result, string representation, string ascii)
    {
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new object?[] { representation, ascii },
            Assert.IsAssignableFrom<System.Collections.IEnumerable>(result.ReturnValue).Cast<object?>().ToArray());
    }
}
