using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CountedReplaceExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "counted", "print('aaaa'.replace('a','b',2))", "bbaa\n" },
        { "zero-negative", "for count in [0,-1,-5]:print('aaaa'.replace('a','b',count))", "aaaa\nbbbb\nbbbb\n" },
        { "empty-old", "print('abc'.replace('','-',2))", "-a-bc\n" },
        { "unicode", "print('\u00e9\u00e9-\ud83d\ude42'.replace('\u00e9','x',1))", "x\u00e9-\ud83d\ude42\n" },
        { "captured-receiver", "value='aaaa'\ndef old():\n global value\n value='xxxx'\n return 'a'\nprint(value.replace(old(),'b',2),value)", "bbaa xxxx\n" },
        { "index-count", "events=[]\nclass Count:\n def __index__(self):events.append('count');return 2\nprint('aaaa'.replace('a','b',Count()),events)", "bbaa ['count']\n" },
        { "arguments-before-validation", "events=[]\ndef count():events.append('count');return 1\ntry:'aaaa'.replace(7,'b',count())\nexcept TypeError:print(events)", "['count']\n" },
        { "helper-text-report", "def edit(line):return line.replace('old','new',1)\nlines=['old-old','plain','\u00e9-old-old']\nprint('\\n'.join(edit(line) for line in lines))", "new-old\nplain\n\u00e9-new-old\n" },
        { "exposed-method", "text='aaaa';method=text.replace\ntext='xxxx'\nprint(method('a','b',2))", "bbaa\n" },
        { "unbound-method", "print(str.replace('aaaa','a','b',2))", "bbaa\n" },
        { "keyword-count", "print('aaaa'.replace('a','b',count=2))", "bbaa\n" },
        { "guest-override", "class Text:\n def replace(self,old,new,count):return old,new,count\nprint(Text().replace('a','b',2))", "('a', 'b', 2)\n" },
        { "noncallable-member", "events=[]\nclass Text:replace=[]\ndef count():events.append('count');return 2\ntry:Text().replace('a','b',count())\nexcept TypeError:print(events)", "['count']\n" },
        { "explicit-none", "try:'aaaa'.replace('a','b',None)\nexcept TypeError as error:print(str(error))", "'NoneType' object cannot be interpreted as an integer\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task CountedReplacementPreservesPythonCallsAndEffects(string name, string source, string expected)
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
