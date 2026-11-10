using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerLocalSemanticsTests
{
    // Outputs captured independently with CPython 3.13.2.
    public static TheoryData<string, string> Programs => new()
    {
        {
            """
            value = 1000
            for i in range(3):
                value += 1
            alias = value
            retained = [value]
            print(value is value, alias is value, retained[0] is value)
            value += 7
            print(alias, retained[0], value)
            value = True
            value += 2
            print(type(value).__name__, value)
            value = 1.5
            value += 2
            print(type(value).__name__, value)
            value = 'a'
            value += 'b'
            print(type(value).__name__, value)
            """,
            "True True True\n1003 1003 1010\nint 3\nfloat 3.5\nstr ab\n"
        },
        {
            """
            for seed in [9223372036854775807, -9223372036854775808, 7, -7]:
                value = seed
                value += 1
                print(value)
                value -= 2
                print(value)
                value *= -3
                print(value)
                value = seed
                value <<= 63
                print(value)
                value >>= 64
                print(value)
                value = seed
                value ^= -1
                value &= 15
                value |= 32
                print(value)
            """,
            "9223372036854775808\n9223372036854775806\n-27670116110564327418\n85070591730234615856620279821087277056\n4611686018427387903\n32\n-9223372036854775807\n-9223372036854775809\n27670116110564327427\n-85070591730234615865843651857942052864\n-4611686018427387904\n47\n8\n6\n-18\n64563604257983430656\n3\n40\n-6\n-8\n24\n-64563604257983430656\n-4\n38\n"
        },
        {
            """
            count = 12
            while count:
                count -= 1
            print(count, count == 0, count != 1, count < 1, count <= 0, count > -1, count >= 0)
            del count
            try:
                print(count)
            except NameError:
                print('missing')
            count = None
            print(count)
            count = 1
            count += 2
            print(count)
            """,
            "0 True True True True True True\nmissing\nNone\n3\n"
        },
        {
            """
            value = 1000
            value += 1
            namespace = globals()
            print(namespace['value'], namespace['value'] is value)
            namespace['value'] = 4
            value += 2
            print(value, namespace['value'])
            del namespace['value']
            try:
                print(value)
            except NameError:
                print('missing')
            value = 9
            value += 1
            print(value, namespace['value'])
            """,
            "1001 True\n6 6\nmissing\n10 10\n"
        },
        {
            """
            def generate():
                value = 1000
                value += 1
                yield value
                value += 2
                yield value
                if True:
                    del value
                try:
                    yield value
                except UnboundLocalError:
                    yield 'missing'
            iterator = generate()
            first = next(iterator)
            second = next(iterator)
            print(first, second, first is first, next(iterator))
            """,
            "1001 1003 True missing\n"
        },
    };

    [Theory, MemberData(nameof(Programs))]
    public async Task UpdatesPreserveValuesAliasesAndNamespaces(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedReadCancellationRefundsCountersAndPreservesRerunAliases(bool asynchronousRerun)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            value = 1000
            value += 1
            retained = [value]
            with open('/gate.txt') as source:
                source.read()
            value += 2
            return [retained[0], value]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate.txt", "data");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/gate.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        // A fresh run uses the same allowance, and retained projection remains
        // charged until the caller disposes it.
        var immediate = new MockLythonHost();
        immediate.SeedFile("/gate.txt", "data");
        using (var completed = asynchronousRerun ? await script.RunAsync(immediate) : script.Run(immediate))
        {
            Assert.True(completed.Success, completed.Failure?.Message);
            var values = Assert.IsType<List<object?>>(completed.ReturnValue);
            Assert.Equal(new BigInteger(1001), Assert.IsType<BigInteger>(values[0]));
            Assert.Equal(new BigInteger(1003), Assert.IsType<BigInteger>(values[1]));
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedWideUpdateCannotBeSuppressedAndAllowanceRemainsReusable(bool asynchronous, bool finallyReturn)
    {
        var tail = finallyReturn ? "finally:\n return 0" : "except MemoryError:\n print('caught')";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(
            "value = 1\nvalue += 2\nprint('ready')\ntry:\n value <<= 524288\n" + tail + "\nprint('after')\nreturn value", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        ILythonMemoryReservation? padding = null;
        host.OnStandardOutputWrite = () =>
        {
            if (padding is null) Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 4096, out padding));
        };
        try
        {
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.FailureAttribution?.Requested >= 65536);
            Assert.Equal("ready\n", result.StandardOutput);
            Assert.NotNull(padding);
        }
        finally { padding?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(finallyReturn ? BigInteger.Zero : new BigInteger(3) << 524288, Assert.IsType<BigInteger>(result.ReturnValue));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
