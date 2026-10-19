using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonStringEmissionTests
{
    [Theory]
    [InlineData("dumps", false)]
    [InlineData("dumps", true)]
    [InlineData("encode", false)]
    [InlineData("encode", true)]
    [InlineData("iterator", false)]
    [InlineData("iterator", true)]
    public async Task RetainedEncodedResultsRemainFundedAcrossExecutions(string route, bool asynchronous)
    {
        var expression = route switch
        {
            "encode" => "json.JSONEncoder(ensure_ascii=False).encode(payload)",
            "iterator" => "list(json.JSONEncoder(ensure_ascii=False).iterencode(payload))",
            _ => "json.dumps(payload,ensure_ascii=False)",
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nreturn " + expression, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["payload"] = "café😀" } };
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var expected = "\"café😀\"";
                Assert.Equal(expected, route == "iterator"
                    ? Assert.IsType<string>(Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>()))
                    : Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                    Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal(expected, route == "iterator"
                    ? Assert.IsType<string>(Assert.Single(Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>()))
                    : Assert.IsType<string>(retained.ReturnValue));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var ensureAscii in new[] { false, true })
        {
            foreach (var size in new[] { 0, 1, 127, 128, 129, 4097 })
                yield return ["'a'*" + size, ensureAscii, "\"" + new string('a', size) + "\""];
            foreach (var size in new[] { 64, 65 })
                yield return ["'é'*" + size, ensureAscii, "\"" + string.Concat(Enumerable.Repeat(ensureAscii ? "\\u00e9" : "é", size)) + "\""];
            foreach (var size in new[] { 32, 33 })
                yield return ["'😀'*" + size, ensureAscii, "\"" + string.Concat(Enumerable.Repeat(ensureAscii ? "\\ud83d\\ude00" : "😀", size)) + "\""];
            yield return ["'é😀\\n\"'*20", ensureAscii, "\"" + string.Concat(Enumerable.Repeat(ensureAscii ? "\\u00e9\\ud83d\\ude00\\n\\\"" : "é😀\\n\\\"", 20)) + "\""];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StringsAndKeysPreserveAllEncoderForms(string expression, bool ensureAscii, string expected)
    {
        var source = "import json\nvalue=" + expression + "\nencoder=json.JSONEncoder(ensure_ascii=" + (ensureAscii ? "True" : "False") + ")\n" +
            "print(json.dumps(value,ensure_ascii=" + (ensureAscii ? "True" : "False") + "))\n" +
            "print(encoder.encode(value))\nprint(''.join(encoder.iterencode(value)))\n" +
            "mapping={value:None}\nprint(json.dumps(mapping,ensure_ascii=" + (ensureAscii ? "True" : "False") + "))\n" +
            "print(encoder.encode(mapping))\nprint(''.join(encoder.iterencode(mapping)))";
        var stdout = string.Concat(Enumerable.Repeat(expected + "\n", 3)) + string.Concat(Enumerable.Repeat("{" + expected + ": null}\n", 3));
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(stdout, result.StandardOutput);
        }
    }
}
