using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonStringLexingTests
{
    // Independent CPython 3.13.2 error messages and original scalar coordinates.
    [Theory]
    [InlineData("\"bad\\q\"", true, "Invalid \\escape", 4, 1, 5)]
    [InlineData("\"é😀\\q\"", true, "Invalid \\escape", 3, 1, 4)]
    [InlineData("\"\\u\"", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"\\u12g4\"", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"\\u123\"", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"é\\u12z4\"", true, "Invalid \\uXXXX escape", 3, 1, 4)]
    [InlineData("\"unterminated", true, "Unterminated string starting at", 0, 1, 1)]
    [InlineData("\"end\\", true, "Unterminated string starting at", 0, 1, 1)]
    [InlineData("\"a\u0000b\"", true, "Invalid control character at", 2, 1, 3)]
    [InlineData("\"\n\\q\"", false, "Invalid \\escape", 2, 2, 1)]
    [InlineData("\"\\ud800\\u12g4\"", true, "Invalid \\uXXXX escape", 8, 1, 9)]
    [InlineData("\"\\ud800", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"\\u0061", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"\\u0000", true, "Invalid \\uXXXX escape", 2, 1, 3)]
    [InlineData("\"\\ud800\\udc00", true, "Invalid \\uXXXX escape", 8, 1, 9)]
    public async Task StringErrorsUsePythonMessagesAndOriginalCoordinates(string document, bool strict,
        string message, int position, int line, int column)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import json
            try:
                json.loads(source, strict=strict)
            except json.JSONDecodeError as error:
                print(error.msg)
                print(error.pos, error.lineno, error.colno, error.doc is source, error.args[0] == str(error))
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
            { ["source"] = document, ["strict"] = strict } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"{message}\n{position} {line} {column} True True\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("\"\\\"\\\\\\/\\b\\f\\n\\r\\t\"", "\"\\/\b\f\n\r\t")]
    [InlineData("\"\\u0000\"", "\u0000")]
    [InlineData("\"\\u007f\\u0080\\u07ff\\u0800\\uffff\"", "߿ࠀ￿")]
    [InlineData("\"\\ud800\\udc00\"", "𐀀")]
    [InlineData("\"\\udbff\\udfff\"", "􏿿")]
    [InlineData("\"café😀\\n\\u03bb\"", "café😀\nλ")]
    public async Task EscapesDecodeIntoIndependentRetainedStrings(string document, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("import json\nreturn json.loads(source)", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        using (var first = script.Run(new MockLythonHost(), options))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = await script.RunAsync(new MockLythonHost(), options))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, first.ReturnValue);
                Assert.Equal(expected, second.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            Assert.Equal(expected, first.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("\"\\ud800\"")]
    [InlineData("\"\\udc00\"")]
    [InlineData("\"\\ud800x\"")]
    [InlineData("\"\\ud800\\u0041\"")]
    public async Task UnpairedSurrogatesFailExplicitlyWithoutLeakingBackendErrors(string document)
    {
        using var script = new LythonEngine().Compile("import json\nreturn json.loads(source)");
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = document } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Equal("ValueError", result.Failure?.ExceptionType);
            Assert.Equal("Lython does not support unpaired Unicode surrogates in JSON strings.", result.Failure?.Message);
        }
    }
}
