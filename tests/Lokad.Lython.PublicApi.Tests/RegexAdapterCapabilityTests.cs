using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterCapabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReversedMatchWindowReportsBackendCapabilityWithoutInventingNoMatch(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance)
            .Compile("import re\nreturn re.compile(pattern).match('a',1,0)", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = "" } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
                Assert.Contains("pos greater than endpos", result.Failure?.Message);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvertedSearchWindowsHaveNoMatch(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            p = re.compile('')
            q = re.compile(pattern)
            print(p.search('😀x尾', 2, 1), p.fullmatch('😀x尾', 2, 1))
            print(q.search(subject, 2, 1), q.fullmatch(subject, 2, 1))
            print(p.search('😀x尾', 5, 4).span(), p.fullmatch('😀x尾', 5, 4).span())
            """, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = "", ["subject"] = "😀x尾" } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("None None\nNone None\n(3, 3) (3, 3)\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

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

    [Theory]
    [InlineData(32, true, "a # ignored", "a", 32)]
    [InlineData(96, false, "a # literal", "a # literal", 96)]
    [InlineData(32, true, "(?x)a # ignored", "a", 96)]
    [InlineData(96, false, "(?i)a # literal", "A # literal", 98)]
    public void ParsingVerboseAndReportedFlagsRemainIndependent(
        int flags, bool verbose, string source, string text, int expectedFlags)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            using var pattern = Utf8RegexProvider.Instance.Compile(System.Text.Encoding.UTF8.GetBytes(source),
                new(flags, verbose), owner, default);
            Assert.Equal(expectedFlags, pattern.Flags);
            var input = System.Text.Encoding.UTF8.GetBytes(text);
            using var subject = pattern.Prepare(input, new(0, text.Length, 0, input.Length), owner, default);
            var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
            Assert.True(subject.TryMatch(LythonRegexMatchMode.FullMatch, captures, out _, default));
            Assert.Equal(new LythonRegexCapture(0, input.Length), captures[0]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancellableCompilationDoesNotSaveItsTokenOnThePattern()
    {
        using var cancellation = new CancellationTokenSource();
        using var script = new LythonEngine(Utf8RegexProvider.Instance)
            .Compile("import re\np=re.compile('x')", null, cancellation.Token);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        cancellation.Cancel();
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
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
    public void SupplementaryUnicodeUsesScalarMatching()
    {
        using var result = new LythonEngine(Utf8RegexProvider.Instance)
            .Run("import re\nreturn re.findall('.', 'é𝒜z')", new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new object?[] { "é", "𝒜", "z" }, Assert.IsType<List<object?>>(result.ReturnValue));
    }
}
