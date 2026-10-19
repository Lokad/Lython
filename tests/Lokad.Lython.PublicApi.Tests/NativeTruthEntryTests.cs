using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeTruthEntryTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "native-values", "for value in [None,False,True,0,1,0.0,-0.0,[],[0],{}, {'a':1},(),(0,),set(),{0},range(0),range(1),b'',b'0','','0']:print(bool(value))", "False\nFalse\nTrue\nFalse\nTrue\nFalse\nFalse\nFalse\nTrue\nFalse\nTrue\nFalse\nTrue\nFalse\nTrue\nFalse\nTrue\nFalse\nTrue\nFalse\nTrue\n" },
        { "any-all", "print(any([0,False,'']),all([1,True,'x']),any(()),all(()))", "False True False True\n" },
        { "bool-priority", "events=[]\nclass Value:\n def __bool__(self):events.append('bool');return False\n def __len__(self):events.append('len');return 1\nprint(bool(Value()),events)", "False ['bool']\n" },
        { "default-shadow", "class Value:pass\nvalue=Value();value.__bool__=lambda:False\nprint(bool(value))", "True\n" },
        { "invalid-length", "class Value:\n def __init__(self,value):self.value=value\n def __len__(self):return self.value\nfor value in [-1,1.5,True]:\n try:print(bool(Value(value)))\n except Exception as error:print(type(error).__name__)", "ValueError\nTypeError\nTrue\n" },
        { "descriptor-effects", "events=[]\nclass Value:\n @property\n def __bool__(self):\n  events.append('bind')\n  def answer():events.append('call');return True\n  return answer\nprint(bool(Value()),events)", "True ['bind', 'call']\n" },
        { "mixed-short-circuit", "events=[]\nclass Value:\n def __bool__(self):events.append('guest');return True\nprint(any([1,Value()]),all([0,Value()]),any([0,Value()]),events)", "True False True ['guest']\n" },
        { "helper-record-filter", "def accepted(row):return all((row[0]=='ok',row[1]>0))\nprint(sum(1 for row in [('ok',1),('bad',2),('ok',3),('ok',0)] if accepted(row)))", "2\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task TruthPreservesPythonCallsAndEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
