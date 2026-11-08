using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CasedWhitespacePipelineTests
{
    // Independently checked with isolated CPython 3.13.2, including every Python
    // whitespace scalar, final sigma, case-ignorable neighbors and expansions.
    public static IEnumerable<object[]> UnicodeCases()
    {
        int[] whitespace = [9, 10, 11, 12, 13, 28, 29, 30, 31, 32, 133, 160, 5760,
            8192, 8193, 8194, 8195, 8196, 8197, 8198, 8199, 8200, 8201, 8202,
            8232, 8233, 8239, 8287, 12288];
        foreach (var codePoint in whitespace)
        {
            var separator = char.ConvertFromUtf32(codePoint);
            var input = separator + string.Join(separator, "ΟΣ", "Σ", "İßﬃ", "AΣ’", "AΣ\u200d") + separator;
            yield return ["lower", input, new[] { "ος", "σ", "i\u0307ßﬃ", "aς’", "aς\u200d" }];
            yield return ["upper", input, new[] { "ΟΣ", "Σ", "İSSFFI", "AΣ’", "AΣ\u200d" }];
        }
        foreach (var operation in new[] { "lower", "upper" })
        {
            yield return [operation, "", Array.Empty<string>()];
            yield return [operation, " \t\u3000", Array.Empty<string>()];
        }
    }

    [Theory]
    [MemberData(nameof(UnicodeCases))]
    public async Task CompleteWordsPreservePythonCasingAndWhitespace(string operation, string text, string[] expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var source in new[]
        {
            $"def words(value):return value.{operation}().split()\nreturn words(text)",
            $"def words(value):\n if value:return value.{operation}().split()\n return []\nreturn words(text)",
            $"try:\n result=text.{operation}().split()\nfinally:\n marker=True\nreturn result",
        }.SelectMany(source => new[] { source, source.Replace(".split()", ".rsplit()") }))
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["text"] = text } };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, Assert.IsType<List<object?>>(result.ReturnValue).Select(value => Assert.IsType<string>(value)));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task TwoResultsKeepIndependentWordsFundedUntilBothOwnersEnd()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var source in new[]
        {
            "def words(value):return value.upper().split()\nreturn words(text)",
            "def words(value):\n if value:return value.upper().split()\n return []\nreturn words(text)",
        }.SelectMany(source => new[] { source, source.Replace(".split()", ".rsplit()") }))
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["text"] = string.Join(' ', Enumerable.Repeat("ßﬃ", 20)) } };
            using (var first = script.Run(new MockLythonHost(), options))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var firstBalance = budget.ReservedBytes;
                using (var second = await script.RunAsync(new MockLythonHost(), options))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.True(budget.ReservedBytes > firstBalance);
                    Assert.All(Assert.IsType<List<object?>>(first.ReturnValue), value => Assert.Equal("SSFFI", value));
                    Assert.All(Assert.IsType<List<object?>>(second.ReturnValue), value => Assert.Equal("SSFFI", value));
                }
                Assert.Equal(firstBalance, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(null, false), InlineData(null, true)]
    [InlineData("/lower", false), InlineData("/lower", true)]
    [InlineData("/split", false), InlineData("/split", true)]
    public async Task CustomMethodsKeepRealSuspensionOrderAndAwaitedCleanup(string? cancelAt, bool reverse)
    {
        var source = """
            events = []
            class Lowered:
                def split(self):
                    with open('/split') as source:
                        events.append(source.read())
                    return ['α']
            class Text:
                def lower(self):
                    with open('/lower') as source:
                        events.append(source.read())
                    return Lowered()
            def words(value): return value.lower().split()
            print(words(Text()), events)
            print('done')
            """;
        if (reverse) source = source.Replace("def split(", "def rsplit(").Replace(".split()", ".rsplit()");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/lower", "lower");
            host.SeedFile("/split", "split");
            using var cancellation = new CancellationTokenSource();
            var reached = cancelAt is null ? null : host.PauseReadUntilCancellation(cancelAt);
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            if (reached is not null)
            {
                await reached.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                if (cancelAt is null)
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("['α'] ['lower', 'split']\ndone\n", result.StandardOutput);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.DoesNotContain("done", result.StandardOutput);
                }
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(cancelAt == "/lower" ? 1 : 2, host.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SuppliedOptionsAndVisibleCasedLocalsKeepPythonSemantics()
    {
        const string source = "text=' A  B  C '\nprint(text.lower().rsplit(None,1))\nprint(text.upper().rsplit(' ',1))\ndef visible(value):\n cased=value.lower()\n parts=cased.rsplit()\n return parts,locals()['cased']\nprint(visible(text))\n";
        const string expected = "[' a  b', 'c']\n[' A  B  C', '']\n(['a', 'b', 'c'], ' a  b  c ')\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
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
}
