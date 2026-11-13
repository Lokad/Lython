using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class XmlSelectionTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return ["descendants-compose-in-python-order", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r xmlns:p="urn:x"><group><p:x n="1"/><group><p:x n="2"/></group><p:x n="3"/></group><group><p:x n="4"/></group><p:x n="5"/></r>')
            for path in ['.//p:x','group//p:x','.//group/p:x','.//group//p:x','group//']:
                print([node.get('n') for node in root.findall(path,{'p':'urn:x'})])
                print(root.find(path,{'p':'urn:x'}).get('n'))
            selected=root.findall('.//p:x',{'p':'urn:x'})
            print(selected[0] is root[0][0],selected[-1] is root[-1])
            selected.pop()
            print(len(root.findall('.//p:x',{'p':'urn:x'})))
            """, "['1', '2', '3', '4', '5']\n1\n['1', '2', '3', '4']\n1\n['1', '3', '2', '4']\n1\n['1', '2', '3', '2', '4']\n1\n['1', None, '2', '3', '4']\n1\nTrue True\n5\n"];
        yield return ["descendant-self-wildcard-and-namespaces", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r xmlns="urn:x" xmlns:p="https://example.test/a[b]@c"><group><x n="é😀"/><p:x n="other"/></group><x n="last"/></r>')
            print([node.tag for node in root.findall('.//*')])
            print([node.get('n') for node in root.findall('.//x',{'':'urn:x'})])
            print(root.find('.//{https://example.test/a[b]@c}x').get('n'))
            print(root.find('.//{urn:x}group/.') is root[0])
            print(root.find('.//missing') is None,root.findall('.//missing/x'))
            """, "['{urn:x}group', '{urn:x}x', '{https://example.test/a[b]@c}x', '{urn:x}x']\n['é😀', 'last']\nother\nTrue\nTrue []\n"];
        yield return ["invalid-descendants-are-validated-before-selection", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<r/>')
            for path in ['.//.','.///x','.//missing:x','missing/no:x']:
                for select in [root.find,root.findall]:
                    try: select(path)
                    except SyntaxError: print('SyntaxError')
            """, "SyntaxError\nSyntaxError\nSyntaxError\nSyntaxError\nSyntaxError\nSyntaxError\nSyntaxError\nSyntaxError\n"];
        yield return ["deep-native-path-without-interpreter-recursion", """
            import xml.etree.ElementTree as ET
            root=ET.fromstring('<x>'*1024+'<leaf a="last"/>'+'</x>'*1024)
            path='x/'*1023+'leaf'
            print(root.find(path).get('a'),len(root.findall(path)))
            print(root.find('.//leaf').get('a'),len(root.findall('.//leaf')))
            """, "last 1\nlast 1\n"];
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task SelectionMatchesPython(string name, string source, string expected)
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
}
