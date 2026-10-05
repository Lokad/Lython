using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonPrefixValidationTests
{
    [Fact]
    public async Task StrictStringsRejectBeforeDependentHooks()
    {
        var script = new LythonEngine().Compile("""
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
                    print(e.pos, events, e.doc == source)
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
        var script = new LythonEngine().Compile("""
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
        var script = new LythonEngine().Compile("import json\nreturn str(json.JSONDecoder().raw_decode(source))");
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = "0 \"" + new string('\u0001', 250000) + "\"" },
            MaxExecutionMemoryBytes = 1_000_000,
            MaxExecutionSteps = 1000
        };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(0, 1)", result.ReturnValue?.ToString());
            Assert.True(result.PeakExecutionMemoryBytes < 600000);
        }
    }

    [Theory]
    [InlineData(100000, true)]
    [InlineData(30000, false)]
    public async Task LenientControlBufferIsFundedBeforeGrowth(long budget, bool success)
    {
        var script = new LythonEngine().Compile("import json\nreturn len(json.loads(source, strict=False))");
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = "\"" + new string('\u0001', 5000) + "\"" },
            MaxExecutionMemoryBytes = budget
        };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.Equal(success, result.Success);
            if (success) Assert.Equal("5000", result.ReturnValue?.ToString());
            else Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
    }
}
