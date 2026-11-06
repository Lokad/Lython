using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedPatternTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["OR overwrites failed captures", "match [0,7]:\n case [x,0]|[0,x]:print(x)\n", "7\n"];
        yield return ["nested working storage", "match {'a':[0,9],'b':{'c':[1,2,3]}}:\n case {'a':([x,0]|[0,x]),'b':{'c':[first,*middle,last]}}:print(x,first,middle,last)\n", "9 1 [2] 3\n"];
        yield return ["sequence snapshot before callbacks", "class D:\n def __get__(self,obj,owner):source.clear();return 1\nclass Keys:expected=D()\nsource=[1,2,3,4]\nmatch source:\n case [Keys.expected,*rest,last]:print(rest,last,source)\n", "[2, 3] 4 []\n"];
        yield return ["OR captures in lowered class", "class Owner:\n match {'row':[0,8]}:\n  case {'row':([x,0]|[0,x])}:value=x\nprint(Owner.value)\n", "8\n"];
        yield return ["starred capture and closure", "def extract(row):\n match row:\n  case {'values':[first,*rest,last]}:\n   def get():return first,rest,last\n   return get\nget=extract({'values':[1,2,3,4]})\nprint(get())\n", "(1, [2, 3], 4)\n"];
        yield return ["guard sees successful alternative", "events=[]\ndef guard(value):events.append(value);return False\nmatch [0,7]:\n case ([x,0]|[0,x]) if guard(x):pass\n case _:print(x,events)\n", "7 [7]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PatternsKeepPythonCapturesAndExtraction(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        var baseline = budget.ReservedBytes;
        using (var result = script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = await script.RunAsync(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
