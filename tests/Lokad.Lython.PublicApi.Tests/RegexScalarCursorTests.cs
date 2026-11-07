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
