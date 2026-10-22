using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessFrontendTests
{
    [Theory]
    [InlineData("'value'", "value\n")]
    [InlineData("['value']", "['value']\n")]
    public async Task LocalObjectNamedSubprocessHasItsOwnCallContract(string argument, string expected)
    {
        using var script = new LythonEngine().Compile("class Runner:\n def run(self,value):return value\nsubprocess=Runner()\nprint(subprocess.run(" + argument + "))");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(x => x.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            Assert.Empty(result.Diagnostics);
        }
    }

    [Theory]
    [InlineData("import subprocess as commands\ncommands.run(123)")]
    [InlineData("from subprocess import run as execute\nexecute(123)")]
    public void ResolvedAliasesKeepOneUsefulArgumentDiagnostic(string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Single(script.Diagnostics, x => x.Severity == LythonDiagnosticSeverity.Error);
        Assert.Contains(script.Diagnostics, x => x.Message.Contains("iterable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ActualImportStillRequiresAuthorityBeforeEffects()
    {
        using var script = new LythonEngine().Compile("print('effect')\nimport subprocess as commands\ncommands.run(['tool'])");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Single(result.Diagnostics, x => x.Code == "LA3041");
        }
    }
}
