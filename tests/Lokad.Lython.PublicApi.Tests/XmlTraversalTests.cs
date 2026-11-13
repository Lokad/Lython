using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class XmlTraversalTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["tree-order-filter-and-mixed-text", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r>before<x>inside<y/>middle</x>after<x/>last</r>')
            print([node.tag for node in root.iter()])
            print([node.tag for node in root.iter(tag='x')])
            print([node.tag for node in root.iter('*')])
            print([node.tag for node in root.iter(None)])
            print(list(root.itertext()))
            print(list(root[0].itertext()))
            print(list(root.iter('missing')),list(root.iter(3)))
            """, "['r', 'x', 'y', 'x']\n['x', 'x']\n['r', 'x', 'y', 'x']\n['r', 'x', 'y', 'x']\n['before', 'inside', 'middle', 'after', 'last']\n['inside', 'middle']\n[] []\n"];
        yield return ["clear-retains-map-and-child-aliases", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r a="kept">before<x b="child">inside</x>after</r>')
            attrs=root.attrib;child=root[0];text=root.text;tail=child.tail
            print(root.clear())
            print(root.tag,root.attrib,root.text,root.tail,len(root))
            print(attrs,child.attrib,child.text,text,tail,root.attrib is attrs)
            attrs['a']='old';root.attrib['a']='new'
            print(attrs,root.attrib,list(root.itertext()))
            child_attrs=child.attrib
            child.clear()
            print(child.tag,child_attrs,child.attrib,child.text,child.tail)
            """, "None\nr {} None None 0\n{'a': 'kept'} {'b': 'child'} inside before after False\n{'a': 'old'} {'a': 'new'} []\nx {'b': 'child'} {} None None\n"];
        yield return ["lazy-identity-empty-trees-and-binding", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r><x/></r>')
            iterator=root.iter()
            print(iter(iterator) is iterator,next(iterator) is root,next(iterator) is root[0])
            print(next(iterator,None),next(iterator,None))
            root.clear()
            print([node.tag for node in root.iter()],list(root.itertext()))
            for call in [lambda: root.iter(1,2),lambda: root.iter(unknown=1),lambda: root.itertext(1),lambda: root.clear(1)]:
                try: call()
                except TypeError: print('TypeError')
            """, "True True True\nNone None\n['r'] []\nTypeError\nTypeError\nTypeError\nTypeError\n"];
        yield return ["clear-during-iteration-and-stale-depth-bounds", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r><group>text<x/><y/></group><z/></r>')
            iterator=root.iter()
            print(next(iterator).tag)
            root[0].clear()
            print([node.tag for node in iterator])
            iterator=root.iter()
            root.clear()
            print([node.tag for node in iterator])
            print([node.tag for node in root.iter()])
            """, "r\n['group', 'z']\n['r']\n['r']\n"];
        yield return ["predicate-errors-are-lazy-and-iteration-can-resume", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r><x/><y/></r>')
            class Tag:
                def __eq__(self,other):
                    print('equal',other)
                    if other=='r': raise ValueError('root')
                    return other=='x'
            iterator=root.iter(Tag())
            print('created')
            try: next(iterator)
            except ValueError: print('ValueError')
            print(next(iterator).tag,next(iterator,None))
            """, "created\nequal r\nValueError\nequal x\nequal y\nx None\n"];
        yield return ["namespaces-and-deep-iteration", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<x xmlns="urn:x">'*1024+'<leaf>é😀</leaf>'+'</x>'*1024)
            print(sum(1 for node in root.iter()),sum(1 for node in root.iter('{urn:x}x')))
            print(list(root.itertext()))
            root.clear()
            print(sum(1 for node in root.iter()),list(root.itertext()))
            """, "1025 1024\n['é😀']\n1 []\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task TreeTraversalMatchesPython(string name, string source, string expected)
    {
        _ = name;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    private const string AsyncTagSource = """
        import xml.etree.ElementTree as ET
        class Tag:
            def __eq__(self,other):
                with open('tag.txt') as source: wanted=source.read()
                return other==wanted
        root=ET.fromstring('<r><x>é😀</x><y>other</y></r>')
        for element in root.iter(Tag()):
            with open('out.txt','w') as target: target.write(element.text)
        print('done')
        """;

    [Fact]
    public async Task TagEqualityAwaitsRealHostReads()
    {
        using var script = new LythonEngine().Compile(AsyncTagSource);
        Assert.True(script.IsValid);
        var immediate = new MockLythonHost(); immediate.SeedFile("/tag.txt", "x");
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/tag.txt", "x");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(delayed, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("done\n", result.StandardOutput);
                Assert.True(budget.ReservedBytes > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal("é😀", immediate.ReadText("/out.txt"));
        Assert.Equal("é😀", delayed.ReadText("/out.txt"));
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task PendingEqualityKeepsStorageFundedAndCancellationJoinsCleanup()
    {
        using var script = new LythonEngine().Compile(AsyncTagSource);
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost(); host.SeedFile("/tag.txt", "x"); host.SeedFile("/out.txt", "original");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = host.PauseReadUntilCancellation("/tag.txt");
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
        var retryHost = new MockLythonHost(); retryHost.SeedFile("/tag.txt", "x");
        using (var retry = script.Run(retryHost, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(retry.Success, retry.Failure?.Message);
            Assert.Equal("é😀", retryHost.ReadText("/out.txt"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RefusedAdmissionInsideTagEqualityCannotBeSuppressed()
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            import time
            class Tag:
                def __eq__(self,other):
                    try:
                        time.time()
                        value='x'*65536
                    except MemoryError:
                        return True
                    return True
            root=ET.fromstring('<r><x/></r>')
            for node in root.iter(Tag()): print('must not run')
            """);
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
