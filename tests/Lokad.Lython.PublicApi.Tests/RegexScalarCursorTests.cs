using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexScalarCursorTests
{
    // Independently compared with isolated CPython 3.13.2.
    public static TheoryData<string, string> Cases => new()
    {
        { "import re\ntext='αβγδεζηθ'\nfor match in re.finditer(r'(?<=(..))(?P<value>.)',text):\n print(match.span(),match.span(1),match.group(1),match.group('value'))", "(2, 3) (0, 2) αβ γ\n(3, 4) (1, 3) βγ δ\n(4, 5) (2, 4) γδ ε\n(5, 6) (3, 5) δε ζ\n(6, 7) (4, 6) εζ η\n(7, 8) (5, 7) ζη θ\n" },
        { "import re\ntext='aé😀bç🦊d'\np=re.compile(r'(?<=(..))(?=(..))')\nfor match in p.finditer(text):print(match.regs,match.groups())", "((2, 2), (0, 2), (2, 4)) ('aé', '😀b')\n((3, 3), (1, 3), (3, 5)) ('é😀', 'bç')\n((4, 4), (2, 4), (4, 6)) ('😀b', 'ç🦊')\n((5, 5), (3, 5), (5, 7)) ('bç', '🦊d')\n" },
        { "import re\ntext='αβγδεζηθ'\np=re.compile(r'(?<=(..))(.)')\nprint(p.sub(lambda m:repr((m.span(),m.span(1),m.groups())),text))", "αβ((2, 3), (0, 2), ('αβ', 'γ'))((3, 4), (1, 3), ('βγ', 'δ'))((4, 5), (2, 4), ('γδ', 'ε'))((5, 6), (3, 5), ('δε', 'ζ'))((6, 7), (4, 6), ('εζ', 'η'))((7, 8), (5, 7), ('ζη', 'θ'))\n" },
        { "import re\ntext='aé😀bç🦊d'\np=re.compile(r'(?<=(..))(.)')\nfor match in p.finditer(text,2,6):print(match.regs,match.pos,match.endpos,match.groups())", "((2, 3), (0, 2), (2, 3)) 2 6 ('aé', '😀')\n((3, 4), (1, 3), (3, 4)) 2 6 ('é😀', 'b')\n((4, 5), (2, 4), (4, 5)) 2 6 ('😀b', 'ç')\n((5, 6), (3, 5), (5, 6)) 2 6 ('bç', '🦊')\n" },
        { "import re\ntext='aé😀bç🦊d'\np=re.compile(r'(?<=(..))(?=(..))|(?P<missing>Z)')\nkept=list(p.finditer(text,2,6))\nre.purge()\nfor m in reversed(kept):print(m.regs,m.groups(),m.span('missing'),m.pos,m.endpos)\n", "((4, 4), (2, 4), (4, 6), (-1, -1)) ('😀b', 'ç🦊', None) (-1, -1) 2 6\n((3, 3), (1, 3), (3, 5), (-1, -1)) ('é😀', 'bç', None) (-1, -1) 2 6\n((2, 2), (0, 2), (2, 4), (-1, -1)) ('aé', '😀b', None) (-1, -1) 2 6\n" },
        { "import re\ntext='aé😀bç🦊d'\np=re.compile(r'(?<=(..))(.)')\nkept=[]\ndef replace(m):\n kept.append(m)\n return m.group(2).upper()\nprint(p.sub(replace,text))\nprint([(m.regs,m.groups()) for m in reversed(kept)])\n", "aé😀BÇ🦊D\n[(((6, 7), (4, 6), (6, 7)), ('ç🦊', 'd')), (((5, 6), (3, 5), (5, 6)), ('bç', '🦊')), (((4, 5), (2, 4), (4, 5)), ('😀b', 'ç')), (((3, 4), (1, 3), (3, 4)), ('é😀', 'b')), (((2, 3), (0, 2), (2, 3)), ('aé', '😀'))]\n" },
        { "import re\ntext='aé😀bç🦊d'\np=re.compile(r'(?<=(..))')\nfor pos,endpos in [(7,7),(4,6),(0,7)]:\n m=p.match(text,pos,endpos)\n print(None if m is None else (m.regs,m.pos,m.endpos,m.groups()))\n", "(((7, 7), (5, 7)), 7, 7, ('🦊d',))\n(((4, 4), (2, 4)), 4, 6, ('😀b',))\nNone\n" },
        { "import re\ntext='é😀a'*64\nmatches=list(re.finditer(r'(?=(.*))',text))\nprint(len(matches),sum(m.start() for m in matches),sum(m.end(1) for m in matches))\nprint(matches[0].regs,matches[-1].regs,matches[17].group(1)==text[17:])\nre.purge()\nprint(matches[0].string is text,matches[0].span(1),matches[-1].group(1))\n", "193 18528 37056\n((0, 0), (0, 192)) ((192, 192), (192, 192)) True\nTrue (0, 192) \n" },
        { "import re\ntext='é😀a'*64\np=re.compile(r'(?<=(..))(?=(.*))|(?P<missing>Z)')\nmatches=list(p.finditer(text,5,len(text)-7))\nprint(len(matches),sum(m.start() for m in matches),sum(m.end(2) for m in matches))\nprint(matches[0].regs,matches[-1].regs,matches[0].pos,matches[0].endpos)\nre.purge()\nprint(matches[0].group(1)==text[3:5],matches[0].group(2)==text[5:-7],matches[-1].group(2),matches[-1].span('missing'))\n", "181 17195 33485\n((5, 5), (3, 5), (5, 185), (-1, -1)) ((185, 185), (183, 185), (185, 185), (-1, -1)) 5 185\nTrue True  (-1, -1)\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OverlappingLookbehindAndLookaheadKeepOriginalScalarPositions(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
