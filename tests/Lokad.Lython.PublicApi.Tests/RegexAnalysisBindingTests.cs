using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAnalysisBindingTests
{
    [Theory]
    [InlineData("import re\nif False: pattern = re.compile('a')")]
    [InlineData("import re\ndef prepare():\n global pattern\n pattern = re.compile('a')")]
    [InlineData("import re\ndef prepare():\n global pattern\n pattern = re.compile('a')\nprepare()")]
    public void ConstantPatternAnalysisPreservesMissingProviderFailureAcrossScopes(string source)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.False(script.IsValid);
            Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA2010");
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string, string> OrdinaryReceivers => new()
    {
        { "class Echo:\n    LOCALE = 4\n    def compile(self, pattern, flags=0): return pattern + ':' + str(flags)\nre = Echo()\nprint(re.compile(r'\\N{NOT A CHARACTER}', re.LOCALE))", "\\N{NOT A CHARACTER}:4\n" },
        { "class Echo:\n    LOCALE = 4\n    def search(self, pattern, text, flags=0): return text\ndef apply(re): return re.search(r'\\N{NOT A CHARACTER}', 'ok', re.LOCALE)\nprint(apply(Echo()))", "ok\n" },
        { "import re\nclass Echo:\n    def compile(self, pattern): return pattern\nre = Echo()\nprint(re.compile(r'\\N{NOT A CHARACTER}'))", "\\N{NOT A CHARACTER}\n" },
        { "value = 3 | 4\nprint(value, value & 2, value ^ 1)", "7 2 6\n" },
    };

    [Theory]
    [MemberData(nameof(OrdinaryReceivers))]
    public async Task OrdinaryNamesAndBitMasksNeedNoRegexProvider(string source, string expected)
        => await CheckRuns(new LythonEngine(), source, expected);

    public static TheoryData<string, string> RegexSemantics => new()
    {
        { "import re as regex\nclass Other:\n    DEBUG = 0\nre = Other()\nprint(regex.compile('a', re.DEBUG).fullmatch('a').group())", "a\n" },
        { "import re\np = re.compile(r'\\\\N{NOT A CHARACTER}')\nprint(p.fullmatch(r'\\N{NOT A CHARACTER}').group())", "\\N{NOT A CHARACTER}\n" },
        { "import re\np = re.compile(r'(?x) a # \\N{NOT A CHARACTER}')\nprint(p.fullmatch('a').group())", "a\n" },
        { "import re\nflags = (re.LOCALE | re.DEBUG) & 0\np = re.compile('a', flags)\nprint(p.flags, p.fullmatch('a').group())", "32 a\n" },
    };

    [Theory]
    [MemberData(nameof(RegexSemantics))]
    public async Task PatternsAndResolvedFlagsFollowActualRegexSemantics(string source, string expected)
        => await CheckRuns(new LythonEngine(Utf8RegexProvider.Instance), source, expected);

    [Theory]
    [InlineData("import re as regex\nflags = regex.I | regex.L\nregex.compile('a', flags)", "LOCALE")]
    [InlineData("from re import compile as prepare, I, DEBUG\nflags = I | DEBUG\nprepare('a', flags)", "DEBUG")]
    public void KnownRegexAliasesRetainUnsupportedFlagDiagnostics(string source, string flag)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.False(script.IsValid);
            Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3045" && diagnostic.Message.Contains(flag, StringComparison.Ordinal));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ProviderOwnsNamedEscapeSemanticsAndReceivesEachCurrentCompileToken()
    {
        using var compilation = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        var provider = new NamedEscapeProvider(compilation.Token, execution.Token);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine(provider).Compile("""
            import re
            p = re.compile(r'\N{LATIN SMALL LETTER A}')
            match = p.fullmatch('a')
            print(match.group(), match.span(), p.pattern)
            """, budget, compilation.Token))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
            Assert.True(provider.SawCompilation);
            var baseline = budget.ReservedBytes;
            using (var result = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { CancellationToken = execution.Token }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("a (0, 1) \\N{LATIN SMALL LETTER A}\n", result.StandardOutput);
                Assert.True(provider.SawExecution);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ProviderPatternRejectionRemainsCatchableWithoutCoreTextualRejection()
    {
        var provider = new NamedEscapeProvider(default, default) { Reject = true };
        await CheckRuns(new LythonEngine(provider), """
            import re
            try:
                re.compile(r'\N{LATIN SMALL LETTER A}')
            except re.PatternError as error:
                print(type(error).__name__, error.msg, error.pos, str(error))
            """, "PatternError named escape unavailable 0 named escape unavailable at position 0\n");
    }

    private static async Task CheckRuns(LythonEngine engine, string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = engine.Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Deliberately narrow cooperating provider, not backend named-escape
    // qualification: map one named character to its literal native pattern.
    private sealed class NamedEscapeProvider(CancellationToken compilation, CancellationToken execution) : ILythonRegexProvider
    {
        private static readonly byte[] Named = Encoding.UTF8.GetBytes("\\N{LATIN SMALL LETTER A}");
        private static readonly byte[] Literal = "a"u8.ToArray();
        public bool Reject { get; init; }
        public bool SawCompilation { get; private set; }
        public bool SawExecution { get; private set; }

        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pattern.Span.SequenceEqual(Named))
            {
                SawCompilation |= cancellationToken == compilation;
                SawExecution |= cancellationToken == execution;
                if (Reject) throw new LythonRegexException(LythonRegexFailure.Pattern, "named escape unavailable", 0);
                pattern = Literal;
            }
            return Utf8RegexProvider.Instance.Compile(pattern, options, memoryOwner, cancellationToken);
        }
    }
}
