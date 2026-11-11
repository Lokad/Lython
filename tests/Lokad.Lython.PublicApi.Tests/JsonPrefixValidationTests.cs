using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonPrefixValidationTests
{
    [Fact]
    public async Task StrictStringsRejectBeforeDependentHooks()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import json
            events = []
            def hook(o):
                events.append('hook')
                return o
            def integer(s):
                events.append('int')
                return int(s)
            for source in ['{"x":"a' + chr(1) + 'b"}', '{"a' + chr(1) + '":1}', '[{}, {"x":"a' + chr(1) + 'b"}]']:
                events.clear()
                try:
                    json.loads(source, object_hook=hook, parse_int=integer)
                except json.JSONDecodeError as e:
                    print(e.pos, events, e.doc is source)
            """);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("7 [] True\n3 [] True\n12 ['hook'] True\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task PrefixIgnoresPrecedingAndTrailingInvalidStrings()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import json
            d = json.JSONDecoder()
            print(d.raw_decode('"a' + chr(1) + '" 42', 5))
            print(d.raw_decode('0 "a' + chr(1) + '"'))
            """);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(42, 7)\n(0, 1)\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task SmallPrefixDoesNotReserveOrScanItsLargeSuffix()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nreturn str(json.JSONDecoder().raw_decode(source))");
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = "0 \"" + new string('\u0001', 250000) + "\"" },
            MemoryBudget = new LythonMemoryBudget(1_000_000)        };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(0, 1)", result.ReturnValue?.ToString());
            Assert.True(result.PeakExecutionMemoryBytes < 600000);
        }
    }

    [Fact]
    public async Task LenientMixedEscapesHaveABoundedWorkingSetAndReleaseTheirAllowance()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import json\nreturn len(json.loads(source, strict=False))");
        var budget = new LythonMemoryBudget(30000);
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = "\"" + new string('\u0001', 5000) + "\\n\"" },
            MemoryBudget = budget
        };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("5001", result.ReturnValue?.ToString());
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
