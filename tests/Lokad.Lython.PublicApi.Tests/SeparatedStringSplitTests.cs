using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SeparatedStringSplitTests
{
    public static TheoryData<string, string> Cases => new()
    {
        { "for text, sep, limit in [('a::b::::c::','::',-1),('aaa','aa',1),('ababa','aba',1),('é😀tail😀','😀',2),('','|',-1),('||','|',-1),('a|b','|',0),('a|b','|',-7),('a|b','|',99)]:\n print(text.split(sep,limit),text.rsplit(sep,limit))\n", "['a', 'b', '', 'c', ''] ['a', 'b', '', 'c', '']\n['', 'a'] ['a', '']\n['', 'ba'] ['ab', '']\n['é', 'tail', ''] ['é', 'tail', '']\n[''] ['']\n['', '', ''] ['', '', '']\n['a|b'] ['a|b']\n['a', 'b'] ['a', 'b']\n['a', 'b'] ['a', 'b']\n" },
        { "def pieces(text):\n left=text.split\n right=text.rsplit\n return [left(sep='<>',maxsplit=1),right(*('<>',1)),right(**{'sep':'<>','maxsplit':True})]\nprint(pieces('α<>β<><>γ'))\nclass Count:\n def __index__(self):return 1\nprint('a::b::c'.split('::',Count()),'a::b::c'.rsplit('::',Count()))\nfor method in ('split','rsplit'):\n try:getattr('a',method)('')\n except ValueError as error:print(type(error).__name__,str(error))\n", "[['α', 'β<><>γ'], ['α<>β<>', 'γ'], ['α<>β<>', 'γ']]\n['a', 'b::c'] ['a::b', 'c']\nValueError empty separator\nValueError empty separator\n" },
        { "text='a'*16383+'<>tail<>end'\nprint([len(part) for part in text.split('<>')],[len(part) for part in text.rsplit('<>')])\nprint(text.split('<>',1)[1],text.rsplit('<>',1)[1])\nvalue='unchanged'\nfirst=value.rsplit('|',0)\nsecond=value.rsplit('|',-1)\nfirst.append('x')\nprint(first,second,first is second)\n", "[16383, 4, 3] [16383, 4, 3]\ntail<>end end\n['unchanged', 'x'] ['unchanged'] False\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task SeparatorsFollowPythonThroughOrdinaryAndExposedCalls(string source, string expected)
    {
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
