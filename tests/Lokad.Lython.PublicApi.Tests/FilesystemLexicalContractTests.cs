using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FilesystemLexicalContractTests
{
    // Independent CPython 3.13.2 outputs; the cases avoid concrete OS path separators.
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[] { "commonprefix-unicode", "import os\nprint(repr(os.path.commonprefix(['😀x','😁y'])))", "''\n" };
        yield return new object[] { "commonprefix-control", "import os\nprint(repr(os.path.commonprefix(['alpha','alpine'])))", "'alp'\n" };
        yield return new object[] { "shadow-os-walk", "class Custom:\n def walk(self,value):return value\nos=Custom()\nprint(os.walk(7))", "7\n" };
        yield return new object[] { "shadow-os-commonpath", "class Paths:\n def commonpath(self,value):return value\nclass Custom:\n def __init__(self):self.path=Paths()\nos=Custom()\nprint(os.path.commonpath(7))", "7\n" };
        yield return new object[] { "shadow-Path", "def Path(value):return 7\np=Path('x')\nprint(p+1)", "8\n" };
        yield return new object[] { "shadow-glob", "class Custom:\n def glob(self,value):return value\nglob=Custom()\nprint(glob.glob(7))", "7\n" };
        yield return new object[] { "commonpath-parent-segment", "import os\ntry:print(os.path.commonpath(['a/../b','a/c']))\nexcept Exception as e:print(type(e).__name__)", "a\n" };
        yield return new object[] { "unicode-full", "import os\nprint(os.path.commonprefix([\"😀a\",\"😀b\"]))", "😀\n" };
        yield return new object[] { "unicode-partial", "import os\nprint(repr(os.path.commonprefix([\"a😀x\",\"a😁y\"])))", "'a'\n" };
        yield return new object[] { "unicode-empty", "import os\nprint(repr(os.path.commonprefix([])))", "''\n" };
    }
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LexicalAndResolvedContractsMatchPython(string name,string source,string expected)
    {
        using var script=new LythonEngine().Compile(source);
        Assert.True(script.IsValid,name+": "+string.Join(";",script.Diagnostics.Select(d=>d.Message)));
        var budget=new LythonMemoryBudget(262144);
        foreach(var asynchronous in new[]{false,true})
        {
            using(var result=asynchronous ? await script.RunAsync(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget})
                : script.Run(new MockLythonHost(),new LythonRunOptions{MemoryBudget=budget}))
            { Assert.True(result.Success,result.Failure?.Message);Assert.Equal(expected,result.StandardOutput); }
            Assert.Equal(0,budget.ReservedBytes);
        }
    }
}
