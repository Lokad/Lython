using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReviewHardening3RegressionTests
{
    private static string Describe(LythonExecutionResult result)
        => result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message));

    private static string Repeat(string unit, int count)
        => string.Concat(Enumerable.Repeat(unit, count));

    [Fact]
    public void PowerChainsBeyondTheNestingBudgetAreRejected()
    {
        var compiled = new LythonEngine().Compile("x = 1" + Repeat("**1", 100) + "\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void AdditiveChainsBeyondTheOperandBudgetAreRejected()
    {
        var compiled = new LythonEngine().Compile("x = " + Repeat("1+", 100) + "1\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void MemberChainsBeyondTheOperandBudgetAreRejected()
    {
        var compiled = new LythonEngine().Compile("a = 1\nx = a" + Repeat(".b", 100) + "\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void TernaryChainsBeyondTheNestingBudgetAreRejected()
    {
        var compiled = new LythonEngine().Compile("c = True\nx = " + Repeat("1 if c else ", 100) + "0\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void LambdaChainsBeyondTheNestingBudgetAreRejected()
    {
        var compiled = new LythonEngine().Compile("f = " + Repeat("lambda: ", 100) + "1\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void CallNestingBeyondTheNestingBudgetIsRejected()
    {
        var compiled = new LythonEngine().Compile("x = " + Repeat("f(", 100) + "1" + Repeat(")", 100) + "\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void SuiteNestingBeyondTheNestingBudgetIsRejected()
    {
        var lines = new List<string>();
        for (var i = 0; i < 100; i++)
        {
            lines.Add(new string((char)32, i * 4) + "if True:");
        }
        lines.Add(new string((char)32, 100 * 4) + "x = 1");
        var compiled = new LythonEngine().Compile(string.Join("\n", lines) + "\n");
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }

    [Fact]
    public void DeepFStringNestingFailsGracefully()
    {
        var level = "1";
        for (var i = 0; i < 200; i++)
        {
            level = "f\"{x" + level + "}\"";
        }
        var compiled = new LythonEngine().Compile("x = 1\ny = " + level + "\n");
        Assert.False(compiled.IsValid);
    }

    [Fact]
    public void ShapesWithinBudgetCompileAndRun()
    {
        var lines = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            lines.Add(new string((char)32, i * 4) + "if True:");
        }
        lines.Add(new string((char)32, 40 * 4) + "x = 1");
        lines.Add("print(x)");
        var result = new LythonEngine().Run(
            "x = 1" + Repeat("**1", 40) + "\n"
            + "y = " + Repeat("1+", 40) + "1\n"
            + "f = " + Repeat("lambda: ", 10) + "1\n"
            + "c = True\nz = " + Repeat("1 if c else ", 10) + "0\n"
            + string.Join("\n", lines) + "\n",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal("1\n", result.StandardOutput);
    }

    [Fact]
    public void SortedValidatesKeyAfterIterableEffects()
    {
        var result = new LythonEngine().Run(
            """
calls = []
class C:
    def __iter__(self):
        calls.append("iter")
        return iter([2, 1])
try:
    sorted(C(), key=1)
except TypeError as ex:
    error = ex.type
return [calls, error, sorted(C(), key=None)]
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        var outer = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new List<object?> { "iter", "iter" }, Assert.IsType<List<object?>>(outer[0]));
        Assert.Equal("TypeError", outer[1]);
    }

    [Fact]
    public async Task SortedValidatesKeyAfterIterableEffectsAsync()
    {
        var result = await new LythonEngine().RunAsync(
            """
calls = []
class C:
    def __iter__(self):
        calls.append("iter")
        return iter([2, 1])
try:
    sorted(C(), key=1)
except TypeError:
    calls.append("caught")
return calls
""",
            new MockLythonHost());
        Assert.True(result.Success, Describe(result));
        Assert.Equal(new List<object?> { "iter", "caught" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }

    [Fact]
    public void SortedEmptyIterableWithBadKeyReturnsEmpty()
    {
        // R13: an invalid key only fails when it would actually be called.
        var sync = new LythonEngine().Run(
            """
print(sorted([], key=1))
""",
            new MockLythonHost());
        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("[]\n", sync.StandardOutput);
    }

    [Fact]
    public async Task SortedEmptyIterableWithBadKeyReturnsEmptyAsync()
    {
        // R13: an invalid key only fails when it would actually be called.
        var asyncResult = await new LythonEngine().RunAsync(
            """
print(sorted([], key=1))
""",
            new MockLythonHost());
        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal("[]\n", asyncResult.StandardOutput);
    }

    [Fact]
    public void SubprocessProbeRejectsPathologicalInputsGracefully()
    {
        var probe = FindProbeDll();
        var snippets = new[]
        {
            "x = 1" + Repeat("**1", 2000) + "\nprint(x)\n",
            "x = " + Repeat("1+", 2000) + "1\nprint(x)\n",
            "a = 1\nx = a" + Repeat(".b", 2000) + "\nprint(x)\n",
            "c = True\nx = " + Repeat("1 if c else ", 2000) + "0\nprint(x)\n",
            "f = " + Repeat("lambda: ", 2000) + "1\nprint(0)\n",
            "x = " + Repeat("f(", 2000) + "1" + Repeat(")", 2000) + "\nprint(x)\n",
        };
        // Concurrent drains plus a deadline: a pathological snippet must surface as
        // a failure or a TimeoutException, never a wedged test host.
        var run = SubprocessProbeRunner.Run(
            "dotnet",
            [probe, "--batch-json"],
            JsonSerializer.Serialize(snippets));
        // Batch probes exit 0/1 for result mismatches; anything else is a crash.
        Assert.True(run.ExitCode is 0 or 1, "probe exited with " + run.ExitCode + ": " + run.StandardError);
        var lines = run.StandardOutput.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(snippets.Length, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            var lython = document.RootElement.GetProperty("Lython");
            Assert.False(lython.GetProperty("Success").GetBoolean());
            Assert.Contains(
                lython.GetProperty("Diagnostics").EnumerateArray(),
                d => d.GetProperty("Code").GetString() == "LA0003");
        }
    }

    private static string FindProbeDll() => SubprocessProbeRunner.FindProbeDll();

}
