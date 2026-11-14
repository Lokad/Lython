using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class XmlIterparseTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["events-root-and-borrowed-reader", """
            import xml.etree.ElementTree as ET
            class Source:
                def __init__(self): self.parts=iter([b'<r a="kept">before<x>inside</x>after<y/></r>',b''])
                def read(self,n): return next(self.parts,b'')
                def close(self): print('must not close')
            parser=ET.iterparse(Source(),events=('start','end','start'))
            print(iter(parser) is parser,parser.root)
            saved=[]
            for event,node in parser:
                saved.append(node)
                print(event,node.tag)
            root=parser.root
            print(root is saved[0],root is saved[-1],saved[1] is saved[2],saved[3] is saved[4])
            print(root.attrib,repr(root.text),repr(root[0].text),repr(root[0].tail),root[1].text)
            print(parser.close(),next(parser,None),parser.root is root)
            """, "True None\nstart r\nstart x\nend x\nstart y\nend y\nend r\nTrue True True True\n{'a': 'kept'} 'before' 'inside' 'after' None\nNone None True\n"];
        yield return ["default-empty-events-and-writable-root", """
            import xml.etree.ElementTree as ET
            class Source:
                def __init__(self): self.done=False
                def read(self,n):
                    if self.done: return ''
                    self.done=True
                    return '<?xml version="1.0" encoding="latin1"?><r>Ω😀<x/></r>'
            for events in [None,[],['end']]:
                parser=ET.iterparse(Source(),events=events)
                parser.root='temporary'
                print(parser.root,[node.tag for event,node in parser],parser.root.tag,repr(parser.root.text))
            parser=ET.iterparse(Source(),events=['start'])
            print(next(parser)[1].tag,parser.close(),next(parser,None),parser.root)
            """, "temporary ['x', 'r'] r 'Ω😀'\ntemporary [] r 'Ω😀'\ntemporary ['x', 'r'] r 'Ω😀'\nr None None None\n"];
        yield return ["fragmented-input-final-text-and-namespaces", """
            import xml.etree.ElementTree as ET
            class Source:
                def __init__(self,data): self.data=data;self.position=0
                def read(self,n):
                    value=self.data[self.position:self.position+1]
                    self.position+=1
                    return value
            text='<r xmlns="urn:r" xmlns:p="urn:p" p:key="é">A<!--ignored-->B<?pi ignored?><p:x><![CDATA[Ω😀]]></p:x>tail&amp;&#13;</r>'
            for encoding in ['utf-8','utf-8-sig','utf-16','utf-16-le','utf-16-be']:
                parser=ET.iterparse(Source(text.encode(encoding)),events=['start','end'])
                print(encoding,[(event,node.tag) for event,node in parser])
                root=parser.root
                print(root.attrib,repr(root.text),repr(root[0].text),repr(root[0].tail))
            """, "utf-8 [('start', '{urn:r}r'), ('start', '{urn:p}x'), ('end', '{urn:p}x'), ('end', '{urn:r}r')]\n{'{urn:p}key': 'é'} 'AB' 'Ω😀' 'tail&\\r'\nutf-8-sig [('start', '{urn:r}r'), ('start', '{urn:p}x'), ('end', '{urn:p}x'), ('end', '{urn:r}r')]\n{'{urn:p}key': 'é'} 'AB' 'Ω😀' 'tail&\\r'\nutf-16 [('start', '{urn:r}r'), ('start', '{urn:p}x'), ('end', '{urn:p}x'), ('end', '{urn:r}r')]\n{'{urn:p}key': 'é'} 'AB' 'Ω😀' 'tail&\\r'\nutf-16-le [('start', '{urn:r}r'), ('start', '{urn:p}x'), ('end', '{urn:p}x'), ('end', '{urn:r}r')]\n{'{urn:p}key': 'é'} 'AB' 'Ω😀' 'tail&\\r'\nutf-16-be [('start', '{urn:r}r'), ('start', '{urn:p}x'), ('end', '{urn:p}x'), ('end', '{urn:r}r')]\n{'{urn:p}key': 'é'} 'AB' 'Ω😀' 'tail&\\r'\n"];
        yield return ["errors-stay-lazy-and-close-is-idempotent", """
            import xml.etree.ElementTree as ET
            class Source:
                def __init__(self,parts): self.parts=iter(parts)
                def read(self,n): return next(self.parts,b'')
            for parts in [[b''],[b'<r>',b''],[b'<r/>',b'<other/>'],[b'<r>\xff</r>']]:
                parser=ET.iterparse(Source(parts))
                print('created')
                try: list(parser)
                except ET.ParseError: print('ParseError')
                print(parser.root,parser.close(),next(parser,None))
            try: ET.iterparse(Source([]),events=['unknown'])
            except ValueError: print('ValueError')
            class Broken:
                def read(self,n): raise MemoryError('guest')
            parser=ET.iterparse(Broken())
            try: next(parser)
            except MemoryError as error: print(error)
            print(next(parser,None))
            """, "created\nParseError\nNone None None\ncreated\nParseError\nNone None None\ncreated\nParseError\nNone None None\ncreated\nParseError\nNone None None\nValueError\nguest\nNone\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task EventsMatchPython(string name, string source, string expected)
    {
        _ = name;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
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

    public static IEnumerable<object[]> ExistingEncodingControls()
        => ElementTreeCompatibilityTests.PythonControls().Where(row => row[0] is string name &&
            name is "xml-byte-encodings-and-unicode-declarations" or "xml-byte-encoding-declaration-mismatches"
                or "xml-single-bom-and-declared-ascii" or "xml-declaration-codec-aliases" or "xml-declaration-alias-scalars");

    [Theory]
    [MemberData(nameof(ExistingEncodingControls))]
    public async Task IncrementalEncodingRulesMatchExistingPythonControls(string name, string source, string expected)
    {
        _ = name;
        const string prefix = """
            import xml.etree.ElementTree as ET
            class Fragmented:
                def __init__(self,data): self.data=data;self.position=0
                def read(self,n):
                    part=self.data[self.position:self.position+1]
                    self.position+=1
                    return part
            def parse(data):
                parser=ET.iterparse(Fragmented(data))
                for event,node in parser: pass
                return parser.root

            """;
        using var script = new LythonEngine().Compile(prefix + source.Replace("ET.fromstring(", "parse(", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            using (result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedFileClosesAfterEarlyExitWithoutReadingTheWholeDocument(bool close)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            parser=ET.iterparse('input.xml')
            for event,node in parser:
                print(node.tag,node.text)
                break
            CLOSE
            """.Replace("CLOSE", close ? "parser.close()" : "pass", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var content = "<r><wanted>kept</wanted>" + new string(' ', 1_000_000) + "</r>";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var immediate = new MockLythonHost(); immediate.SeedFile("/input.xml", content);
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.xml", content);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(delayed, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("wanted kept\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(1, immediate.ClosedFileCount);
        Assert.Equal(1, delayed.ClosedFileCount);
        Assert.InRange(immediate.RangedReadCount, 1, 2);
        Assert.InRange(delayed.RangedReadCount, 1, 2);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedReaderCanSuspendAndRemainsOpenAfterParserClose(bool custom)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            class Reader:
                def __init__(self,file): self.file=file
                def read(self,n): return self.file.read(n)
                def close(self): raise ValueError('borrowed source was closed')
            with open('input.xml','rb') as source:
                parser=ET.iterparse(SOURCE)
                event,child=next(parser)
                parser.close()
                print(child.tag,child.text,source.closed,parser.root)
                print(source.read())
            """.Replace("SOURCE", custom ? "Reader(source)" : "source", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost(); host.SeedFile("/input.xml", "<r><x>é😀</x></r>");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("x é😀 False None\nb''\n", result.StandardOutput);
        }
        Assert.True(host.CompletedAsynchronously > 0);
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationWaitsForOwnedCleanupAndReturnsSharedAllowance(bool custom)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            class Reader:
                def read(self,n):
                    with open('input.xml','rb') as source: return source.read(n)
            parser=ET.iterparse(SOURCE)
            next(parser)
            print('must not run')
            """.Replace("SOURCE", custom ? "Reader()" : "'input.xml'", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var host = new DelayedLythonHost(); host.SeedFile("/input.xml", "<r/>");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var readStarted = host.PauseReadUntilCancellation("/input.xml");
        var closeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeFileClose = () => { closeStarted.TrySetResult(); return new ValueTask(closeRelease.Task); };
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        LythonExecutionResult? completed = null;
        try
        {
            try
            {
                await readStarted.WaitAsync(cancellation.Token);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > 0);
                cancellation.Cancel();
                await closeStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > 0);
            }
            finally
            {
                cancellation.Cancel(); closeRelease.TrySetResult();
                completed = await pending;
            }
            Assert.False(completed.Success);
            Assert.Equal(LythonFailureReason.Cancelled, completed.FailureAttribution?.Reason);
            Assert.Equal("", completed.StandardOutput);
        }
        finally { completed?.Dispose(); }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
        using var next = new LythonEngine().Compile("import xml.etree.ElementTree as ET\nprint(ET.fromstring('<r/>').tag)");
        using (var result = next.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalBeforeReadOrInsideReadIsTerminalAndClosesOwnedUsers(bool custom)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            import time
            class Reader:
                def read(self,n):
                    with open('input.xml','rb') as source:
                        time.time()
                        try: value='x'*65536
                        except MemoryError: return b'<r/>'
            parser=ET.iterparse(SOURCE)
            GATE
            try: next(parser)
            except MemoryError: print('must not suppress')
            print('must not continue')
            """.Replace("SOURCE", custom ? "Reader()" : "'input.xml'", StringComparison.Ordinal)
                .Replace("GATE", custom ? "pass" : "time.time()", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new AdmissionGateBudget();
            var files = new MockLythonHost(); files.SeedFile("/input.xml", "<r/>");
            var host = new AdmissionGateHost(budget, 0, files);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
                Assert.Equal(0, files.RangedReadCount);
                Assert.Equal(1, files.ClosedFileCount);
            }
            Assert.Equal(0, budget.Root.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LongTokensUseAsyncReadsAndSurvivingAliasesKeepTheirOwner(bool attribute)
    {
        var payload = string.Concat(Enumerable.Repeat("éΩ😀", 16_384));
        var xml = attribute ? "<r><x a=\"" + payload + "\"/></r>" : "<r><x>" + payload + "</x></r>";
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            parser=ET.iterparse('input.xml')
            event,child=next(parser)
            attributes=child.attrib
            text=child.text
            child.clear()
            parser.close()
            return VALUE
            """.Replace("VALUE", attribute ? "attributes['a']" : "text", StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        var firstHost = new MockLythonHost(); firstHost.SeedFile("/input.xml", xml);
        var secondHost = new DelayedLythonHost(); secondHost.SeedFile("/input.xml", xml);
        using (var first = script.Run(firstHost, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal(payload, first.ReturnValue);
            var retained = budget.ReservedBytes;
            Assert.True(retained > Encoding.UTF8.GetByteCount(payload));
            using (var second = await script.RunAsync(secondHost, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(payload, second.ReturnValue);
                Assert.Equal(payload, first.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.True(secondHost.RangedReadCount > 2);
        Assert.True(secondHost.CompletedAsynchronously > 0);
        Assert.Equal(1, firstHost.ClosedFileCount);
        Assert.Equal(1, secondHost.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task TraversalCreatedFromStartSurvivesLaterChildrenAndClear()
    {
        const string source = """
            import xml.etree.ElementTree as ET
            class Reader:
                def __init__(self): self.parts=iter([b'<r>',b'<x>'*64,b'<leaf>kept</leaf>',b'</x>'*64+b'</r>'])
                def read(self,n): return next(self.parts,b'')
            parser=ET.iterparse(Reader(),events=['start','end'])
            event,root=next(parser)
            root.clear()
            traversal=root.iter()
            for event,node in parser: pass
            print(sum(1 for node in traversal),len(list(root.iter())),list(root.itertext()))
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("66 66 ['kept']\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\" encoding=\"latin1\"?><r>é</r>", "latin1", "é")]
    [InlineData("<?xml version=\"1.0\" encoding=\"windows-1252\"?><r>€Ÿ</r>", "windows-1252", "€Ÿ")]
    [InlineData("<?xml version=\"1.0\" encoding=\"ascii\"?><r>A</r>", "ascii", "A")]
    public async Task FragmentedSingleByteDeclarationsMatchPython(string xml, string encoding, string expected)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            class Reader:
                def __init__(self): self.position=0
                def read(self,n):
                    result=data[self.position:self.position+1]
                    self.position+=1
                    return result
            data=XML.encode(ENCODING)
            parser=ET.iterparse(Reader())
            print([node.text for event,node in parser],parser.root.text)
            """.Replace("XML", System.Text.Json.JsonSerializer.Serialize(xml), StringComparison.Ordinal)
                .Replace("ENCODING", System.Text.Json.JsonSerializer.Serialize(encoding), StringComparison.Ordinal));
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            using (result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal($"['{expected}'] {expected}\n", result.StandardOutput);
            }
        }
    }

    [Theory]
    [InlineData("<!DOCTYPE r [<!ENTITY e 'external'>]><r>&e;</r>")]
    [InlineData("<r><x></r>")]
    public async Task InvalidOrProhibitedXmlClosesTheOwnedFile(string xml)
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            parser=ET.iterparse('input.xml')
            try: list(parser)
            except ET.ParseError: print('ParseError')
            print(parser.root,next(parser,None))
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost(); host.SeedFile("/input.xml", xml);
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ParseError\nNone None\n", result.StandardOutput);
            }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task IncrementalDepthUsesTheExistingManagedBound()
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            parser=ET.iterparse('input.xml',events=['start'])
            count=0
            try:
                for event,node in parser: count+=1
            except ET.ParseError: print('depth',count)
            print(parser.root,next(parser,None))
            """);
        Assert.True(script.IsValid);
        var host = new MockLythonHost();
        host.SeedFile("/input.xml", string.Concat(Enumerable.Repeat("<x>", 1026)) + string.Concat(Enumerable.Repeat("</x>", 1026)));
        using var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("depth 1025\nNone None\n", result.StandardOutput);
        Assert.Equal(1, host.ClosedFileCount);
    }

    [Fact]
    public async Task ReentrantReadCannotCloseOrResumeItsActiveParser()
    {
        using var script = new LythonEngine().Compile("""
            import xml.etree.ElementTree as ET
            class Reader:
                def __init__(self): self.done=False
                def read(self,n):
                    if self.done: return b''
                    self.done=True
                    try: next(parser)
                    except ValueError: print('next blocked')
                    try: parser.close()
                    except ValueError: print('close blocked')
                    return b'<r/>'
            parser=ET.iterparse(Reader())
            print([node.tag for event,node in parser],parser.root.tag)
            """);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            using (result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("next blocked\nclose blocked\n['r'] r\n", result.StandardOutput);
            }
        }
    }
}
