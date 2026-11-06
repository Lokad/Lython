using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexPatternErrorLifetimeTests
{
    [Theory]
    [InlineData("compile", 3)]
    [InlineData("compile", null)]
    [InlineData("prepare", 3)]
    [InlineData("match", 3)]
    [InlineData("find", 3)]
    [InlineData("next", 3)]
    public async Task ProviderFailuresRetainOriginalPatternAndCloseActualUsers(string stage, int? position)
    {
        var provider = new ErrorProvider(stage) { Position = position };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var operation = stage is "find" or "next" ? "list(re.finditer(pattern, '😀\\na'))" : "re.match(pattern, '😀\\na')";
        var source = "import re\ntry:\n " + operation + "\n" + """
            except re.PatternError as error:
             print(error.pattern is pattern,error.msg,error.pos,error.lineno,error.colno,error.args)
             return error.__dict__
            """;
        using (var script = new LythonEngine(provider).Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = await script.RunAsync(new MockLythonHost(), new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["pattern"] = "😀\n(a)" }
            }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(position is null ? "True synthetic failure None None None ('synthetic failure',)\n"
                    : "True synthetic failure 3 2 2 ('synthetic failure at position 3 (line 2, column 2)',)\n", result.StandardOutput);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(stage == "compile" ? 0 : 1, provider.PatternsClosed);
                Assert.Equal(stage is "match" or "find" or "next" ? 1 : 0, provider.SubjectsClosed);
                Assert.Equal(stage == "next" ? 1 : 0, provider.CursorsClosed);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(LythonRegexFailure.Pattern, LythonFailureReason.Cancelled)]
    [InlineData(LythonRegexFailure.Capability, LythonFailureReason.Cancelled)]
    [InlineData(LythonRegexFailure.MemoryAdmission, LythonFailureReason.SharedMemoryAllowance)]
    public async Task FailureTranslationPreservesCancellationAndAdmission(LythonRegexFailure failure, LythonFailureReason expected)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ErrorProvider("compile") { BeforeError = cancellation.Cancel, Failure = failure };
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine(provider).Compile("""
            import re
            try: re.compile(pattern)
            except BaseException: pass
            print('later')
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var result = await script.RunAsync(new MockLythonHost(), new LythonRunOptions
            {
                CancellationToken = cancellation.Token,
                Globals = new Dictionary<string, object?> { ["pattern"] = "a" }
            }))
            {
                Assert.False(result.Success);
                Assert.Equal(expected, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                if (failure == LythonRegexFailure.MemoryAdmission) Assert.Equal(4096, result.FailureAttribution?.Requested);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("refuse")]
    public async Task SuspendedMessageKeepsStorageUntilCompletionAndAllowsSharedBudgetReuse(string outcome)
    {
        const long limit = 2 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        var timing = new HeldTiming();
        var host = new MockLythonHost(); host.SetTiming(timing);
        using var cancellation = new CancellationTokenSource();
        using (var script = new LythonEngine().Compile("""
            import re,time
            class Message:
             def __str__(self):
              time.sleep(0)
              return 'bad'
            pattern='😀\n'+'x'*4096
            try: error=re.error(Message(),pattern,2)
            except BaseException: pass
            print('later')
            print(error.pattern is pattern)
            return error.pattern
            """, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            ILythonMemoryReservation? fill = null;
            try
            {
                await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(cancellation.Token, timing.SeenToken);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                var otherHost = new MockLythonHost(); otherHost.EnableTiming();
                using (var other = await script.RunAsync(otherHost))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.Equal("later\nTrue\n", other.StandardOutput);
                    Assert.True(budget.ReservedBytes > held);
                }
                Assert.Equal(held, budget.ReservedBytes);
                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out fill));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= held);
                timing.Release.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (outcome == "success")
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("later\nTrue\n", result.StandardOutput);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                        result.FailureAttribution?.Reason);
                    Assert.DoesNotContain("later", result.StandardOutput, StringComparison.Ordinal);
                }
            }
            finally
            {
                timing.Release.TrySetResult();
                cancellation.Cancel();
                using var settled = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                fill?.Dispose();
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var reused = await script.RunAsync(host)) Assert.True(reused.Success, reused.Failure?.Message);
            Assert.Equal(CancellationToken.None, timing.SeenToken);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken SeenToken;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            Entered.TrySetResult();
            // A real outstanding host user must finish even after cancellation.
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private sealed class ErrorProvider(string stage) : ILythonRegexProvider
    {
        private readonly string _stage = stage;
        public int? Position { get; init; } = 3;
        public Action? BeforeError { get; init; }
        public LythonRegexFailure Failure { get; init; } = LythonRegexFailure.Pattern;
        public int PatternsClosed, SubjectsClosed, CursorsClosed;
        private void Fail()
        {
            BeforeError?.Invoke();
            throw new LythonRegexException(Failure, "synthetic failure", Position, requestedBytes: 4096);
        }
        public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options, ILythonMemoryBudget owner, CancellationToken token)
        {
            if (_stage == "compile") Fail();
            return new Pattern(this, Utf8RegexProvider.Instance.Compile(pattern, options, owner, token));
        }
        private sealed class Pattern(ErrorProvider provider, ILythonRegexPattern inner) : ILythonRegexPattern
        {
            private bool _disposed;
            public int Flags => inner.Flags;
            public int CaptureSlotCount => inner.CaptureSlotCount;
            public IReadOnlyList<LythonRegexNamedGroup> NamedGroups => inner.NamedGroups;
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, LythonRegexWindow window, ILythonMemoryBudget owner, CancellationToken token)
            {
                if (provider._stage == "prepare") provider.Fail();
                return new Subject(provider, inner.Prepare(subject, window, owner, token));
            }
            public void Dispose() { if (_disposed) return; _disposed = true; inner.Dispose(); provider.PatternsClosed++; }
        }
        private sealed class Subject(ErrorProvider provider, ILythonRegexSubject inner) : ILythonRegexSubject
        {
            private bool _disposed;
            public bool TryMatch(LythonRegexMatchMode mode, Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken token)
            {
                if (provider._stage == "match") provider.Fail();
                return inner.TryMatch(mode, captures, out lastIndex, token);
            }
            public ILythonRegexCursor Find(CancellationToken token)
            {
                if (provider._stage == "find") provider.Fail();
                return new Cursor(provider, inner.Find(token));
            }
            public void Dispose() { if (_disposed) return; _disposed = true; inner.Dispose(); provider.SubjectsClosed++; }
        }
        private sealed class Cursor(ErrorProvider provider, ILythonRegexCursor inner) : ILythonRegexCursor
        {
            private bool _disposed;
            public bool TryNext(Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken token)
            {
                if (provider._stage == "next") provider.Fail();
                return inner.TryNext(captures, out lastIndex, token);
            }
            public void Dispose() { if (_disposed) return; _disposed = true; inner.Dispose(); provider.CursorsClosed++; }
        }
    }
}
