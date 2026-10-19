using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComprehensionEffectDiagnosticsTests
{
    // Expectations independently captured with CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "direct-call-control", "items=[]\ndef add(value):\n items.append(value)\n return value\nadd(7)\nprint(items[0])", "7\n" },
        { "list-item", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(i) for i in (7,)]\nprint(items[0],rows)", "7 [7]\n" },
        { "list-filter", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[i for i in (7,) if add(i)]\nprint(items[0],rows)", "7 [7]\n" },
        { "set-item", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={add(i) for i in (7,)}\nprint(items[0],sorted(rows))", "7 [7]\n" },
        { "dict-key", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={add(i):i for i in (7,)}\nprint(items[0],rows)", "7 {7: 7}\n" },
        { "dict-value", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={i:add(i) for i in (7,)}\nprint(items[0],rows)", "7 {7: 7}\n" },
        { "inner-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[j for i in (0,) for j in (add(7),)]\nprint(items[0],rows)", "7 [7]\n" },
        { "outer-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[i for i in (add(7),)]\nprint(items[0],rows)", "7 [7]\n" },
        { "false-filter-after-effect", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[i for i in (7,) if add(i) and False]\nprint(items[0],rows)", "7 []\n" },
        { "nested-comprehension", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[[add(i) for i in (7,)] for j in (0,)]\nprint(items[0],rows)", "7 [[7]]\n" },
        { "conditional-effect", "import sys\nflag=bool(sys.argv)\nitems=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(i) if flag else 0 for i in (7,)]\nprint(items[0],rows)", "7 [7]\n" },
        { "short-circuit-effect", "import sys\nflag=bool(sys.argv)\nitems=[]\ndef add(value):\n items.append(value)\n return value\nrows=[flag and add(i) for i in (7,)]\nprint(items[0],rows)", "7 [7]\n" },
        { "shadowed-name", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(7) for items in (99,)]\nprint(items[0],rows)", "7 [7]\n" },
        { "aliased-list", "items=[]\nalias=items\ndef add(value):\n alias.append(value)\n return value\nrows=[add(i) for i in (7,)]\nprint(items[0],rows)", "7 [7]\n" },
        { "dictionary-alias", "data={}\nholder=(data,)\ndef store():\n holder[0]['k']=7\n return 7\nrows=[store() for i in (0,)]\nprint(data['k'],rows)", "7 [7]\n" },
        { "removed-dictionary-key", "data={'k':7}\ndef remove():\n del data['k']\n return 0\nrows=[remove() for i in (0,)]\ntry: print(data['k'])\nexcept KeyError: print('missing')", "missing\n" },
        { "local-closure", "def run():\n items=[]\n def add(value):\n  items.append(value)\n  return value\n rows=[add(i) for i in (7,)]\n print(items[0],rows)\nrun()", "7 [7]\n" },
        { "generator-outer-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=(i for i in (add(7),))\nprint(items[0],list(rows))", "7 [7]\n" },
        { "generator-consumption", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=(add(i) for i in (7,))\nprint(list(rows),items[0])", "[7] 7\n" },
        { "capture-before-store", "fs=[]\ndef check(f):\n fs.append(f)\n try: print(f())\n except NameError: print('unbound')\n return True\nrows=[later for first in (0,) if check(lambda:later) for later in (2,)]\nprint(fs[0](),rows)", "unbound\n2 [2]\n" },
        { "empty-body-not-analyzed", "rows=[[][0] for i in ()]\nprint(rows)", "[]\n" },
        { "empty-dict-body-not-analyzed", "rows={[][0]:[][0] for i in ()}\nprint(rows)", "{}\n" },
        { "empty-outer-with-effects", "items=[]\ndef add(value):\n items.append(value)\n return value\ndef load():\n add(7)\n return ()\nrows=[[][0] for i in load()]\nprint(items[0],rows)", "7 []\n" },
        { "empty-generator-outer-with-effects", "items=[]\ndef add(value):\n items.append(value)\n return value\ndef load():\n add(7)\n return ()\nrows=([][0] for i in load())\nprint(items[0],list(rows))", "7 []\n" },
        { "later-expression-in-same-display", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=([add(i) for i in (7,)],items[0])\nprint(rows)", "([7], 7)\n" },
        { "nested-effects-before-read", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[([add(i) for i in (7,)],items[0]) for j in (0,)]\nprint(rows)", "[([7], 7)]\n" },
        { "lambda-default-in-comprehension", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[lambda saved=add(7):saved for i in (0,)]\nprint(items[0],rows[0]())", "7 7\n" },
        { "lambda-keyword-default-in-comprehension", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[lambda *,saved=add(7):saved for i in (0,)]\nprint(items[0],rows[0]())", "7 7\n" },
        { "lambda-default-outside-comprehension", "items=[]\ndef add(value):\n items.append(value)\n return value\nf=lambda saved=add(7):saved\nprint(items[0],f())", "7 7\n" },
        { "empty-comprehension-skips-default", "rows=[lambda saved=[][0]:0 for i in ()]\nprint(rows)", "[]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task EvaluatedComprehensionEffectsInvalidateSurroundingFacts(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Code + " " + d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { Args = ["case.py"] };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string, string> UnchangedFacts => new()
    {
        { "pure-item", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[i for i in (7,)]\nprint(items[0])" },
        { "scalar-builtin", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[len('abc') for i in (7,)]\nprint(items[0])" },
        { "pure-set", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={i for i in (7,)}\nprint(items[0])" },
        { "pure-dict", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={i:i for i in (7,)}\nprint(items[0])" },
        { "skipped-filter", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(i) for i in (7,) if False]\nprint(items[0])" },
        { "short-circuit-and", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[False and add(i) for i in (7,)]\nprint(items[0])" },
        { "short-circuit-or", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[True or add(i) for i in (7,)]\nprint(items[0])" },
        { "empty-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(i) for i in ()]\nprint(items[0])" },
        { "empty-set-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={add(i) for i in ()}\nprint(items[0])" },
        { "empty-dict-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows={i:add(i) for i in ()}\nprint(items[0])" },
        { "empty-inner-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[add(j) for i in (0,) for j in ()]\nprint(items[0])" },
        { "unreached-inner-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[j for i in () for j in (add(7),)]\nprint(items[0])" },
        { "deferred-generator-item", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=(add(i) for i in (7,))\nprint(items[0])" },
        { "deferred-generator-filter", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=(i for i in (7,) if add(i))\nprint(items[0])" },
        { "deferred-generator-inner-iterable", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=(j for i in (0,) for j in (add(7),))\nprint(items[0])" },
        { "deferred-lambda-body", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[lambda:add(i) for i in (7,)]\nprint(items[0])" },
        { "earlier-effect-does-not-taint-new-scope", "items=[]\ndef add(value):\n items.append(value)\n return value\nadd(7)\nuntouched=[]\nrows=[len('abc') for i in (7,)]\nprint(untouched[0])" },
        { "nested-deferred-generator-body", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[(add(i) for i in (7,)) for j in (0,)]\nprint(items[0])" },
        { "deferred-body-with-pure-default", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[lambda saved=0:add(i) for i in (7,)]\nprint(items[0])" },
        { "skipped-default", "items=[]\ndef add(value):\n items.append(value)\n return value\nrows=[lambda saved=add(i):saved for i in (7,) if False]\nprint(items[0])" },
    };

    [Theory]
    [MemberData(nameof(UnchangedFacts))]
    public void UnexecutedOrPureBodiesKeepCertainIndexDiagnostics(string name, string source)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.False(script.IsValid, name);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3117");
    }
}
