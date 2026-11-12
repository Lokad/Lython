using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// JSON storage is admitted before construction, including scratch and
// replacement overlap. Iterative decoding has no separate nesting allowance.
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
        var sync = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost());
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
        var result = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"a\": 2, \"b\": 1}", result.ReturnValue);
    }

    [Fact]
    public void SortKeysHugeDeniesUnderTinyMemory()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nd = {str(i): i for i in range(20000)}\nreturn len(json.dumps(d, sort_keys=True))\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void LongIndentDeniesBeforeWriting()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nreturn json.dumps([1], indent=\"x\" * 100000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        // The 100k-per-level run is funded before any of it lands.
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void LongSeparatorDeniesBeforeWriting()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nreturn json.dumps([1, 2], separators=(\",\", \":\" * 100000))\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.PeakExecutionMemoryBytes < 8192, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void LargeScalarDeniesBeforeWriting()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import math\nimport json\nreturn json.dumps([10**100000])\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void HugeDefaultResultDenies()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nreturn json.dumps({\"a\": {1, 2}}, default=lambda o: \"z\" * 200000)\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public void DefaultCallbackFundedEquivalence()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nreturn json.dumps({\"a\": {1, 2}}, default=lambda o: sorted(o))\n",
            new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("{\"a\": [1, 2]}", result.ReturnValue);
    }

    [Fact]
    public async Task LoadsDeepNestingUsesAdmittedFrames()
    {
        const string source = """
            import json
            depth = 257
            value = json.loads('[' * depth + '42' + ']' * depth)
            for unused in range(depth):
                value = value[0]
            return value
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(42), result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, 64)]
    [InlineData(false, 65)]
    [InlineData(true, 64)]
    [InlineData(true, 65)]
    public async Task LoadsNestedContainersPreserveHookOrderAndBudgetReuse(bool objects, int depth)
    {
        const string source = """
            import json
            events = []
            def hook(value):
                events.append(value['a'] is True)
                return value
            value = json.loads(source, object_hook=hook)
            if objects:
                assert events[0] and not any(events[1:])
            for unused in range(depth):
                value = value['a'] if objects else value[0]
            print(value, depth, len(events))
            """;
        var document = string.Concat(Enumerable.Repeat(objects ? "{\"a\":" : "[", depth))
            + "true" + new string(objects ? '}' : ']', depth);
        var expected = $"True {depth} {(objects ? depth : 0)}\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["source"] = document, ["depth"] = depth, ["objects"] = objects }
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
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nx = []\nfor i in range(600):\n x = [x]\nreturn json.dumps(x)\n",
            new MockLythonHost());
        AssertError(result, "RecursionError", "maximum recursion depth exceeded");
    }

    [Fact]
    public void ExpansionDenialPreservesFirstAdmission()
    {
        // Conservative capacity/overlap admission may reject before final
        // projection. Preserve the first refused estimate and resource origin.
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            "import json\nreturn len(json.dumps([\"x\" * 4998, \"x\" * 4998]))\n",
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(45000) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
        Assert.True(result.DeniedReservationBytes > 0);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
    }

    [Fact]
    public void DenseSmallTokensCalibrateDocumentEstimate()
    {
        const string code = "import json\nx = json.loads(\"[\" + \"1,\" * 200000 + \"1]\")\nreturn len(x)\n";
        var denied = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1000000) });
        AssertError(denied, "MemoryError", "shared memory budget exceeded");
        var funded = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(256 * 1024 * 1024) });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(200001), funded.ReturnValue);
    }

    [Fact]
    public async Task DenseSmallTokensCalibrateDocumentEstimateAsync()
    {
        const string code = "import json\nx = json.loads(\"[\" + \"1,\" * 200000 + \"1]\")\nreturn len(x)\n";
        var denied = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1000000) });
        AssertError(denied, "MemoryError", "shared memory budget exceeded");
        var funded = await new LythonEngine(Utf8RegexProvider.Instance).RunAsync(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(256 * 1024 * 1024) });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(200001), funded.ReturnValue);
    }

    [Fact]
    public void EscapedTextCalibratesDocumentEstimate()
    {
        // Single-quoted Lython literals plus chr() keep every quote and the
        // JSON backslash out of the Lython string-escape rules entirely.
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
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
        var denied = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1000000) });
        AssertError(denied, "MemoryError", "shared memory budget exceeded");
        var funded = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(256 * 1024 * 1024) });
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(100000), funded.ReturnValue);
    }
}
