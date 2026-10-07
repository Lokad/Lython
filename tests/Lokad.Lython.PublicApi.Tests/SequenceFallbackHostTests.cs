using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceFallbackHostTests
{
    public static IEnumerable<object[]> Sources()
    {
        yield return new object[] { """
            from pathlib import Path
            class Sequence:
                def __getitem__(self,index):
                    if index>=int(Path('/url').read_text()):
                        raise IndexError
                    return index
            print(list(Sequence()))
            """ };
        yield return new object[] { """
            from pathlib import Path
            class Sequence:
                def __init__(self):
                    self.index=0
                def __iter__(self):
                    return self
                @property
                def __next__(self):
                    limit=int(Path('/url').read_text())
                    def advance():
                        if self.index>=limit:
                            raise StopIteration
                        value=self.index
                        self.index+=1
                        return value
                    return advance
            print(list(Sequence()))
            """ };
        yield return new object[] { """
            from pathlib import Path
            class Sequence:
                @property
                def __getitem__(self):
                    limit=int(Path('/url').read_text())
                    def get(index):
                        if index>=limit:
                            raise IndexError
                        return index
                    return get
            print(list(Sequence()))
            """ };
        yield return new object[] { """
            from pathlib import Path
            class Sequence:
                @property
                def __iter__(self):
                    limit=int(Path('/url').read_text())
                    return lambda:iter(range(limit))
            print(list(Sequence()))
            """ };
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task IterationSlotsAwaitMediatedHostEffects(string source)
    {
        var script = Compile(source);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/url", "3");
        var host = new DelayedLythonHost();
        host.SeedFile("/url", "3");
        var sync = script.Run(syncHost);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 1, 2]\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [MemberData(nameof(Sources))]
    public async Task CancellationStopsDuringSlotBindingOrIndexing(string source)
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/url", "3");
        var started = host.PauseReadUntilCancellation("/url");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile(source).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedIteratorsReclaimAndRetainedIteratorsStayCharged(bool retain)
    {
        var script = Compile("""
            class Sequence:
                def __getitem__(self,index):
                    if index>=1:
                        raise IndexError
                    return 0
            source=Sequence()
            held=[]
            for index in range(2000):
            """ + "\n    " + (retain ? "held.append(iter(source))" : "next(iter(source))") + "\nprint(1)");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success != retain, result.Failure?.Message);
            Assert.Equal(retain ? "" : "1\n", result.StandardOutput);
            if (retain) Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
