using System.Diagnostics;
using System.Text;
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
    public void SortedEmptyIterableWithBadKeyStillRaisesTypeError()
    {
        var sync = new LythonEngine().Run(
            """
try:
    sorted([], key=1)
except TypeError:
    print(1)
""",
            new MockLythonHost());
        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("1\n", sync.StandardOutput);
    }

    [Fact]
    public async Task SortedEmptyIterableWithBadKeyStillRaisesTypeErrorAsync()
    {
        var asyncResult = await new LythonEngine().RunAsync(
            """
try:
    sorted([], key=1)
except TypeError:
    print(1)
""",
            new MockLythonHost());
        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal("1\n", asyncResult.StandardOutput);
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
        var psi = new ProcessStartInfo("dotnet", "\"" + probe + "\" --batch-json")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi);
        Assert.NotNull(process);
        process.StandardInput.Write(JsonSerializer.Serialize(snippets));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        Assert.True(process.WaitForExit(120000), "Probe batch timed out; a pathological input may have crashed it.");
        var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
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

    private static string FindProbeDll()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            foreach (var configuration in new[] { "Debug", "Release" })
            {
                var candidate = Path.Combine(
                    directory.FullName, "tools", "LythonProbe", "bin", configuration, "net10.0", "LythonProbe.dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("LythonProbe.dll was not found; build the solution before running this test.");
    }
}

