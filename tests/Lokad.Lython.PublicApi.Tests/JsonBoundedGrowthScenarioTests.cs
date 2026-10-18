using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R07: the JSON serializer funds variable-sized appends before they land,
// keeps sort scratch alive beside the builder, and accounts overlapping
// builder/UTF-16/UTF-8 storage at final conversion. Deserializer calibration
// pins dense and escaped shapes against the existing 4x document estimate.
public sealed class JsonBoundedGrowthScenarioTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SortKeysFundedEquivalence()
    {
        const string code = """
import json
return [json.dumps({"b": 1, "a": 2}, sort_keys=True), json.dumps({}, sort_keys=True)]
""";
        var sync = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { "{\"a\": 2, \"b\": 1}", "{}" }, sync.ReturnValue);
    }

    [Fact]
    public async Task SortKeysFundedEquivalenceAsync()
    {
        const string code = """
import json
return json.dumps({"b": 1, "a": 2}, sort_keys=True)
""";
        var result = await new LythonEngine().RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"a\": 2, \"b\": 1}", result.ReturnValue);
    }

    [Fact]
    public void SortKeysHugeDeniesUnderTinyMemory()
    {
        var result = new LythonEngine().Run(
            "import json\nd = {str(i): i for i in range(20000)}\nreturn len(json.dumps(d, sort_keys=True))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void LongIndentDeniesBeforeWriting()
    {
        var result = new LythonEngine().Run(
            "import json\nreturn json.dumps([1], indent=\"x\" * 100000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        // The 100k-per-level run is funded before any of it lands.
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void LongSeparatorDeniesBeforeWriting()
    {
        var result = new LythonEngine().Run(
            "import json\nreturn json.dumps([1, 2], separators=(\",\", \":\" * 100000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void LargeScalarDeniesBeforeWriting()
    {
        var result = new LythonEngine().Run(
            "import math\nimport json\nreturn json.dumps([10**100000])\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void HugeDefaultResultDenies()
    {
        var result = new LythonEngine().Run(
            "import json\nreturn json.dumps({\"a\": {1, 2}}, default=lambda o: \"z\" * 200000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 65536 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DefaultCallbackFundedEquivalence()
    {
        var result = new LythonEngine().Run(
            "import json\nreturn json.dumps({\"a\": {1, 2}}, default=lambda o: sorted(o))\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"a\": [1, 2]}", result.ReturnValue);
    }

    [Fact]
    public async Task LoadsDeepNestingMeetsBackendDepthCap()
    {
        // The streaming core preserves the previous backend limit: nesting
        // past 64 containers fails explicitly instead of recursing unbounded.
        var script = new LythonEngine().Compile(
            """
            import json
            return json.loads("[" * 100 + "]" * 100)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("JSONDecodeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum configured depth of 64", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("JSONDecodeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum configured depth of 64", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, 64)]
    [InlineData(false, 65)]
    [InlineData(true, 64)]
    [InlineData(true, 65)]
    public async Task LoadsExactNestingBoundaryPrecedesObjectHooks(bool objects, int depth)
    {
        const string source = """
            import json
            events = []
            def hook(value):
                events.append(1)
                return value
            try:
                json.loads(source, object_hook=hook)
                print('ok', depth, len(events))
            except json.JSONDecodeError as error:
                print('limited', depth, error.pos, error.lineno, error.colno, len(events))
            """;
        var document = string.Concat(Enumerable.Repeat(objects ? "{\"a\":" : "[", depth))
            + "true" + new string(objects ? '}' : ']', depth);
        var position = objects ? 320 : 64;
        var expected = depth == 64
            ? $"ok 64 {(objects ? 64 : 0)}\n"
            : $"limited 65 {position} 1 {position + 1} 0\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = document, ["depth"] = depth },
            MaxExecutionMemoryBytes = 1024 * 1024
        };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeepContainersStayDepthBounded()
    {
        var result = new LythonEngine().Run(
            "import json\nx = []\nfor i in range(600):\n x = [x]\nreturn json.dumps(x)\n",
            new MockLythonHost());
        AssertError(result, "RecursionError", "maximum recursion depth exceeded");
    }

    [Fact]
    public void FinalConversionDenialNamesCopyCoexistence()
    {
        // 10,004 output chars: each leaf decode and transient scratch fit
        // 45,000 B, but the final UTF-16 copy (2x output) does not. Two leaves
        // isolate final-copy overlap from one large active input decode.
        var result = new LythonEngine().Run(
            "import json\nreturn len(json.dumps([\"x\" * 4998, \"x\" * 4998]))\n",
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 45000 });
        AssertError(result, "MemoryError", "execution memory budget exceeded (45000)");
        Assert.Equal(20008, result.DeniedReservationBytes);
    }

    [Fact]
    public void DenseSmallTokensCalibrateDocumentEstimate()
    {
        const string code = "import json\nx = json.loads(\"[\" + \"1,\" * 200000 + \"1]\")\nreturn len(x)\n";
        var denied = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1000000 });
        AssertError(denied, "MemoryError", "execution memory budget exceeded");
        var funded = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 256 * 1024 * 1024 });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(200001), funded.ReturnValue);
    }

    [Fact]
    public async Task DenseSmallTokensCalibrateDocumentEstimateAsync()
    {
        const string code = "import json\nx = json.loads(\"[\" + \"1,\" * 200000 + \"1]\")\nreturn len(x)\n";
        var denied = await new LythonEngine().RunAsync(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1000000 });
        AssertError(denied, "MemoryError", "execution memory budget exceeded");
        var funded = await new LythonEngine().RunAsync(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 256 * 1024 * 1024 });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(200001), funded.ReturnValue);
    }

    [Fact]
    public void EscapedTextCalibratesDocumentEstimate()
    {
        // Single-quoted Lython literals plus chr() keep every quote and the
        // JSON backslash out of the Lython string-escape rules entirely.
        var result = new LythonEngine().Run(
            "import json\nx = json.loads('[' + chr(34) + chr(92) + 'u00e9' + chr(34) + ']')\nreturn x\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new List<object?> { "\u00e9" }, result.ReturnValue);
    }

    [Fact]
    public void EscapedDenseCalibratesDocumentEstimate()
    {
        var code =
            "import json\ne = chr(34) + chr(92) + 'u00e9' + chr(34)\nx = json.loads('[' + ','.join([e] * 100000) + ']')\nreturn len(x)\n";
        var denied = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1000000 });
        AssertError(denied, "MemoryError", "execution memory budget exceeded");
        var funded = new LythonEngine().Run(code, new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 256 * 1024 * 1024 });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(100000), funded.ReturnValue);
    }
}
