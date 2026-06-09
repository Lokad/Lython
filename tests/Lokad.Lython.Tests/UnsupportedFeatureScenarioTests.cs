using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class UnsupportedFeatureScenarioTests
{
    [Fact]
    public void MissingImportFixture_ReportsExpectedImportError()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Language", "Failures", "UnsupportedImport"));

        var result = new LythonEngine().Run(fixture.Script, new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041");
    }

    [Fact]
    public void ProcessSpawningImports_RemainExplicitlyOutsideTheSafeBoundary()
    {
        var result = new LythonEngine().Run("import subprocess\n", new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Code == "LA3041");
    }

    [Theory]
    [InlineData("yield 1\n", "yield")]
    [InlineData("async def f():\n    pass\n", "async")]
    [InlineData("value = await work()\n", "await")]
    [InlineData("global value\n", "global")]
    [InlineData("nonlocal value\n", "nonlocal")]
    [InlineData("def f(a = 1, b):\n    pass\n", "non-default parameter after default parameter")]
    [InlineData("f(a = 1, 2)\n", "positional argument after keyword argument")]
    [InlineData("a, *b, *c = [1, 2, 3]\n", "multiple starred assignment targets")]
    public void UnsupportedConstruct_ReportsNamedDiagnostic(string source, string construct)
    {
        var compiled = new LythonEngine().Compile(source);

        Assert.False(compiled.IsValid);
        Assert.Contains(
            compiled.Diagnostics,
                          diagnostic => diagnostic.Code == "LA2000" &&
                          diagnostic.Message.Contains(construct, StringComparison.Ordinal));
    }

    [Fact]
    public void DecoratorExpression_MustEvaluateToCallable()
    {
        var result = new LythonEngine().Run(
            """
dec = 1

@dec
def f():
    return 1

f()
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("Decorator expression must evaluate to a callable", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttributeAssignment_OnUnsupportedObjectFailsAtRuntime()
    {
        var result = new LythonEngine().Run(
            """
value = "x"
value.member = 1
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("attribute assignment", result.Failure.Message, StringComparison.Ordinal);
    }
}
