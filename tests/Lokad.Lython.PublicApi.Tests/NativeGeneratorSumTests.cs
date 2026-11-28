using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeGeneratorSumTests
{
    public static TheoryData<string> Programs => new()
    {
        """
        events = []
        builtin = sum
        def replacement(items):
            assert type(items).__name__ == 'generator'
            events.append('replacement')
            return next(items)
        class Source:
            def __iter__(self):
                global sum
                events.append('iter')
                sum = replacement
                return iter((2, 3))
        class Term:
            def __radd__(self, left):
                events.append(left)
                return 7
        assert sum(x for x in Source()) == 5
        assert sum(x for x in (4, 5)) == 4
        assert builtin(x for x in (Term(), 2)) == 9
        assert events == ['iter', 'replacement', 0]
        return 'ok'
        """,
        """
        from collections import ChainMap
        kept = []
        class Collector:
            def __radd__(self, total): return self
            def __add__(self, value):
                kept.append(value)
                return self
        head = Collector()
        def collect():
            return sum(head if index == 0 else ChainMap({'value':index}) if index == 1
                       else (lambda: index) for index in (0, 1, 2))
        assert collect() is head
        assert kept[0]['value'] == 1
        assert kept[1]() == 2
        return 'ok'
        """,
        """
        observed = []
        def take(value, outer):
            observed.append(outer)
            if value == 2:
                assert next(outer) == 3
            return value
        def reduce(values):
            return sum(value if value == 1 else take(value, locals()['.0']) for value in values)
        assert reduce((1, 2, 3, 5, 7)) == 15
        assert len(observed) == 3
        assert observed[0] is observed[1] is observed[2]
        assert next(observed[0], 'end') == 'end'
        return 'ok'
        """,
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
        checks = []
        class Term:
            def __add__(self, right):
                checks.append(right)
                return 7
            def __radd__(self, left):
                checks.append(left)
                return left + 8
        def arithmetic(values):
            return sum(value + 1 for value in values)
        def consume(values):
            return sum(value for value in values)
        assert arithmetic((2, 3, Term(), 4)) == 19
        assert consume((2, 3, Term(), 4)) == 17
        assert consume((1 << 62, 1 << 62, -4)) == (1 << 63) - 4
        assert consume([3, 1e16, 1.0, -1e16, -3]) == 2.0
        assert consume(range(100)) == 4950
        assert checks == [1, 5]
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
                try: sum(g)
                except ValueError as error: assert str(error) == 'generator already executing'
                else: assert False
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
    public async Task PrivateOuterIteratorsReturnTheirAllowance(bool asynchronous)
    {
        const string source = """
            items = (2, 3)
            total = 0
            for index in range(256):
                total += sum(item for item in items)
            return total
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(16 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var host = new MockLythonHost();
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(1280), result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedPrivateIteratorAdmissionPrecedesGeneratorEffects(bool asynchronous)
    {
        const string source = """
            import time
            items = (2, 3)
            def effect(item):
                print('callback')
                return item
            time.time()
            try:
                sum(effect(item) for item in items)
            except MemoryError:
                print('caught')
            print('continued')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 0);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(8, result.Failure?.Span?.Line);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PendingGeneratorIoRemainsFundedAndIsJoined(bool cancel, bool exactOuter)
    {
        var source = exactOuter ? """
            class Term:
                def __radd__(self, total):
                    assert total == 660
                    with open('/sum.txt', 'a') as target: target.write('done')
                    return total + 6
            def reduce(terms):
                return sum(term for term in terms)
            return reduce((220, 440, Term(), 3))
            """ : """
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
