using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class XmlAttributeLookupTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["protocol-values-defaults-and-errors", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r name="kept"/>')
            events=[]
            class Key:
                def __hash__(self):
                    events.append('hash')
                    return hash('name')
                def __eq__(self,other):
                    events.append('equal')
                    return other=='name'
            print(root.get(Key(),'missing'),events)
            default=[]
            print(root.get('name'),root.get('missing'),root.get('missing',default) is default)
            for key in [[],{}]:
                try: root.get(key)
                except TypeError: print('TypeError')
            class Broken:
                def __hash__(self): raise ValueError('hash')
            try: root.get(Broken())
            except ValueError as error: print(error)
            class GuestMemory:
                def __hash__(self): raise MemoryError('guest')
            try: root.get(GuestMemory())
            except MemoryError as error: print(error)
            """, "kept ['hash', 'equal']\nkept None True\nTypeError\nTypeError\nhash\nguest\n"];
        yield return ["hash-keeps-the-already-bound-attribute-map", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r name="kept"/>')
            alias=root.attrib
            get=root.get
            class Key:
                def __hash__(self):
                    root.clear()
                    return hash('name')
                def __eq__(self,other): return other=='name'
            print(get(Key(),'missing'),root.attrib,alias)
            print(root.get('name','new map'),alias['name'])
            """, "kept {} {'name': 'kept'}\nnew map kept\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task AttributeLookupMatchesPython(string name, string source, string expected)
    {
        _ = name;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private const string ReadingKey = """
        import xml.etree.ElementTree as ET
        class Key:
            def __hash__(self):
                root.clear()
                with open('hash.txt') as source: name=source.read()
                return hash(name)
            def __eq__(self,other):
                with open('equal.txt') as source: name=source.read()
                return other==name
        root=ET.fromstring('<r name="kept"/>')
        value=root.get(Key(),'missing')
        with open('out.txt','w') as target: target.write(value)
        print(value)
        """;

    [Fact]
    public async Task AttributeKeyProtocolsAwaitRealHostReads()
    {
        using var script = new LythonEngine().Compile(ReadingKey);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/hash.txt", "name");
        immediate.SeedFile("/equal.txt", "name");
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/hash.txt", "name");
        delayed.SeedFile("/equal.txt", "name");
        using (var sync = script.Run(immediate, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(sync.Success, sync.Failure?.Message);
            Assert.Equal("kept\n", sync.StandardOutput);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using (var result = await script.RunAsync(delayed, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("kept\n", result.StandardOutput);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal("kept", immediate.ReadText("/out.txt"));
        Assert.Equal("kept", delayed.ReadText("/out.txt"));
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("/hash.txt")]
    [InlineData("/equal.txt")]
    public async Task PendingKeyProtocolsKeepFundingAndJoinCancellation(string path)
    {
        using var script = new LythonEngine().Compile(ReadingKey);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost();
        host.SeedFile("/hash.txt", "name");
        host.SeedFile("/equal.txt", "name");
        host.SeedFile("/out.txt", "original");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = host.PauseReadUntilCancellation(path);
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.True(budget.ReservedBytes > 0);
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("original", host.ReadText("/out.txt"));
        }
        Assert.Equal(0, budget.ReservedBytes);
        var retryHost = new MockLythonHost();
        retryHost.SeedFile("/hash.txt", "name");
        retryHost.SeedFile("/equal.txt", "name");
        using (var result = script.Run(retryHost, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("kept", retryHost.ReadText("/out.txt"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__hash__")]
    [InlineData("__eq__")]
    public async Task RefusedAdmissionInsideAKeyProtocolCannotBeSuppressed(string protocol)
    {
        var source = """
            import xml.etree.ElementTree as ET
            import time
            def refuse():
                try:
                    time.time()
                    value='x'*65536
                except MemoryError: return True
                return True
            class Key:
                def __hash__(self):
                    HASH
                    return hash('name')
                def __eq__(self,other):
                    EQUAL
                    return other=='name'
            root=ET.fromstring('<r name="kept"/>')
            try: root.get(Key())
            except MemoryError: print('suppressed')
            print('must not run')
            """.Replace("HASH", protocol == "__hash__" ? "refuse()" : "pass", StringComparison.Ordinal)
                .Replace("EQUAL", protocol == "__eq__" ? "refuse()" : "pass", StringComparison.Ordinal);
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var host = new AdmissionGateHost(budget, 0);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }
}
