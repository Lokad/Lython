using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PythonTokenizationExecutionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["ordinary-continuation", "print(repr('back\\\nline'))\n", "'backline'\n"];
        yield return ["raw-continuation", "print(repr(r'raw\\\nline'))\n", "'raw\\\\\\nline'\n"];
        yield return ["escaped-triple", "print(repr('''escaped\\'''tail'''))\n", "\"escaped'''tail\"\n"];
        yield return ["nested-formatted-quotes", "print(f\"{ {'x': 'y'}['x'] }\")\n", "y\n"];
        yield return ["logical-comments", "if True:\n    values = (\n  1, # note\n  2)\n    print(values)\n", "(1, 2)\n"];
        yield return ["numeric-categories", "print(0x_10, 0o_17, 0b_11, 00_0, 01.5, 01j, .25e+2, 1.e-2j)\n", "16 15 3 0 1.5 1j 25.0 0.01j\n"];
        yield return ["unicode-normalization", "\u2167=3\na\u0301=2\nprint(VIII, \u00e1)\n", "3 2\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LexicalFormsMatchIndependentPythonGoldens(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("value=1\\ \n +2\n")]
    [InlineData("value=1\\\t\n +2\n")]
    public void WhitespaceAfterAContinuationBackslashIsRejected(string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        var diagnostic = Assert.Single(script.Diagnostics);
        Assert.Equal("LA0001", diagnostic.Code);
        Assert.Equal(new LythonSourceSpan(7, 1, 1, 8), diagnostic.Span);
    }

    [Fact]
    public void CancellationAfterSourceAdmissionReturnsAnInvalidScriptAndReleasesOwnership()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var supplied = new CancellingScope(budget.CreateScope(), cancellation);
        using (var script = new LythonEngine().Compile("value = (1 + 2)\n", supplied, cancellation.Token))
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.False(script.IsValid);
            Assert.Equal("LA2011", Assert.Single(script.Diagnostics).Code);
            using var result = script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var recovered = new LythonEngine().Compile("value = 3\n", budget)) Assert.True(recovered.IsValid);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("value = foo(\n # comment\n", 12, "LA1000")]
    [InlineData("def f(\n # comment\n", 6, "LA1032")]
    [InlineData("value = x[\n", 10, "LA1000")]
    public void IncompleteJoinedLinesKeepTheirLogicalEndDiagnostic(string source, int position, string code)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Equal(code, script.Diagnostics[0].Code);
        Assert.Equal(new LythonSourceSpan(position, 0, 1, position + 1), script.Diagnostics[0].Span);
    }

    private sealed class CancellingScope(ILythonMemoryScope inner, CancellationTokenSource cancellation) : ILythonMemoryScope
    {
        public ILythonMemoryScope CreateScope() => new CancellingScope(inner.CreateScope(), cancellation);
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out reservation)) return false;
            if (bytes > 0) cancellation.Cancel();
            return true;
        }
        public void Dispose() => inner.Dispose();
    }
}
