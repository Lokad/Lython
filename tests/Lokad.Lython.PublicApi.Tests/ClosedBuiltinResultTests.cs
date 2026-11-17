using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClosedBuiltinResultTests
{
    public static TheoryData<string> Programs => new()
    {
        """
        def parse(text): return float(text)
        def measure(text): return len(text)
        a = float('2.5')
        b = int('2147483648')
        c = len('😀é')
        aliases = [a, b, c]
        assert aliases[0] is a and aliases[1] is b and aliases[2] is c
        assert parse('3.25') + a == 5.75
        assert measure('😀é') == 2
        def capture(): return a, b, c
        captured = capture()
        assert captured[0] is a and captured[1] is b and captured[2] is c
        return 'ok'
        """,
        """
        def values():
            for text in ['1.25', '2.5', '-0.75']:
                yield float(text)
        assert sum(values()) == 3.0
        assert sum(int(text) for text in ['2147483648', '-2147483648', '9']) == 9
        huge = int('9' * 101)
        alias = huge
        assert huge is alias and huge == 10 ** 101 - 1
        text = 'already a string'
        assert str(text) is text
        assert abs(-7) == 7 and sum(b'abc') == 294
        return 'ok'
        """,
        """
        native_float, native_int, native_len = float, int, len
        events = []
        def argument():
            global float, int, len
            float = lambda value: events.append('float') or 19
            int = lambda value: events.append('int') or 23
            len = lambda value: events.append('len') or 29
            return '2.5'
        assert native_float(argument()) == 2.5
        assert native_int('31') == 31 and native_len('😀') == 1
        assert float('ignored') == 19 and int('ignored') == 23 and len('ignored') == 29
        assert events == ['float', 'int', 'len']
        return 'ok'
        """
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ResultsKeepPythonAliasesAndCallableBoundaries(string source)
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
    public async Task PendingIoAndRetainedResultsKeepTheirFunding(bool cancel)
    {
        const string source = """
            def make():
                value = float('2.5')
                whole = int('9223372036854775808')
                count = len('😀é')
                alias = value
                with open('/pending.txt', 'a') as target: target.write('done')
                assert value is alias
                result = [value, whole, count, alias]
                assert result[0] is result[3]
                return result
            return make()
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/pending.txt", "before");
            using (var retained = script.Run(immediate))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var retainedBaseline = budget.ReservedBytes;
                Assert.True(retainedBaseline > baseline);
                var host = new DelayedLythonHost(); host.SeedFile("/pending.txt", "before");
                using var cancellation = new CancellationTokenSource();
                var entered = cancel ? host.PauseWriteUntilCancellation("/pending.txt") : null;
                var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
                try
                {
                    if (entered is not null)
                    {
                        await entered.WaitAsync(TimeSpan.FromSeconds(5));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > retainedBaseline);
                        cancellation.Cancel();
                    }
                    using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Equal(!cancel, result.Success);
                    Assert.Equal(cancel ? "before" : "beforedone", host.ReadText("/pending.txt"));
                    if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    else Assert.True(host.CompletedAsynchronously > 0);
                }
                finally
                {
                    cancellation.Cancel();
                    using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                }
                Assert.Equal(retainedBaseline, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
