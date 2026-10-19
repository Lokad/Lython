using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AssertionExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "passing-skips-message", "events=[]\ndef condition():events.append('condition');return True\ndef message():events.append('message');return 'payload'\nassert condition(),message()\nprint(events)", "['condition']\n" },
        { "failing-evaluates-message", "events=[]\ndef condition():events.append('condition');return False\ndef message():events.append('message');return 'payload'\ntry:assert condition(),message()\nexcept AssertionError as error:print(str(error),events)", "payload ['condition', 'message']\n" },
        { "custom-truth-once", "events=[]\nclass Value:\n def __bool__(self):events.append('truth');return False\ntry:assert Value(),'payload'\nexcept AssertionError as error:print(str(error),events)", "payload ['truth']\n" },
        { "message-identity-without-rendering", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message()\ntry:assert False,message\nexcept AssertionError as error:print(error.args[0] is message,events)", "True []\n" },
        { "ordinary-helper", "def validate(value):\n assert value>0,'positive'\n return value+1\nprint(validate(7))\ntry:validate(-1)\nexcept AssertionError as error:print(str(error))", "8\npositive\n" },
        { "protected-cleanup", "events=[]\ndef validate():\n try:assert False,'payload'\n finally:events.append('finally')\ntry:validate()\nexcept AssertionError as error:print(str(error),events)", "payload ['finally']\n" },
        { "complete-csv-with-assert", "import csv\nimport io\n\nclass Total:\n    def __init__(self):\n        self.amount = 0\n\n    def add(self, amount):\n        self.amount += amount\n\n    def __format__(self, spec):\n        return format(self.amount, spec)\n\ntotals = {}\naccepted = 0\nsource = io.StringIO('kind,amount\\nred,3\\nblue,2\\nred,-1\\nblue,5\\n')\nfor row in csv.DictReader(source):\n    assert row['amount'], 'missing amount'\n    amount = int(row['amount'])\n    if amount >= 0:\n        kind = row['kind']\n        if kind not in totals:\n            totals[kind] = Total()\n        totals[kind].add(amount)\n        accepted += 1\ntarget = io.StringIO()\ntarget.write('kind,total\\n')\nfor kind in sorted(totals):\n    target.write(f'{kind},{totals[kind]:04d}\\n')\nprint(target.getvalue(), end='')\nprint(accepted)\n", "kind,total\nblue,0007\nred,0003\n3\n" },
        { "absent-versus-none", "try:assert False\nexcept AssertionError as e:print(e.args,repr(str(e)))\ntry:assert False,None\nexcept AssertionError as e:print(e.args,str(e))", "() ''\n(None,) None\n" },
        { "render-only-when-requested", "events=[]\nclass Message:\n def __str__(self):events.append('str');raise RuntimeError('render')\nmessage=Message()\ntry:assert False,message\nexcept AssertionError as error:\n print(error.args[0] is message,events)\n try:str(error)\n except RuntimeError as failure:print(str(failure),events)", "True []\nrender ['str']\n" },
        { "mutable-message", "message=[7]\ntry:assert False,message\nexcept AssertionError as error:\n message.append(8)\n print(str(error),repr(error),error.args[0] is message)", "[7, 8] AssertionError([7, 8]) True\n" },
        { "repeated-deliberate-rendering", "events=[]\nclass Message:\n def __str__(self):events.append('str');return str(len(events))\ntry:assert False,Message()\nexcept AssertionError as error:\n print(events)\n print(str(error),str(error),events)", "[]\n1 2 ['str', 'str']\n" },
        { "generator-preserves-message", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message()\ndef values():\n try:assert False,(yield 'message')\n except AssertionError as error:yield error.args[0] is message,events\ng=values()\nprint(next(g))\nprint(g.send(message))", "message\n(True, [])\n" },
        { "context-suppression-preserves-message", "events=[]\nclass Message:\n def __str__(self):events.append('str');return 'payload'\nmessage=Message()\nclass Manager:\n def __enter__(self):events.append('enter')\n def __exit__(self,kind,error,tb):events.append(error.args[0] is message);return True\nwith Manager():assert False,message\nprint(events)", "['enter', True]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task AssertionsPreserveEvaluationAndMessageIdentity(string name, string source, string expected)
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
