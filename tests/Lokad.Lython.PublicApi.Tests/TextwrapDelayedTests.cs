using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextwrapDelayedTests
{
    private const string PredicateSource = """
        import textwrap
        class Truth:
            def __init__(self,line):
                self.line=line
            def __bool__(self):
                print('truth',repr(self.line))
                with open('/truth') as marker:
                    return marker.read() == 'yes'
        def predicate(line):
            print('predicate',repr(line))
            with open('/predicate') as marker:
                marker.read()
            return Truth(line)
        print(repr(textwrap.indent('a\n\nb','> ',predicate)))
        """;

    [Fact]
    public async Task PredicateAndItsTruthSlotAwaitInLineOrder()
    {
        var script = Compile(PredicateSource);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/predicate", "go");
        syncHost.SeedFile("/truth", "yes");
        var sync = script.Run(syncHost);
        var host = new DelayedLythonHost();
        host.SeedFile("/predicate", "go");
        host.SeedFile("/truth", "yes");
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("predicate 'a\\n'\ntruth 'a\\n'\npredicate '\\n'\ntruth '\\n'\npredicate 'b'\ntruth 'b'\n'> a\\n> \\n> b'\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 6);
    }

    [Fact]
    public async Task GuestLookupLineSourceWhitespaceAndTruthSlotsAwaitBeforeJoinValidation()
    {
        var script = Compile("""
            import textwrap
            def read():
                with open('/marker') as marker:
                    marker.read()
            class Truth:
                def __bool__(self):
                    read()
                    print('truth')
                    return False
            class Line:
                @property
                def isspace(self):
                    read()
                    print('space-lookup')
                    return self.space
                def space(self):
                    read()
                    print('space')
                    return Truth()
            class Text:
                @property
                def splitlines(self):
                    read()
                    print('lookup')
                    return self.split
                def split(self,keepends):
                    read()
                    print('split',keepends)
                    def lines():
                        for i in range(2):
                            read()
                            print('yield',i)
                            yield Line()
                    return lines()
            try:
                textwrap.indent(Text(),'>')
            except TypeError:
                print('TypeError')
            """);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/marker", "go");
        var sync = script.Run(syncHost);
        var host = new DelayedLythonHost();
        host.SeedFile("/marker", "go");
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("lookup\nsplit True\nyield 0\nspace-lookup\nspace\ntruth\nyield 1\nspace-lookup\nspace\ntruth\nTypeError\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 10);
    }

    [Theory]
    [InlineData("/predicate", "predicate 'a\\n'\n")]
    [InlineData("/truth", "predicate 'a\\n'\ntruth 'a\\n'\n")]
    public async Task CancellationStopsTheAwaitedPredicateOrTruthBeforeLaterLines(string path, string expected)
    {
        var script = Compile(PredicateSource);
        var host = new DelayedLythonHost();
        host.SeedFile("/predicate", "go");
        host.SeedFile("/truth", "yes");
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
