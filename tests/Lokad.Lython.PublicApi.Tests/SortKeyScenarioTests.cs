using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R13: invalid sort keys fail only when actually called. Empty input with a
/// bad key succeeds; iterable effects precede the key error.
/// </summary>
public sealed class SortKeyScenarioTests
{
    [Fact]
    public async Task SortedEmptyWithBadKeyReturnsEmpty()
    {
        var script = new LythonEngine().Compile("return sorted([], key=1)");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task ListSortEmptyWithBadKeySucceeds()
    {
        var script = new LythonEngine().Compile("x = []\nx.sort(key=1)\nreturn x");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Empty(Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task SortedNonEmptyWithBadKeyRaisesTypeError()
    {
        const string source = "try:\n    sorted([1], key=1)\n    return \"no-error\"\nexcept TypeError:\n    return \"TypeError\"\n";
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("TypeError", sync.ReturnValue);
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("TypeError", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ListSortNonEmptyWithBadKeyRaisesTypeError()
    {
        const string source = "try:\n    x = [1]\n    x.sort(key=1)\n    return \"no-error\"\nexcept TypeError:\n    return \"TypeError\"\n";
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("TypeError", sync.ReturnValue);
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("TypeError", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SortedIterableEffectsPrecedeKeyError()
    {
        const string source = "calls = []\nclass C:\n    def __iter__(self):\n        calls.append(\"iter\")\n        return iter([2, 1])\ntry:\n    sorted(C(), key=1)\nexcept TypeError:\n    calls.append(\"caught\")\nreturn calls\n";
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { "iter", "caught" }, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { "iter", "caught" }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task SortedKeyCalledOncePerElementWithReverse()
    {
        var script = new LythonEngine().Compile("calls = []\ndef key(v):\n    calls.append(v)\n    return v\nresult = sorted([3, 1, 2], key=key, reverse=True)\nreturn [result, len(calls)]");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        var result = Assert.IsType<List<object?>>(values[0]);
        Assert.Equal(new List<object?> { new BigInteger(3), new BigInteger(2), new BigInteger(1) }, result);
        Assert.Equal(new BigInteger(3), values[1]);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(new List<object?> { new BigInteger(3), new BigInteger(2), new BigInteger(1) }, Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Equal(new BigInteger(3), asyncValues[1]);
    }

    [Fact]
    public async Task SortedIterationExceptionPrecedesKeyValidation()
    {
        const string source = "class C:\n    def __iter__(self):\n        raise ValueError(\"boom\")\ntry:\n    sorted(C(), key=1)\nexcept ValueError:\n    return \"ValueError\"\nreturn \"no-error\"\n";
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("ValueError", sync.ReturnValue);
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("ValueError", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MinMaxEmptyWithBadKeySkipsKeyError()
    {
        var script = new LythonEngine().Compile("return [min([], key=1, default=99), max([], key=1, default=7)]");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(99), new BigInteger(7) }, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(99), new BigInteger(7) }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task MinMaxEmptyWithBadKeyRaisesValueError()
    {
        const string source = "try:\n    min([], key=1)\nexcept ValueError:\n    return \"ValueError\"\nreturn \"no-error\"\n";
        var sync = new LythonEngine().Run(source, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("ValueError", sync.ReturnValue);
        var asyncResult = await new LythonEngine().RunAsync(source, new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("ValueError", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task NoneKeyStaysValid()
    {
        var script = new LythonEngine().Compile("return [sorted([2, 1], key=None), sorted([], key=None)]");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(2) }, Assert.IsType<List<object?>>(values[0]));
        Assert.Empty(Assert.IsType<List<object?>>(values[1]));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}
