using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexCaptureMaterializationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubstitutionRetainsASoleWholeSubjectReplacement(bool asynchronous)
    {
        const string source = """
            import re
            text = ''.join(['alpha-', 'café😀-omega'])
            literal = ''.join(['new-', 'café😀-text'])
            print(re.sub(r'^.*$', literal, text) is literal)
            print(re.sub(r'^.*$', lambda m: m.string, text) is text)
            result, count = re.subn(r'^.*$', lambda m: literal, text)
            print(result is literal, count)
            one = re.sub(r'.*', lambda m: literal, text, count=1)
            many = re.sub(r'.*', lambda m: literal, text)
            print(one is literal, many == literal * 2, many is literal)
            unchanged = re.sub(r'$', '', text)
            print(unchanged == text, unchanged is text)
            expanded = re.fullmatch(r'.+', text).expand(literal)
            print(expanded is literal, expanded == literal)
            """;
        const string expected = "True\nTrue\nTrue 1\nTrue True False\nTrue False\nFalse True\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindAllAndSplitRetainWholeSubjectCaptures(bool asynchronous)
    {
        const string source = """
            import re
            text = ''.join(['prefix-', 'café😀-suffix'])
            plain, single, named, multiple = [re.findall(pattern, text)
                for pattern in (r'.+', r'(.+)', r'(?P<all>.+)', r'(.+)()')]
            split = re.split(r'(?=(.*))', text, maxsplit=1)
            print(plain[0] is text, single[0] is text, named[0] is text, multiple[0][0] is text)
            print(split[1] is text, split[2] is text, multiple[0][1] == '')
            print(re.compile(r'(.+)').findall(text, 7, 12), re.split(r'(-)|(x)', text, maxsplit=1))
            """;
        // CPython 3.13.2 output.
        const string expected = "True True True True\nTrue True True\n['café😀'] ['prefi', None, 'x', '-café😀-suffix']\n";
        var allowance = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, allowance);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = allowance.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, allowance.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedRangesSurviveAdvancesDisposalAndPurge(bool asynchronous)
    {
        const string source = """
            import re
            source = "éab😀 βab"
            pattern = re.compile(r"(?P<head>ab)(?P<empty>)|(?P<tail>β)")
            matches = list(pattern.finditer(source))
            re.purge()
            assert all(m.string is source for m in matches)
            regs = matches[0].regs
            assert matches[0].regs is regs
            assert matches[1].regs is not regs
            marker = []
            assert matches[0].groups(marker)[2] is marker
            assert matches[1].groupdict(marker)['head'] is marker
            prefix = re.compile(r"(?<=(é))ab").search(source, 1, 3)
            empty = re.match(r"(?P<missing>a)?(?P<empty>)", "")
            full = re.fullmatch(r"(.+)", source)
            assert full.group() is source and full.group(1) is source
            result = [(m.span(), m.groups(), m.lastindex, m.lastgroup, m.regs) for m in matches]
            result += [(prefix.span(1), prefix.group(1), prefix.pos, prefix.endpos),
                       (empty.groups(), empty.regs, empty.lastindex, empty.lastgroup),
                       (matches[0].group(2, 1, 3), matches[0].expand(r"\g<head>|\g<empty>|\g<tail>"))]
            print(repr(result))
            """;
        // Independent CPython 3.13 output, including unmatched/empty captures,
        // scalar coordinates, prefix lookbehind and retained original subjects.
        const string expected = "[((1, 3), ('ab', '', None), 2, 'empty', ((1, 3), (1, 3), (3, 3), (-1, -1))), ((5, 6), (None, None, 'β'), 3, 'tail', ((5, 6), (-1, -1), (-1, -1), (5, 6))), ((6, 8), ('ab', '', None), 2, 'empty', ((6, 8), (6, 8), (8, 8), (-1, -1))), ((0, 1), 'é', 1, 3), ((None, ''), ((0, 0), (-1, -1), (0, 0)), 2, 'empty'), (('', 'ab', None), 'ab||')]\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandOwnedCaptureReusesFundedPayloadAfterMatchCleanup(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CompetingProvider(budget);
        using var script = new LythonEngine(provider).Compile("""
            import re
            def read_capture():
                m = re.search(pattern, text)
                re.purge()
                return m.group(1)
            capture = read_capture()
            assert capture is not text
            assert capture.startswith('éé') and capture.endswith('éé')
            return len(capture)
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["pattern"] = "!(?P<body>.+)!", ["text"] = $"!{new string('é', 8192)}!" }
        };
        provider.Compete = true;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(provider.MatchCompleted);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(8192), result.ReturnValue);
        }
        Assert.Equal(baseline + provider.HeldBytes, budget.ReservedBytes);
        provider.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WideSingleMatchRetainsScalarsAndOriginalSubject(bool asynchronous)
    {
        const string source = """
            import re
            subject = 'é' + '😀' * 40 + 'tail'
            pattern = re.compile('(?<=(é))' + '(😀)' * 40 + '(z)?')
            match = pattern.match(subject, 1, 41)
            re.purge()
            print(match.regs[0], match.regs[1], match.regs[41], match.regs[-1])
            print(match.lastindex, match.string is subject, match.group(1), match.group(41))
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(1, 41) (0, 1) (40, 41) (-1, -1)\n41 True é 😀\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task GroupAccessRefusalIsTerminalAndReturnsWorkingAllowance(bool asynchronous, bool exposed)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CompetingProvider(budget);
        var body = new string('é', 8192);
        using var script = new LythonEngine(provider).Compile("""
            import re
            m = re.search(pattern, SUBJECT)
            positions = m.regs
            selected = m.group
            try:
                CALL
            except MemoryError:
                print('must not run')
            finally:
                return 'must fail'
            """.Replace("SUBJECT", $"'!{body}!'", StringComparison.Ordinal).Replace("CALL", exposed ? "selected(1)" : "m.group(1)", StringComparison.Ordinal), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["pattern"] = "!(?P<body>.+)!" }
        };
        provider.Compete = true;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(provider.MatchCompleted);
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(6, result.Failure?.Span?.Line);
            Assert.True(result.DeniedReservationBytes >= Encoding.UTF8.GetByteCount(body));
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline + provider.HeldBytes, budget.ReservedBytes);
        provider.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegisterProjectionRefusalUnwindsAndRetainedRegistersStayFunded(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CompetingProvider(budget);
        using var script = new LythonEngine(provider).Compile("""
            import re
            m = re.search(pattern, text)
            try:
                registers = m.regs
            except MemoryError:
                print('must not run')
                return None
            assert m.regs is registers
            return registers
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?>
            {
                ["pattern"] = string.Concat(Enumerable.Repeat("(é)", 64)) + "(z)?",
                ["text"] = new string('é', 64),
            },
        };
        var host = new MockLythonHost();
        provider.Compete = true;
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(provider.MatchCompleted);
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(4, result.Failure?.Span?.Line);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline + provider.HeldBytes, budget.ReservedBytes);
        provider.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            var registers = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
            Assert.Equal(66, registers.Count);
            Assert.Equal(new object[] { System.Numerics.BigInteger.MinusOne, System.Numerics.BigInteger.MinusOne },
                Assert.IsAssignableFrom<IReadOnlyList<object?>>(registers[^1]));
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    // Model another host user claiming the same allowance after native matching
    // has closed. Match metadata fits and larger projections can be refused.
    private sealed class CompetingProvider(LythonMemoryBudget budget) : ILythonRegexProvider, IDisposable
    {
        private ILythonMemoryReservation? _held;
        public bool Compete, MatchCompleted;
        public long HeldBytes => _held?.Bytes ?? 0;
        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
            ILythonMemoryBudget owner, CancellationToken token)
            => new Pattern(this, Utf8RegexProvider.Instance.Compile(pattern, options, owner, token));
        private void Closed()
        {
            MatchCompleted = true;
            if (!Compete) return;
            Compete = false;
            var bytes = budget.LimitBytes!.Value - budget.ReservedBytes - 8192;
            Assert.True(bytes > 0);
            Assert.True(budget.TryReserve(bytes, out _held));
        }
        public void Dispose() { Compete = false; _held?.Dispose(); _held = null; }
        private sealed class Pattern(CompetingProvider owner, ILythonRegexPattern inner) : ILythonRegexPattern
        {
            public int Flags => inner.Flags;
            public int CaptureSlotCount => inner.CaptureSlotCount;
            public IReadOnlyList<LythonRegexNamedGroup> NamedGroups => inner.NamedGroups;
            public bool TryMatch(ReadOnlyMemory<byte> input, LythonRegexWindow window, LythonRegexMatchMode mode,
                Span<LythonRegexCapture?> captures, out int? lastIndex, ILythonMemoryBudget memoryOwner, CancellationToken token)
            {
                var matched = inner.TryMatch(input, window, mode, captures, out lastIndex, memoryOwner, token);
                owner.Closed();
                return matched;
            }
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> input, LythonRegexWindow window,
                ILythonMemoryBudget memoryOwner, CancellationToken token)
                => inner.Prepare(input, window, memoryOwner, token);
            public void Dispose() => inner.Dispose();
        }
    }
}
