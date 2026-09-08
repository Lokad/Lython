using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExceptionScenarioTests
{
    [Fact]
    public void RaiseCatchAndFinallyFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Exceptions", "RaiseCatchAndFinally"));
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
}

