using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedArgumentSlotCompatibilityTests
{
    public static IEnumerable<object[]> PythonControls()
    {
        yield return new object[] { "named-argument-before-starred-position", "import textwrap,json\ndef invoke(f,*args,**kwargs):\n    try:\n        print(repr(f(*args,**kwargs)))\n    except Exception as e:\n        print(type(e).__name__)\ninvoke(textwrap.indent,'x',text='y')\ninvoke(textwrap.indent,'x','>',prefix='!')\ninvoke(textwrap.indent,'x',prefix='>')\ninvoke(json.dumps,'x',obj='y')\ninvoke(json.dumps,'x',ensure_ascii=False)\n", "TypeError\nTypeError\n'>x'\nTypeError\n'\"x\"'\n" };
        yield return new object[] { "explicit-keyword-before-starred-position", "import textwrap,json\ntry:\n    print(textwrap.indent(text='x',*['y']))\nexcept Exception as e:\n    print(type(e).__name__)\nprint(textwrap.indent(prefix='>',*['x']))\ntry:\n    print(json.dumps(obj='x',*['y']))\nexcept Exception as e:\n    print(type(e).__name__)\nprint(json.dumps(ensure_ascii=False,*['\u00e9\ud83d\ude00']))\n", "TypeError\n>x\nTypeError\n\"\u00e9\ud83d\ude00\"\n" };
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
