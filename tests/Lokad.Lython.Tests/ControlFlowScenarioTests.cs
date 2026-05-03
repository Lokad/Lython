using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

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

write_text("/out.txt", "|".join(vals))
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("for-else|while-fell-through", host.ReadText("/out.txt"));
    }
}
