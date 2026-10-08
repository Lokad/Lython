using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ElementTreeCompatibilityTests
{
    [Theory]
    [InlineData("ET.parse('ambient.xml')")]
    [InlineData("ET.iterparse('ambient.xml')")]
    [InlineData("ET.Element('r')")]
    [InlineData("ET.XMLParser()")]
    [InlineData("ET.tostring(root)")]
    [InlineData("ET.fromstring('<r/>',parser=object())")]
    [InlineData("root.append(root[0])")]
    [InlineData("root.tag='changed'")]
    [InlineData("root.text='changed'")]
    [InlineData("root.find('.//x')")]
    [InlineData("root.findall('x[1]')")]
    [InlineData("root.find('..')")]
    [InlineData("root.find('/r')")]
    [InlineData("root.find('{*}x')")]
    [InlineData("root.find('{urn:x}*')")]
    public async Task DeferredSurfacesFailExplicitly(string operation)
    {
        var script = new LythonEngine().Compile("import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r><x/></r>')\ntry: " +
            operation + "\nexcept NotImplementedError: print('unsupported')\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("unsupported\n", result.StandardOutput);
        }
    }

    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "xml-root-attributes-and-mixed-text", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r b=\"2\" a=\"1\">before<a>inside</a>between<b/>after</r>')\nprint(root.tag,root.attrib,repr(root.text),root.tail,len(root))\nprint(root.get('a'),root.get('missing'),root.get('missing',7))\nfor child in root: print(child.tag,repr(child.text),repr(child.tail))\nprint(root.find('a') is root[0],root[-1] is root[1])\n", "r {'b': '2', 'a': '1'} 'before' None 2\n1 None 7\na 'inside' 'between'\nb None 'after'\nTrue True\n" };
        yield return new object[] { "xml-empty-text-children-and-indexing", "from xml.etree.ElementTree import fromstring\nfor text in ['<r/>','<r></r>','<r> </r>','<r><x/><y/></r>']:\n    root=fromstring(text)\n    print(repr(root.text),repr(root.tail),len(root),[c.tag for c in root])\nroot=fromstring('<r><a/><b/><c/></r>')\nprint([c.tag for c in root[1:]],root[-1].tag)\nfor index in [3,-4]:\n    try: root[index]\n    except IndexError: print('IndexError')\n", "None None 0 []\nNone None 0 []\n' ' None 0 []\nNone None 2 ['x', 'y']\n['b', 'c'] c\nIndexError\nIndexError\n" };
        yield return new object[] { "xml-comments-pi-cdata-and-predefined-entities", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<?xml version=\"1.0\"?><r>before<!--ignored--><?test ignored?>after<![CDATA[<raw>]]><x/>tail&amp;&lt;&gt;&quot;&apos;&#233;&#x1F600;</r>')\nprint(repr(root.text),len(root),repr(root[0].text),repr(root[0].tail))\n", "'beforeafter<raw>' 1 None 'tail&<>\"\\'\u00e9\ud83d\ude00'\n" };
        yield return new object[] { "xml-namespaces-and-clark-names", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r xmlns=\"urn:example\" xmlns:p=\"https://example.test/ns\" p:key=\"v\"><p:item a=\"1\"/><item/><p:item a=\"2\"/></r>')\nprint(root.tag,root.attrib)\nprint([c.tag for c in root])\nprint(root.find('{https://example.test/ns}item').get('a'))\nprint([c.get('a') for c in root.findall('{https://example.test/ns}item')])\nprint(root.find('{urn:example}item') is root[1])\n", "{urn:example}r {'{https://example.test/ns}key': 'v'}\n['{https://example.test/ns}item', '{urn:example}item', '{https://example.test/ns}item']\n1\n['1', '2']\nTrue\n" };
        yield return new object[] { "xml-simple-relative-paths-and-wildcards", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r><group><item n=\"1\"/><item n=\"2\"/></group><group><item n=\"3\"/></group><item n=\"4\"/></r>')\nfor path in ['.','./group','group/item','./group/item','*','group/*','item','missing','missing/item']:\n    first=root.find(path)\n    print(path,first.tag if first is not None else None,[e.get('n') for e in root.findall(path)])\n", ". r [None]\n./group group [None, None]\ngroup/item item ['1', '2', '3']\n./group/item item ['1', '2', '3']\n* group [None, None, '4']\ngroup/* item ['1', '2', '3']\nitem item ['4']\nmissing None []\nmissing/item None []\n" };
        yield return new object[] { "xml-prefix-and-default-namespace-maps", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r xmlns=\"urn:x\"><group><item n=\"1\"/></group><item n=\"2\"/></r>')\nprint(root.find('p:group/p:item',{'p':'urn:x'}).get('n'))\nprint([e.get('n') for e in root.findall('item',{'':'urn:x'})])\nprint(root.find('group/item',{'':'urn:x'}).get('n'))\ntry: root.find('missing:item')\nexcept SyntaxError: print('SyntaxError')\n", "1\n['2']\n1\n" };
        yield return new object[] { "xml-byte-encodings-and-unicode-declarations", "import xml.etree.ElementTree as ET\ntext='<r a=\"\u00e9\">\u03a9\ud83d\ude00</r>'\nfor encoding in ['utf-8','utf-8-sig','utf-16','utf-16-le','utf-16-be']:\n    data=text.encode(encoding)\n    root=ET.fromstring(data)\n    print(encoding,root.tag,root.attrib,repr(root.text))\nfor declaration in ['utf-8','iso-8859-1']:\n    root=ET.fromstring('<?xml version=\"1.0\" encoding=\"'+declaration+'\"?><r>\u03a9\u00e9</r>')\n    print(declaration,repr(root.text))\ndata='<?xml version=\"1.0\" encoding=\"iso-8859-1\"?><r>\u00e9</r>'.encode('latin1')\nprint(repr(ET.fromstring(data).text))\n", "utf-8 r {'a': '\u00e9'} '\u03a9\ud83d\ude00'\nutf-8-sig r {'a': '\u00e9'} '\u03a9\ud83d\ude00'\nutf-16 r {'a': '\u00e9'} '\u03a9\ud83d\ude00'\nutf-16-le r {'a': '\u00e9'} '\u03a9\ud83d\ude00'\nutf-16-be r {'a': '\u00e9'} '\u03a9\ud83d\ude00'\nutf-8 '\u03a9\u00e9'\niso-8859-1 '\u03a9\u00e9'\n'\u00e9'\n" };
        yield return new object[] { "xml-line-and-attribute-normalization", "import xml.etree.ElementTree as ET\ntext='<r a=\"one'+chr(13)+chr(10)+'two'+chr(9)+'three\">a'+chr(13)+chr(10)+'b'+chr(13)+'c'+chr(10)+'d&#13;</r>'\nroot=ET.fromstring(text)\nprint(repr(root.text),repr(root.get('a')))\nroot=ET.fromstring('<r xml:space=\"preserve\"> <x/> </r>')\nprint(repr(root.text),repr(root[0].tail),root.attrib)\n", "'a\\nb\\nc\\nd\\r' 'one two three'\n' ' ' ' {'{http://www.w3.org/XML/1998/namespace}space': 'preserve'}\n" };
        yield return new object[] { "xml-malformed-input-and-error-identity", "import xml.etree.ElementTree as ET\nfrom xml.etree.ElementTree import ParseError\nprint(ParseError is ET.ParseError,ParseError.__name__,ParseError.__module__,issubclass(ParseError,SyntaxError))\nfor data in ['','<r>','<r/><r/>','<r a=\"1\" a=\"2\"/>','<r>&missing;</r>','<p:r/>','<r>'+chr(0)+'</r>',bytes([60,114,62,255,60,47,114,62])]:\n    try: ET.fromstring(data)\n    except ParseError as error: print(type(error) is ParseError,isinstance(error,SyntaxError))\n", "True ParseError xml.etree.ElementTree True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue True\n" };
        yield return new object[] { "xml-fromstring-binding-and-input-types", "import xml.etree.ElementTree as ET\nparse=ET.fromstring\nprint(parse(text='<r/>',parser=None).tag)\nfor value in [None,3,['<r/>']]:\n    try: parse(value)\n    except TypeError: print('TypeError')\n", "r\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "xml-windows1252-declared-bytes", "import xml.etree.ElementTree as ET\ndata='<?xml version=\"1.0\" encoding=\"windows-1252\"?><r a=\"\u20ac\">\u00e9\u20ac</r>'.encode('cp1252')\nroot=ET.fromstring(data)\nprint(root.tag,root.attrib,repr(root.text))\n", "r {'a': '\u20ac'} '\u00e9\u20ac'\n" };
        yield return new object[] { "xml-unicode-declaration-and-unknown-byte-codec", "import xml.etree.ElementTree as ET\nfor encoding in ['utf-16','windows-1252','not-a-real-encoding']:\n    text='<?xml version=\"1.0\" encoding=\"'+encoding+'\"?><r>\u03a9\ud83d\ude00</r>'\n    print(encoding,repr(ET.fromstring(text).text))\ntry: ET.fromstring(b'<?xml version=\"1.0\" encoding=\"not-a-real-encoding\"?><r/>')\nexcept LookupError: print('LookupError')\n", "utf-16 '\u03a9\ud83d\ude00'\nwindows-1252 '\u03a9\ud83d\ude00'\nnot-a-real-encoding '\u03a9\ud83d\ude00'\nLookupError\n" };
        yield return new object[] { "xml-module-and-element-identity", "import xml\nimport xml.etree\nimport xml.etree.ElementTree as ET\nfrom xml.etree.ElementTree import Element,fromstring,ParseError\nroot=fromstring('<r><x/></r>')\nprint(xml.etree.ElementTree is ET,ET.fromstring is fromstring)\nprint(type(root) is Element,root.__class__ is Element,isinstance(root,Element),isinstance(root,object))\nprint(Element.__name__,Element.__module__,ParseError.__bases__[0] is SyntaxError)\n", "True True\nTrue True True True\nElement xml.etree.ElementTree True\n" };
        yield return new object[] { "xml-live-attribute-map-and-child-aliases", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r><x a=\"1\">text</x><x a=\"2\"/></r>')\nchild=root[0]; attributes=child.attrib; text=child.text\nattributes['a']='changed'; attributes['b']='added'; del attributes['a']\nprint(child.get('a','gone'),child.get('b'),child.attrib is attributes)\nselected=root.findall('x'); selected.pop()\nprint(len(root),len(selected),selected[0] is child)\ndel root,selected,child\nprint(attributes,text)\n", "gone added True\n2 1 True\n{'b': 'added'} text\n" };
        yield return new object[] { "xml-sequence-truth-and-index-protocol", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r><a/><b/><c/><d/></r>')\nclass Index:\n    def __index__(self): return -2\nprint(root[Index()].tag,[e.tag for e in root[::-1]],[e.tag for e in root[::2]])\nprint(bool(root),bool(root[0]),len(root),list(root)[0] is root[0])\nfor index in [10**100,-10**100]:\n    try: root[index]\n    except IndexError: print('IndexError')\n", "c ['d', 'c', 'b', 'a'] ['a', 'c']\nTrue False 4 True\nIndexError\nIndexError\n" };
        yield return new object[] { "xml-relative-empty-trailing-and-self-paths", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r><group><x/><y/></group><z/></r>')\nfor path in ['', '.', './.', 'group/.', 'group/', './group/', 'missing/']:\n    result=root.find(path)\n    print(path,result.tag if result is not None else None,[e.tag for e in root.findall(path)])\n", " None []\n. r ['r']\n./. r ['r']\ngroup/. group ['group']\ngroup/ x ['x', 'y']\n./group/ x ['x', 'y']\nmissing/ None []\n" };
        yield return new object[] { "xml-namespace-path-validation-and-uri-punctuation", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r xmlns:p=\"https://example.test/a[b]@c\"><p:x/><group><x/></group></r>')\nprint(root.find('{https://example.test/a[b]@c}x') is root[0])\nfor path,namespaces in [('missing:x',None),('missing:x',{}),('./missing:x',None),('missing/no:x',None)]:\n    try:\n        result=root.find(path,namespaces)\n        print(result is None)\n    except SyntaxError: print('SyntaxError')\n", "True\nTrue\nSyntaxError\nSyntaxError\nSyntaxError\n" };
        yield return new object[] { "xml-method-keywords-and-binding", "import xml.etree.ElementTree as ET\nroot=ET.fromstring('<r a=\"1\"><x/></r>')\nprint(root.get(key='a'),root.get(key='missing',default=7))\nprint(root.find(path='x',namespaces=None) is root[0],len(root.findall(path='x')))\ncalls=[root.get,root.find,root.findall,ET.fromstring]\nfor call in calls:\n    try: call()\n    except TypeError: print('TypeError')\n    try: call('<r/>',unknown=1)\n    except TypeError: print('TypeError')\n", "1 7\nTrue 1\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "xml-encoding-prefix-and-declaration-composition", "import xml.etree.ElementTree as ET\ntext='<?xml version=\"1.0\" encoding=\"utf-16\"?><r>\u03a9\ud83d\ude00</r>'\nfor encoding in ['utf-16','utf-16-le','utf-16-be']:\n    print(encoding,repr(ET.fromstring(text.encode(encoding)).text))\ntext='<?xml version=\"1.0\" encoding=\"windows-1252\"?><r>\u00e9</r>'\nfor bom in [b'',bytes([239,187,191])]:\n    try: print(repr(ET.fromstring(bom+text.encode('cp1252')).text))\n    except ET.ParseError: print('ParseError')\n", "utf-16 '\u03a9\ud83d\ude00'\nutf-16-le '\u03a9\ud83d\ude00'\nutf-16-be '\u03a9\ud83d\ude00'\n'\u00e9'\n'\u00e9'\n" };
        yield return new object[] { "xml-text-fragment-concatenation-and-root-trivia", "import xml.etree.ElementTree as ET\nroot=ET.fromstring(' \\n<!--before--><?before x?><r>A<![CDATA[B]]><!--skip-->C<x/>D<?skip x?>E<![CDATA[F]]><y/>G<!--skip-->H</r> \\n<!--after-->')\nprint(repr(root.text),repr(root[0].tail),repr(root[1].tail),root.tail)\n", "'ABC' 'DEF' 'GH' None\n" };
        yield return new object[] { "xml-byte-encoding-declaration-mismatches", "import xml.etree.ElementTree as ET\nfor encoding in ['utf-16','utf-16-le','utf-16-be','utf-8']:\n    for declaration in ['utf-8','utf-16','utf-16-le','utf-16-be','latin1','windows-1252','not-a-real-encoding']:\n        text='<?xml version=\"1.0\" encoding=\"'+declaration+'\"?><r>\u00e9</r>'\n        try: print(encoding,declaration,repr(ET.fromstring(text.encode(encoding)).text))\n        except ET.ParseError: print(encoding,declaration,'ParseError')\n        except LookupError: print(encoding,declaration,'LookupError')\n        except ValueError: print(encoding,declaration,'ValueError')\n", "utf-16 utf-8 ParseError\nutf-16 utf-16 '\u00e9'\nutf-16 utf-16-le ValueError\nutf-16 utf-16-be ValueError\nutf-16 latin1 ParseError\nutf-16 windows-1252 ParseError\nutf-16 not-a-real-encoding LookupError\nutf-16-le utf-8 ParseError\nutf-16-le utf-16 '\u00e9'\nutf-16-le utf-16-le ValueError\nutf-16-le utf-16-be ValueError\nutf-16-le latin1 ParseError\nutf-16-le windows-1252 ParseError\nutf-16-le not-a-real-encoding LookupError\nutf-16-be utf-8 ParseError\nutf-16-be utf-16 '\u00e9'\nutf-16-be utf-16-le ValueError\nutf-16-be utf-16-be ValueError\nutf-16-be latin1 ParseError\nutf-16-be windows-1252 ParseError\nutf-16-be not-a-real-encoding LookupError\nutf-8 utf-8 '\u00e9'\nutf-8 utf-16 ParseError\nutf-8 utf-16-le ValueError\nutf-8 utf-16-be ValueError\nutf-8 latin1 '\u00c3\u00a9'\nutf-8 windows-1252 '\u00c3\u00a9'\nutf-8 not-a-real-encoding LookupError\n" };
        yield return new object[] { "xml-single-bom-and-declared-ascii", "import xml.etree.ElementTree as ET\nbom=bytes([239,187,191])\nfor data in [bom+b'<r/>',bom+bom+b'<r/>',b'<?xml version=\"1.0\" encoding=\"ascii\"?><r>A</r>',b'<?xml version=\"1.0\" encoding=\"ascii\"?><r>'+bytes([233])+b'</r>']:\n    try: print(ET.fromstring(data).tag)\n    except ET.ParseError: print('ParseError')\n", "r\nParseError\nr\nParseError\n" };
        yield return new object[] { "xml-exception-base-tuple-identity", "import xml.etree.ElementTree as ET\nimport struct,io,re\nfor exception,base in [(ET.ParseError,SyntaxError),(struct.error,Exception),(io.UnsupportedOperation,OSError),(Exception,BaseException),(BaseException,object)]:\n    print(exception.__bases__[0] is base,exception.__bases__ is exception.__bases__)\nprint(io.UnsupportedOperation.__bases__[1] is ValueError)\n", "True True\nTrue True\nTrue True\nTrue True\nTrue True\nTrue\n" };
        yield return new object[] { "xml-declaration-codec-aliases", "import xml.etree.ElementTree as ET\nfor name in ['utf-8','utf8','UTF-8','UTF8','utf_8','utf-8-sig','cp65001','utf-16','utf16','UTF-16','utf_16','utf-16-le','utf_16_be','latin-1','latin1','iso-8859-1','iso8859-1','cp1252','windows-1252','cp_1252','ascii','us-ascii']:\n    text='<?xml version=\"1.0\" encoding=\"'+name+'\"?><r>A</r>'\n    encoding='utf-16' if name.lower().replace('_','-').startswith('utf-16') or name=='utf16' else 'utf-8'\n    try: print(name,ET.fromstring(text.encode(encoding)).text)\n    except ET.ParseError: print(name,'ParseError')\n    except LookupError: print(name,'LookupError')\n    except ValueError: print(name,'ValueError')\n", "utf-8 A\nutf8 A\nUTF-8 A\nUTF8 A\nutf_8 A\nutf-8-sig A\ncp65001 A\nutf-16 A\nutf16 ValueError\nUTF-16 A\nutf_16 ValueError\nutf-16-le ValueError\nutf_16_be ValueError\nlatin-1 A\nlatin1 A\niso-8859-1 A\niso8859-1 A\ncp1252 A\nwindows-1252 A\ncp_1252 LookupError\nascii A\nus-ascii A\n" };
        yield return new object[] { "xml-declaration-alias-scalars", "import xml.etree.ElementTree as ET\nfor name in ['utf-8','utf8','UTF-8','UTF8','utf_8','utf-8-sig','cp65001','latin-1','latin1','iso-8859-1','iso8859-1','cp1252','windows-1252','ascii','us-ascii']:\n    text='<?xml version=\"1.0\" encoding=\"'+name+'\"?><r>\u00e9\u03a9\ud83d\ude00</r>'\n    try: print(name,repr(ET.fromstring(text.encode('utf-8')).text))\n    except ET.ParseError: print(name,'ParseError')\n    except LookupError: print(name,'LookupError')\n    except ValueError: print(name,'ValueError')\n", "utf-8 '\u00e9\u03a9\ud83d\ude00'\nutf8 ParseError\nUTF-8 '\u00e9\u03a9\ud83d\ude00'\nUTF8 ParseError\nutf_8 ParseError\nutf-8-sig ParseError\ncp65001 ParseError\nlatin-1 '\u00c3\u00a9\u00ce\u00a9\u00f0\\x9f\\x98\\x80'\nlatin1 '\u00c3\u00a9\u00ce\u00a9\u00f0\\x9f\\x98\\x80'\niso-8859-1 '\u00c3\u00a9\u00ce\u00a9\u00f0\\x9f\\x98\\x80'\niso8859-1 '\u00c3\u00a9\u00ce\u00a9\u00f0\\x9f\\x98\\x80'\ncp1252 '\u00c3\u00a9\u00ce\u00a9\u00f0\u0178\u02dc\u20ac'\nwindows-1252 '\u00c3\u00a9\u00ce\u00a9\u00f0\u0178\u02dc\u20ac'\nascii ParseError\nus-ascii ParseError\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task ContainedTreesMatchPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task MediatedSourceAndPublicationCanSuspend()
    {
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedFile("/doc.xml", "<?xml version=\"1.0\"?><r xmlns=\"urn:sample\" a=\"\u00e9\">before<item>\u03a9\ud83d\ude00</item>after</r>");
        delayed.SeedFile("/doc.xml", "<?xml version=\"1.0\"?><r xmlns=\"urn:sample\" a=\"\u00e9\">before<item>\u03a9\ud83d\ude00</item>after</r>");
        var script = new LythonEngine().Compile("import xml.etree.ElementTree as ET\nfrom pathlib import Path\nwith open('doc.xml') as file:\n    root=ET.fromstring(file.read())\nchild=root.find('{urn:sample}item')\nprint(root.tag,root.attrib,repr(root.text),child.tag,repr(child.text),repr(child.tail))\nprint(Path('result.txt').write_text(child.text),Path('result.txt').read_text())\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("{urn:sample}r {'a': '\u00e9'} 'before' {urn:sample}item '\u03a9\ud83d\ude00' 'after'\n2 \u03a9\ud83d\ude00\n", result.StandardOutput);
        }
        Assert.Equal(Convert.FromHexString("cea9f09f9880"),
            (await immediate.ReadTextUtf8Async("/result.txt", CancellationToken.None)).ToArray());
        Assert.Equal(Convert.FromHexString("cea9f09f9880"),
            (await delayed.ReadTextUtf8Async("/result.txt", CancellationToken.None)).ToArray());
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task TypeIndexAndSliceBoundsAwaitHostReads()
    {
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        immediate.SeedFile("/index.txt", "1");
        delayed.SeedFile("/index.txt", "1");
        var script = new LythonEngine().Compile(SuspendedIndexSource);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("index 1\nindex 1\nb ['a']\nindex 1\nB\nindex 1\n", result.StandardOutput);
        }
        Assert.Equal("B", immediate.ReadText("/result.txt"));
        Assert.Equal("B", delayed.ReadText("/result.txt"));
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task SuspendedIndexCancellationPreservesExistingOutput()
    {
        var script = new LythonEngine().Compile(SuspendedIndexSource);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var host = new DelayedLythonHost();
        host.SeedFile("/index.txt", "1");
        host.SeedFile("/result.txt", "original");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = host.PauseReadUntilCancellation("/index.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message);
        Assert.Equal("original", host.ReadText("/result.txt"));
        var retryHost = new DelayedLythonHost();
        retryHost.SeedFile("/index.txt", "1");
        var retry = await script.RunAsync(retryHost);
        Assert.True(retry.Success, retry.Failure?.Message);
        Assert.Equal("index 1\nindex 1\nb ['a']\nindex 1\nB\nindex 1\n", retry.StandardOutput);
        Assert.Equal("B", retryHost.ReadText("/result.txt"));
    }

    private const string SuspendedIndexSource = "import xml.etree.ElementTree as ET\nimport operator\nfrom pathlib import Path\nclass Index:\n    def __index__(self):\n        with open('index.txt') as file: value=file.read()\n        print('index',value)\n        return int(value)\nindex=Index()\nindex.__index__=lambda: 0\nroot=ET.fromstring('<r><a>A</a><b>B</b><c>C</c></r>')\nprint(root[index].tag,[element.tag for element in root[:index]])\nprint(operator.getitem(root,index).text)\nPath('result.txt').write_text(root[index].text)\n";
}
