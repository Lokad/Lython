using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BoundedStartsWithExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "two-arguments", "print('abc'.startswith('b',1))", "True\n" },
        { "three-arguments", "print('abc'.startswith('b',1,2))", "True\n" },
        { "none-bounds", "print('abc'.startswith('a',None,None))", "True\n" },
        { "range-edges", "for start in [-5,0,3,4]:print('abc'.startswith('',start), 'abc'.startswith(('z','a'),start,3))", "True True\nTrue True\nTrue False\nFalse False\n" },
        { "index-bounds", "events=[]\nclass Bound:\n def __init__(self,name,value):self.name=name;self.value=value\n def __index__(self):events.append(self.name);return self.value\nprint('abc'.startswith('b',Bound('start',1),Bound('end',2)),events)", "True ['start', 'end']\n" },
        { "bad-start-prevents-end", "events=[]\nclass Start:\n def __index__(self):events.append('start');return 'bad'\nclass End:\n def __index__(self):events.append('end');return 3\ntry:'abc'.startswith('a',Start(),End())\nexcept TypeError:print(events)", "['start']\n" },
        { "captured-receiver", "value='abc'\ndef start():\n global value\n value='xyz'\n return 1\nprint(value.startswith('b',start(),2),value)", "True xyz\n" },
        { "helper-search-report", "def wanted(line):return line.startswith('id:',4,7)\nfor line in ['row id:7','row tag:8','row id:9']:\n if wanted(line):print(line)", "row id:7\nrow id:9\n" },
        { "tuple-short-circuit", "print('abc'.startswith(('a',7),0,3))\ntry:'abc'.startswith(('z',7),0,3)\nexcept TypeError:print('bad tuple')", "True\nbad tuple\n" },
        { "exposed-method", "text='abc';method=text.startswith\ntext='xyz'\nprint(method('b',1,2),method.__self__)", "True abc\n" },
        { "unbound-method", "print(str.startswith('abc','b',1,2))", "True\n" },
        { "guest-override", "class Text:\n def startswith(self,prefix,start,end):return prefix,start,end\nprint(Text().startswith('b',1,2))", "('b', 1, 2)\n" },
        { "noncallable-member", "events=[]\nclass Text:startswith=[]\ndef start():events.append('start');return 1\ntry:Text().startswith('b',start(),2)\nexcept TypeError:print(events)", "['start']\n" },
        { "keyword-bound", "try:'abc'.startswith('b',start=1)\nexcept TypeError:print('positional only')", "positional only\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task BoundsPreservePythonCallsAndEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
