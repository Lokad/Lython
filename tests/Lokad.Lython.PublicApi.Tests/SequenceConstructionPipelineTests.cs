using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceConstructionPipelineTests
{
    [Theory]
    [InlineData("list", 0)] [InlineData("tuple", 0)]
    [InlineData("list", 1)] [InlineData("tuple", 1)]
    [InlineData("list", 8)] [InlineData("tuple", 8)]
    [InlineData("list", 9)] [InlineData("tuple", 9)]
    [InlineData("list", 37)] [InlineData("tuple", 37)]
    public async Task ConstructorsKeepAliasesAndConsumeLateFailures(string constructor, int count)
    {
        var source = $$"""
            shared = []
            integer = 10 ** 100
            events = []
            def values():
                for i in range({{count}}):
                    events.append(i)
                    yield integer if i % 2 == 0 else shared
                if fail: raise ValueError('late')
            try: result = {{constructor}}(values())
            except ValueError as error:
                assert fail and str(error) == 'late'
            else:
                assert not fail and len(result) == {{count}}
                for i in range({{count}}):
                    assert result[i] is (integer if i % 2 == 0 else shared)
            assert events == list(range({{count}}))
            return 'ok'
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            foreach (var fail in new[] { false, true })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["fail"] = fail } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
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
    [InlineData(false)] [InlineData(true)]
    public async Task ExtensionSeesGeneratorMutationOfItsLiveBacking(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            target = [-1]
            def values():
                for i in range(37):
                    if i == 6: target.clear()
                    if i == 12: target.extend([90] * 20)
                    yield i
            target.extend(values())
            assert target == [6,7,8,9,10,11] + [90] * 20 + list(range(12,37))
            return 'ok'
            """);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok", result.ReturnValue);
    }

    [Theory]
    [InlineData("list", false)] [InlineData("tuple", false)]
    [InlineData("list", true)] [InlineData("tuple", true)]
    public async Task PendingConstructionKeepsFundingAndAwaitsCleanup(string constructor, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile($$"""
            def values():
                for i in range(6): yield i
                with open('/audit.txt','a') as output: output.write('seen')
                for i in range(6,37): yield i
            result = {{constructor}}(values())
            assert len(result) == 37 and result[6] == 6 and result[-1] == 36
            return 'ok'
            """, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/audit.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/audit.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.Equal("ok", result.ReturnValue);
            Assert.Equal(cancel ? "before" : "beforeseen", host.ReadText("/audit.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
