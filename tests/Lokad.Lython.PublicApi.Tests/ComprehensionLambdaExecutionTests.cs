using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComprehensionLambdaExecutionTests
{
    // Independently captured with CPython 3.13.2.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "global-directive", "value=\"module\"\ndef make():\n global value\n return [lambda:value for value in range(3)]\nfs=make()\nprint([f() for f in fs],value)", "[2, 2, 2] module\n" },
        { "nested-binding-scopes", "value=\"outer\"\ngroups=[[lambda:(value,inner) for inner in range(2)] for value in range(3)]\nprint([[f() for f in group] for group in groups],value)", "[[(2, 1), (2, 1)], [(2, 1), (2, 1)], [(2, 1), (2, 1)]] outer\n" },
        { "default-and-transitive-capture", "fs=[lambda saved=value:lambda:value+saved for value in range(3)]\nprint([f()() for f in fs])", "[2, 3, 4]\n" },
        { "outer-rebinding", "def make():\n offset=10\n fs=[lambda:offset+value for value in range(3)]\n offset=20\n return fs\nprint([f() for f in make()])", "[22, 22, 22]\n" },
        { "class-namespace", "value=\"global\"\nclass C:\n value=\"class\"\n fs=[lambda:value for unused in range(2)]\nprint([f() for f in C.fs])", "['global', 'global']\n" },
        { "lexical-super", "class Base:\n def value(self):return 3\nclass Child(Base):\n def methods(self):return [lambda self=self:super().value() for unused in range(2)]\nprint([f() for f in Child().methods()])", "[3, 3]\n" },
        { "walrus-binding", "def make():\n held=\"old\"\n fs=[(held:=lambda:value) for value in range(3)]\n return fs,held\nfs,last=make()\nprint([f() for f in fs],last is fs[-1])", "[2, 2, 2] True\n" },
        { "retained-container", "def make():\n held=[]\n return [lambda:held.append(value) for value in range(3)],held\nfs,held=make()\nfor f in fs:f()\nprint(held)", "[2, 2, 2]\n" },
        { "free-variable-locals", "fs=[lambda:(value,locals()['value']) for value in range(3)]\nprint([f() for f in fs])", "[(2, 2), (2, 2), (2, 2)]\n" },
        { "nonlocal-directive", "def make():\n value='outer'\n def collect():\n  nonlocal value\n  return [lambda:value for value in range(3)]\n fs=collect()\n return [f() for f in fs],value\nprint(make())", "([2, 2, 2], 'outer')\n" },
        { "future-binding-after-capture", "fs=[]\nrows=[later for first in (0,) if fs.append(lambda:later) or True for later in (2,)]\nprint(fs[0](),rows)", "2 [2]\n" },
        { "future-binding-before-and-after-store", "fs=[]\ndef check(f):\n fs.append(f)\n try: print(f())\n except NameError: print('unbound')\n return True\nrows=[later for first in (0,) if check(lambda:later) for later in (2,)]\nprint([f() for f in fs],rows)", "unbound\n[2] [2]\n" },
        { "dictionary-bodies", "fs={value:lambda:value for value in range(3)}\nprint([(key,f()) for key,f in fs.items()])", "[(0, 2), (1, 2), (2, 2)]\n" },
        { "set-bodies", "fs={lambda:value for value in range(3)}\nprint(len(fs),sorted(f() for f in fs))", "3 [2, 2, 2]\n" },
        { "function-identity-hashing", "def regular(value=1):return value\ndef generator():yield 1\nf=lambda:2\nfor function in (regular,generator,f):\n alias=function\n keys={function:'kept'}\n before=hash(function)\n function.tag=[]\n function.__name__='changed'\n function.__hash__=None\n print(len({function,alias}),keys[alias],hash(function)==before)", "1 kept True\n1 kept True\n1 kept True\n" },
        { "live-free-variable-snapshots", "def outer():\n value=1\n def change():\n  nonlocal value\n  value=2\n def snapshot():\n  before=value\n  change()\n  return before,value,locals()['value'],'value' in dir()\n return snapshot\nprint(outer()())", "(1, 2, 2, True)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LexicalBindingsMatchPythonAfterTheirComprehensionEnds(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Code + " " + d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
