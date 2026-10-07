using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MembershipTypeSlotDelayedTests
{
    private const string Source = """
        class Answer:
            def __bool__(self):
                print('truth')
                with open('/truth') as marker:
                    return marker.read() == 'yes'
        class Container:
            def __getattribute__(self, name):
                raise RuntimeError('ordinary lookup')
            @property
            def __contains__(self):
                print('descriptor')
                with open('/descriptor') as marker:
                    marker.read()
                def contains(needle):
                    print('contains', needle)
                    with open('/contains') as marker:
                        marker.read()
                    return Answer()
                return contains
        print('x' in Container())
        """;

    [Fact]
    public async Task DescriptorCallAndTruthAwaitWithoutOrdinaryLookup()
    {
        var script = Compile();
        var syncHost = new MockLythonHost();
        foreach (var path in new[] { "/descriptor", "/contains", "/truth" }) syncHost.SeedFile(path, "yes");
        var sync = script.Run(syncHost);
        var host = Host();
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("descriptor\ncontains x\ntruth\nTrue\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 3);
    }

    [Theory]
    [InlineData("/descriptor", "descriptor\n")]
    [InlineData("/contains", "descriptor\ncontains x\n")]
    [InlineData("/truth", "descriptor\ncontains x\ntruth\n")]
    public async Task CancellationStopsBeforeLaterProtocolEffects(string path, string expected)
    {
        var host = Host();
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile().RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    private static DelayedLythonHost Host()
    {
        var host = new DelayedLythonHost();
        foreach (var path in new[] { "/descriptor", "/contains", "/truth" }) host.SeedFile(path, "yes");
        return host;
    }

    private static LythonCompiledScript Compile()
    {
        var script = new LythonEngine().Compile(Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
