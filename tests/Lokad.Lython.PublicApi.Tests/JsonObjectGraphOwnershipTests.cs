using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R10: every fresh JSON object-graph node (containers, decoded strings, pair
// tuples, hook argument strings) owns a refundable pool snapshot so dropped
// parses reclaim on sweep. Hook results adopt via plain TrackCallResult,
// never refunding an arbitrary callback value that may alias live state.
public sealed class JsonObjectGraphOwnershipTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscardedNestedLoadsSucceed()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('{"a":["hello","world"]}')
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedNestedLoadsSucceedAsync()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('{"a":["hello","world"]}')
            """;
        var result = await new LythonEngine().RunAsync(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void RetainedNestedLoadsDeny()
    {
        const string code = """
            import json
            xs = []
            for i in range(20000):
                xs.append(json.loads('{"a":["hello","world"]}'))
            return len(xs)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public async Task RetainedNestedLoadsDenyAsync()
    {
        const string code = """
            import json
            xs = []
            for i in range(20000):
                xs.append(json.loads('{"a":["hello","world"]}'))
            return len(xs)
            """;
        var result = await new LythonEngine().RunAsync(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DiscardedScalarLoadsSucceed()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('"hello"')
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedScalarLoadsSucceedAsync()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('"hello"')
            """;
        var result = await new LythonEngine().RunAsync(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void RetainedScalarLoadsDeny()
    {
        const string code = """
            import json
            xs = []
            for i in range(20000):
                xs.append(json.loads('"hi"'))
            return len(xs)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void RepeatedKeysKeepLast()
    {
        const string code = """
            import json
            x = json.loads('{"a":1,"a":2}')
            return [x["a"], len(x)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(1) }, result.ReturnValue);
    }

    [Fact]
    public void ObjectHookAliasPreserved()
    {
        const string code = """
            import json
            y = {"k": 9}
            x = json.loads('{"a":1}', object_hook=lambda d: d)
            z = json.loads('{"a":1}', object_hook=lambda d: y)
            return [x["a"], len(x), z is y, z["k"]]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1), new BigInteger(1), true, new BigInteger(9) },
            result.ReturnValue);
    }

    [Fact]
    public void ObjectHookDropReclaims()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('{"a":1}', object_hook=lambda d: 42)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void ObjectPairsHookTuplesExact()
    {
        const string code = """
            import json
            x = json.loads('{"a":1}', object_pairs_hook=lambda p: p)
            return [x[0][0], x[0][1], len(x)]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { "a", new BigInteger(1), new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void ObjectPairsHookDropReclaims()
    {
        const string code = """
            import json
            for i in range(20000):
                json.loads('{"a":1}', object_pairs_hook=lambda p: 0)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void ParseHooksExact()
    {
        const string code = """
            import json
            a = json.loads('[1,2]', parse_int=lambda s: 99)
            b = json.loads('1.5', parse_float=lambda s: 0.25)
            c = json.loads('NaN', parse_constant=lambda s: 7)
            return [a[0], a[1], b, c]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(99), new BigInteger(99), 0.25, new BigInteger(7) },
            result.ReturnValue);
    }

    [Fact]
    public void FailedParsesThenSuccess()
    {
        const string code = """
            import json
            for i in range(20000):
                try:
                    json.loads('{"a":')
                except Exception:
                    pass
            return json.loads('{"a":1}')["a"]
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(1), result.ReturnValue);
    }
}
