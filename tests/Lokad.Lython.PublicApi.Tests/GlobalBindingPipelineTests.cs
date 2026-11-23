using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GlobalBindingPipelineTests
{
    public static IEnumerable<object[]> Programs()
    {
        yield return ["""
            data=[1,2,3]
            def read(index):return data[index]
            values=(read(index) for index in range(3))
            assert next(values)==1
            data=[4,5,6]
            assert next(values)==5
            read=lambda index:index+10
            assert next(values)==12
            print('ok')
            """];
        yield return ["""
            original = float
            def replace():
                globals()['float'] = lambda text: 'shadowed ' + text
                return '2.5'
            assert float(replace()) == 2.5
            assert float('value') == 'shadowed value'
            del globals()['float']
            assert float is original and float('2.5') == 2.5
            class Scope:
                float = 'class'
                kinds = [float is original for index in range(2)]
                alias = float
            assert Scope.alias == 'class' and Scope.kinds == [True, True]
            print('ok')
            """];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task GlobalReadsFollowLiveBindingsAndNamespaceRules(string source)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            for (var run = 0; run < 2; run++)
            {
                using var result = run == 0 ? script.Run(new MockLythonHost()) : await script.RunAsync(new MockLythonHost());
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EachExecutionReadsItsOwnBindingsAcrossPendingIo(bool cancel)
    {
        const string source = """
            value=1.0
            def read():return value
            def compute():
                global value
                first=read()
                with open('input.txt') as source:value=float(source.read())
                return first,read()
            return compute()
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "7");
            using (var first = script.Run(immediate))
            {
                Assert.True(first.Success, first.Failure?.Message);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "9");
                using var cancellation = new CancellationTokenSource();
                var entered = cancel ? host.PauseReadUntilCancellation("/input.txt") : null;
                var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
                try
                {
                    if (entered is not null)
                    {
                        await entered.WaitAsync(TimeSpan.FromSeconds(10));
                        Assert.False(pending.IsCompleted);
                        Assert.True(budget.ReservedBytes > retained);
                        cancellation.Cancel();
                    }
                    using var second = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Equal(!cancel, second.Success);
                    if (cancel) Assert.Equal(LythonFailureReason.Cancelled, second.FailureAttribution?.Reason);
                    else
                    {
                        Assert.True(host.CompletedAsynchronously > 0);
                        Assert.Equal(new object[] { 1.0, 9.0 }, Assert.IsAssignableFrom<IReadOnlyList<object>>(second.ReturnValue));
                    }
                }
                finally
                {
                    cancellation.Cancel();
                    using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                }
                Assert.Equal(new object[] { 1.0, 7.0 }, Assert.IsAssignableFrom<IReadOnlyList<object>>(first.ReturnValue));
                Assert.Equal(retained, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
