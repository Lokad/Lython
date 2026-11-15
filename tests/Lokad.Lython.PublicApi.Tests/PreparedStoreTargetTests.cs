using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedStoreTargetTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ClassBodyHeadersKeepOrderAliasesAndFundSuspendedSetters(bool manager, bool cancel)
    {
        var header = manager
            ? "with Manager() as (box[slot('first')], (box[slot('second')], *rest)):\n saved=rest"
            : "for box[slot('first')], (box[slot('second')], *rest) in [(payload,(payload,payload))]:\n saved=rest";
        var source = """
            events=[]
            values={}
            payload=['x'*4096]
            class Box:
             def __setitem__(self,key,value):
              with open('data.txt') as stream:
               events.append(key+stream.read())
              values[key]=value
            box=Box()
            def slot(name):
             events.append('index:'+name)
             return name
            class Manager:
             def __enter__(self): return payload,(payload,payload)
             def __exit__(self,kind,value,trace): return False
            """ + "\nclass Stored:\n" + string.Join("\n", header.Split('\n').Select(line => " " + line)) + """

            assert values['first'] is payload
            assert values['second'] is payload
            assert Stored.saved[0] is payload
            print(events)
            return Stored.saved
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            const string expected = "['index:first', 'first!', 'index:second', 'second!']\n";
            if (!cancel)
            {
                var host = new MockLythonHost(); host.SeedFile("data.txt", "!");
                using (var result = script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                    Assert.NotNull(result.ReturnValue);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            var delayed = new DelayedLythonHost(); delayed.SeedFile("data.txt", "!");
            var entered = cancel ? delayed.PauseReadUntilCancellation("/data.txt") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(delayed, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    Assert.Equal(!cancel, result.Success);
                    if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.Equal(cancel ? "" : expected, result.StandardOutput);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.True(delayed.ClosedFileCount > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task TargetLambdasKeepTheirMethodClassCellAndArgumentAlias()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            class Manager:
             def __init__(self,value): self.value=value
             def __enter__(self): return self.value
             def __exit__(self,kind,value,trace): return False
            class Entry:
             def run(self,payload):
              values={}
              with Manager(payload) as values[(lambda:(__class__.__name__,len(payload)))()]:
               pass
              key=next(iter(values))
              return key,values[key] is payload
            print(Entry().run([17]))
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("(('Entry', 1), True)\n", sync.StandardOutput);
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(sync.StandardOutput, asynchronous.StandardOutput);
    }
}
