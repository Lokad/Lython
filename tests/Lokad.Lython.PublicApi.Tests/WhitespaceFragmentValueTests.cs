using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class WhitespaceFragmentValueTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedWordsKeepOrdinaryStringSemantics(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import json
            literal = 'café😀\0β'
            words = ('head ' + literal + ' tail').split()
            word = words[1]
            words.clear()
            assert word == literal and len(word) == 7
            assert [word[0], word[3], word[4], word[-1]] == ['c', 'é', '😀', 'β']
            assert word[1:5] == 'afé😀' and word[::-1] == 'β\0😀éfac'
            assert ''.join(word) == literal
            assert '[' + word + ']' == '[' + literal + ']'
            assert word * 3 == literal + literal + literal
            assert {word: 'found'}[literal] == 'found'
            assert len({word, literal}) == 1
            assert word.startswith('caf') and word.endswith('\0β')
            assert word.find('😀') == 4 and word.rfind('β') == 6 and word.count('é') == 1
            assert word.replace('é', 'E') == 'cafE😀\0β'
            assert word.replace('', '-') == '-c-a-f-é-😀-\0-β-'
            assert word.split('é') == ['caf', '😀\0β']
            assert word.upper() == 'CAFÉ😀\0Β' and word.lower() == literal
            assert word.encode() == literal.encode()
            assert repr(word) == repr(literal) and ascii(word) == ascii(literal)
            assert f'{word:>10.5s}' == '     café😀'
            assert json.loads(json.dumps({'word': word}, ensure_ascii=False))['word'] == literal
            with open('word.txt', 'w', newline='') as target:
                target.write(word)
            return word
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        using (var result = asynchronous ? await script.RunAsync(delayed) : script.Run(immediate))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("café😀\0β", result.ReturnValue);
            Assert.Equal("café😀\0β", asynchronous ? delayed.ReadText("/word.txt") : immediate.ReadText("/word.txt"));
            if (asynchronous)
            {
                Assert.True(delayed.CompletedAsynchronously > 0);
                Assert.True(delayed.ClosedFileCount > 0);
            }
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PatternsSubjectsAndReplacementsKeepLogicalOffsets(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import re
            pattern_text = 'before (?P<cap>é😀) after'.split()[1]
            subject = 'before aÉ😀b after'.split()[1]
            replacement = r'before [\g<cap>] after'.split()[1]
            pattern = re.compile(pattern_text, re.I)
            match = pattern.search(subject, 1, 3)
            assert match.string is subject
            assert (match.pos, match.endpos, match.span(), match.groupdict()) == (1, 3, (1, 3), {'cap': 'É😀'})
            assert pattern.fullmatch(subject, 1, 3).group() == 'É😀'
            assert [entry.span() for entry in pattern.finditer(subject)] == [(1, 3)]
            assert pattern.sub(replacement, subject) == 'a[É😀]b'
            assert pattern.split(subject) == ['a', 'É😀', 'b']
            return match.group()
            """, budget);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("É😀", result.ReturnValue);
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
