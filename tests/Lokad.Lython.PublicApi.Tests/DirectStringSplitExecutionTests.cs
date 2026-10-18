using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectStringSplitExecutionTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "unicode-whitespace", "print(' alpha\\tbeta\\n café\\u00a0😀\\u2009tail '.split())", "['alpha', 'beta', 'café', '😀', 'tail']\n" },
        { "empty-and-boundaries", "for text in ('',' ','\\t\\n','word',' word ',' a  b '):print(text.split())", "[]\n[]\n[]\n['word']\n['word']\n['a', 'b']\n" },
        { "fresh-results", "text='one two'\na=text.split()\nb=text.split()\na.append('three')\nprint(a,b,a is b,text)", "['one', 'two', 'three'] ['one', 'two'] False one two\n" },
        { "explicit-options", "text=' a  b c '\nprint(text.split(None),text.split(sep=None,maxsplit=1),text.split(' '),text.split(None,0))", "['a', 'b', 'c'] ['a', 'b c '] ['', 'a', '', 'b', 'c', ''] ['a  b c ']\n" },
        { "exposed", "method=' a b '.split\nprint(method(),method(None,1))", "['a', 'b'] ['a', 'b ']\n" },
        { "custom-receiver", "class Text:\n def split(self):return ['custom']\nprint(Text().split())", "['custom']\n" },
        { "expanded", "print(' a b '.split(*[],**{}))", "['a', 'b']\n" },
        { "receiver-once", "events=[]\ndef text():\n events.append('receiver')\n return ' a b '\nprint(text().split(),events)", "['a', 'b'] ['receiver']\n" },
        { "unicode-separator", "print('café😀tail😀'.split('😀'),'a<>b<><>c'.split('<>'))", "['café', 'tail', ''] ['a', 'b', '', 'c']\n" },
        { "explicit-none", "print(' a\\tb café '.split(None),'word'.split(None),''.split(','))", "['a', 'b', 'café'] ['word'] ['']\n" },
        { "separator-alias", "text='a,b'\na=text.split(',')\nb=text.split(',')\na.append('c')\nprint(a,b,text)", "['a', 'b', 'c'] ['a', 'b'] a,b\n" },
        { "separator-effects", "events=[]\ndef text():\n events.append('receiver')\n return 'a,b'\ndef sep():\n events.append('separator')\n return ','\nprint(text().split(sep()),events)", "['a', 'b'] ['receiver', 'separator']\n" },
        { "receiver-rebinding", "text='a,b'\ndef sep():\n global text\n text='changed'\n return ','\nprint(text.split(sep()),text)", "['a', 'b'] changed\n" },
        { "dynamic-separator", "class Text:\n def split(self,sep):return ['custom',sep]\nprint(Text().split(','))", "['custom', ',']\n" },
        { "exposed-and-expanded-separator", "method='a,b'.split\nprint(method(','),'a,b'.split(*[',']),'a,b'.split(**{'sep':','}))", "['a', 'b'] ['a', 'b'] ['a', 'b']\n" },
        { "separator-errors", "def run(sep):\n try:\n  print('a,b'.split(sep))\n except (TypeError,ValueError) as error:\n  print(type(error).__name__,str(error))\nrun('')\nrun(1)", "ValueError empty separator\nTypeError must be str or None, not int\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryAndGeneralCallsFollowPython(string name, string source, string expected)
    {
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
