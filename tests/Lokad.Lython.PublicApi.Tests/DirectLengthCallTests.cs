using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DirectLengthCallTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "for value in ['\u00e9\ud83d\ude00',b'abc',[1,2],(1,),{'a':1},{1,2},range(7)]:print(len(value))", "2\n3\n2\n1\n1\n2\n7\n" },
        { "from collections import ChainMap,Counter,defaultdict\nshared={'a':1}\nmapping=ChainMap(shared,{'b':2,'a':0})\nprint(len(mapping),len(mapping.keys()),len(mapping.values()),len(mapping.items()),len(Counter('abb')),len(defaultdict(int,shared)))", "2 2 2 2 2 1\n" },
        { "original=len\nevents=[]\ndef argument():\n global len\n events.append('argument')\n len=lambda value:99\n return '\u00e9\ud83d\ude00'\nprint(len(argument()),len('x'),original([1,2]),events)", "2 99 2 ['argument']\n" },
        { "events=[]\ndef arg(label,value):events.append(label);return value\ndef exercise(length,value):\n for action in [lambda:length(arg('bad',value)),lambda:length(arg('extra',[1]),arg('extra2',[2])),lambda:length(value=arg('kw',[1])),lambda:length()]:\n  try:action()\n  except TypeError:events.append('error')\nexercise(len,4)\nprint(events)", "['bad', 'error', 'extra', 'extra2', 'error', 'kw', 'error', 'error']\n" },
        { "events=[]\ndef args():events.append('args');return ('abc',)\nprint(len(*args()),events)\ntry:len(**{'obj':[1]})\nexcept TypeError:print('keyword error')", "3 ['args']\nkeyword error\n" },
        { "original=len\ndef len(value):return 99\nprint(len('abc'),original('abc'))", "99 3\n" },
        { "events=[]\nclass Holder:\n @property\n def get(self):events.append('lookup');return len\nprint(Holder().get('\u00e9\ud83d\ude00'),events)", "2 ['lookup']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task OrdinaryAndGeneralCallsPreservePythonEvaluationOrder(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
