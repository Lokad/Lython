using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexStagedProviderTests
{
    [Fact]
    public async Task GuestCallbacksFollowParseAndCompilationPhases()
    {
        const string source = """
            import re
            class Flags:
                def __init__(self):
                    self.calls = []
                def __and__(self, bit):
                    self.calls.append(('and', int(bit)))
                    return 0
                def __ior__(self, bit):
                    self.calls.append(('ior', int(bit)))
                    return bit
                def __ror__(self, state):
                    self.calls.append(('program', int(state)))
                    return state
                def __or__(self, state):
                    self.calls.append(('reported', int(state)))
                    return 32

            def check(expression):
                flags = Flags()
                try:
                    pattern = re.compile(expression, flags)
                    print(expression, pattern.flags, pattern.fullmatch('X') is not None)
                except re.error as error:
                    print(expression, type(error).__name__, str(error))
                print(flags.calls)

            for expression in ('(?i)x', '[', 'x(?i)', 'x|y', '(?<=a+)x'):
                check(expression)
            print(re.compile('z', re.I).fullmatch('Z').group())
            from helper import word
            print(word.fullmatch('Q').group())
            """;
        const string expected = """
            (?i)x 32 True
            [('and', 64), ('ior', 2), ('and', 128), ('program', 34), ('and', 128), ('reported', 34), ('and', 128)]
            [ PatternError unterminated character set at position 0
            [('and', 64)]
            x(?i) PatternError global flags not at the start of the expression at position 1
            [('and', 64), ('ior', 2)]
            x|y 32 False
            [('and', 64), ('and', 64), ('and', 4), ('and', 256), ('ior', 32), ('and', 128), ('program', 32), ('and', 128), ('reported', 32), ('and', 128)]
            (?<=a+)x PatternError look-behind requires fixed-width pattern
            [('and', 64), ('and', 4), ('and', 256), ('ior', 32), ('and', 128), ('program', 32)]
            Z
            Q
            """;
        var expectedOutput = expected.ReplaceLineEndings("\n") + "\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        var provider = new ScriptedProvider();
        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var exactCalls = provider.ExactCalls;
        Assert.True(exactCalls > 0);
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import re\nword = re.compile('q', re.I)");
        var options = new LythonRunOptions { AllowedLocalModules = new HashSet<string> { "helper" } };
        using (var result = script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expectedOutput, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expectedOutput, result.StandardOutput);
        }
        Assert.Equal(10, provider.ParseCalls);
        Assert.Equal(6, provider.ProgramCalls);
        Assert.Equal(exactCalls + 2, provider.ExactCalls);
        Assert.Equal(0, provider.LiveParses);
        Assert.Equal(0, provider.LivePrograms);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task ParserReusesTheVerboseOperandAndAwaitsEachTruthCheck()
    {
        const string source = """
            import re
            events = []
            class Verbose:
                def __bool__(self):
                    events.append('truth')
                    with open('/flag.txt') as stream:
                        return bool(int(stream.read()))
            class Flags:
                def __and__(self, bit):
                    if bit == 64:
                        events.append('operand')
                        return Verbose()
                    return 0
                def __ior__(self, bits): return bits
                def __ror__(self, bits): return bits
                def __or__(self, bits): return bits
            print(re.compile('abc', Flags()).fullmatch('abc').group())
            print(events)
            """;
        var provider = new ScriptedProvider();
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(provider).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/flag.txt", "0");
        using (var result = await script.RunAsync(host))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("abc\n['operand', 'truth', 'truth', 'truth']\n", result.StandardOutput);
        }
        Assert.Equal(3, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(0, provider.LiveParses);
        Assert.Equal(0, provider.LivePrograms);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonintegerCompilerOperandFailsBeforeProgramConstruction(bool staged)
    {
        const string source = """
            import re
            class Program:
                def __and__(self, bit): return 34 & bit
            class Flags:
                def __and__(self, bit): return 0
                def __ior__(self, bit): return bit
                def __ror__(self, state):
                    with open('/flag.txt') as stream:
                        stream.read()
                    return Program()
                def __or__(self, state):
                    print('reported')
                    return 32
            print(re.compile('x', Flags()).fullmatch('X') is not None)
            """;
        var provider = new ScriptedProvider();
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(staged ? provider : new SingleStepProvider(provider)).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var exactCalls = provider.ExactCalls;
        var host = new DelayedLythonHost();
        host.SeedFile("/flag.txt", "flags");
        using (var result = await script.RunAsync(host))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(staged ? 1 : 0, provider.ParseCalls);
        Assert.Equal(0, provider.LiveParses);
        Assert.Equal(0, provider.ProgramCalls);
        Assert.Equal(exactCalls, provider.ExactCalls);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task GuestCompilerFlagsPreserveRepeatedTruthAndScopedFlagOperations()
    {
        const string source = """
            import re
            trace = []
            class Truth:
                def __init__(self, bit, value):
                    self.bit, self.value = bit, value
                def __bool__(self):
                    trace.append(('truth', self.bit))
                    return bool(self.value)
            class Operand:
                def __init__(self, bits):
                    self.bits = bits
                def __and__(self, bit):
                    trace.append(('and', int(bit)))
                    return Operand(self.bits & bit) if bit < 0 else Truth(bit, self.bits & bit)
                def __iand__(self, bit):
                    trace.append(('iand', int(bit)))
                    return Operand(self.bits & bit)
                def __or__(self, bit):
                    trace.append(('or', int(bit)))
                    return Operand(self.bits | bit)
            class Flags:
                def __and__(self, bit): return 0
                def __ior__(self, bits): return bits
                def __ror__(self, bits): return Operand(34)
                def __or__(self, bits):
                    trace.append(('reported', int(bits)))
                    return 32
            for expression in ('x', '(?a-i:x)'):
                trace.clear()
                flags = Flags()
                pattern = re.compile(expression, flags)
                assert re.compile(expression, flags) is pattern
                print(expression, pattern.flags, pattern.fullmatch('X') is not None)
                print(trace)
            """;
        const string expected = """
            x 32 True
            [('and', 2), ('truth', 2), ('and', 4), ('truth', 4), ('and', 2), ('truth', 2), ('and', 32), ('truth', 32), ('and', 2), ('truth', 2), ('and', 32), ('truth', 32), ('and', 2), ('truth', 2), ('and', 4), ('truth', 4), ('and', 32), ('truth', 32), ('and', 2), ('truth', 2), ('and', 4), ('truth', 4), ('reported', 32)]
            (?a-i:x) 32 False
            [('and', 2), ('truth', 2), ('and', 4), ('truth', 4), ('and', 2), ('truth', 2), ('and', 32), ('truth', 32), ('iand', -293), ('or', 256), ('and', -3), ('and', 2), ('truth', 2), ('and', 2), ('truth', 2), ('and', 2), ('truth', 2), ('and', 4), ('truth', 4), ('and', 32), ('truth', 32), ('iand', -293), ('or', 256), ('and', -3), ('and', 2), ('truth', 2), ('and', 2), ('truth', 2), ('reported', 32)]
            """;
        var provider = new ScriptedProvider { GuestProgram = CompileProbe };
        var budget = new LythonMemoryBudget(1024 * 1024);
        var script = new LythonEngine(provider).Compile(source, budget);
        using (script)
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var asynchronous = 0; asynchronous < 2; asynchronous++)
            {
                var host = new MockLythonHost();
                using (var result = asynchronous == 0 ? script.Run(host) : await script.RunAsync(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected.ReplaceLineEndings("\n") + "\n", result.StandardOutput);
                }
                Assert.Equal(0, provider.LiveParses);
                Assert.Equal(0, provider.LivePrograms);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(4, provider.ProgramCalls);
        Assert.Equal(0, budget.ReservedBytes);

        static async ValueTask<int> CompileProbe(ReadOnlyMemory<byte> text, LythonRegexCompilerFlags flags, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            // The literal and scoped-literal CPython probes reach these decisions
            // in prefix analysis and code generation. The adapter builds the match.
            Assert.True(await flags.TestAsync(2));
            Assert.False(await flags.TestAsync(4));
            Assert.True(await flags.TestAsync(2));
            Assert.True(await flags.TestAsync(32));
            var scoped = text.Span.SequenceEqual("(?a-i:x)"u8);
            if (scoped)
            {
                var prefix = await flags.WithScopedFlagsAsync(256, 2);
                Assert.False(await prefix.TestAsync(2));
                Assert.False(await prefix.TestAsync(2));
            }
            else
            {
                Assert.True(await flags.TestAsync(2));
                Assert.True(await flags.TestAsync(32));
            }
            Assert.True(await flags.TestAsync(2));
            Assert.False(await flags.TestAsync(4));
            Assert.True(await flags.TestAsync(32));
            var body = scoped ? await flags.WithScopedFlagsAsync(256, 2) : flags;
            var ignoreCase = await body.TestAsync(2);
            Assert.False(await body.TestAsync(scoped ? 2 : 4));
            return 32 | (ignoreCase ? 2 : 0);
        }
    }

    [Theory]
    [InlineData("parser", false)]
    [InlineData("program", false)]
    [InlineData("compiler-operator", false)]
    [InlineData("compiler-truth", false)]
    [InlineData("compiler-truth", true)]
    [InlineData("reported", false)]
    [InlineData("reported", true)]
    public async Task PendingFlagFailureReleasesTheCorrectStage(string failureStage, bool refuse)
    {
        const string body = """
            import re
            def read():
                with open('/flag.txt') as stream:
                    if stream.read() == 'refuse':
                        return len('x' * 4000000)
                return 32
            class Truth:
                def __bool__(self): return bool(read())
            class Program:
                def __and__(self, bits): return read() if failure_stage == 'compiler-operator' else Truth()
            class Flags:
                def __and__(self, bit): return 0
                def __ior__(self, bits): return read() if failure_stage == 'parser' else bits
                def __ror__(self, bits):
                    if failure_stage.startswith('compiler-'): return Program()
                    return read() if failure_stage == 'program' else bits
                def __or__(self, bits): return read() if failure_stage == 'reported' else bits
            try:
                re.compile('x', Flags())
            except MemoryError:
                print('refusal was swallowed')
            print('continued')
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        var guestCompiler = failureStage.StartsWith("compiler-", StringComparison.Ordinal);
        var provider = new ScriptedProvider
        {
            GuestProgram = guestCompiler ? static async (_, flags, _) => await flags.TestAsync(2) ? 34 : 32 : null,
        };
        using var script = new LythonEngine(provider).Compile($"failure_stage = '{failureStage}'\n" + body, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using var tokenSource = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        host.SeedFile("/flag.txt", refuse ? "refuse" : "cancel");
        var entered = refuse ? null : host.PauseReadUntilCancellation("/flag.txt");
        var pending = script.RunAsync(host, tokenSource.Token);
        var expectedProgramCount = failureStage == "reported" ? 1 : 0;
        if (entered is not null)
        {
            try
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1 - expectedProgramCount, provider.LiveParses);
                Assert.Equal(expectedProgramCount, provider.LivePrograms);
                Assert.Equal(guestCompiler ? 1 : 0, provider.LiveGuestCompilers);
                Assert.True(budget.ReservedBytes > baseline);
            }
            catch
            {
                tokenSource.Cancel();
                using var failed = await pending;
                throw;
            }
            tokenSource.Cancel();
        }
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(tokenSource.Token, provider.ParseToken);
        if (guestCompiler) Assert.Equal(tokenSource.Token, provider.GuestCompilerToken);
        Assert.Equal(expectedProgramCount, provider.ProgramCalls);
        Assert.Equal(0, provider.LiveParses);
        Assert.Equal(0, provider.LivePrograms);
        Assert.Equal(0, provider.LiveGuestCompilers);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellingProviderWorkJoinsItBeforeReturningTheAllowance(bool duringCompilation)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var provider = new ScriptedProvider
        {
            PauseParsing = duringCompilation ? null : entered,
            GuestProgram = duringCompilation ? async (_, _, token) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return 32;
            } : null,
        };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine(provider).Compile("""
            import re
            class Operand:
                def __and__(self, bit): return 0
            class Flags:
                def __and__(self, bit): return 0
                def __ior__(self, bits): return bits
                def __ror__(self, bits): return Operand()
                def __or__(self, bits): return bits
            re.compile('x', Flags())
            """, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(new MockLythonHost(), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cancellation.Token, provider.ParseToken);
            if (duringCompilation) Assert.Equal(cancellation.Token, provider.GuestCompilerToken);
            Assert.Equal(1, provider.LiveParses);
            Assert.Equal(duringCompilation ? 1 : 0, provider.LiveGuestCompilers);
            Assert.True(budget.ReservedBytes > baseline);
        }
        catch
        {
            cancellation.Cancel();
            using var failed = await pending;
            throw;
        }
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, provider.LiveParses);
        Assert.Equal(0, provider.ProgramCalls);
        Assert.Equal(0, provider.LiveGuestCompilers);
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    private sealed class SingleStepProvider(ILythonRegexProvider provider) : ILythonRegexProvider
    {
        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
            ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
            => provider.Compile(pattern, options, memoryOwner, cancellationToken);
    }

    // Supplies the known events for the oracle's syntax without implementing a
    // second regex parser. The optional adapter handles the resulting programs.
    private sealed class ScriptedProvider : ILythonRegexStagedProvider
    {
        internal int ExactCalls { get; private set; }
        internal int ParseCalls { get; private set; }
        internal int ProgramCalls { get; private set; }
        internal int LiveParses { get; private set; }
        internal int LivePrograms { get; private set; }
        internal int LiveGuestCompilers { get; private set; }
        internal CancellationToken ParseToken { get; private set; }
        internal CancellationToken GuestCompilerToken { get; private set; }
        internal TaskCompletionSource? PauseParsing { get; init; }
        internal Func<ReadOnlyMemory<byte>, LythonRegexCompilerFlags, CancellationToken, ValueTask<int>>? GuestProgram { get; init; }

        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
            ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            ExactCalls++;
            return Utf8RegexProvider.Instance.Compile(pattern, options, memoryOwner, cancellationToken);
        }

        public async ValueTask<ILythonRegexParsedPattern> ParseAsync(ReadOnlyMemory<byte> pattern,
            ILythonRegexParserFlags flags, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ParseCalls++;
            ParseToken = cancellationToken;
            var owner = memoryOwner.CreateScope();
            LiveParses++;
            try
            {
                var bytes = 2048 + pattern.Length;
                if (!owner.TryReserve(bytes, out _))
                    throw new LythonRegexException(LythonRegexFailure.MemoryAdmission, "Parser storage refused.", requestedBytes: bytes);
                if (PauseParsing is { } entered)
                {
                    entered.SetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                var verbose = await flags.ReadVerboseAsync();
                if (pattern.Span.SequenceEqual("abc"u8))
                {
                    for (var i = 0; i < 3; i++) Assert.False(await verbose.IsEnabledAsync());
                }
                if (pattern.Span.SequenceEqual("["u8))
                    throw new LythonRegexException(LythonRegexFailure.Pattern, "unterminated character set", position: 0);
                if (pattern.Span.SequenceEqual("x(?i)"u8))
                {
                    await flags.AddGlobalAsync(2);
                    throw new LythonRegexException(LythonRegexFailure.Pattern, "global flags not at the start of the expression", position: 1);
                }
                if (pattern.Span.StartsWith("(?i)"u8))
                {
                    await flags.AddGlobalAsync(2);
                    _ = await flags.ReadVerboseAsync();
                    pattern = pattern[4..];
                }
                if (pattern.Span.SequenceEqual("x|y"u8)) _ = await flags.ReadVerboseAsync();
                await flags.FinishAsync();
                cancellationToken.ThrowIfCancellationRequested();
                return GuestProgram is null ? new Parsed(this, pattern.ToArray(), owner)
                    : new CallbackParsed(this, pattern.ToArray(), owner);
            }
            catch
            {
                owner.Dispose();
                LiveParses--;
                throw;
            }
        }

        private class Parsed(ScriptedProvider provider, byte[] text, ILythonMemoryScope owner) : ILythonRegexParsedPattern
        {
            private bool _disposed;
            protected ScriptedProvider Provider { get; } = provider;
            protected byte[] Text { get; } = text;

            public ILythonRegexPattern Compile(int programFlags, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
            {
                Assert.False(_disposed);
                Provider.ProgramCalls++;
                var program = Utf8RegexProvider.Instance.Compile(Text, new(programFlags, false), memoryOwner, cancellationToken);
                Provider.LivePrograms++;
                return new Program(Provider, program);
            }

            public void Dispose()
            {
                if (_disposed) return;
                Assert.Equal(0, Provider.LiveGuestCompilers);
                _disposed = true;
                owner.Dispose();
                Provider.LiveParses--;
            }
        }

        private sealed class CallbackParsed(ScriptedProvider provider, byte[] text, ILythonMemoryScope owner)
            : Parsed(provider, text, owner), ILythonRegexParsedPattern
        {
            public async ValueTask<ILythonRegexPattern> CompileAsync(LythonRegexCompilerFlags flags,
                ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
            {
                Provider.GuestCompilerToken = cancellationToken;
                Provider.LiveGuestCompilers++;
                try
                {
                    var bits = await Provider.GuestProgram!(Text, flags, cancellationToken);
                    return Compile(bits, memoryOwner, cancellationToken);
                }
                finally { Provider.LiveGuestCompilers--; }
            }
        }

        private sealed class Program(ScriptedProvider provider, ILythonRegexPattern inner) : ILythonRegexPattern
        {
            private bool _disposed;
            public int Flags => inner.Flags;
            public int CaptureSlotCount => inner.CaptureSlotCount;
            public IReadOnlyList<LythonRegexNamedGroup> NamedGroups => inner.NamedGroups;

            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, LythonRegexWindow window,
                ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
                => inner.Prepare(subject, window, memoryOwner, cancellationToken);

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                inner.Dispose();
                provider.LivePrograms--;
            }
        }
    }
}
