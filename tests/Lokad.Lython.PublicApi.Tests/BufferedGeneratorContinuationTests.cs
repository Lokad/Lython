using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BufferedGeneratorContinuationTests
{
    [Fact]
    public async Task LambdaReturnsAndConsumerErrorsPreserveSuspendedYieldValues()
    {
        const string source = """
            f = lambda: (yield 7)
            g = f()
            assert next(g) == 7
            try: g.send(3)
            except StopIteration as error: assert error.value == 3
            else: assert False
            assert sum(f()) == 7
            def nested(): yield (yield 2)
            g = nested()
            try: sum(g)
            except TypeError: pass
            else: assert False
            assert next(g, 'end') == 'end'
            return 'ok'
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok", result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task GeneratorCloseAwaitsNestedFileOwnersBeforeReturning()
    {
        const string source = """
            def scan():
                with open('/outer') as outer:
                    with open('/inner') as inner:
                        yield outer.readline() + inner.readline()
            g = scan()
            assert next(g) == 'one two'
            g.close()
            print('closed')
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/outer", "one ");
        host.SeedFile("/inner", "two");
        var closing = new[] { new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var release = new[] { new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var output = 0;
        host.OnStandardOutputWrite = () => Interlocked.Increment(ref output);
        host.BeforeFileClose = async () =>
        {
            var index = host.ClosedFileCount;
            closing[index].SetResult();
            await release[index].Task;
        };

        var pending = script.RunAsync(host);
        try
        {
            for (var index = 0; index < closing.Length; index++)
            {
                var first = await Task.WhenAny(closing[index].Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Execution returned before its file cleanup.");
                }
                Assert.Equal(index, host.ClosedFileCount);
                Assert.Equal(0, Volatile.Read(ref output));
                Assert.True(budget.ReservedBytes > baseline);
                release[index].SetResult();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(2, host.ClosedFileCount);
            Assert.Equal("closed\n", result.StandardOutput);
        }
        finally
        {
            foreach (var gate in release) gate.TrySetResult();
            using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "guest-error")]
    [InlineData(true, "guest-error")]
    [InlineData(false, "refuse")]
    [InlineData(true, "refuse")]
    [InlineData(true, "cancel")]
    public async Task PartialHelpersPreserveEffectsAliasesAndAwaitedCleanup(bool asynchronous, string mode)
    {
        const string source = """
            events = []
            marker = 'm' * 8192
            with open('/numbers') as stream:
                def reading():
                    events.append(marker)
                    value = stream.readline()
                    if mode == 'refuse': return len('x' * 8388608)
                    return float(value)
                try:
                    total = sum(reading() for index in range(3))
                except ValueError:
                    assert mode == 'guest-error'
                    assert len(events) == 2
                except MemoryError:
                    return 'unexpected recovery'
                else:
                    assert total == 9.0
                    assert len(events) == 3
                assert all(value is marker for value in events)
                return marker
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var delayed = new DelayedLythonHost();
        var synchronous = new MockLythonHost();
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        delayed.BeforeFileClose = async () =>
        {
            cleanupEntered.TrySetResult();
            await releaseCleanup.Task;
        };
        var text = mode == "guest-error" ? "2\nbad\n4\n" : "2\n3\n4\n";
        delayed.SeedFile("/numbers", text);
        synchronous.SeedFile("/numbers", text);
        var entered = mode == "cancel" ? delayed.PauseReadUntilCancellation("/numbers") : null;
        var options = new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["mode"] = mode },
            CancellationToken = cancellation.Token
        };
        var pending = asynchronous ? script.RunAsync(delayed, options) : Task.FromResult(script.Run(synchronous, options));
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Helper did not suspend in its first read.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            if (asynchronous)
            {
                var first = await Task.WhenAny(cleanupEntered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Execution returned before its file cleanup.");
                }
                Assert.False(pending.IsCompleted);
                Assert.Equal(0, delayed.ClosedFileCount);
                Assert.True(budget.ReservedBytes > baseline);
                releaseCleanup.TrySetResult();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (asynchronous) Assert.Equal(1, delayed.ClosedFileCount);
            if (mode is "refuse" or "cancel")
            {
                Assert.False(result.Success);
                Assert.Equal(mode == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new string('m', 8192), result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            releaseCleanup.TrySetResult();
            using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        synchronous.SeedFile("/numbers", "2\n3\n4\n");
        using (var reusable = script.Run(synchronous, new LythonRunOptions
        {
            Globals = new Dictionary<string, object?> { ["mode"] = "success" }
        })) Assert.True(reusable.Success, reusable.Failure?.Message);
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
