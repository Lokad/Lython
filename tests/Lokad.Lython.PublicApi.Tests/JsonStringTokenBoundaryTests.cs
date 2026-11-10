using Lokad.Lython.Tests.Harness;
using System.Text;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonStringTokenBoundaryTests
{
    [Theory]
    [InlineData(1021, false)]
    [InlineData(1021, true)]
    [InlineData(1022, false)]
    [InlineData(1022, true)]
    [InlineData(1023, false)]
    [InlineData(1023, true)]
    [InlineData(1024, false)]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    [InlineData(1025, true)]
    public async Task RawStringWindowsPreserveEscapesControlsAndScalarEnd(int prefixLength, bool strict)
    {
        var prefix = new string('a', prefixLength);
        var token = "\"" + prefix + (strict ? "\\n\\\"\\\\\\u00e9" : "\n\\\"") + "😀" + prefix + "\"";
        var expected = prefix + (strict ? "\n\"\\é" : "\n\"") + "😀" + prefix;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
        {
            ["payload"] = token + " ignored",
            ["end"] = token.EnumerateRunes().Count()
        } };
        using var script = new LythonEngine().Compile("import json\nvalue,position=json.JSONDecoder(strict=" +
            (strict ? "True" : "False") + ").raw_decode(payload)\nassert position==end\nreturn value");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }

    [Theory]
    [InlineData(1016)]
    [InlineData(1017)]
    [InlineData(2040)]
    [InlineData(2041)]
    public async Task Utf8WindowsPreserveAllControlBytesAndMalformedPositions(int asciiLength)
    {
        // The control or escape starts at byte 1023/1024 or 2047/2048, but
        // Python error positions count scalars. Goldens checked with CPython.
        var prefix = "é😀" + new string('a', asciiLength);
        var budget = new LythonMemoryBudget(512000);
        using (var script = new LythonEngine().Compile("""
            import json
            payload = '"' + prefix + chr(code) + 'z"'
            value, end = json.JSONDecoder(strict=False).raw_decode(payload + ' tail')
            assert value == prefix + chr(code) + 'z'
            assert end == len(payload)
            try:
                json.loads(payload)
                assert False
            except json.JSONDecodeError as exc:
                assert exc.pos == len(prefix) + 1
                assert exc.lineno == 1 and exc.colno == len(prefix) + 2
            positions = []
            for suffix in ['\\q"', '\\u12xz"', '\\', '']:
                try:
                    json.loads('"' + prefix + suffix)
                    assert False
                except json.JSONDecodeError as exc:
                    positions.append(exc.pos)
            return positions == [len(prefix) + 1, len(prefix) + 2, 0, 0]
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var globals = new Dictionary<string, object?> { ["prefix"] = prefix };
            var options = new LythonRunOptions { Globals = globals };
            for (var code = 0; code < 32; code++)
            foreach (var asynchronous in new[] { false, true })
            {
                globals["code"] = code;
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(true, result.ReturnValue);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
