using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlEscapeCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "escape-basic-unicode-quotes", "import html\nfor text in ['', 'plain \u00e9\ud83d\ude00', '&<>'+chr(34)+chr(39), '&amp;<a>', '\\n\\t\\xa0\\u2028']:\n    print(repr(html.escape(text)),repr(html.escape(text,False)),repr(html.escape(text,quote=True)))\ntext='plain \u00e9\ud83d\ude00'\nprint(html.escape(text) is text)\n", "'' '' ''\n'plain \u00e9\ud83d\ude00' 'plain \u00e9\ud83d\ude00' 'plain \u00e9\ud83d\ude00'\n'&amp;&lt;&gt;&quot;&#x27;' '&amp;&lt;&gt;\"\\'' '&amp;&lt;&gt;&quot;&#x27;'\n'&amp;amp;&lt;a&gt;' '&amp;amp;&lt;a&gt;' '&amp;amp;&lt;a&gt;'\n'\\n\\t\\xa0\\u2028' '\\n\\t\\xa0\\u2028' '\\n\\t\\xa0\\u2028'\nTrue\n" };
        yield return new object[] { "escape-truth-slot-order", "import html\nclass Quote:\n    def __bool__(self):\n        print('truth')\n        return False\nprint(repr(html.escape('<&'+chr(34),Quote())))\nclass Fail:\n    def __bool__(self):\n        raise ValueError('quote')\ntry:\n    html.escape('plain',Fail())\nexcept Exception as e:\n    print(type(e).__name__)\n", "truth\n'&lt;&amp;\"'\nValueError\n" };
        yield return new object[] { "escape-guest-replace-order", "import html\nclass Quote:\n    def __bool__(self):\n        print('truth')\n        return True\nclass Text:\n    marker=42\n    def replace(self,old,new):\n        print('replace',repr(old),repr(new))\n        return self\ntext=Text()\nprint(html.escape(text,Quote()).marker,html.escape(text,False) is text)\n", "replace '&' '&amp;'\nreplace '<' '&lt;'\nreplace '>' '&gt;'\ntruth\nreplace '\"' '&quot;'\nreplace \"'\" '&#x27;'\nreplace '&' '&amp;'\nreplace '<' '&lt;'\nreplace '>' '&gt;'\n42 True\n" };
        yield return new object[] { "escape-aliases-binding-and-invalid-inputs", "import html as h\nfrom html import escape as clean\nprint(repr(clean(s='<'+chr(34),quote=False)))\ndef call(f,*args,**kwargs):\n    try:\n        print(repr(f(*args,**kwargs)))\n    except Exception as e:\n        print(type(e).__name__)\nfor value in [None,1,b'a',[]]:\n    call(clean,value)\ncall(clean)\ncall(clean,'x',False,True)\ncall(clean,'x',s='y')\ncall(clean,'x',bad=True)\ndef escape(text):\n    return 42\nprint(escape('x'))\n", "'&lt;\"'\nAttributeError\nAttributeError\nTypeError\nAttributeError\nTypeError\nTypeError\nTypeError\nTypeError\n42\n" };
        yield return new object[] { "escape-guest-results-and-error-priority", "import html\nclass Quote:\n    def __bool__(self):\n        print('truth')\n        return True\nclass Text:\n    def replace(self,old,new):\n        print('replace',repr(old))\n        return '<'+chr(34)+chr(39)\nprint(repr(html.escape(Text(),Quote())))\nclass Failure:\n    def replace(self,old,new):\n        print('replace',repr(old))\n        raise ValueError('replace')\ntry:\n    html.escape(Failure(),Quote())\nexcept Exception as e:\n    print(type(e).__name__)\n", "replace '&'\ntruth\n'&lt;&quot;&#x27;'\nreplace '&'\nValueError\n" };
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
