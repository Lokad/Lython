using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NestedSequenceFactDiagnosticsTests
{
    // Expectations independently captured with CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "tuple-list-direct", "items=[]\ndef add():items.append(7)\nholder=(items,)\nadd()\nprint(holder[0][0])", "7\n" },
        { "tuple-list-eager", "items=[]\ndef add():items.append(7)\nholder=(items,)\nrows=[add() for i in (0,)]\nprint(holder[0][0])", "7\n" },
        { "nested-tuple-list", "items=[]\ndef add():items.append(7)\nholder=((items,),)\nadd()\nprint(holder[0][0][0])", "7\n" },
        { "changed-element-type", "items=[0]\ndef add():items.append('x')\nadd()\nprint(items[1].upper())", "X\n" },
        { "aliased-list-element-type", "items=[0]\ndef add():items.append('x')\nalias=items\nadd()\nprint(alias[1].upper())", "X\n" },
        { "nested-list-element-type", "items=[0]\ndef add():items.append('x')\nholder=((items,),)\nadd()\nprint(holder[0][0][1].upper())", "X\n" },
        { "inferred-list-element-type", "items=[i for i in (0,)]\ndef add():items.append('x')\nadd()\nprint(items[1].upper())", "X\n" },
        { "maybe-none-tuple", "import sys\nflag=bool(sys.argv)\nitems=[]\ndef add():items.append(7)\nholder=(items,) if flag else None\nadd()\nif holder is not None:print(holder[0][0])", "7\n" },
        { "builtin-append-element-type", "items=[0]\nitems.append('x')\nprint(items[1].upper())", "X\n" },
        { "slice-replaces-element-type", "items=[0]\nitems[:]=['x']\nprint(items[0].upper())", "X\n" },
        { "shadowed-comprehension-name", "items=[]\ndef add():items.append(7)\nholder=(items,)\nrows=[add() for items in (99,)]\nprint(holder[0][0],items[0])", "7 7\n" },
        { "scalar-sibling-survives", "items=[]\ndef add():items.append(7)\nholder=('abc',(items,))\nadd()\nprint(holder[0].upper(),holder[1][0][0])", "ABC 7\n" },
        { "runtime-index-check", "items=[1]\nholder=(items,)\ndef clear():items.clear()\nclear()\ntry: print(holder[0][0])\nexcept IndexError: print('empty')", "empty\n" },
        { "local-captured-tuple", "def run():\n items=[]\n holder=(items,)\n def add():items.append(7)\n add()\n print(holder[0][0])\nrun()", "7\n" },
        { "tuple-bound-refinement", "items=[]\ndef add():items.append(7)\nholder=(items,)\nadd()\nif len(holder)==0:print(holder[0])\nprint(holder[0][0])", "7\n" },
        { "bounded-built-fact-depth", "items=[]\ndef add():items.append(7)\ndeep=items\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\nholder=(deep,'abc')\nadd()\nfor i in range(66):deep=deep[0]\nprint(deep[0],holder[1].upper())", "7 ABC\n" },
        { "conservative-copy-keeps-runtime-error", "items=['alpha','beta']\ncopy=items.copy()\nitems.pop()\ntry:copy[0].find(1)\nexcept TypeError:print('TypeError')", "TypeError\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task MutationDiscardsStaleSequenceContents(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
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

    public static TheoryData<string, string, string> CertainErrors => new()
    {
        { "immutable-outer-length", "items=[]\ndef add():items.append(7)\nholder=(items,)\nadd()\nprint(holder[2])", "LA3117" },
        { "immutable-nested-element", "items=[]\ndef add():items.append(7)\nholder=('abc',items)\nadd()\nprint(holder[0].missing)", "LA3113" },
        { "pure-nested-bound", "holder=((),)\nlen('abc')\nprint(holder[0][0])", "LA3117" },
        { "immutable-inner-length", "items=[]\ndef add():items.append(7)\nholder=((items,),)\nadd()\nprint(holder[0][2])", "LA3117" },
        { "nested-immutable-scalar", "items=[]\ndef add():items.append(7)\nholder=(('abc',),items)\nadd()\nprint(holder[0][0].missing)", "LA3113" },
        { "fresh-list-length", "items=[]\ndef add():items.append(7)\nadd()\nfresh=[]\nprint(fresh[0])", "LA3117" },
        { "fresh-list-element", "items=[]\ndef add():items.append(7)\nadd()\nfresh=[0]\nprint(fresh[0].missing)", "LA3113" },
        { "noninteger-index-after-call", "items=[]\ndef add():items.append(7)\nholder=(items,)\nadd()\nprint(holder[0]['x'])", "LA3116" },
        { "pure-call-keeps-nested-list-bound", "items=[]\ndef add():items.append(7)\nholder=(items,)\nlen('abc')\nprint(holder[0][0])", "LA3117" },
        { "unreached-call-keeps-nested-list-bound", "items=[]\ndef add():items.append(7)\nholder=(items,)\nrows=[add() for i in (0,) if False]\nprint(holder[0][0])", "LA3117" },
        { "deferred-lambda-keeps-nested-list-bound", "items=[]\ndef add():items.append(7)\nholder=(items,)\nf=lambda:add()\nprint(holder[0][0])", "LA3117" },
        { "deferred-generator-keeps-nested-list-bound", "items=[]\ndef add():items.append(7)\nholder=(items,)\nrows=(add() for i in (0,))\nprint(holder[0][0])", "LA3117" },
        { "bounded-depth-keeps-outer-scalar", "items=[]\ndef add():items.append(7)\ndeep=items\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\ndeep=(deep,)\nholder=(deep,'abc')\nadd()\nprint(holder[1].missing)", "LA3113" },
    };

    [Theory]
    [MemberData(nameof(CertainErrors))]
    public void ImmutableAndFreshFactsKeepTheirDiagnostics(string name, string source, string code)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid, name);
        Assert.Contains(script.Diagnostics, d => d.Code == code);
    }
}
