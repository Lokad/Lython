using System.Collections;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexProviderContractTests
{
    [Theory]
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
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImportedFrontendAndRuntimeKeepTheEngineProvider(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        var host = new MockLythonHost();
        host.SeedFile("/helper.py", "import re\ndef value():\n return re.search('x','X',re.I).group()");
        using (var script = new LythonEngine(provider).Compile("import helper\nreturn helper.value()", budget))
        using (var result = asynchronous ? await script.RunAsync(host, ImportOptions()) : script.Run(host, ImportOptions()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("X", result.ReturnValue);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
        static LythonRunOptions ImportOptions() => new() { AllowedLocalModules = new HashSet<string> { "helper" } };
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
        using var provider = new CooperatingProvider { BlockStage = stage, HoldCleanup = true, ExpectedToken = cancellation.Token };
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

    [Fact]
    public async Task CaptureProcessingObservesCancellationAndUnwindsItsSubject()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using var provider = new CooperatingProvider { CancelCapture = cancellation, ExpectedToken = cancellation.Token };
        using var script = new LythonEngine(provider).Compile("import re\nreturn re.search(pattern,text).group()", budget);
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
        provider.ExpectedToken = secondToken.Token;
        var second = await script.RunAsync(new MockLythonHost(), Options(secondToken.Token));
        Assert.True(first.Success, first.Failure?.Message);
        Assert.True(second.Success, second.Failure?.Message);
        Assert.Equal("x", first.ReturnValue);
        Assert.Equal("x", second.ReturnValue);
        script.Dispose();
        Assert.True(budget.ReservedBytes > CooperatingProvider.PatternBytes);
        first.Dispose(); second.Dispose();
        Assert.Equal(CooperatingProvider.PatternBytes, budget.ReservedBytes);
        cacheOwner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CompiledPatternConstantAnalysisKeepsEffectiveFlags()
    {
        using var provider = new CooperatingProvider();
        using var script = new LythonEngine(provider).Compile("import re\np=re.compile('x',re.I)\nreturn p.match('X').group()");
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        using var result = script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("X", result.ReturnValue);
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
        private Pattern? _cached;

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
            if (_cached is not null) return _cached;
            var retained = (cacheOwner ?? owner).CreateScope();
            Reserve(retained, PatternBytes);
            var result = new Pattern(this, options.Flags, CancelCapture is null ? 1 : 4);
            if (cacheOwner is not null) _cached = result;
            return result;
        }

        private sealed class Pattern(CooperatingProvider provider, int flags, int slots) : ILythonRegexPattern
        {
            public int Flags => flags;
            public int CaptureSlotCount => slots;
            public IReadOnlyDictionary<string, int> NamedGroups { get; } = new Dictionary<string, int>();
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> input, ILythonMemoryBudget owner, CancellationToken token)
            {
                var scope = owner.CreateScope();
                Reserve(scope, 1024);
                try
                {
                    provider.Work("prepare", token);
                    var subject = new Subject(provider, this, input, scope);
                    if (provider.CancelOnReturnStage == "prepare") provider.CancelOnReturn?.Cancel();
                    return subject;
                }
                catch { provider.Cleanup(scope); throw; }
            }
        }

        private sealed class Subject(CooperatingProvider provider, Pattern pattern, ReadOnlyMemory<byte> input, ILythonMemoryScope owner)
            : ILythonRegexSubject
        {
            public LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken token)
            {
                provider.Work("match", token);
                if (provider.Refuse) Reserve(owner, long.MaxValue);
                var text = Encoding.UTF8.GetString(input.Span);
                if (!text.Equals("x", (pattern.Flags & 2) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return null;
                return new(new Captures(input, pattern.CaptureSlotCount, provider.CancelCapture), null);
            }
            public ILythonRegexCursor Find(CancellationToken token)
            {
                provider.Work("find", token);
                var cursor = new Cursor(provider, this);
                if (provider.CancelOnReturnStage == "find") provider.CancelOnReturn?.Cancel();
                return cursor;
            }
            public LythonRegexSubstitution Substitute(ReadOnlyMemory<byte> replacement, int count, CancellationToken token)
            { provider.Work("substitute", token); return new(replacement, 1); }
            public IReadOnlyList<ReadOnlyMemory<byte>?> Split(int maxSplit, CancellationToken token)
            { provider.Work("split", token); return [input]; }
            public void Dispose() => provider.Cleanup(owner);
        }

        private sealed class Cursor(CooperatingProvider provider, Subject subject) : ILythonRegexCursor
        {
            private bool _done;
            public LythonRegexMatch? Next(CancellationToken token)
            { provider.Work("next", token); if (_done) return null; _done = true; return subject.Match(LythonRegexMatchMode.Search, token); }
            public void Dispose() { provider.CursorDisposed = true; }
        }

        private sealed class Captures(ReadOnlyMemory<byte> value, int count, CancellationTokenSource? cancel) : IReadOnlyList<LythonRegexCapture?>
        {
            public int Count => count;
            public LythonRegexCapture? this[int index]
            {
                get { if (index == 1) cancel?.Cancel(); return new(value, 0, 1, 0, value.Length); }
            }
            public IEnumerator<LythonRegexCapture?> GetEnumerator() { for (var i = 0; i < count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public void Dispose() { ReleaseCleanup.Set(); ReleaseCleanup.Dispose(); _work.Dispose(); }
    }
}
