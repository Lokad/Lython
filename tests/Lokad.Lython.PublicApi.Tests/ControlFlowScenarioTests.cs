using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ControlFlowScenarioTests
{
    [Fact]
    public void BreakContinueAndWhileFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "ControlFlow", "BreakContinueAndWhile"));
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void ElifBranchingFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "ControlFlow", "ElifBranching"));
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public async Task BreakAbandonmentReclaimsIteratorSlots()
    {
        // The executable loop used to leave the abandoned iterator on the
        // value stack, rooting its charges on every break; 10k breaks now
        // complete at 3 MiB with the slot popped at the break edge.
        var script = new LythonEngine().Compile("""
            i = 0
            while i < 10000:
                for t in [[1, 2, 3]]:
                    break
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task NestedForBreakKeepsOuterIteration()
    {
        var script = new LythonEngine().Compile("""
            seen = []
            for a in [1, 2, 3]:
                for b in [10, 20, 30]:
                    if b == 20:
                        break
                    seen.append(a * 100 + b)
            return seen
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(110), new BigInteger(210), new BigInteger(310) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task WhileBreakInsideForKeepsOuterSlot()
    {
        var script = new LythonEngine().Compile("""
            seen = []
            for a in [1, 2]:
                while True:
                    seen.append(a)
                    break
            return seen
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public void LoopElse_RunsWithPythonLikeBreakSemantics()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
vals = []
for item in [1, 2]:
    if item == 3:
        break
else:
    vals.append("for-else")

n = 0
while n < 3:
    n += 1
    if n == 2:
        break
else:
    vals.append("while-else")

m = 0
while m < 2:
    m += 1
else:
    vals.append("while-fell-through")

__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("for-else|while-fell-through", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData(1, "a")]
    [InlineData(2, "b")]
    [InlineData(3, "c")]
    [InlineData(9, "d")]
    public async Task ElifChain_SelectsMatchingBranch(int value, string expected)
    {
        var script = new LythonEngine().Compile(
            "x = " + value + Environment.NewLine +
            """
            if x == 1:
                r = "a"
            elif x == 2:
                r = "b"
            elif x == 3:
                r = "c"
            else:
                r = "d"
            return r
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ElifChain_NoMatchWithoutElse_FallsThrough()
    {
        var script = new LythonEngine().Compile("""
            x = 9
            if x == 1:
                r = "a"
            elif x == 2:
                r = "b"
            return "fell-through"
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("fell-through", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("fell-through", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ElifChain_ShortCircuitsAfterFirstMatch()
    {
        var script = new LythonEngine().Compile("""
            calls = []
            def probe(name, value):
                calls.append(name)
                return value
            if probe("if", False):
                r = "if"
            elif probe("e1", True):
                r = "e1"
            elif probe("e2", True):
                r = "e2"
            else:
                r = "else"
            return [r, calls]
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { "e1", new List<object?> { "if", "e1" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ElifChain_InlineSuites()
    {
        var script = new LythonEngine().Compile("""
            x = 3
            if x == 1: r = "a"
            elif x == 2: r = "b"
            elif x == 3: r = "c"
            else: r = "d"
            return r
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("c", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("c", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ElifChain_NestedChains()
    {
        var script = new LythonEngine().Compile("""
            x = 2
            y = 3
            if x == 1:
                r = "a"
            elif x == 2:
                if y == 1:
                    r = "b"
                elif y == 2:
                    r = "c"
                elif y == 3:
                    r = "d"
                else:
                    r = "e"
            else:
                r = "f"
            return r
            """);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("d", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("d", asyncResult.ReturnValue);
    }

    [Fact]
    public void ElifChain_MalformedClauses_ReportDiagnostics()
    {
        var missingCondition = new LythonEngine().Compile("""
            x = 1
            if x == 1:
                pass
            elif
                pass
            """);
        Assert.False(missingCondition.IsValid);
        Assert.Contains(missingCondition.Diagnostics, d => d.Code == "LA1013");
        var missingColon = new LythonEngine().Compile("""
            x = 1
            if x == 1:
                pass
            elif x
                pass
            """);
        Assert.False(missingColon.IsValid);
        Assert.Contains(missingColon.Diagnostics, d => d.Code == "LA1014");
    }

    private static string BuildElifChain(int elifCount, int match)
    {
        var builder = new StringBuilder();
        builder.Append("x = ").Append(match).AppendLine();
        builder.AppendLine("if x == 0:");
        builder.AppendLine("    r = 'c0'");
        for (var i = 1; i <= elifCount; i++)
        {
            builder.Append("elif x == ").Append(i).AppendLine(":");
            builder.Append("    r = 'c").Append(i).AppendLine("'");
        }

        builder.AppendLine("else:");
        builder.AppendLine("    r = 'other'");
        builder.AppendLine("return r");
        return builder.ToString();
    }

    [Fact]
    public async Task ElifChain_DeepChainWithinBudget_Runs()
    {
        var script = new LythonEngine().Compile(BuildElifChain(63, 63));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("c63", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("c63", asyncResult.ReturnValue);
    }

    [Fact]
    public void ElifChain_BeyondBudget_RejectsExplicitly()
    {
        var compiled = new LythonEngine().Compile(BuildElifChain(64, 65));
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0003");
    }
}

