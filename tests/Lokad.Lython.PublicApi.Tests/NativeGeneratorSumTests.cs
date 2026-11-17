using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeGeneratorSumTests
{
    public static TheoryData<string> Programs => new()
    {
        """
        def values():
            yield from [1e16, 1.0, -1e16]
        assert sum(values()) == 1.0
        assert sum((x for x in [1 << 62, 1 << 62, -(1 << 63), 1e16, 1.0, -1e16])) == 0.0
        assert sum((x for x in [1e16, 1, -1e16])) == 0.0
        marker = []
        assert sum((x for x in []), marker) is marker
        return 'ok'
        """,
        """
        events = []
        def values():
            try:
                yield from range(37)
                yield 'bad'
            except TypeError: events.append('wrong handler')
            yield 5
        g = values()
        try: sum(g)
        except TypeError: pass
        else: assert False
        assert next(g) == 5
        assert events == []
        return 'ok'
        """,
        """
        class Box:
            def __radd__(self, total):
                assert total == sum(range(37))
                assert next(g) == 4
                return 7
        def values():
            yield from range(37)
            yield Box()
            yield 4
            yield 9
        g = values()
        assert sum(g) == 16
        assert next(g, 'end') == 'end'
        return 'ok'
        """,
        """
        def values():
            try: raise KeyError('inner')
            except KeyError as original:
                for i in range(37):
                    try: next(g)
                    except ValueError as error: assert str(error) == 'generator already executing'
                    else: assert False
                    try: raise
                    except KeyError as error: assert error is original
                    yield i
            try: raise
            except ValueError as error: assert str(error) == 'caller'
        g = values()
        try: raise ValueError('caller')
        except ValueError as original:
            assert sum(g) == sum(range(37))
            try: raise
            except ValueError as error: assert error is original
        return 'ok'
        """
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NumericConsumptionPreservesGeneratorBoundaries(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
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
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingGeneratorIoRemainsFundedAndIsJoined(bool cancel)
    {
        const string source = """
            def values():
                yield from range(37)
                with open('/sum.txt', 'a') as target: target.write('done')
                yield 3
            return sum(values())
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/sum.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/sum.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "before" : "beforedone", host.ReadText("/sum.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else
            {
                Assert.Equal(new System.Numerics.BigInteger(669), result.ReturnValue);
                Assert.True(host.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalInNumericGeneratorCannotBeSuppressed(bool asynchronous)
    {
        const string source = """
            def values():
                try:
                    yield from range(37)
                    yield len('x' * 2000000)
                finally: return
            try: print(sum(values()))
            except MemoryError: print('caught')
            print('after')
            """;
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
