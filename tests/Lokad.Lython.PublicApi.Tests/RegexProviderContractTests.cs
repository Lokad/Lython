using System.Buffers;
using System.Collections;
using System.Numerics;
using System.Text;
using System.Runtime.InteropServices;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexProviderContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompiledPatternEqualityUsesProgramAndSourceWithSameEffectiveFlags(bool asynchronous)
    {
        using var provider = new CooperatingProvider
        {
            ProgramKeyFactory = (source, options) => options.Verbose || source.Span.SequenceEqual("a"u8)
                ? "literal a"u8.ToArray() : "literal space a space"u8.ToArray()
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile("""
            import re
            class Flag:
                def __init__(self, verbose): self.verbose = verbose
                def __and__(self, other): return 64 if other == 64 and self.verbose else 0
                def __or__(self, other): return 32
                def __ror__(self, other): return 32
            p = re.compile('a', Flag(False))
            q = re.compile('a', Flag(True))
            r = re.compile(' a ', Flag(False))
            s = re.compile(' a ', Flag(True))
            keys = {p: 'same', q: 'value', r: 'spaced', s: 'trimmed'}
            re.purge()
            t = re.compile('a', Flag(False))
            print(p == q, r == s, p == s, [p] == [q], p is t)
            print(hash(p) == hash(q), len(keys), len({t,p,q,r,s}), keys[t], keys[r], keys[s])
            """, budget);
        Assert.True(script.IsValid, string.Join(", ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True False False True False\nTrue 3 3 value spaced trimmed\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ProgramEqualityHonorsCancellationOrReportsUnavailableKey(bool asynchronous, bool provideKey)
    {
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider();
        if (provideKey)
        {
            var memory = new CancellingProgramKey("literal a"u8.ToArray(), cancellation);
            provider.ProgramKeyFactory = (_, options) => options.Verbose
                ? memory.Borrow() : "literal a"u8.ToArray();
        }

        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(provider).Compile("""
            import re
            class Flag:
                def __and__(self, other): return 64 if other == 64 else 0
                def __or__(self, other): return 32
                def __ror__(self, other): return 32
            a = re.compile(pattern)
            b = re.compile(pattern, Flag())
            print({a: 1}[b])
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["pattern"] = "a" },
            CancellationToken = cancellation.Token
        };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options, cancellation.Token)
            : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(provideKey ? LythonFailureReason.Cancelled : LythonFailureReason.HostCapabilityUnavailable,
                result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(provideKey, cancellation.IsCancellationRequested);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Cancel after the provider returns the borrowed range but before the
    // first comparison chunk, exercising the active dictionary-probe context.
    private sealed class CancellingProgramKey(byte[] bytes, CancellationTokenSource cancellation) : MemoryManager<byte>
    {
        internal ReadOnlyMemory<byte> Borrow() => CreateMemory(bytes.Length);
        public override Span<byte> GetSpan()
        {
            cancellation.Cancel();
            return bytes;
        }
        public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();
        public override void Unpin() { }
        protected override void Dispose(bool disposing) { }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConstructedSubjectsStayValidAndStableAfterGuestRebinding(bool asynchronous)
    {
        const string payload = "x😀é\r\n\0";
        string[] expected = [payload, payload, "[" + payload + "]", payload.ToUpperInvariant(), payload, payload];
        var visited = new List<string>();
        using var provider = new CooperatingProvider
        {
            OnPrepare = (subject, _) => Assert.True(System.Text.Unicode.Utf8.IsValid(subject.Span)),
            MatchFactory = (subject, _) =>
            {
                Assert.True(System.Text.Unicode.Utf8.IsValid(subject.Span));
                visited.Add(Encoding.UTF8.GetString(subject.Span));
                return new([new LythonRegexCapture(0, subject.Length)], null);
            },
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(provider).Compile("""
            import json, os, re
            texts = [payload, payload.encode().decode(), f'[{payload}]', payload.upper(),
                     json.loads(json.dumps(payload)), os.fsdecode(payload.encode())]
            result = []
            for text in texts:
                cursor = re.finditer('.*', text)
                text = 'rebound'
                result.extend(match.group() for match in cursor)
            return result
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["payload"] = payload } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, Assert.IsAssignableFrom<IList>(result.ReturnValue).Cast<string>());
                Assert.Equal(expected, visited);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecodedSubjectsAreValidUtf8AndIndependentOfMutableHostInput(bool asynchronous)
    {
        var expected = Encoding.UTF8.GetBytes("x😀\r\n\0é");
        var input = expected.ToArray();
        var prepared = false;
        using var provider = new CooperatingProvider
        {
            OnPrepare = (subject, _) =>
            {
                Assert.True(System.Text.Unicode.Utf8.IsValid(subject.Span));
                Assert.True(subject.Span.SequenceEqual(expected));
                Array.Fill(input, (byte)0xff);
                Assert.True(subject.Span.SequenceEqual(expected));
                prepared = true;
            },
            MatchFactory = (_, _) => new([new LythonRegexCapture(0, 1)], null),
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(provider).Compile(
            "import os,re\ntext=os.fsdecode(data)\nreturn re.search('x',text).group()", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["data"] = input } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("x", result.ReturnValue);
                Assert.True(prepared);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidatedProgramsStayFundedAcrossRunsPurgeAndIndependentTokens(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compilation = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        using var provider = new CooperatingProvider { ExpectedToken = compilation.Token };
        using (var script = new LythonEngine(provider).Compile("""
            import re
            first = re.compile('x')
            assert re.compile('x') is first
            re.purge()
            second = re.compile('x')
            assert second is not first
            return second.match(subject).group()
            """, budget, compilation.Token))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(0, provider.ClosedPatterns);
            var baseline = budget.ReservedBytes;
            compilation.Cancel();
            provider.ExpectedToken = execution.Token;
            for (var run = 0; run < 2; run++)
            {
                var options = new LythonRunOptions { CancellationToken = execution.Token,
                    Globals = new Dictionary<string, object?> { ["subject"] = "x" } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("x", result.ReturnValue);
                }
                Assert.Equal(1, provider.Compilations);
                Assert.Equal(0, provider.ClosedPatterns);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedPatternWrapperDoesNotCloseTheCompiledProgramsOwner()
    {
        var budget = new AdmissionGateBudget();
        using var provider = new CooperatingProvider();
        using (var script = new LythonEngine(provider).Compile(
            "import re,time\ncompile=re.compile\ntime.time()\nreturn compile('x').pattern", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.Root.ReservedBytes;
            using (var denied = script.Run(new AdmissionGateHost(budget, 0)))
            {
                Assert.False(denied.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, denied.FailureAttribution?.Reason);
                Assert.True(denied.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(0, provider.ClosedPatterns);
            budget.Gate(1024 * 1024);
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("x", result.ReturnValue);
            }
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(0, provider.ClosedPatterns);
        }
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Fact]
    public async Task CachedProgramCancellationAwaitsWorkingCleanupAndLeavesTheProgramReusable()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider();
        using var script = new LythonEngine(provider).Compile("import re\nreturn re.search('x', subject).group()", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        provider.BlockStage = "match";
        provider.HoldCleanup = true;
        provider.ExpectedToken = cancellation.Token;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["subject"] = "x" } };
        var pending = script.RunAsync(new MockLythonHost(), options, cancellation.Token);
        try
        {
            await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            Assert.Equal(0, provider.ClosedPatterns);
            provider.ReleaseCleanup.Set();
            using var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(cancelled.Success);
            Assert.Equal(LythonFailureReason.Cancelled, cancelled.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            provider.ReleaseCleanup.Set();
            using var drained = await pending;
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        provider.BlockStage = null;
        provider.HoldCleanup = false;
        provider.ExpectedToken = default(CancellationToken);
        using (var next = script.Run(new MockLythonHost(), options))
        {
            Assert.True(next.Success, next.Failure?.Message);
            Assert.Equal("x", next.ReturnValue);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(1, provider.Compilations);
        Assert.Equal(0, provider.ClosedPatterns);
        script.Dispose();
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompiledProgramsReuseEqualPatternsAndOwnDistinctOptionsUntilDisposal()
    {
        const string source = """
            import re
            exact = re.compile('x')
            same = re.compile('\x78')
            folded = re.compile('x', re.I)
            explicit = re.compile('x', re.UNICODE)
            return exact is same, exact.fullmatch(text) is None, folded.fullmatch(text).group(0), exact is explicit, exact == explicit
            """;
        var compiledOptions = new List<LythonRegexOptions>();
        using var provider = new CooperatingProvider
        {
            ProgramKeyFactory = (_, options) => { compiledOptions.Add(options); return null; },
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        Assert.Contains(new LythonRegexOptions(32, false) { ImplicitUnicode = true }, compiledOptions);
        Assert.Contains(new LythonRegexOptions(32, false), compiledOptions);
        Assert.Equal(3, provider.Compilations);
        Assert.Equal(3, provider.CaptureSlotReads);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = "X" } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object?[] { true, true, "X", false, true }, Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(3, provider.Compilations);
        Assert.Equal(3, provider.CaptureSlotReads);
        Assert.Equal(0, provider.ClosedPatterns);
        script.Dispose();
        Assert.Equal(3, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(4, 36)]
    [InlineData(288, 288)]
    public async Task ExactStringModeConflictsReachTheProviderBeforeErrorClassification(int flags, int expected)
    {
        using var provider = new CooperatingProvider
        {
            OnCompile = (pattern, options) =>
            {
                Assert.Equal(expected, options.Flags);
                if (pattern.Span.SequenceEqual("["u8))
                    throw new LythonRegexException(LythonRegexFailure.Pattern, "unterminated character set", 0);
                throw new LythonRegexException(LythonRegexFailure.Flags, "invalid string-pattern mode");
            },
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile("""
            import re
            for pattern in ['a', '[']:
                try:
                    re.compile(pattern, flags)
                except (ValueError, re.error) as error:
                    print(type(error).__name__, str(error))
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var run = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["flags"] = flags } };
        using (var result = await script.RunAsync(new MockLythonHost(), run))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ValueError invalid string-pattern mode\nPatternError unterminated character set at position 0\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RejectedConstantAnalysisRefundsScratchWhileKeepingValidProgram()
    {
        const string source = """
            import re
            p = re.compile('x')
            try:
                rejected_pattern = re.compile('[')
            except re.error as error:
                print(type(error).__name__, error.pattern, error.pos)
            return p.fullmatch(text).group()
            """;
        using var provider = new CooperatingProvider();
        var rejected = 0;
        provider.OnCompile = (pattern, _) =>
        {
            if (!pattern.Span.SequenceEqual("["u8)) return;
            rejected++;
            throw new LythonRegexException(LythonRegexFailure.Pattern, "unterminated character set", 0);
        };
        var budget = new LythonMemoryBudget(1024 * 1024);
        long validStorage;
        using (var control = new LythonEngine(provider).Compile(source.Replace("'['", "'x'"), budget))
        {
            Assert.True(control.IsValid);
            validStorage = budget.ReservedBytes;
        }
        Assert.Equal(0, budget.ReservedBytes);

        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(item => item.Message)));
        Assert.True(rejected > 0);
        Assert.Equal(validStorage, budget.ReservedBytes);

        var beforeRun = rejected;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = "x" } };
        using (var run = await script.RunAsync(new MockLythonHost(), options))
        {
            Assert.True(run.Success, run.Failure?.Message);
            Assert.Equal("PatternError [ 0\n", run.StandardOutput);
            Assert.Equal("x", run.ReturnValue);
        }
        Assert.Equal(beforeRun + 1, rejected);
        Assert.Equal(validStorage, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(2, provider.Compilations);
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ActualAdapterSharesValidatedProgramsAcrossConcurrentRunsAndRetainedResults()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var compilation = new CancellationTokenSource();
        using var execution = new CancellationTokenSource();
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import re\nreturn re.fullmatch('(?P<é>.)', text).groupdict()", budget, compilation.Token);
        Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        compilation.Cancel();
        var runs = Enumerable.Range(0, 8).Select(_ => script.RunAsync(new MockLythonHost(), new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["text"] = "😀" },
            CancellationToken = execution.Token
        })).ToArray();
        try
        {
            var results = await Task.WhenAll(runs);
            Assert.All(results, result => Assert.True(result.Success, result.Failure?.Message));
            Assert.True(budget.ReservedBytes > baseline);
            script.Dispose();
            foreach (var result in results)
            {
                var groups = Assert.IsAssignableFrom<IDictionary<object, object?>>(result.ReturnValue);
                Assert.Equal("😀", groups["é"]);
            }
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            execution.Cancel();
            try { await Task.WhenAll(runs); }
            finally
            {
                foreach (var run in runs)
                    if (run.IsCompletedSuccessfully) (await run).Dispose();
                script.Dispose();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile", "Regex pattern compilation")]
    [InlineData("prepare", "Regex constant-subject preparation")]
    [InlineData("match", "Regex constant matching")]
    public async Task FrontendAdmissionDiagnosticsPreserveProviderOperationAndExplanation(string stage, string operation)
    {
        const string source = "import re\nvalue = re.search('x', 'x')\nprint('must not run')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider { RefuseStage = stage };
        var engine = new LythonEngine(provider);
        LythonDiagnostic expected;
        using (var script = engine.Compile(source, budget))
        {
            Assert.False(script.IsValid);
            expected = Assert.Single(script.Diagnostics);
            Assert.Equal("LA0008", expected.Code);
            Assert.Contains(operation, expected.Message);
            Assert.Contains("provider scratch admission refused", expected.Message);
            Assert.Equal(2, expected.Span?.Line);
            Assert.True(expected.Span?.Length > 0);
            foreach (var asynchronous in new[] { false, true })
            {
                using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
                CheckFailure(result);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = engine.Run(source, new MockLythonHost(), options)) CheckFailure(result);
        Assert.Equal(0, budget.ReservedBytes);
        using (var result = await engine.RunAsync(source, new MockLythonHost(), options)) CheckFailure(result);
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
        using (var script = engine.Compile("return 42", budget))
        using (var result = script.Run(new MockLythonHost())) Assert.True(result.Success);
        Assert.Equal(0, budget.ReservedBytes);

        void CheckFailure(LythonExecutionResult result)
        {
            Assert.False(result.Success);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(long.MaxValue, result.FailureAttribution?.Requested);
            Assert.Equal(expected, Assert.Single(result.Diagnostics));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchingOnlyProviderSupportsNativeSplitSubstituteAndExpand(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider
        {
            CaptureSlots = 3,
            GroupNames = [new("é"u8.ToArray(), 1)],
            MatchFactory = (_, _) => new(new LythonRegexCapture?[] { new(0, 4), new(0, 4), null }, 1)
        };
        using (var script = new LythonEngine(provider).Compile("""
            import re
            p=re.compile(pattern)
            print(p.split(text))
            print(p.sub(r'<\g<é>:\2>',text))
            print(p.subn(r'<\g<é>:\2>',text))
            return p.match(text).expand(r'<\g<é>:\2>')
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = "stub", ["text"] = "😀x" } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("['', '😀', None, 'x']\n<😀:>x\n('<😀:>x', 1)\n", result.StandardOutput);
                Assert.Equal("<😀:>", result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(provider.Compilations, provider.ClosedPatterns);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FindAllCaptureProjectionClosesProviderWorkOnRefusalAndCancellation(bool asynchronous)
    {
        var budget = new AdmissionGateBudget();
        using var provider = new CooperatingProvider { CaptureSlots = 3 };
        var match = new CooperatingProvider.FixtureMatch([new(0, 4), new(0, 4), null], 1);
        using var script = new LythonEngine(provider).Compile("""
            import re
            found = re.findall(pattern, text)
            print(found)
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.Root.ReservedBytes;
        var globals = new Dictionary<string, object?> { ["pattern"] = "(.)(z)?", ["text"] = "😀x" };
        foreach (var cancel in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var matches = 0;
            provider.CursorDisposed = false;
            provider.CleanupFinished = false;
            provider.MatchFactory = (_, _) =>
            {
                matches++;
                // The next growth is Lython's projection of the already
                // returned capture ranges, while the provider cursor is live.
                if (cancel) budget.OnNextResizeGrowth = cancellation.Cancel;
                else budget.Gate(0);
                return match;
            };
            var options = new LythonRunOptions { Globals = globals, CancellationToken = cancellation.Token };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.Null(result.ReturnValue);
            }
            Assert.Equal(1, matches);
            Assert.True(provider.CursorDisposed && provider.CleanupFinished);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            budget.Gate(1024 * 1024);
        }

        provider.MatchFactory = (_, _) => match;
        var recoveryOptions = new LythonRunOptions { Globals = globals };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), recoveryOptions)
            : script.Run(new MockLythonHost(), recoveryOptions))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[('😀', '')]\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeGroupNamesSurviveBorrowedMetadataDisposalAndUsePythonOrder(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider
        {
            CaptureSlots = 3,
            GroupNames = [new("é"u8.ToArray(), 1), new("tail"u8.ToArray(), 2)],
            MatchFactory = (_, _) => new(new LythonRegexCapture?[] { new(0, 4), new(0, 4), new(4, 4) }, 1)
        };
        using (var script = new LythonEngine(provider).Compile("""
            import re
            p=re.compile('(?P<é>.(?P<tail>))')
            m=p.match('😀')
            names=p.groupindex
            assert names is not p.groupindex
            assert type(names).__name__ == 'mappingproxy'
            try:
                names['é'] = 3
            except TypeError:
                pass
            else:
                raise AssertionError('groupindex allowed mutation')
            re.purge()
            assert list(names) == ['é', 'tail']
            assert names['é'] is p.groupindex['é']
            assert m.lastgroup is next(iter(names))
            assert repr(names) == "mappingproxy({'é': 1, 'tail': 2})"
            print(names,m.groupdict(),m.lastgroup,m.span('é'))
            return m.expand(r'<\g<é>:\g<tail>>')
            """, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            Assert.True(provider.Compilations > 0);
            var compilations = provider.Compilations;
            Assert.Equal(0, provider.ClosedPatterns);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("{'é': 1, 'tail': 2} {'é': '😀', 'tail': ''} é (0, 1)\n", result.StandardOutput);
                Assert.Equal("<😀:>", result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(compilations, provider.Compilations);
                Assert.Equal(0, provider.ClosedPatterns);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CompileOnlyGroupFactsReleaseTheirStorageBeforeTheProgramDisposes()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var retained = new List<long>();
        foreach (var name in new[] { "x", new string('x', 128) })
        {
            using var provider = new CooperatingProvider
            {
                CaptureSlots = 2,
                GroupNames = [new(Encoding.UTF8.GetBytes(name), 1)]
            };
            using (var script = new LythonEngine(provider).Compile("import re\np=re.compile('x')", budget))
            {
                Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
                Assert.Equal(1, provider.Compilations);
                Assert.Equal(0, provider.ClosedPatterns);
                retained.Add(budget.ReservedBytes);
            }
            Assert.Equal(1, provider.ClosedPatterns);
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(retained[0], retained[1]);
    }

    [Fact]
    public void PatternWithoutNamedGroupsReturnsIndependentEmptyDictionaries()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        using (var script = new LythonEngine(provider).Compile("""
            import re
            p = re.compile('x')
            names = p.groupindex
            assert type(names) is dict
            assert names is not p.groupindex
            names['new'] = 1
            assert p.groupindex == {}
            """, budget))
        {
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
                Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedGroupIndexViewLeavesTheCompiledPatternOwnerUsable()
    {
        var budget = new AdmissionGateBudget();
        using var provider = new CooperatingProvider
        {
            CaptureSlots = 2,
            GroupNames = [new("name"u8.ToArray(), 1)]
        };
        using (var script = new LythonEngine(provider).Compile("""
            import re,time
            p=re.compile('(?P<name>x)')
            time.time()
            return p.groupindex['name']
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.Root.ReservedBytes;
            using (var refused = script.Run(new AdmissionGateHost(budget, 0)))
            {
                Assert.False(refused.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, refused.FailureAttribution?.Reason);
                Assert.True(refused.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            Assert.Equal(0, provider.ClosedPatterns);

            budget.Gate(1024 * 1024);
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(BigInteger.One, result.ReturnValue);
            }
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            Assert.Equal(1, provider.Compilations);
        }
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("outside")]
    [InlineData("order")]
    [InlineData("duplicate")]
    [InlineData("utf8")]
    [InlineData("empty")]
    public async Task InvalidNativeGroupMetadataFailsFrontendAndRuntimeWithoutLeaking(string kind)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider
        {
            CaptureSlots = 3,
            GroupNames = kind switch
            {
                "zero" => [new("x"u8.ToArray(), 0)],
                "outside" => [new("x"u8.ToArray(), 3)],
                "order" => [new("x"u8.ToArray(), 2), new("y"u8.ToArray(), 1)],
                "duplicate" => [new("x"u8.ToArray(), 1), new("x"u8.ToArray(), 2)],
                "utf8" => [new(new byte[] { 0xff }, 1)],
                _ => [new(ReadOnlyMemory<byte>.Empty, 1)]
            }
        };
        var error = Assert.ThrowsAny<Exception>(() => new LythonEngine(provider).Compile("import re\np=re.compile('x')", budget));
        Assert.Contains("Regex provider", error.Message);
        Assert.Equal(0, budget.ReservedBytes);
        using (var script = new LythonEngine(provider).Compile("import re\np=re.compile(pattern)\nprint('after')", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), Options(default))
                    : script.Run(new MockLythonHost(), Options(default)))
                {
                    Assert.False(result.Success);
                    Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(true, "inconsistent named-group metadata")]
    [InlineData(false, "Host regex provider failed")]
    public async Task BorrowedGroupListFailuresCloseThePattern(bool oversized, string message)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var entries = new FaultingGroupList(oversized ? int.MaxValue : 1);
        using var provider = new CooperatingProvider { CaptureSlots = 2, GroupListOverride = entries };
        var engine = new LythonEngine(provider);
        var error = Assert.ThrowsAny<Exception>(() => engine.Compile("import re\np=re.compile('x')", budget));
        Assert.Contains(message, error.Message);
        Assert.Equal(0, budget.ReservedBytes);

        using (var script = engine.Compile("import re\np=re.compile(pattern)\nprint('after')", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), Options(default))
                    : script.Run(new MockLythonHost(), Options(default)))
                {
                    Assert.False(result.Success);
                    Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                    Assert.Contains(message, result.Failure?.Message);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }

        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(provider.Compilations, entries.CountReads);
        Assert.Equal(oversized ? 0 : entries.CountReads, entries.IndexReads);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("prepare")]
    [InlineData("find")]
    public async Task MissingProviderHandleReportsTheContractErrorAndReturnsAllowance(string stage)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider { MissingHandleStage = stage };
        var engine = new LythonEngine(provider);
        if (stage == "compile")
        {
            var error = Assert.ThrowsAny<Exception>(() => engine.Compile("import re\np=re.compile('x')", budget));
            Assert.Contains("Regex pattern compilation returned no result", error.Message);
        }
        else if (stage == "prepare")
        {
            var error = Assert.ThrowsAny<Exception>(() => engine.Compile("import re\np=re.compile('x')\nm=p.match('x')\nreturn m", budget));
            Assert.Contains("Regex constant-subject preparation returned no result", error.Message);
        }
        Assert.Equal(0, budget.ReservedBytes);

        var match = stage == "find" ? "list(p.finditer(text))" : "p.match(text)";
        using (var script = engine.Compile($"import re\np=re.compile(pattern)\nresult={match}\nprint('after')", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), Options(default))
                    : script.Run(new MockLythonHost(), Options(default)))
                {
                    Assert.False(result.Success);
                    Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                    Assert.Contains("Regex provider returned no result", result.Failure?.Message);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class FaultingGroupList(int count) : IReadOnlyList<LythonRegexNamedGroup>
    {
        internal int CountReads, IndexReads;
        public int Count { get { CountReads++; return count; } }
        public LythonRegexNamedGroup this[int index]
        {
            get { IndexReads++; throw new InvalidOperationException("Group list index unavailable."); }
        }
        public IEnumerator<LythonRegexNamedGroup> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(0, 99)]
    [InlineData(2, 1)]
    [InlineData(-1, 2)]
    public async Task InvalidCaptureRangesFailBeforeProjectionAndReturnAllowance(int startByte, int endByte)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var provider = new CooperatingProvider
        {
            MatchFactory = (_, _) => new(new LythonRegexCapture?[] { new(startByte, endByte) }, null)
        };
        using (var script = new LythonEngine(provider).Compile("import re\nprint(re.match('x',text).group())", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = "éx" } };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.False(result.Success);
                    Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
                    Assert.Contains("capture ranges", result.Failure?.Message);
                    Assert.Empty(result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("search", 0, 4)]
    [InlineData("search", 4, 8)]
    [InlineData("match", 5, 5)]
    [InlineData("fullmatch", 4, 4)]
    public async Task WholeMatchMustUseTheRequestedWindow(string operation, int startByte, int endByte)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var provider = new CooperatingProvider
        {
            MatchFactory = (_, _) => new([new LythonRegexCapture(startByte, endByte)], null)
        };
        using var script = new LythonEngine(provider).Compile(
            $"import re\nreturn re.compile(pattern).{operation}(text, 1, 2)", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["pattern"] = "x", ["text"] = "😀x尾" },
        };
        using (var result = await script.RunAsync(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("outside its window", result.Failure?.Message);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("re.findall(pattern, text)", false)]
    [InlineData("re.split(pattern, text)", true)]
    [InlineData("re.sub(pattern, 'new', text)", false)]
    [InlineData("list(re.finditer(pattern, text))", false)]
    public async Task InvalidCursorProgressionClosesItsSubjectAndCursor(string expression, bool empty)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        var first = new LythonRegexCapture(0, empty ? 0 : 1);
        var second = new LythonRegexCapture(0, 0);
        using var provider = new CooperatingProvider { CursorMatches = [first, second] };
        using var script = new LythonEngine(provider).Compile($"import re\nreturn {expression}", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = await script.RunAsync(new MockLythonHost(), Options(default)))
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("cursor did not advance", result.Failure?.Message);
            Assert.True(provider.CursorDisposed);
            Assert.True(provider.CleanupFinished);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1, 2, 2, 5, false)]
    [InlineData(2, 1, 1, 4, false)]
    [InlineData(1, 99, 3, 8, false)]
    [InlineData(1, 2, 2, 5, true)]
    public async Task FrontendAndRuntimePassOriginalSubjectAndAbsoluteWindowCaptures(int pos, int end, int endPos, int endByte,
        bool longSubject)
    {
        var reversed = pos > end;
        var text = "😀x尾" + (longSubject ? new string('é', 4097) : "");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var compilation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { CaptureSlots = 2 };
        var preparations = 0;
        provider.OnPrepare = (input, window) =>
        {
            Assert.Equal(text, Encoding.UTF8.GetString(input.Span));
            Assert.Equal(new LythonRegexWindow(pos, endPos, reversed ? 5 : 4, endByte), window);
            preparations++;
        };
        provider.MatchFactory = (input, _) => reversed
            ? new(new LythonRegexCapture?[] { new(5, 5), new(5, 5) }, 1)
            : new(new LythonRegexCapture?[] { new(4, 5), new(0, 4) }, 1);
        using (var script = new LythonEngine(provider).Compile("import re\nm=re.compile('" +
            (reversed ? "()" : "(?<=(😀))x") + $"').match('{text}',{pos},{end})\n" +
            "print(m.span(),m.pos,m.endpos,m.string)\nprint(m.span(1),repr(m.group(1)))", budget, compilation.Token))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            Assert.True(preparations > 0);
            var frontendPreparations = preparations;
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(reversed ? $"(2, 2) 2 1 {text}\n(2, 2) ''\n"
                        : $"(1, 2) 1 {endPos} {text}\n(0, 1) '😀'\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.True(preparations > frontendPreparations);
            Assert.Equal(0, provider.ClosedPatterns);
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ReversedSearchAndFullmatchResolveBeforePreparingTheSubject()
    {
        const string source = """
            import re
            p = re.compile('')
            q = re.compile(pattern)
            print(p.search('😀x尾', 2, 1), p.fullmatch('😀x尾', 2, 1))
            print(q.search(subject, 2, 1), q.fullmatch(subject, 2, 1))
            print(p.search('😀x尾', 5, 4).span(), p.fullmatch('😀x尾', 5, 4).span())
            """;
        using var provider = new CooperatingProvider { CaptureSlots = 1 };
        var preparations = 0;
        provider.OnPrepare = (input, window) =>
        {
            Assert.Equal("😀x尾", Encoding.UTF8.GetString(input.Span));
            Assert.Equal(new LythonRegexWindow(3, 3, 8, 8), window);
            preparations++;
        };
        provider.MatchFactory = (_, _) => new(new LythonRegexCapture?[] { new(8, 8) }, null);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        Assert.True(preparations > 0);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["pattern"] = "", ["subject"] = "😀x尾" } };
        foreach (var asynchronous in new[] { false, true })
        {
            var beforeRun = preparations;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("None None\nNone None\n(3, 3) (3, 3)\n", result.StandardOutput);
                Assert.True(preparations > beforeRun);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("prepare")]
    [InlineData("find")]
    public async Task CancellationAsAWorkingResourceReturnsStillDisposesIt(string stage)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider
        {
            CancelOnReturnStage = stage, CancelOnReturn = cancellation, HoldCleanup = true, ExpectedToken = cancellation.Token,
        };
        using var script = new LythonEngine(provider).Compile("import re\nreturn list(re.finditer(pattern,text))", budget);
        var baseline = budget.ReservedBytes;
        var pending = script.RunAsync(new MockLythonHost(), Options(cancellation.Token));
        try
        {
            var first = await Task.WhenAny(provider.CleanupEntered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(provider.CleanupEntered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            Assert.Equal(0, provider.ClosedPatterns);
        }
        finally
        {
            var cancellationRequest = cancellation.CancelAsync();
            provider.ReleaseCleanup.Set();
            await cancellationRequest.WaitAsync(TimeSpan.FromSeconds(5));
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        if (stage == "find") Assert.True(provider.CursorDisposed);
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrontendProgramsCloseOnDiagnosticOrScriptDisposal(bool diagnostic)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        using (var script = new LythonEngine(provider).Compile("import re\np=re.compile('x')" +
            (diagnostic ? "\nreturn p.match('other').group()" : ""), budget))
        {
            Assert.Equal(!diagnostic, script.IsValid);
            Assert.True(provider.Compilations > 0);
            Assert.Equal(diagnostic ? provider.Compilations : 0, provider.ClosedPatterns);
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FrontendClosesAnUnpublishedPatternWhenCompilationReturnsCancelled()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider
        {
            CancelOnReturnStage = "compile", CancelOnReturn = cancellation,
            ExpectedToken = cancellation.Token,
        };
        using (var script = new LythonEngine(provider).Compile("import re\np=re.compile('x')", budget, cancellation.Token))
        {
            Assert.False(script.IsValid);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(1, provider.ClosedPatterns);
            using var result = script.Run(new MockLythonHost());
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FrontendCancelsDuringCaptureAdmissionAndClosesThePreparedSubject()
    {
        var budget = new AdmissionGateBudget();
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider
        {
            ExpectedToken = cancellation.Token,
            CaptureSlots = 12, // Large enough to require admitted capture storage.
            OnPrepare = (_, _) => budget.OnNextResizeGrowth = cancellation.Cancel,
        };
        var engine = new LythonEngine(provider);
        using (var script = engine.Compile("import re\np=re.compile('x')\nreturn p.match('x').group()", budget, cancellation.Token))
        {
            Assert.True(cancellation.IsCancellationRequested);
            Assert.False(script.IsValid);
            Assert.Equal("LA2011", Assert.Single(script.Diagnostics).Code);
            using var result = script.Run(new MockLythonHost());
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.True(provider.CleanupFinished);
            Assert.Equal(1, provider.ClosedPatterns);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Fact]
    public async Task PurgeAndEvictionKeepRetainedPatternsAndPendingCursorsAlive()
    {
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        using var script = new LythonEngine(provider).Compile("""
            import re,time
            p=re.compile(pattern)
            i=p.finditer(text)
            re.purge()
            for n in range(513):
                re.compile(str(n))
            time.sleep(1)
            return [next(i).group(),p.match(text).group()]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var pending = script.RunAsync(host, Options(default));
        try
        {
            var first = await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(timing.Entered.Task, first);
            Assert.Equal(514, provider.Compilations);
            Assert.Equal(0, provider.ClosedPatterns);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally { timing.Release.TrySetResult(); }
        using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(5)))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new object?[] { "x", "x" }, Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue));
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedPatternPublicationClosesItsProgramBeforeReturning(bool cacheStorage, bool asynchronous)
    {
        var budget = new AdmissionGateBudget();
        using var provider = new CooperatingProvider();
        if (cacheStorage) provider.OnMetadata = () => budget.Gate(0);
        else provider.OnCompileReturn = () => budget.Gate(0);
        using var script = new LythonEngine(provider).Compile("""
            import re
            try:
                re.compile(pattern)
            except MemoryError:
                print('must not run')
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.Root.ReservedBytes;
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), Options(default))
            : script.Run(new MockLythonHost(), Options(default)))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(1, provider.ClosedPatterns);
        }
        Assert.Equal(baseline, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ImportedFrontendAndRuntimeShareAProgramUntilRunCleanup(bool failedImport, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        var host = new MockLythonHost();
        var helper = "import re,sys\ndef value(text):\n return re.search('x',text,re.I).group()\n";
        if (failedImport) helper += "sys.modules['saved_value'] = value\nraise ValueError('failed import')\n";
        host.SeedFile("/helper.py", helper);
        var entry = "import re,sys\n" + (failedImport
            ? "try:\n import helper\nexcept ValueError:\n value = sys.modules['saved_value']\n"
            : "from helper import value\n");
        using var script = new LythonEngine(provider).Compile(entry + "first=value('X')\nre.purge()\nreturn [first,value('x')]", budget);
        var baseline = budget.ReservedBytes;
        for (var run = 1; run <= 2; run++)
        {
            using var cancellation = new CancellationTokenSource();
            provider.ExpectedToken = cancellation.Token;
            var options = new LythonRunOptions
            {
                AllowedLocalModules = new HashSet<string> { "helper" },
                CancellationToken = cancellation.Token,
            };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object?[] { "X", "x" }, Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue));
                Assert.Equal(run, provider.Compilations);
                Assert.Equal(run, provider.ClosedPatterns);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedLocalAnalysisClosesItsProgramBeforeGuestContinues(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        provider.OnCompile = (pattern, _) =>
        {
            if (pattern.Span.SequenceEqual("second"u8)) Assert.Equal(1, provider.ClosedPatterns);
        };
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import re\np=re.compile('x')\nm=p.search('x')\nm.group(3)");
        using var script = new LythonEngine(provider).Compile("""
            import re
            try:
                import helper
            except SyntaxError:
                return re.compile(pattern).pattern
            """, budget);
        var options = new LythonRunOptions
        {
            AllowedLocalModules = new HashSet<string> { "helper" },
            Globals = new Dictionary<string, object?> { ["pattern"] = "second" },
        };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("second", result.ReturnValue);
            Assert.Equal(2, provider.Compilations);
            Assert.Equal(2, provider.ClosedPatterns);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusedLocalModulePublicationClosesItsPreparedProgram()
    {
        var budget = new AdmissionGateBudget();
        using var provider = new CooperatingProvider { OnMetadata = () => budget.Gate(0) };
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import re\np=re.compile('x')");
        using var script = new LythonEngine(provider).Compile("""
            try:
                import helper
            except MemoryError:
                print('must not run')
            """, budget);
        var baseline = budget.Root.ReservedBytes;
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        using (var result = await script.RunAsync(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(1, provider.ClosedPatterns);
        }
        Assert.Equal(baseline, budget.Root.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Fact]
    public async Task CancellationDrainsTheImportedProgramCursorBeforeRefundingItsOwner()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider
        {
            BlockStage = "next", HoldCleanup = true, ExpectedToken = cancellation.Token,
        };
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import re\np=re.compile('x')\ndef rows(text):\n return p.finditer(text)");
        using var script = new LythonEngine(provider).Compile("from helper import rows\nreturn next(rows(text)).group()", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            AllowedLocalModules = new HashSet<string> { "helper" },
            Globals = new Dictionary<string, object?> { ["text"] = "x" },
            CancellationToken = cancellation.Token,
        };
        var pending = Task.Run(() => script.RunAsync(host, options));
        Task cancellationRequest = Task.CompletedTask;
        try
        {
            var first = await Task.WhenAny(provider.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(provider.Entered.Task, first);
            cancellationRequest = cancellation.CancelAsync();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(provider.CursorDisposed);
            Assert.False(pending.IsCompleted);
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(0, provider.ClosedPatterns);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            var cleanupCancellation = cancellation.CancelAsync();
            provider.ReleaseCleanup.Set();
            await Task.WhenAll(cancellationRequest, cleanupCancellation).WaitAsync(TimeSpan.FromSeconds(5));
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EngineMergesBothTokensBeforeFrontendProviderWork(bool externalCancellation)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var optionToken = new CancellationTokenSource();
        using var externalToken = new CancellationTokenSource();
        using var provider = new CooperatingProvider { BlockStage = "compile", HoldCleanup = true };
        var pending = Task.Run(() => new LythonEngine(provider).RunAsync("import re\np=re.compile('x')",
            new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget, CancellationToken = optionToken.Token }, externalToken.Token));
        Task cancellationRequest = Task.CompletedTask;
        try
        {
            var first = await Task.WhenAny(provider.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(provider.Entered.Task, first);
            cancellationRequest = (externalCancellation ? externalToken : optionToken).CancelAsync();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            var cleanupCancellation = optionToken.CancelAsync();
            provider.ReleaseCleanup.Set();
            await Task.WhenAll(cancellationRequest, cleanupCancellation).WaitAsync(TimeSpan.FromSeconds(5));
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReplacementCallbacksAwaitHostIoAndKeepProviderStorageUntilCleanup(bool compiledMethod, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { HoldCleanup = true, ExpectedToken = cancellation.Token };
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        var source = "import re,time\ndef rewrite(m):\n time.sleep(1)\n return m.group()\n" +
            (compiledMethod ? "p=re.compile(pattern)\nreturn p.subn(rewrite,text)" : "return re.sub(pattern,rewrite,text)");
        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var pending = script.RunAsync(host, Options(cancellation.Token));
        Task cancellationRequest = Task.CompletedTask;
        var reachedCleanup = false;
        try
        {
            var first = await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(timing.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            if (cancel) cancellationRequest = cancellation.CancelAsync();
            timing.Release.TrySetResult();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            reachedCleanup = true;
        }
        finally
        {
            if (!reachedCleanup) cancellationRequest = cancellation.CancelAsync();
            timing.Release.TrySetResult(); provider.ReleaseCleanup.Set();
            await cancellationRequest.WaitAsync(TimeSpan.FromSeconds(5));
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else if (compiledMethod) Assert.Equal(new object?[] { "x", new System.Numerics.BigInteger(1) }, Assert.IsAssignableFrom<IEnumerable<object?>>(result.ReturnValue));
            else Assert.Equal("x", result.ReturnValue);
        }
        Assert.True(provider.CleanupFinished);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task FrontendProviderCompilationUsesItsCurrentTokenAndFinishesCleanup()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { BlockStage = "compile", HoldCleanup = true, ExpectedToken = cancellation.Token };
        var pending = Task.Run(() => new LythonEngine(provider).Compile("import re\np=re.compile('x')", budget, cancellation.Token));
        LythonCompiledScript? script = null;
        Task cancellationRequest = Task.CompletedTask;
        try
        {
            var first = await Task.WhenAny(provider.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(provider.Entered.Task, first);
            cancellationRequest = cancellation.CancelAsync();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            cancellationRequest = cancellation.CancelAsync(); provider.ReleaseCleanup.Set();
            await cancellationRequest.WaitAsync(TimeSpan.FromSeconds(5));
            script = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        using (script)
        {
            Assert.False(script.IsValid);
            using var result = await script.RunAsync(new MockLythonHost());
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.True(provider.CleanupFinished);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancelledHostIoStillClosesAnAbandonedRegexCursorBeforeRefund()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { HoldCleanup = true, ExpectedToken = cancellation.Token };
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        using var script = new LythonEngine(provider).Compile("import re,time\ni=re.finditer(pattern,text)\nnext(i)\ntime.sleep(1)", budget);
        var baseline = budget.ReservedBytes;
        var pending = script.RunAsync(host, Options(cancellation.Token));
        LythonFailureAttribution? attribution = null;
        Task cancellationRequest = Task.CompletedTask;
        try
        {
            var first = await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(timing.Entered.Task, first);
            cancellationRequest = cancellation.CancelAsync();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            cancellationRequest = cancellation.CancelAsync(); provider.ReleaseCleanup.Set();
            await cancellationRequest.WaitAsync(TimeSpan.FromSeconds(5));
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            attribution = result.FailureAttribution;
        }
        Assert.Equal(LythonFailureReason.Cancelled, attribution?.Reason);
        Assert.True(provider.CleanupFinished);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("prepare")]
    [InlineData("match")]
    [InlineData("find")]
    [InlineData("next")]
    [InlineData("substitute")]
    [InlineData("split")]
    public async Task CancellationInterruptsProviderWorkAndWaitsForFundedCleanup(string stage)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { BlockStage = stage is "substitute" or "split" ? "next" : stage, HoldCleanup = true, ExpectedToken = cancellation.Token };
        var engine = new LythonEngine(provider);
        using var script = engine.Compile(stage switch
        {
            "find" or "next" => "import re\nreturn list(re.finditer(pattern,text))",
            "substitute" => "import re\nreturn re.sub(pattern,'y',text)",
            "split" => "import re\nreturn re.split(pattern,text)",
            _ => "import re\nreturn re.search(pattern,text).group()",
        }, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var pending = script.RunAsync(new MockLythonHost(), Options(cancellation.Token));
        LythonFailureAttribution? attribution = null;
        var succeeded = false;
        Task cancellationRequest = Task.CompletedTask;
        try
        {
            var first = await Task.WhenAny(provider.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(provider.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);

            // A second execution borrows the same allowance while the first is active.
            if (stage == "match")
            {
                var held = budget.ReservedBytes;
                using (var other = engine.Compile("return 'other'", budget))
                using (var result = await other.RunAsync(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("other", result.ReturnValue);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
            }

            cancellationRequest = cancellation.CancelAsync();
            await provider.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
        }
        finally
        {
            cancellationRequest = cancellation.CancelAsync();
            provider.ReleaseCleanup.Set();
            await cancellationRequest.WaitAsync(TimeSpan.FromSeconds(5));
            using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            succeeded = completed.Success;
            attribution = completed.FailureAttribution;
        }
        Assert.False(succeeded);
        Assert.Equal(LythonFailureReason.Cancelled, attribution?.Reason);
        Assert.True(provider.CleanupFinished);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ProviderRefusalIsTerminalAndAllowanceIsReusableAfterCleanup()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider { Refuse = true };
        using var script = new LythonEngine(provider).Compile("""
            import re
            try:
                re.search(pattern,text)
            except MemoryError:
                print('must not run')
            return 7
            """, budget);
        var baseline = budget.ReservedBytes;
        for (var i = 0; i < 2; i++)
        {
            using (var result = await script.RunAsync(new MockLythonHost(), Options(default)))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal(long.MaxValue, result.FailureAttribution?.Requested);
                Assert.Empty(result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("search")]
    [InlineData("sub")]
    [InlineData("split")]
    public async Task CaptureProcessingObservesCancellationAndUnwindsItsSubject(string operation)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { CancelCapture = cancellation, ExpectedToken = cancellation.Token };
        using var script = new LythonEngine(provider).Compile("import re\nreturn " + (operation switch
        {
            "sub" => "re.sub(pattern,r'\\1',text)",
            "split" => "re.split(pattern,text)",
            _ => "re.search(pattern,text).group()"
        }), budget);
        var baseline = budget.ReservedBytes;
        using (var result = await script.RunAsync(new MockLythonHost(), Options(cancellation.Token)))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.True(provider.CleanupFinished);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CachedPatternsUseCurrentTokensAndRetainedOwnersStayFunded()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cacheOwner = budget.CreateScope();
        using var provider = new CooperatingProvider(cacheOwner);
        using var script = new LythonEngine(provider).Compile("import re\nreturn re.search(pattern,text).group()", budget);
        using var firstToken = new CancellationTokenSource();
        using var secondToken = new CancellationTokenSource();
        provider.ExpectedToken = firstToken.Token;
        var first = await script.RunAsync(new MockLythonHost(), Options(firstToken.Token));
        firstToken.Cancel();
        provider.ExpectedToken = secondToken.Token;
        var second = await script.RunAsync(new MockLythonHost(), Options(secondToken.Token));
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal("x", first.ReturnValue);
        Assert.Equal("x", second.ReturnValue);
        Assert.Equal(2, provider.Compilations);
        Assert.Equal(2, provider.ClosedPatterns);
        script.Dispose();
        Assert.True(budget.ReservedBytes > CooperatingProvider.PatternBytes);
        first.Dispose(); second.Dispose();
        Assert.Equal(CooperatingProvider.PatternBytes, budget.ReservedBytes);
        cacheOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void CompiledPatternConstantAnalysisKeepsEffectiveFlags(int captureSlots)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var provider = new CooperatingProvider { CaptureSlots = captureSlots };
        var source = $"import re\np=re.compile('x',re.I)\nreturn p.match('X').group({captureSlots - 1})";
        using (var script = new LythonEngine(provider).Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            using var result = script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("X", result.ReturnValue);
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinctCompileOptionsKeepTheirOwnPreparedPatterns(bool asynchronous)
    {
        // Model a provider applying the leading inline flag to both patterns.
        using var provider = new CooperatingProvider { AdditionalEffectiveFlags = 2 };
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile(
            "import re\np=re.compile('(?i)x')\nq=re.compile('(?i)x',re.I)\n" +
            "assert p.flags == q.flags\nreturn p.match('X').group() + re.match(q,'X').group()", budget);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, provider.Compilations);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("XX", result.ReturnValue);
            Assert.Equal(2, provider.Compilations);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, provider.ClosedPatterns);
        script.Dispose();
        Assert.Equal(2, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CoreReportsMissingProviderWithoutInstallingAnotherDialect()
    {
        using var script = new LythonEngine().Compile("import re\np=re.compile('x')\nreturn p");
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Message.Contains("host-supplied Lython regex provider", StringComparison.Ordinal));
        using var dynamic = new LythonEngine().Compile("import re\nreturn re.search(pattern,text)");
        using var result = dynamic.Run(new MockLythonHost(), Options(default));
        Assert.False(result.Success);
        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidCaptureCountsFailBeforePreparationAndCloseThePattern(int captureSlots)
    {
        using var provider = new CooperatingProvider
        {
            CaptureSlots = captureSlots,
            OnPrepare = (_, _) => Assert.Fail("Invalid provider metadata reached subject preparation."),
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var engine = new LythonEngine(provider);
        using (var literal = engine.Compile("import re\np = re.compile('x')\nm = p.search('x')\nreturn m", budget))
        {
            Assert.False(literal.IsValid);
            Assert.Contains(literal.Diagnostics, diagnostic => diagnostic.Message.Contains("invalid capture slot count", StringComparison.Ordinal));
            Assert.Equal(1, provider.ClosedPatterns);
        }
        Assert.Equal(0, budget.ReservedBytes);

        using (var dynamic = engine.Compile("import re\nreturn re.search(pattern,text)", budget))
        {
            Assert.True(dynamic.IsValid, string.Join("; ", dynamic.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = Options(default);
                using (var result = asynchronous ? await dynamic.RunAsync(new MockLythonHost(), options)
                    : dynamic.Run(new MockLythonHost(), options))
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
                    Assert.Contains("invalid capture slot count", result.Failure!.Message);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(3, provider.Compilations);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EffectiveFlagFailureIsAttributedAndClosesThePattern(bool asynchronous)
    {
        using var provider = new CooperatingProvider
        {
            OnReadFlags = () => throw new LythonRegexException(LythonRegexFailure.Capability, "Effective flags unavailable."),
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(provider).Compile("import re\nreturn re.compile(pattern).flags", budget);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), Options(default))
            : script.Run(new MockLythonHost(), Options(default)))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
            Assert.Contains("Effective flags unavailable.", result.Failure!.Message);
        }
        Assert.Equal(1, provider.Compilations);
        Assert.Equal(1, provider.ClosedPatterns);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("search", 1, 2, null, "inconsistent capture ranges")]
    [InlineData("match", 2, 3, null, "outside its window")]
    [InlineData("fullmatch", 0, 2, null, "outside its window")]
    [InlineData("search", 0, 2, 1, "inconsistent capture metadata")]
    [InlineData("search", 0, 2, null, "inconsistent capture metadata", 2)]
    [InlineData("search", 0, 2, 1, "capture outside its window", 3)]
    public async Task InvalidProviderMatchCannotSupplyACompilerFact(string mode, int start, int end,
        int? lastIndex, string message, int? groupEnd = null)
    {
        using var provider = new CooperatingProvider
        {
            CaptureSlots = groupEnd.HasValue ? 2 : 1,
            MatchFactory = (_, _) => new(groupEnd is { } limit
                ? [new LythonRegexCapture(start, end), new LythonRegexCapture(0, limit)]
                : [new LythonRegexCapture(start, end)], lastIndex),
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var engine = new LythonEngine(provider);
        var arguments = groupEnd.HasValue ? "(subject, 0, 1)" : "(subject)";
        var program = $"import re\np = re.compile('x')\nm = p.{mode}{arguments}\nreturn m";
        using (var literal = engine.Compile(program.Replace("(subject", "('éx'", StringComparison.Ordinal), budget))
        {
            Assert.False(literal.IsValid);
            Assert.Contains(literal.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
            Assert.True(provider.Compilations > 0);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        }
        Assert.Equal(0, budget.ReservedBytes);

        using (var dynamic = engine.Compile(program, budget))
        {
            Assert.True(dynamic.IsValid, string.Join("; ", dynamic.Diagnostics));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["subject"] = "éx" } };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await dynamic.RunAsync(new MockLythonHost(), options)
                    : dynamic.Run(new MockLythonHost(), options))
                {
                    Assert.False(result.Success);
                    var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
                    Assert.Equal("RuntimeError", failure.ExceptionType);
                    Assert.Contains(message, failure.Message);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ConstantUtf8EncodingKeepsSupplementaryScalarsAtChunkEdges()
    {
        var prefix = new string('a', 4095);
        var pattern = prefix + "😀.";
        var subject = prefix + "😀z";
        var expectedPattern = Encoding.UTF8.GetBytes(pattern);
        var expectedSubject = Encoding.UTF8.GetBytes(subject);
        var preparations = 0;
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider
        {
            ExpectedToken = cancellation.Token,
            OnCompile = (input, _) => Assert.True(input.Span.SequenceEqual(expectedPattern)),
            OnPrepare = (input, window) =>
            {
                Assert.True(input.Span.SequenceEqual(expectedSubject));
                Assert.Equal(new LythonRegexWindow(0, 4097, 0, expectedSubject.Length), window);
                preparations++;
            },
            MatchFactory = (input, _) => new([new(0, input.Length)], null),
        };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(provider).Compile($"import re\nprint(re.fullmatch('{pattern}', '{subject}').end())",
                   budget, cancellation.Token))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
            Assert.Equal(1, provider.Compilations);
            Assert.True(preparations > 0);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { CancellationToken = cancellation.Token };
            using (var result = await script.RunAsync(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("4097\n", result.StandardOutput);
            }
            Assert.Equal(1, provider.Compilations);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRunOptions Options(CancellationToken token) => new()
    {
        Globals = new Dictionary<string, object?> { ["pattern"] = "x", ["text"] = "x" },
        CancellationToken = token,
    };

    private sealed class HeldTiming : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CooperatingProvider(ILythonMemoryBudget? cacheOwner = null) : ILythonRegexProvider, IDisposable
    {
        internal const long PatternBytes = 512;
        internal string? BlockStage;
        internal bool HoldCleanup;
        internal bool Refuse;
        internal string? RefuseStage;
        internal string? MissingHandleStage;
        internal bool CleanupFinished;
        internal CancellationToken? ExpectedToken;
        internal CancellationTokenSource? CancelCapture;
        internal string? CancelOnReturnStage;
        internal CancellationTokenSource? CancelOnReturn;
        internal bool CursorDisposed;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CleanupEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim ReleaseCleanup = new(false);
        private readonly ManualResetEventSlim _work = new(false);
        private (int Flags, int Slots, ILythonMemoryScope Owner)? _cached;
        internal int Compilations, ClosedPatterns, CaptureSlotReads;
        internal Action? OnCompileReturn, OnMetadata, OnReadFlags;
        internal int CaptureSlots = 1;
        internal int AdditionalEffectiveFlags;
        internal IReadOnlyList<LythonRegexNamedGroup> GroupNames = [];
        internal IReadOnlyList<LythonRegexNamedGroup>? GroupListOverride;
        internal Action<ReadOnlyMemory<byte>, LythonRegexOptions>? OnCompile;
        internal Action<ReadOnlyMemory<byte>, LythonRegexWindow>? OnPrepare;
        internal Func<ReadOnlyMemory<byte>, LythonRegexWindow, FixtureMatch?>? MatchFactory;
        internal Func<ReadOnlyMemory<byte>, LythonRegexOptions, ReadOnlyMemory<byte>?>? ProgramKeyFactory;
        internal LythonRegexCapture[]? CursorMatches;

        private static void Reserve(ILythonMemoryBudget owner, long bytes)
        {
            if (!owner.TryReserve(bytes, out _))
                throw new LythonRegexException(LythonRegexFailure.MemoryAdmission, "provider admission refused", requestedBytes: bytes);
        }

        internal void Work(string stage, CancellationToken token)
        {
            if (ExpectedToken is { } expected) Assert.Equal(expected, token);
            if (BlockStage == stage) { Entered.TrySetResult(); _work.Wait(token); }
            token.ThrowIfCancellationRequested();
            if (RefuseStage == stage)
                throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                    "provider scratch admission refused", requestedBytes: long.MaxValue);
        }

        internal void Cleanup(ILythonMemoryScope owner)
        {
            CleanupEntered.TrySetResult();
            try { if (HoldCleanup) ReleaseCleanup.Wait(); }
            finally { owner.Dispose(); CleanupFinished = true; }
        }

        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options, ILythonMemoryBudget owner, CancellationToken token)
        {
            using var scratch = owner.CreateScope();
            Reserve(scratch, 256);
            try { Work("compile", token); }
            catch { Cleanup(scratch); throw; }
            OnCompile?.Invoke(pattern, options);
            if (MissingHandleStage == "compile") return null!;
            ILythonMemoryScope? retained = null;
            var flags = options.Flags | AdditionalEffectiveFlags;
            var slots = CancelCapture is null ? CaptureSlots : 4;
            if (_cached is { } cached) { flags = cached.Flags; slots = cached.Slots; }
            else
            {
                retained = (cacheOwner ?? owner).CreateScope();
                try { Reserve(retained, PatternBytes); }
                catch { retained.Dispose(); throw; }
                if (cacheOwner is not null) { _cached = (flags, slots, retained); retained = null; }
            }
            var result = new Pattern(this, flags, slots, retained, ProgramKeyFactory?.Invoke(pattern, options));
            Compilations++;
            OnCompileReturn?.Invoke();
            if (CancelOnReturnStage == "compile") CancelOnReturn?.Cancel();
            return result;
        }

        private sealed class Pattern(CooperatingProvider provider, int flags, int slots, ILythonMemoryScope? retained,
            ReadOnlyMemory<byte>? programKey) : ILythonRegexPattern
        {
            private bool _disposed;
            private readonly LythonRegexNamedGroup[] _names = provider.GroupNames
                .Select(entry => new LythonRegexNamedGroup(entry.Name.ToArray(), entry.Index)).ToArray();
            internal int ActiveSubjects;
            public ReadOnlyMemory<byte>? ProgramEqualityKey => programKey;
            public int Flags
            {
                get { provider.OnReadFlags?.Invoke(); return flags; }
            }
            public int CaptureSlotCount
            {
                get { Interlocked.Increment(ref provider.CaptureSlotReads); return slots; }
            }
            public IReadOnlyList<LythonRegexNamedGroup> NamedGroups
            {
                get { ObjectDisposedException.ThrowIf(_disposed, this); provider.OnMetadata?.Invoke(); return provider.GroupListOverride ?? _names; }
            }
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryBudget owner, CancellationToken token)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (provider.MissingHandleStage == "prepare") return null!;
                var scope = owner.CreateScope();
                Reserve(scope, 1024);
                try
                {
                    provider.Work("prepare", token);
                    provider.OnPrepare?.Invoke(input, window);
                    var subject = new Subject(provider, this, input, window, scope);
                    ActiveSubjects++;
                    if (provider.CancelOnReturnStage == "prepare") provider.CancelOnReturn?.Cancel();
                    return subject;
                }
                catch { provider.Cleanup(scope); throw; }
            }
            public void Dispose()
            {
                if (_disposed) return;
                Assert.Equal(0, ActiveSubjects);
                _disposed = true;
                foreach (var entry in _names)
                    if (MemoryMarshal.TryGetArray(entry.Name, out var bytes)) Array.Clear(bytes.Array!, bytes.Offset, bytes.Count);
                if (retained is not null) provider.Cleanup(retained);
                provider.ClosedPatterns++;
            }
        }

        // Immutable scenario data; provider output itself uses the caller's buffer.
        internal sealed record FixtureMatch(LythonRegexCapture?[] Captures, int? LastIndex);

        private sealed class Subject(CooperatingProvider provider, Pattern pattern, ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryScope owner)
            : ILythonRegexSubject
        {
            private bool _disposed;
            internal int ActiveCursors;
            public bool TryMatch(LythonRegexMatchMode mode, Span<LythonRegexCapture?> captures, out int? lastIndex,
                CancellationToken token)
            {
                provider.Work("match", token);
                lastIndex = null;
                if (provider.MatchFactory is { } factory)
                {
                    var fixture = factory(input, window);
                    if (fixture is null) return false;
                    fixture.Captures.CopyTo(captures);
                    lastIndex = fixture.LastIndex;
                    return true;
                }
                if (provider.Refuse) Reserve(owner, long.MaxValue);
                var text = Encoding.UTF8.GetString(input.Span);
                if (!text.Equals("x", (pattern.Flags & 2) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return false;
                for (var i = 0; i < captures.Length; i++)
                {
                    if (i == 1) provider.CancelCapture?.Cancel();
                    token.ThrowIfCancellationRequested();
                    captures[i] = new(0, input.Length);
                }
                if (captures.Length > 1) lastIndex = captures.Length - 1;
                return true;
            }
            public ILythonRegexCursor Find(CancellationToken token)
            {
                provider.Work("find", token);
                if (provider.MissingHandleStage == "find") return null!;
                var cursor = new Cursor(provider, this);
                ActiveCursors++;
                if (provider.CancelOnReturnStage == "find") provider.CancelOnReturn?.Cancel();
                return cursor;
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                var activeCursors = ActiveCursors;
                try
                {
                    provider.Cleanup(owner);
                    Assert.Equal(0, activeCursors);
                }
                finally { pattern.ActiveSubjects--; }
            }
        }

        private sealed class Cursor(CooperatingProvider provider, Subject subject) : ILythonRegexCursor
        {
            private bool _done;
            private bool _disposed;
            private int _next;
            public bool TryNext(Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken token)
            {
                provider.Work("next", token); lastIndex = null;
                if (provider.CursorMatches is { } matches)
                {
                    if (_next == matches.Length) return false;
                    captures[0] = matches[_next++];
                    return true;
                }
                if (_done) return false;
                _done = true;
                return subject.TryMatch(LythonRegexMatchMode.Search, captures, out lastIndex, token);
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                subject.ActiveCursors--;
                provider.CursorDisposed = true;
            }
        }

        public void Dispose() { _cached?.Owner.Dispose(); ReleaseCleanup.Set(); ReleaseCleanup.Dispose(); _work.Dispose(); }
    }
}
