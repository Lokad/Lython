using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterCapabilityTests
{
    [Fact]
    public void ScopedInlineFlagsDoNotLeakIntoPatternFlagsOrConstantAnalysis()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance)
            .Compile("import re\np=re.compile('(?s:a).')\nprint(p.flags)\nreturn p.match('a\\n')");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("32\n", result.StandardOutput);
        Assert.Null(result.ReturnValue);
    }

    [Fact]
    public void BackendCancellationLimitationIsAnExplicitCapabilityError()
    {
        using var cancellation = new CancellationTokenSource();
        using var script = new LythonEngine(Utf8RegexProvider.Instance)
            .Compile("import re\np=re.compile('x')", null, cancellation.Token);
        Assert.False(script.IsValid);
        using var result = script.Run(new MockLythonHost());
        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
        Assert.Contains(script.Diagnostics, d => d.Message.Contains("cannot interrupt backend work", StringComparison.Ordinal));
    }

    [Fact]
    public void AlreadyCancelledAdapterCompilationDoesNotAcquireStorage()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => Utf8RegexProvider.Instance.Compile("x"u8.ToArray(), new(32, false), budget, cancellation.Token));
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void AffectedBufferedUnicodeOperationReportsCapabilityBeforeEnteringBackend()
    {
        using var result = new LythonEngine(Utf8RegexProvider.Instance)
            .Run("import re\nreturn re.findall('.', 'é𝒜z')", new MockLythonHost());
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
        Assert.Contains("buffered matching", result.Failure?.Message);
    }
}
