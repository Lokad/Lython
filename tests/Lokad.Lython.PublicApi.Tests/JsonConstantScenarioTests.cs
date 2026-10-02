using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Nested NaN/Infinity/-Infinity literals parse like CPython (case-sensitive
// prefix match) with hooks applied at every level, while malformed literals
// and truncated documents stay catchable JSONDecodeErrors.
public sealed class JsonConstantScenarioTests
{
    [Fact]
    public async Task NestedConstantsParseLikeCpython()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            first = json.loads('{"a": NaN, "b": [Infinity, -Infinity]}')
            second = json.loads('[NaN]')
            return [first["a"] != first["a"], first["b"][0] > 0, first["b"][1] < 0, second[0] != second[0]]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { true, true, true, true }, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, true, true, true }, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ParseConstantHookFiresForNestedLiterals()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            return json.loads('{"a": NaN}', parse_constant=lambda text: "C" + text)["a"]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("CNaN", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("CNaN", asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("FALSE")]
    [InlineData("Null")]
    [InlineData("Nana")]
    [InlineData("inf")]
    [InlineData("nan")]
    public async Task InvalidConstantLiteralsFailWithJsonDecodeError(string token)
    {
        var script = new LythonEngine().Compile("import json\njson.loads('" + token + "')");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("JSONDecodeError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("JSONDecodeError", asyncResult.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData("{\"a\": True}")]
    [InlineData("[Nana]")]
    public async Task InvalidNestedConstantLiteralsFailWithJsonDecodeError(string token)
    {
        var script = new LythonEngine().Compile("import json\njson.loads('" + token + "')");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("JSONDecodeError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("JSONDecodeError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task TruncatedDocumentsReportCpythonCoordinates()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            texts = ["{", "[1,", '{"a":1', "[1", '{"a"']
            values = []
            for text in texts:
                try:
                    json.loads(text)
                    values.append("ok")
                except json.JSONDecodeError as exc:
                    values.append(str(exc.pos) + ":" + str(exc.lineno) + ":" + str(exc.colno))
            return "|".join(values)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("1:1:2|3:1:4|6:1:7|2:1:3|4:1:5", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("1:1:2|3:1:4|6:1:7|2:1:3|4:1:5", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AdjacentScalarsSplitAtCpythonPrefixEnd()
    {
        // Literals match case-sensitive prefixes and numbers match NUMBER_RE,
        // so the remainder surfaces as trailing data at the CPython offset.
        var script = new LythonEngine().Compile(
            """
            import json
            texts = ["truefalse", "0x", "1e", "nullx", "01", "[1] [2]"]
            values = []
            for text in texts:
                try:
                    json.loads(text)
                    values.append("ok")
                except json.JSONDecodeError as exc:
                    values.append(str(exc.pos) + ":" + str(exc.lineno) + ":" + str(exc.colno))
            return "|".join(values)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("4:1:5|1:1:2|1:1:2|4:1:5|1:1:2|4:1:5", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("4:1:5|1:1:2|1:1:2|4:1:5|1:1:2|4:1:5", asyncResult.ReturnValue);
    }
}