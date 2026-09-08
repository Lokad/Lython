using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R41: JSON output growth is charged while building, default-callback loops meet
/// an independent recursion bound, and error boundaries use Python type names.
/// </summary>
public sealed class JsonStrictnessScenarioTests
{
    [Fact]
    public async Task JsonDumpsSetUsesPythonTypeName()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return json.dumps({1, 2})
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);
        Assert.Contains("Unsupported json.dumps value type: set", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("Unsupported json.dumps value type: set", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonDumpsDefaultFreshObjectsHitRecursionBound()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            def fresh(value):
                return {2}
            return json.dumps({1}, default=fresh)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RecursionError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("RecursionError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum recursion depth exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonDumpsDefaultCallbackContracts()
    {
        var same = new LythonEngine().Compile(
            """
            import json
            def same(value):
                return value
            return json.dumps({1}, default=same)
            """);
        Assert.True(same.IsValid);
        var sameResult = same.Run(new MockLythonHost());
        Assert.False(sameResult.Success);
        Assert.Equal("ValueError", sameResult.Failure?.ExceptionType);

        var fortytwo = new LythonEngine().Compile(
            """
            import json
            def fortytwo(value):
                return 42
            return json.dumps({1}, default=fortytwo)
            """);
        Assert.True(fortytwo.IsValid);
        var sync = fortytwo.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("42", sync.ReturnValue);

        var asyncResult = await fortytwo.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("42", asyncResult.ReturnValue);
    }
    [Fact]
    public async Task JsonDumpsLargeScalarMeetsMemoryBudget()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return json.dumps({"k": "x" * 200000})
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonLoadsLargeDocumentMeetsMemoryBudget()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return json.loads(json.dumps(list(range(50000))))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task JsonDumpsStreamsUnsortedEntries()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            ordered = {"b": 1, "a": 2}
            skipped = json.dumps({1: "a", (2, 3): "b"}, skipkeys=True)
            return [json.dumps(ordered), json.dumps(ordered, sort_keys=True), skipped]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(JsonCanonical(0), values[0]);
        Assert.Equal(JsonCanonical(1), values[1]);
        Assert.Equal(JsonCanonical(2), values[2]);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(JsonCanonical(0), asyncValues[0]);
        Assert.Equal(JsonCanonical(1), asyncValues[1]);
        Assert.Equal(JsonCanonical(2), asyncValues[2]);
    }

    private static string JsonCanonical(int index)
    {
        return index switch
        {
            0 => "{\"b\": 1, \"a\": 2}",
            1 => "{\"a\": 2, \"b\": 1}",
            _ => "{\"1\": \"a\"}",
        };
    }
}
