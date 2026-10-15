using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ListAppendCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "items=[]\ndef produce():\n    items.append(1)\n    yield 7\n    items.append(2)\n    yield 8\ng=produce()\nprint(next(g),items)\nprint(next(g),items)", "7 [1]\n8 [1, 2]\n" },
        { "def f(value): return value+2\nclass C: pass\nc=C()\nc.append=f\nprint(c.append(5))", "7\n" },
        { "from collections import deque\nitems=deque()\nitems.append(3)\nprint(list(items))", "[3]\n" },
        { "items=[]\ndef argument():\n    print('argument')\n    return 3\ntry: items.append(value=argument())\nexcept TypeError: print('keyword')\nprint(items)", "argument\nkeyword\n[]\n" },
        { "items=[]\nmethod=items.append\ntry: method(value=3)\nexcept TypeError: print('keyword')\nmethod(4)\nprint(items)", "keyword\n[4]\n" },
        { "items=[]\ntry: list.append(items,value=3)\nexcept TypeError: print('keyword')\nlist.append(items,4)\nprint(items)", "keyword\n[4]\n" },
        { "items=[]\nfor method in [items.append,list.append.__get__(items,list)]:\n    try: method(**{'value':3})\n    except TypeError: print('keyword')\ntry: list.append(items,**{'value':4})\nexcept TypeError: print('keyword')\nprint(items)", "keyword\nkeyword\nkeyword\n[]\n" },
        { "items=[]\ntry: items.append()\nexcept TypeError: print('missing')\ntry: items.append(1,2)\nexcept TypeError: print('extra')\ntry: items.append(1,value=2)\nexcept TypeError: print('duplicate')\ntry: items.append(object=3)\nexcept TypeError: print('keyword')\nitems.append(*(4,))\nprint(items)", "missing\nextra\nduplicate\nkeyword\n[4]\n" },
        { "items=[]\ntry: [items.append(value=i) for i in range(2)]\nexcept TypeError: print('keyword')\nprint(items)", "keyword\n[]\n" },
        { "class C:\n    def append(self,value): return value+1\nprint(C().append(value=3))", "4\n" },
        { "items=[]\nold=items\ndef argument():\n    global items\n    items=[]\n    return 5\nitems.append(argument())\nprint(old,items)", "[5] []\n" },
        { "class C:\n    append=[]\ndef argument():\n    print('argument')\n    return 3\ntry: C().append(argument())\nexcept TypeError: print('not-callable')", "argument\nnot-callable\n" },
        { "class C:\n    def append(self,value): print('old',value)\nc=C()\ndef replacement(self,value): print('new',value)\ndef argument():\n    C.append=replacement\n    return 3\nc.append(argument())\nc.append(4)", "old 3\nnew 4\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task AppendPreservesPositionalBindingAndLookupOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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
    }
}
