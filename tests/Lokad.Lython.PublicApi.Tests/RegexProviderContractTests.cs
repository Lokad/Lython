using System.Collections;
using System.Text;
using System.Runtime.InteropServices;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexProviderContractTests
{
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
            re.purge()
            print(p.groupindex,m.groupdict(),m.lastgroup,m.span('é'))
            return m.expand(r'<\g<é>:\g<tail>>')
            """, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics.Select(d => d.Message)));
            Assert.True(provider.Compilations > 0);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("{'é': 1, 'tail': 2} {'é': '😀', 'tail': ''} é (0, 1)\n", result.StandardOutput);
                Assert.Equal("<😀:>", result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(provider.Compilations, provider.ClosedPatterns);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
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
        using (var script = new LythonEngine(provider).Compile("import re\nprint(re.match('x','éx').group())", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task FrontendAndRuntimePassOriginalSubjectAndAbsoluteWindowCaptures(bool reversed)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider { CaptureSlots = reversed ? 1 : 2 };
        var preparations = 0;
        provider.OnPrepare = (input, window) =>
        {
            Assert.Equal("😀x尾", Encoding.UTF8.GetString(input.Span));
            Assert.Equal(reversed ? new(2, 1, 5, 4) : new LythonRegexWindow(1, 2, 4, 5), window);
            preparations++;
        };
        provider.MatchFactory = (input, _) => reversed
            ? new(new LythonRegexCapture?[] { new(5, 5) }, null)
            : new(new LythonRegexCapture?[] { new(4, 5), new(0, 4) }, 1);
        using (var script = new LythonEngine(provider).Compile("import re\nm=re.compile('" +
            (reversed ? "" : "(?<=(😀))x") + "').match('😀x尾'," + (reversed ? "2,1" : "1,2") + ")\n" +
            "print(m.span(),m.pos,m.endpos,m.string)\n" + (reversed ? "" : "print(m.span(1),m.group(1))"), budget))
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
                    Assert.Equal(reversed ? "(2, 2) 2 1 😀x尾\n" : "(1, 2) 1 2 😀x尾\n(0, 1) 😀\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.True(preparations > frontendPreparations);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        }
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
    public void FrontendClosesPatternsAfterSuccessOrDiagnostic(bool diagnostic)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var provider = new CooperatingProvider();
        using (var script = new LythonEngine(provider).Compile("import re\np=re.compile('x')" +
            (diagnostic ? "\nreturn p.match('other').group()" : ""), budget))
        {
            Assert.Equal(!diagnostic, script.IsValid);
            Assert.True(provider.Compilations > 0);
            Assert.Equal(provider.Compilations, provider.ClosedPatterns);
        }
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
        Assert.True(provider.Compilations > 0);
        Assert.Equal(provider.Compilations, provider.ClosedPatterns);
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
        private (int Flags, int Slots, ILythonMemoryScope Owner)? _cached;
        internal int Compilations, ClosedPatterns;
        internal Action? OnCompileReturn, OnMetadata;
        internal int CaptureSlots = 1;
        internal IReadOnlyList<LythonRegexNamedGroup> GroupNames = [];
        internal Action<ReadOnlyMemory<byte>, LythonRegexWindow>? OnPrepare;
        internal Func<ReadOnlyMemory<byte>, LythonRegexWindow, LythonRegexMatch?>? MatchFactory;

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
            ILythonMemoryScope? retained = null;
            var flags = options.Flags;
            var slots = CancelCapture is null ? CaptureSlots : 4;
            if (_cached is { } cached) { flags = cached.Flags; slots = cached.Slots; }
            else
            {
                retained = (cacheOwner ?? owner).CreateScope();
                try { Reserve(retained, PatternBytes); }
                catch { retained.Dispose(); throw; }
                if (cacheOwner is not null) { _cached = (flags, slots, retained); retained = null; }
            }
            var result = new Pattern(this, flags, slots, retained);
            Compilations++;
            OnCompileReturn?.Invoke();
            if (CancelOnReturnStage == "compile") CancelOnReturn?.Cancel();
            return result;
        }

        private sealed class Pattern(CooperatingProvider provider, int flags, int slots, ILythonMemoryScope? retained) : ILythonRegexPattern
        {
            private bool _disposed;
            private readonly LythonRegexNamedGroup[] _names = provider.GroupNames
                .Select(entry => new LythonRegexNamedGroup(entry.Name.ToArray(), entry.Index)).ToArray();
            internal int ActiveSubjects;
            public int Flags => flags;
            public int CaptureSlotCount => slots;
            public IReadOnlyList<LythonRegexNamedGroup> NamedGroups
            {
                get { ObjectDisposedException.ThrowIf(_disposed, this); provider.OnMetadata?.Invoke(); return _names; }
            }
            public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryBudget owner, CancellationToken token)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
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

        private sealed class Subject(CooperatingProvider provider, Pattern pattern, ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryScope owner)
            : ILythonRegexSubject
        {
            private bool _disposed;
            public LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken token)
            {
                provider.Work("match", token);
                if (provider.MatchFactory is { } factory) return factory(input, window);
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
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try { provider.Cleanup(owner); }
                finally { pattern.ActiveSubjects--; }
            }
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
                get { if (index == 1) cancel?.Cancel(); return new(0, value.Length); }
            }
            public IEnumerator<LythonRegexCapture?> GetEnumerator() { for (var i = 0; i < count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public void Dispose() { _cached?.Owner.Dispose(); ReleaseCleanup.Set(); ReleaseCleanup.Dispose(); _work.Dispose(); }
    }
}
