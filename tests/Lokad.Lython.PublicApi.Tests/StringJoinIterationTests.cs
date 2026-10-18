using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringJoinIterationTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        {
            foreach (var form in new[] { "direct", "exposed", "expanded" })
            {
                var call = form switch { "exposed" => "method(parts())", "expanded" => "'|'.join(*[parts()])", _ => "'|'.join(parts())" };
                foreach (var failLate in new[] { false, true })
                {
                    var source = "events=[]\ndef parts():\n events.append('first')\n yield 1\n events.append('second')\n "
                        + (failLate ? "raise ValueError('later iteration failure')" : "yield 'valid'")
                        + "\nmethod='|'.join\ntry:\n " + call + "\nexcept Exception as error:\n print(type(error).__name__,str(error))\nprint(events)";
                    var expected = (failLate ? "ValueError later iteration failure" : "TypeError sequence item 0: expected str instance, int found")
                        + "\n['first', 'second']\n";
                    yield return [asynchronous, form + (failLate ? "-late-error" : "-drain"), source, expected];
                }
            }
            yield return [asynchronous, "values", "print('|'.join([]),'|'.join(['one']),'|'.join(('café','😀')),'-'.join('ab'))", " one café|😀 a-b\n"];
            yield return [asynchronous, "custom-iterator", "events=[]\nclass Parts:\n def __iter__(self):\n  events.append('iter')\n  yield 1\n  events.append('after')\n  raise ValueError('custom')\ntry:'|'.join(Parts())\nexcept Exception as error:print(type(error).__name__,str(error))\nprint(events)", "ValueError custom\n['iter', 'after']\n"];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesPythonDrainAndErrorOrder(bool asynchronous, string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DrainAwaitsHostIoBeforeValidationAndCleanup(bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("def parts():\n yield 1\n with open('audit.txt','a') as output:output.write('seen')\n yield 'valid'\nreturn '|'.join(parts())", budget);
        Assert.True(script.IsValid);
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
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.Equal("TypeError", result.Failure?.ExceptionType);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedResultKeepsSharedFundingAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("return '|'.join(['café','😀'])", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal("café|😀", retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                    Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
                Assert.Equal("café|😀", retained.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
