using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexMatchIndexTests
{
    [Fact]
    public async Task IndexedCursorKeepsTheOriginalSubjectAndScalarWindow()
    {
        await AssertBothModes("""
            import re
            subject = ('α😀bβ😀cγ' + '!') * 2
            assert subject[2] == 'b'
            pattern = re.compile(r'(?<=😀)(?P<letter>.)')
            print(pattern.findall(subject, 2, 5))
            print([(m.group('letter'),m.span(),m.pos,m.endpos,m.string is subject)
                   for m in pattern.finditer(subject, 2, 6)])
            print(list(pattern.finditer(subject, 6, 2)))
            """, "['b']\n[('b', (2, 3), 2, 6, True), ('c', (5, 6), 2, 6, True)]\n[]\n");
    }

    [Fact]
    public async Task SplitResultSubjectsKeepTheirOwnUnicodeOffsetsAndText()
    {
        await AssertBothModes("""
            import re
            import fnmatch
            def words(line):
                return line.upper().split()
            word = words('HEAD a😀bé TAIL')[1]
            pattern = re.compile(r'😀(?P<letter>.)')
            match = pattern.search(word)
            print(word, match.group('letter'), match.span(), match.span('letter'))
            print(pattern.sub(r'<\g<letter>>', word))
            print(re.split('😀', word))
            print(fnmatch.fnmatchcase(word, 'A😀*'))
            print(repr(word), word[::-1], f'<{word:>6s}>')
            """, "A😀BÉ B (1, 3) (2, 3)\nA<B>É\n['A', 'BÉ']\nTrue\n'A😀BÉ' ÉB😀A <  A😀BÉ>\n");
    }

    [Fact]
    public async Task DirectAndCallbackSubscriptsShareGroupSelection()
    {
        await AssertBothModes("""
            import re
            import operator
            match = re.search(r'(?P<first>a)(?P<tail>b)?', 'a')
            print(match[0], match[1], match['first'], match[2], match.__getitem__('tail'))
            print(operator.itemgetter(0, 'first', 2)(match))
            print(re.sub(r'(?P<word>[a-z]+)', lambda m: m['word'].upper(), 'one two'))
            """, "a a a None None\n('a', 'a', None)\nONE TWO\n");
    }

    [Fact]
    public async Task BooleanAndIndexProtocolSelectorsWorkAcrossAllMethods()
    {
        await AssertBothModes("""
            import re
            from dataclasses import dataclass
            @dataclass
            class Index:
                value: int
                def __index__(self):
                    return self.value
            m = re.search(r'(?P<first>a)(?P<tail>b)', 'ab')
            print(m.group(False, True, Index(2)))
            print(m[False], m[True], m[Index(2)])
            print(m.start(True), m.end(False), m.span(Index(2)))
            """, "('ab', 'a', 'b')\nab a b\n0 2 (1, 2)\n");
    }

    [Theory]
    [InlineData("1.0", "IndexError")]
    [InlineData("None", "IndexError")]
    [InlineData("[]", "TypeError")]
    [InlineData("slice(None)", "IndexError")]
    [InlineData("10**100", "IndexError")]
    [InlineData("-(10**100)", "IndexError")]
    [InlineData("-2147483648", "IndexError")]
    [InlineData("-1", "IndexError")]
    [InlineData("3", "IndexError")]
    [InlineData("'missing'", "IndexError")]
    public async Task InvalidKeysRaisePythonErrors(string key, string error)
    {
        var source = "import re\nimport operator\n" +
            "def check(m, key):\n" +
            "    for fn in [lambda: m[key], lambda: m.__getitem__(key), lambda: m.group(key), lambda: m.group(0, key),\n" +
            "               lambda: m.start(key), lambda: m.end(key), lambda: m.span(key), lambda: operator.getitem(m, key)]:\n" +
            "        try:\n            fn()\n        except " + error + ":\n            print('bad key')\n" +
            "check(re.search('(?P<first>a)(?P<tail>b)', 'ab'), " + key + ")";
        await AssertBothModes(source, string.Concat(Enumerable.Repeat("bad key\n", 8)));
    }

    [Theory]
    [InlineData("return 'bad'", "TypeError")]
    [InlineData("raise ValueError('inside')", "ValueError")]
    [InlineData("raise SystemExit(7)", "SystemExit")]
    public async Task IndexConversionFailuresPropagate(string body, string error)
    {
        var source = "import re\nclass Index:\n    def __index__(self):\n        " + body + "\n" +
            "def check(m, key):\n" +
            "    for fn in [lambda: m[key], lambda: m.group(key), lambda: m.start(key), lambda: m.end(key), lambda: m.span(key)]:\n" +
            "        try:\n            fn()\n        except " + error + ":\n            print('conversion')\n" +
            "check(re.search('(a)', 'a'), Index())";
        await AssertBothModes(source, string.Concat(Enumerable.Repeat("conversion\n", 5)));
    }

    [Fact]
    public async Task NumericProtocolUsesTheTypeSlot()
    {
        await AssertBothModes("""
            import re
            class Index:
                def __index__(self):
                    return 1
                def __getattribute__(self, name):
                    if name == '__index__':
                        raise AssertionError('ordinary lookup')
                    return object.__getattribute__(self, name)
            m = re.search('(a)b', 'ab')
            key = Index()
            key.__index__ = lambda: 0
            print(m[key], m.group(key), m.span(key))
            class Missing:
                pass
            key = Missing()
            key.__index__ = lambda: 1
            try:
                print(m[key])
            except IndexError:
                print('missing slot')
            """, "a a (0, 1)\nmissing slot\n");
    }

    [Theory]
    [InlineData("m[key]", "b\n")]
    [InlineData("m.__getitem__(key)", "b\n")]
    [InlineData("m.group(key)", "b\n")]
    [InlineData("m.group(0, key)", "('ab', 'b')\n")]
    [InlineData("m.start(key)", "1\n")]
    [InlineData("m.end(key)", "2\n")]
    [InlineData("m.span(key)", "(1, 2)\n")]
    [InlineData("operator.getitem(m, key)", "b\n")]
    [InlineData("operator.itemgetter(key)(m)", "b\n")]
    [InlineData("operator.itemgetter(0, key)(m)", "('ab', 'b')\n")]
    public async Task RunAsyncAwaitsIndexMethods(string expression, string expected)
    {
        var source = "import re\nimport operator\nclass Index:\n    def __index__(self):\n" +
            "        with open('/index.txt') as handle:\n            return int(handle.read())\n" +
            "m = re.search('(a)(b)', 'ab')\nkey = Index()\nprint(" + expression + ")";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/index.txt", "2");
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/index.txt", "2");
        var asynchronous = await script.RunAsync(delayed);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task MatchProtocolsRetainTheirNonSequenceBoundaries()
    {
        await AssertBothModes("""
            import re
            def check(m):
                for fn in [lambda: len(m), lambda: iter(m)]:
                    try:
                        fn()
                    except TypeError:
                        print('not a sequence')
                try:
                    m[:]
                except IndexError:
                    print('not a slice')
            check(re.search('(a)', 'a'))
            """, "not a sequence\nnot a sequence\nnot a slice\n");
    }

    [Fact]
    public async Task GroupSelectorMethodsRejectKeywordArguments()
    {
        await AssertBothModes("""
            import re
            def check(m):
                for fn in [lambda: m.group(group=0), lambda: m.start(group=0), lambda: m.end(group=0),
                           lambda: m.span(group=0), lambda: m.__getitem__(key=0)]:
                    try:
                        fn()
                    except TypeError:
                        print('positional only')
            check(re.search('(a)', 'a'))
            """, string.Concat(Enumerable.Repeat("positional only\n", 5)));
    }

    [Fact]
    public async Task MatchKeysUseIdentityAndSurviveIteratorExhaustionAndPurge()
    {
        await AssertBothModes("""
            import re
            cursor = re.finditer(r'😀(?P<letter>é)', 'a😀é')
            first = next(cursor)
            original = hash(first)
            second = re.search(r'😀(?P<letter>é)', 'a😀é')
            keys = {first: 'first', second: 'second'}
            print(next(cursor, None))
            re.purge()
            print(first == second, first == first, hash(first) == original)
            print(len(keys), len({first, first, second}), keys[first], keys[second])
            print(first.span(), first.group('letter'), second.span())
            """, "None\nFalse True True\n2 2 first second\n(1, 3) é (1, 3)\n");
    }

    private static async Task AssertBothModes(string source, string expected)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
