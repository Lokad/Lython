using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Starmap keeps its arguments admitted through pending iteration and callbacks.
public sealed class StarmapMaterializationParityTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MemoryBudget = new LythonMemoryBudget(8192),
    };

    [Fact]
    public async Task StarmapArgumentsRefuseUnderSharedAllowanceInBothModes()
    {
        const string code = "import itertools\nreturn next(itertools.starmap(lambda *x: len(x), [range(1000)]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Tiny());
        Assert.False(sync.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, sync.FailureAttribution?.Reason);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.False(asyncResult.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, asyncResult.FailureAttribution?.Reason);
    }

    [Fact]
    public async Task StarmapFunded_MatchesBothModes()
    {
        const string code = "import itertools\nreturn list(itertools.starmap(lambda a, b: a + b, [(1, 2), (3, 4)]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StarmapDelayedSource_ComposesAsyncAndFailsFastSync()
    {
        const string code = "import itertools\nwith open(\"/r.txt\") as f:\n    return list(itertools.starmap(lambda a: a.strip(), [(line,) for line in f]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        Assert.True(script.IsValid);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/r.txt", "a\nb\n");
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        var syncHost = new DelayedLythonHost();
        syncHost.SeedFile("/r.txt", "a\nb\n");
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
    }

    [Fact]
    public async Task StarmapNestedCallback_SucceedsFunded()
    {
        const string code = "import itertools\nreturn list(itertools.starmap(lambda *a: list(a), [[1, 2], [3]]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/input.txt")]
    [InlineData("/pause")]
    public async Task IterableRowSurvivesPendingSourceAndCallback(string? cancelAt)
    {
        const string source = """
            from itertools import starmap
            def arguments():
                yield marker
                with open('input.txt') as source:
                    for line in source:
                        yield line.strip()
            def consume(value, first, second):
                with open('pause') as source:
                    source.read()
                print(value is marker, first, second)
            marker = []
            next(starmap(consume, [arguments()]))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var host = new DelayedLythonHost();
        host.SeedFile("/input.txt", "first\nsecond\n");
        host.SeedFile("/pause", "data");
        using var cancellation = new CancellationTokenSource();
        var cancel = cancelAt is not null;
        var cancelInSource = cancelAt == "/input.txt";
        var started = cancelAt is null ? null : host.PauseReadUntilCancellation(cancelAt);
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
        try
        {
            if (started is not null)
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.Equal(cancelInSource ? 0 : 1, host.ClosedFileCount);
                Assert.True(budget.ReservedBytes > 0);
                cancellation.Cancel();
            }
            using var result = await pending;
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "True first second\n", result.StandardOutput);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending;
        }
        Assert.Equal(cancelInSource ? 1 : 2, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactRowsKeepLiveDefaultsAndRetainedAliases(bool asynchronous)
    {
        const string source = """
            from itertools import starmap
            class Policy:
                def apply(self, value, /, offset=1, *, scale=2):
                    return (value + offset) * scale
                def identity(self, value):
                    return value
                def identity_wide(self, a, b, c, d, value):
                    return value
            policy = Policy()
            saved = policy.apply
            Policy.apply = lambda self, value: -1
            print(list(starmap(saved, [(1,), (2,)])), list(map(saved, [1, 2])))
            saved.__func__.__defaults__ = (5,)
            saved.__func__.__kwdefaults__ = {'scale': 3}
            print(list(starmap(saved, [(1,), (2,)])), list(map(saved, [1, 2])))
            def make_row(value):
                return (value + 1,)
            row = make_row(1000)
            print(next(starmap(policy.identity, [row])) is row[0])
            def make_wide_row(value):
                return (None, None, None, None, value + 1)
            wide = make_wide_row(2000)
            print(next(starmap(policy.identity_wide, [wide])) is wide[4],
                  next(starmap(policy.identity_wide, [list(wide)])) is wide[4])
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[4, 6] [4, 6]\n[18, 21] [18, 21]\nTrue\nTrue True\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ExactRowCallbackKeepsBorrowUntilSuspendedCallFinishes(bool zipped, bool wide)
    {
        const string source = """
            from itertools import starmap
            def use(value):
                with open('pause') as stream:
                    stream.read()
                return value
            def use_wide(value, a, b, c, d):
                return use(value)
            marker = []
            if wide:
                rows = [[marker, None, None, None, None]]
                callback = use_wide
            else:
                rows = zip([marker]) if zipped else [(marker,)]
                callback = use
            print(next(starmap(callback, rows)) is marker)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var blocked = new DelayedLythonHost();
        blocked.SeedFile("/pause", "content");
        var started = blocked.PauseReadUntilCancellation("/pause");
        using var cancellation = new CancellationTokenSource();
        var globals = new Dictionary<string, object?> { ["zipped"] = zipped, ["wide"] = wide };
        var pending = script.RunAsync(blocked, new LythonRunOptions
        {
            MemoryBudget = budget, Globals = globals, CancellationToken = cancellation.Token
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var interrupted = await pending;
            Assert.Equal(LythonFailureReason.Cancelled, interrupted.FailureAttribution?.Reason);
            Assert.Empty(interrupted.StandardOutput);
        }
        Assert.Equal(1, blocked.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);

        var host = new DelayedLythonHost();
        host.SeedFile("/pause", "content");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, Globals = globals }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n", result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratorListYieldKeepsAliasesAndAllowsCallbackToResumeSource(bool asynchronous)
    {
        const string source = """
            from itertools import starmap
            marker = []
            def produce():
                held = (marker, 21000 + len(marker))
                sent = yield [held[0], held[1], held[1]]
                yield [sent, marker]
            def inspect(a, b, c):
                return a is marker, b is c, rows.send('sent')
            class Callback:
                def call(self, a, b, c):
                    return inspect(a, b, c)
            rows = produce()
            mapped = starmap(Callback().call, rows)
            same_object, same_int, row = next(mapped)
            print(same_object, same_int, type(row) is list, row[0], row[1] is marker, list(mapped))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True True sent True []\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task UnpackedGeneratorDisplayPreservesOrderAndAliasesAcrossBothConsumers(bool asynchronous, bool cancel)
    {
        const string source = """
            from itertools import starmap
            a, middle, b = [], [], []
            def display(pairs, between):
                return ([left, between, right, None, 7] for left, right in pairs)
            def source():
                with open('/ready') as stream:
                    stream.read()
                yield b
                yield a
            direct = next(display([[a, b]], middle))
            print(direct[0] is a, direct[1] is middle, direct[2] is b, direct[3:])
            rows = display(zip([a, b], source(), strict=True), middle)
            def consume(first, between, last, empty, number):
                resumed = next(rows)
                return (first is a, between is middle, last is b, empty is None, number,
                        resumed[0] is b, resumed[1] is middle, resumed[2] is a)
            print(list(starmap(consume, rows)))
            try:
                next(display([[a]], middle))
            except ValueError:
                print('wrong arity')
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        host.SeedFile("/ready", "row");
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/ready", "row");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? delayed.PauseReadUntilCancellation("/ready") : null;
        if (asynchronous)
        {
            var pending = script.RunAsync(delayed, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Check(result);
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.True(delayed.CompletedAsynchronously > 0);
            Assert.Equal(1, delayed.ClosedFileCount);
        }
        else
        {
            using var result = script.Run(host);
            Check(result);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        void Check(LythonExecutionResult result)
        {
            if (cancel)
            {
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("True True True [None, 7]\n", result.StandardOutput);
                return;
            }
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True True True [None, 7]\n[(True, True, True, True, 7, True, True, True)]\nwrong arity\n",
                result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StagedSelectorKeepsAliasAndRetiresRowBeforeTruthProtocol(bool asynchronous)
    {
        const string source = """
            from itertools import starmap
            preferred, alternative = [], []
            class Condition:
                def __bool__(self):
                    row = rows.send('sent')
                    print(row[0], row[1] is preferred)
                    return True
            class Selector:
                def select(self, flag, first, second):
                    return first if flag else second
            def produce():
                yield [False, 23000 + len(preferred), alternative]
                sent = yield [Condition(), preferred, alternative]
                yield [sent, preferred]
                yield [True, preferred, alternative]
            rows = produce()
            selected = starmap(Selector().select, rows)
            print(next(selected) is alternative)
            print(next(selected) is preferred)
            print(next(selected) is preferred, list(selected))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\nsent True\nTrue\nTrue []\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratorListYieldKeepsStorageThroughSuspendedSourceOrCallback(bool pauseCallback)
    {
        const string source = """
            from itertools import starmap
            marker = []
            def pause(value):
                with open('/pause') as stream:
                    stream.read()
                return value
            def identity(value):
                return value
            source_op = identity if pause_callback else pause
            callback_op = pause if pause_callback else identity
            def callback(a, b, c, d, e):
                return callback_op(a)
            rows = ([source_op(value), 2, 3, 4, 5] for value in [marker])
            print(next(starmap(callback, rows)) is marker, list(rows))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var globals = new Dictionary<string, object?> { ["pause_callback"] = pauseCallback };
        var host = new DelayedLythonHost();
        host.SeedFile("/pause", "content");
        var started = host.PauseReadUntilCancellation("/pause");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions
        {
            MemoryBudget = budget, Globals = globals, CancellationToken = cancellation.Token,
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var interrupted = await pending;
            Assert.Equal(LythonFailureReason.Cancelled, interrupted.FailureAttribution?.Reason);
            Assert.Empty(interrupted.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);

        var completedHost = new DelayedLythonHost();
        completedHost.SeedFile("/pause", "content");
        using (var completed = await script.RunAsync(completedHost, new LythonRunOptions { MemoryBudget = budget, Globals = globals }))
        {
            Assert.True(completed.Success, completed.Failure?.Message);
            Assert.Equal("True []\n", completed.StandardOutput);
        }
        Assert.Equal(1, completedHost.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ZipOperandsSurviveReentrantPullsAndRetainedSource(bool asynchronous, bool delay)
    {
        const string source = """
            from itertools import starmap
            first = [[], [], []]
            events = []
            class Interleave:
                def __init__(self):
                    self.step = 0
                def __iter__(self):
                    return self
                def __next__(self):
                    self.step += 1
                    if self.step == 1:
                        if pause:
                            with open('/pause') as source:
                                source.read()
                        item, text = next(stars)
                        events.append((item is first[1], text))
                        return 'outer'
                    if self.step == 2:
                        return 'inner'
                    return 'tail'
            rows = zip(first, Interleave())
            stars = starmap(lambda a, b: (a, b), rows)
            item, text = next(stars)
            last = next(rows)
            print(item is first[0], text, events, last[0] is first[2], last[1], list(stars))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions
        {
            MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["pause"] = delay },
        };
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ILythonHost host = new MockLythonHost();
        if (delay)
        {
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/pause", "ready");
            delayed.BeforeFileClose = () => { closing.TrySetResult(); return new ValueTask(release.Task); };
            host = delayed;
        }
        var run = asynchronous ? script.RunAsync(host, options) : Task.FromResult(script.Run(host, options));
        try
        {
            if (delay)
            {
                await closing.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(run.IsCompleted);
                release.TrySetResult();
            }
            using var result = await run;
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True outer [(True, 'inner')] True tail []\n", result.StandardOutput);
        }
        finally
        {
            release.TrySetResult();
            using var drained = await run;
        }
        if (host is DelayedLythonHost closed) Assert.Equal(1, closed.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedCallbackFailureLeavesTheNextRowAvailable(bool zipped)
    {
        const string source = """
            from itertools import starmap
            def callback(value):
                if value == 0:
                    raise LookupError('first row')
                return value
            rows = zip([0, 1]) if zipped else [(0,), (1,)]
            mapped = starmap(callback, rows)
            try:
                next(mapped)
            except LookupError as error:
                print(str(error))
            print(next(mapped), list(mapped))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions
        {
            MemoryBudget = budget,
            Globals = new Dictionary<string, object?> { ["zipped"] = zipped },
        };
        using (var result = await script.RunAsync(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("first row\n1 []\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZipAdvanceKeepsItsOperandsThroughSuspendedRowOrStrictTail(bool strictTail)
    {
        const string source = """
            from itertools import starmap
            marker = []
            first = [] if strict_tail else [marker]
            def classify(value, line):
                return (value is marker, line.strip())
            with open('/pause') as stream:
                try:
                    item = next(starmap(classify, zip(first, stream, strict=strict_tail)))
                    print(item)
                except ValueError as error:
                    print(str(error))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var globals = new Dictionary<string, object?> { ["strict_tail"] = strictTail };
        var blocked = new DelayedLythonHost();
        blocked.SeedFile("/pause", "ready\n");
        var started = blocked.PauseReadUntilCancellation("/pause");
        using var cancellation = new CancellationTokenSource();
        var run = script.RunAsync(blocked, new LythonRunOptions
        {
            MemoryBudget = budget, Globals = globals, CancellationToken = cancellation.Token
        });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var interrupted = await run;
            Assert.Equal(LythonFailureReason.Cancelled, interrupted.FailureAttribution?.Reason);
            Assert.Empty(interrupted.StandardOutput);
        }
        Assert.Equal(1, blocked.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);

        var ready = new DelayedLythonHost();
        ready.SeedFile("/pause", "ready\n");
        using (var result = await script.RunAsync(ready, new LythonRunOptions { MemoryBudget = budget, Globals = globals }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(strictTail ? "zip() argument 2 is longer than argument 1\n" : "(True, 'ready')\n", result.StandardOutput);
        }
        Assert.Equal(1, ready.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }
}
