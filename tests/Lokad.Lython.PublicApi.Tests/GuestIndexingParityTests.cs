using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GuestIndexingParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuestLookupPreservesKeysOrderAndExceptions(bool delayed)
    {
        const string script = """
            events = []
            class C:
                def __getitem__(self, key):
                    events.append(key)
                    with open('/value') as f:
                        prefix = f.read()
                    if key == 'bad':
                        raise KeyError(key)
                    return prefix + str(key)
            def receiver():
                events.append('receiver')
                return C()
            def key():
                events.append('key')
                return (2, 3)
            print(receiver()[key()])
            try:
                C()['bad']
            except KeyError:
                print('caught')
            print(events)
            """;
        var compiled = new LythonEngine().Compile(script);
        Assert.True(compiled.IsValid);
        var host = new MockLythonHost();
        host.SeedFile("/value", "v");
        var delayedHost = new DelayedLythonHost();
        delayedHost.SeedFile("/value", "v");
        var result = delayed ? await compiled.RunAsync(delayedHost) : compiled.Run(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("v(2, 3)\ncaught\n['receiver', 'key', (2, 3), 'bad']\n", result.StandardOutput);
        if (delayed) Assert.True(delayedHost.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task ScalarNestedLookupsAgreeBetweenModes()
    {
        var compiled = new LythonEngine().Compile("""
            class C:
                def __getitem__(self, key):
                    return key + 1
            print(C()[C()[3]])
            """);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("5\n", result.StandardOutput);
        }
    }
}
