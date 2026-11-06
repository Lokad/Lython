using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexCaptureMaterializationTests
{
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
    public async Task GroupAccessRefusalIsTerminalAndReturnsWorkingAllowance(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CompetingProvider(budget);
        using var script = new LythonEngine(provider).Compile("""
            import re
            m = re.search(pattern, text)
            positions = m.regs
            try:
                m.group(1)
            except MemoryError:
                print('must not run')
            finally:
                return 'must fail'
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var body = new string('é', 8192);
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["pattern"] = "!(?P<body>.+)!", ["text"] = "!" + body + "!" }
        };
        provider.Compete = true;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(provider.SubjectClosed);
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(5, result.Failure?.Span?.Line);
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

    // Model another host user claiming the same allowance after native matching
    // has closed. Match metadata fits; a later substantial group copy does not.
    private sealed class CompetingProvider(LythonMemoryBudget budget) : ILythonRegexProvider, IDisposable
    {
        private ILythonMemoryReservation? _held;
        public bool Compete, SubjectClosed;
        public long HeldBytes => _held?.Bytes ?? 0;
        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
            ILythonMemoryBudget owner, CancellationToken token)
            => new Pattern(this, Utf8RegexProvider.Instance.Compile(pattern, options, owner, token));
        private void Closed()
        {
            SubjectClosed = true;
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
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> input, LythonRegexWindow window,
                ILythonMemoryBudget memoryOwner, CancellationToken token)
                => new Subject(owner, inner.Prepare(input, window, memoryOwner, token));
            public void Dispose() => inner.Dispose();
        }
        private sealed class Subject(CompetingProvider owner, ILythonRegexSubject inner) : ILythonRegexSubject
        {
            private bool _disposed;
            public bool TryMatch(LythonRegexMatchMode mode, Span<LythonRegexCapture?> captures, out int? lastIndex,
                CancellationToken token) => inner.TryMatch(mode, captures, out lastIndex, token);
            public ILythonRegexCursor Find(CancellationToken token) => inner.Find(token);
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                inner.Dispose();
                owner.Closed();
            }
        }
    }
}
