using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvWriterowCallTests
{
    // Frozen isolated CPython semantic references; no timing.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "ordinary", "import csv,io\nsink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nprint(writer.writerow(['a','b']))\nprint(repr(sink.getvalue()))", "4\n'a,b\\n'\n" },
        { "unicode-quoting", "import csv,io\nsink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nprint(writer.writerow(['\u00e9,\ud83d\ude00','\"',None,7]))\nprint(repr(sink.getvalue()))", "14\n'\"\u00e9,\ud83d\ude00\",\"\"\"\",,7\\n'\n" },
        { "exposed-control", "import csv,io\nsink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nwrite=writer.writerow\nprint(write(['a','b']))\nprint(repr(sink.getvalue()))", "4\n'a,b\\n'\n" },
        { "custom-write-result", "import csv\nevents=[]\nclass Sink:\n def write(self,text):\n  events.append(text)\n  return 'saved'\nwriter=csv.writer(Sink(),lineterminator='\\n')\nprint(writer.writerow(['a','b']))\nprint(events)", "saved\n['a,b\\n']\n" },
        { "iteration-failure-before-write", "import csv\nevents=[]\nclass Sink:\n def write(self,text):\n  events.append('write:'+text)\n  return 9\ndef row():\n events.append('yield:a')\n yield 'a'\n events.append('raise')\n raise ValueError('row failed')\nwriter=csv.writer(Sink(),lineterminator='\\n')\ntry:writer.writerow(row())\nexcept ValueError as error:print(type(error).__name__,str(error))\nprint(events)", "ValueError row failed\n['yield:a', 'raise']\n" },
        { "parenthesized", "import csv,io\nsink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nprint((writer.writerow)(['a','b']))\nprint(repr(sink.getvalue()))", "4\n'a,b\\n'\n" },
        { "receiver-before-argument", "import csv,io\nsink=io.StringIO()\nother_sink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nother=csv.writer(other_sink,lineterminator='\\n')\ndef argument():\n global writer\n writer=other\n return ['a','b']\nprint(writer.writerow(argument()))\nprint(repr(sink.getvalue()),repr(other_sink.getvalue()))", "4\n'a,b\\n' ''\n" },
        { "custom-property", "events=[]\ndef accept(row):\n events.append(row)\n return len(row)\nclass Sink:\n @property\n def writerow(self):\n  events.append('lookup')\n  return accept\ndef receiver():\n events.append('receiver')\n return Sink()\ndef argument():\n events.append('argument')\n return ['a','b']\nprint(receiver().writerow(argument()),events)", "2 ['receiver', 'lookup', 'argument', ['a', 'b']]\n" },
        { "expanded-and-bad-arity", "import csv,io\nsink=io.StringIO()\nwriter=csv.writer(sink,lineterminator='\\n')\nprint(writer.writerow(*[['a','b']]))\ntry:writer.writerow()\nexcept TypeError:print('TypeError')\ntry:writer.writerow([],[])\nexcept TypeError:print('TypeError')\ntry:writer.writerow(bad=[])\nexcept TypeError:print('TypeError')\nprint(repr(sink.getvalue()))", "4\nTypeError\nTypeError\nTypeError\n'a,b\\n'\n" },
        { "dictwriter-fallback", "import csv,io\nsink=io.StringIO()\nwriter=csv.DictWriter(sink,['a','b'],lineterminator='\\n')\nprint(writer.writerow({'b':7,'a':'\u00e9'}))\nprint(repr(sink.getvalue()))", "4\n'\u00e9,7\\n'\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OrdinaryAndGeneralCallsKeepPythonBehavior(string name, string source, string expected)
    {
        using var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await compiled.RunAsync(new MockLythonHost()) : compiled.Run(new MockLythonHost());
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
