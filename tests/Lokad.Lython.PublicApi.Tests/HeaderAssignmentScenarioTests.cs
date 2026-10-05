using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HeaderAssignmentScenarioTests
{
    [Theory]
    [InlineData("for receiver()[index()] in [1, 2]:\n    pass\n")]
    [InlineData("[None for receiver()[index()] in [1, 2]]\n")]
    [InlineData("list(None for receiver()[index()] in [1, 2])\n")]
    public async Task SubscriptTargetsAwaitReceiversIndicesAndGuestSetters(string header)
    {
        var script = new LythonEngine().Compile("""
            events=[]
            def effect(label):
                with open('data.txt') as f:
                    events.append(label + f.read())
            class Box:
                def __setitem__(self, index, value):
                    effect('set')
                    events.append((index,value))
            box=Box()
            def receiver():
                effect('receiver')
                return box
            def index():
                effect('index')
                return 0
            """ + "\n" + header + "print(events)\n");
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("data.txt", "!");
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("['receiver!', 'index!', 'set!', (0, 1), 'receiver!', 'index!', 'set!', (0, 2)]\n", sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("data.txt", "!");
        var result = await script.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously >= 6);
    }

    [Fact]
    public async Task AttributeTargetsAwaitPropertySetters()
    {
        var script = new LythonEngine().Compile("""
            class Box:
                @property
                def value(self):
                    return 0
                @value.setter
                def value(self, value):
                    with open('data.txt') as f:
                        print(value, f.read())
            box=Box()
            for box.value in [1,2]:
                pass
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("data.txt", "!");
        var result = await script.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("1 !\n2 !\n", result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }
}
