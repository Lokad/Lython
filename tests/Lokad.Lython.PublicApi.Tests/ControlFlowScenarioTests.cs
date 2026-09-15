using System.Numerics;
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
}

