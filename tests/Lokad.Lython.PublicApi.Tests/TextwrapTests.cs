using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextwrapTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "textwrap-dedent-0", "import textwrap\nprint(repr(textwrap.dedent('')))", "''\n" };
        yield return new object[] { "textwrap-indent-0", "import textwrap\nprint(repr(textwrap.indent('', \"> \")))", "''\n" };
        yield return new object[] { "textwrap-dedent-1", "import textwrap\nprint(repr(textwrap.dedent('  abc')))", "'abc'\n" };
        yield return new object[] { "textwrap-indent-1", "import textwrap\nprint(repr(textwrap.indent('  abc', \"> \")))", "'>   abc'\n" };
        yield return new object[] { "textwrap-dedent-2", "import textwrap\nprint(repr(textwrap.dedent('  a\\n  b\\n')))", "'a\\nb\\n'\n" };
        yield return new object[] { "textwrap-indent-2", "import textwrap\nprint(repr(textwrap.indent('  a\\n  b\\n', \"> \")))", "'>   a\\n>   b\\n'\n" };
        yield return new object[] { "textwrap-dedent-3", "import textwrap\nprint(repr(textwrap.dedent('\\talpha\\n\\tbeta')))", "'alpha\\nbeta'\n" };
        yield return new object[] { "textwrap-indent-3", "import textwrap\nprint(repr(textwrap.indent('\\talpha\\n\\tbeta', \"> \")))", "'> \\talpha\\n> \\tbeta'\n" };
        yield return new object[] { "textwrap-dedent-4", "import textwrap\nprint(repr(textwrap.dedent(' \\ta\\n  b')))", "'\\ta\\n b'\n" };
        yield return new object[] { "textwrap-indent-4", "import textwrap\nprint(repr(textwrap.indent(' \\ta\\n  b', \"> \")))", "'>  \\ta\\n>   b'\n" };
        yield return new object[] { "textwrap-dedent-5", "import textwrap\nprint(repr(textwrap.dedent('  \\n\\t\\n  a\\n\\t b\\n')))", "'\\n\\n  a\\n\\t b\\n'\n" };
        yield return new object[] { "textwrap-indent-5", "import textwrap\nprint(repr(textwrap.indent('  \\n\\t\\n  a\\n\\t b\\n', \"> \")))", "'  \\n\\t\\n>   a\\n> \\t b\\n'\n" };
        yield return new object[] { "textwrap-dedent-6", "import textwrap\nprint(repr(textwrap.dedent('\\n  a\\n    b\\n')))", "'\\na\\n  b\\n'\n" };
        yield return new object[] { "textwrap-indent-6", "import textwrap\nprint(repr(textwrap.indent('\\n  a\\n    b\\n', \"> \")))", "'\\n>   a\\n>     b\\n'\n" };
        yield return new object[] { "textwrap-dedent-7", "import textwrap\nprint(repr(textwrap.dedent('  \u00e9\ud83d\ude00\\n  \u03b2\\n')))", "'\u00e9\ud83d\ude00\\n\u03b2\\n'\n" };
        yield return new object[] { "textwrap-indent-7", "import textwrap\nprint(repr(textwrap.indent('  \u00e9\ud83d\ude00\\n  \u03b2\\n', \"> \")))", "'>   \u00e9\ud83d\ude00\\n>   \u03b2\\n'\n" };
        yield return new object[] { "textwrap-dedent-8", "import textwrap\nprint(repr(textwrap.dedent('  a\\r\\n  b\\r\\n')))", "'a\\r\\nb\\r\\n'\n" };
        yield return new object[] { "textwrap-indent-8", "import textwrap\nprint(repr(textwrap.indent('  a\\r\\n  b\\r\\n', \"> \")))", "'>   a\\r\\n>   b\\r\\n'\n" };
        yield return new object[] { "textwrap-dedent-9", "import textwrap\nprint(repr(textwrap.dedent('  a\\rb')))", "'a\\rb'\n" };
        yield return new object[] { "textwrap-indent-9", "import textwrap\nprint(repr(textwrap.indent('  a\\rb', \"> \")))", "'>   a\\r> b'\n" };
        yield return new object[] { "textwrap-dedent-10", "import textwrap\nprint(repr(textwrap.dedent('  a\\x0b  b\\x0c  c')))", "'a\\x0b  b\\x0c  c'\n" };
        yield return new object[] { "textwrap-indent-10", "import textwrap\nprint(repr(textwrap.indent('  a\\x0b  b\\x0c  c', \"> \")))", "'>   a\\x0b>   b\\x0c>   c'\n" };
        yield return new object[] { "textwrap-dedent-11", "import textwrap\nprint(repr(textwrap.dedent(' \\xa0\\n  a')))", "'\\xa0\\n a'\n" };
        yield return new object[] { "textwrap-indent-11", "import textwrap\nprint(repr(textwrap.indent(' \\xa0\\n  a', \"> \")))", "' \\xa0\\n>   a'\n" };
        yield return new object[] { "textwrap-dedent-12", "import textwrap\nprint(repr(textwrap.dedent('  a\\x85  b\\u2028  c\\u2029  d')))", "'a\\x85  b\\u2028  c\\u2029  d'\n" };
        yield return new object[] { "textwrap-indent-12", "import textwrap\nprint(repr(textwrap.indent('  a\\x85  b\\u2028  c\\u2029  d', \"> \")))", "'>   a\\x85>   b\\u2028>   c\\u2029>   d'\n" };
        yield return new object[] { "textwrap-dedent-13", "import textwrap\nprint(repr(textwrap.dedent('a\\n  b')))", "'a\\n  b'\n" };
        yield return new object[] { "textwrap-indent-13", "import textwrap\nprint(repr(textwrap.indent('a\\n  b', \"> \")))", "'> a\\n>   b'\n" };
        yield return new object[] { "textwrap-dedent-14", "import textwrap\nprint(repr(textwrap.dedent('\\x1c\\x1d\\x1e\\x1f\\n')))", "'\\x1c\\x1d\\x1e\\x1f\\n'\n" };
        yield return new object[] { "textwrap-indent-14", "import textwrap\nprint(repr(textwrap.indent('\\x1c\\x1d\\x1e\\x1f\\n', \"> \")))", "'\\x1c\\x1d\\x1e\\x1f\\n'\n" };
        yield return new object[] { "textwrap-indent-predicate-all", "import textwrap\nprint(repr(textwrap.indent('a\\n \\nb\\r\\n', '\ud83d\ude00:', lambda line:True)))", "'\ud83d\ude00:a\\n\ud83d\ude00: \\n\ud83d\ude00:b\\r\\n'\n" };
        yield return new object[] { "textwrap-indent-predicate-none", "import textwrap\nprint(repr(textwrap.indent('a\\n \\nb\\r\\n', '\ud83d\ude00:', lambda line:False)))", "'a\\n \\nb\\r\\n'\n" };
        yield return new object[] { "textwrap-indent-predicate-selected", "import textwrap\nprint(repr(textwrap.indent('a\\n \\nb\\r\\n', '\ud83d\ude00:', lambda line:line.startswith('a'))))", "'\ud83d\ude00:a\\n \\nb\\r\\n'\n" };
        yield return new object[] { "textwrap-indent-predicate-default", "import textwrap\nprint(repr(textwrap.indent('a\\n \\nb\\r\\n', '\ud83d\ude00:', None)))", "'\ud83d\ude00:a\\n \\n\ud83d\ude00:b\\r\\n'\n" };
        yield return new object[] { "textwrap-indent-predicate-order", "import textwrap\ndef predicate(line):\n    print(repr(line))\n    if line.startswith('stop'):\n        raise ValueError('stop')\n    return True\ntry:\n    textwrap.indent('first\\nstop\\nlater\\n', '> ', predicate)\nexcept ValueError:\n    print('caught')\n", "'first\\n'\n'stop\\n'\ncaught\n" };
        yield return new object[] { "textwrap-aliases-keywords", "import textwrap as tw\nfrom textwrap import dedent as clean, indent as add\nprint(repr(clean(text='  a\\n  b')))\nprint(repr(add(prefix='> ',text='a\\n\\nb',predicate=lambda line:True)))\nprint('dedent' in dir(tw),'indent' in dir(tw))\ndef dedent(text):\n    return 42\nprint(dedent('x'))\n", "'a\\nb'\n'> a\\n> \\n> b'\nTrue True\n42\n" };
        yield return new object[] { "textwrap-lazy-invalid-prefix-and-predicate", "import textwrap\nprint(repr(textwrap.indent('',None,False)))\nprint(repr(textwrap.indent(' \\n\\t\\n',None)))\nprint(repr(textwrap.indent('a',None,lambda line:False)))\nfor text,prefix,predicate in [('a',None,None),('a','>',False),(' \\n','>',False)]:\n    try:\n        textwrap.indent(text,prefix,predicate)\n    except Exception as e:\n        print(type(e).__name__)\n", "''\n' \\n\\t\\n'\n'a'\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "textwrap-callable-and-truth-slots", "import textwrap\nclass Truth:\n    def __init__(self,line):\n        self.line=line\n    def __bool__(self):\n        print('truth',repr(self.line))\n        return self.line.startswith('a')\nclass Predicate:\n    def __call__(self,line):\n        print('call',repr(line))\n        return Truth(line)\np=Predicate();p.__call__=lambda line:False\nprint(repr(textwrap.indent('a\\n\\nb','> ',p)))\n", "call 'a\\n'\ntruth 'a\\n'\ncall '\\n'\ntruth '\\n'\ncall 'b'\ntruth 'b'\n'> a\\n\\nb'\n" };
        yield return new object[] { "textwrap-guest-prefix-addition", "import textwrap\nclass Prefix:\n    def __add__(self,line):\n        print('add',repr(line))\n        return '\ud83d\ude00:'+line\ntry:\n    textwrap.indent('a\\n\\nb',Prefix())\nexcept TypeError:\n    print('TypeError')\n", "TypeError\n" };
        yield return new object[] { "textwrap-guest-prefix-nonstring-join-order", "import textwrap\nclass Prefix:\n    def __add__(self,line):\n        print('add',repr(line))\n        return 3\ndef predicate(line):\n    print('predicate',repr(line))\n    return True\ntry:\n    textwrap.indent('a\\nb\\nc',Prefix(),predicate)\nexcept TypeError:\n    print('TypeError')\n", "predicate 'a\\n'\npredicate 'b\\n'\npredicate 'c'\nTypeError\n" };
        yield return new object[] { "textwrap-guest-prefix-error-priority", "import textwrap\nclass Prefix:\n    def __add__(self,line):\n        print('add',repr(line))\n        return 3\ndef predicate(line):\n    print('predicate',repr(line))\n    if line.startswith('b'):\n        raise ValueError('later')\n    return True\ntry:\n    textwrap.indent('a\\nb\\nc',Prefix(),predicate)\nexcept Exception as e:\n    print(type(e).__name__)\n", "predicate 'a\\n'\npredicate 'b\\n'\nValueError\n" };
        yield return new object[] { "textwrap-invalid-inputs-and-binding", "import textwrap\ndef call(f,*args,**kwargs):\n    try:\n        f(*args,**kwargs)\n    except Exception as e:\n        print(type(e).__name__)\nfor value in [None,1,b'x',[]]:\n    call(textwrap.dedent,value)\n    call(textwrap.indent,value,'>')\ncall(textwrap.dedent)\ncall(textwrap.dedent,'x','y')\ncall(textwrap.dedent,'x',text='y')\ncall(textwrap.indent,'x')\ncall(textwrap.indent,'x','>',wrong=True)\n", "TypeError\nAttributeError\nTypeError\nAttributeError\nTypeError\nTypeError\nTypeError\nAttributeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" };
        yield return new object[] { "textwrap-dedent-identity-and-newline-anchors", "import textwrap\ntext='a\ud83d\ude00\\nb'\nprint(textwrap.dedent(text) is text)\nfor text in ['  \\r\\n  a','  a\\r  b','  a\\x85  b','  \\t\\n  a\\n\\t b','\\f  a\\n  b']:\n    print(repr(textwrap.dedent(text)))\n", "True\n'\\r\\na'\n'a\\r  b'\n'a\\x85  b'\n'\\n  a\\n\\t b'\n'\\x0c  a\\n  b'\n" };
        yield return new object[] { "textwrap-bytes-protocol-and-lazy-errors", "import textwrap\nfor text in [b'',b'a',b' \\n',b'a\\nb\\nc']:\n    try:\n        print(repr(textwrap.indent(text,'>')))\n    except Exception as e:\n        print(type(e).__name__)\ndef predicate(line):\n    print('predicate',repr(line))\n    return False\ntry:\n    textwrap.indent(b'a\\nb\\nc','>',predicate)\nexcept Exception as e:\n    print(type(e).__name__)\ndef fail(line):\n    print('predicate',repr(line))\n    if line.startswith(b'b'):\n        raise ValueError('later')\n    return True\ntry:\n    textwrap.indent(b'a\\nb\\nc',None,fail)\nexcept Exception as e:\n    print(type(e).__name__)\n", "''\nTypeError\nTypeError\nTypeError\npredicate b'a\\n'\npredicate b'b\\n'\npredicate b'c'\nTypeError\npredicate b'a\\n'\npredicate b'b\\n'\nValueError\n" };
        yield return new object[] { "textwrap-guest-splitlines-generator", "import textwrap\nclass Text:\n    def splitlines(self,keepends):\n        print('split',keepends)\n        def generate():\n            for line in ['a\\n','',' \\n','b']:\n                print('yield',repr(line))\n                yield line\n        return generate()\nprint(repr(textwrap.indent(Text(),'>')))\ndef predicate(line):\n    print('predicate',repr(line))\n    return bool(line)\nprint(repr(textwrap.indent(Text(),'>',predicate)))\n", "split True\nyield 'a\\n'\nyield ''\nyield ' \\n'\nyield 'b'\n'>a\\n> \\n>b'\nsplit True\nyield 'a\\n'\npredicate 'a\\n'\nyield ''\npredicate ''\nyield ' \\n'\npredicate ' \\n'\nyield 'b'\npredicate 'b'\n'>a\\n> \\n>b'\n" };
        yield return new object[] { "textwrap-guest-line-whitespace-and-truth", "import textwrap\nclass Truth:\n    def __bool__(self):\n        print('truth')\n        return False\nclass Line:\n    def isspace(self):\n        print('isspace')\n        return Truth()\nclass Text:\n    def splitlines(self,keepends):\n        print('split',keepends)\n        return [Line(),Line()]\ntry:\n    textwrap.indent(Text(),'>')\nexcept Exception as e:\n    print(type(e).__name__)\n", "split True\nisspace\ntruth\nisspace\ntruth\nTypeError\n" };
        yield return new object[] { "textwrap-guest-line-errors-preempt-join", "import textwrap\nclass Text:\n    def splitlines(self,keepends):\n        yield 1\n        yield 2\n        raise ValueError('later source')\ndef predicate(line):\n    print('predicate',line)\n    return False\ntry:\n    textwrap.indent(Text(),None,predicate)\nexcept Exception as e:\n    print(type(e).__name__)\nclass Other:\n    def splitlines(self,keepends):\n        return [1,2]\ntry:\n    textwrap.indent(Other(),None)\nexcept Exception as e:\n    print(type(e).__name__)\n", "predicate 1\npredicate 2\nValueError\nAttributeError\n" };
        yield return new object[] { "textwrap-guest-member-binding-and-missing", "import textwrap\nclass Text:\n    @property\n    def splitlines(self):\n        print('lookup')\n        return lambda keepends: ['a\\n','b']\nprint(repr(textwrap.indent(Text(),'>',lambda line:True)))\nfor value in [None,1,[],object()]:\n    try:\n        textwrap.indent(value,'>')\n    except Exception as e:\n        print(type(e).__name__)\n", "lookup\n'>a\\n>b'\nAttributeError\nAttributeError\nAttributeError\nAttributeError\n" };
    }

    [Theory]
    [MemberData(nameof(PythonControls))]
    public async Task MatchesPython(string name, string source, string expected)
    {
        _ = name;
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
