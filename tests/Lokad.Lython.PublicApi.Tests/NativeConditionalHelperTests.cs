using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeConditionalHelperTests
{
    // Independent isolated CPython outputs; both APIs share these semantics.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "guarded-fresh","def f(line:str)->list:\n if line.isspace():return []\n return line.lower().split()\na=f('  ')\nb=f('  ')\na.append('kept')\nprint(a,b,a is b)\nprint(f('AbC ΣΟΣ ß'))\n","['kept'] [] False\n['abc', 'σος', 'ß']\n" },
        { "nested","def f(text):\n if text.isspace():return None\n if text.isalpha():\n  if text.islower():return text.upper()\n  return text.lower()\n return text\nfor text in ['  ','abc','ABC','1!']:print(f(text))\n","None\nABC\nabc\n1!\n" },
        { "implicit-none","def f(text):\n if text.isalpha():return text.upper()\nprint(f('abc'),f('3'))\n","ABC None\n" },
        { "empty-sequences","def f(flag):\n if flag:return ()\n return []\na=f(False)\nb=f(False)\na.append(1)\nprint(f(True),a,b,a is b)\n","() [1] [] False\n" },
        { "unselected-read","def f(flag,mapping):\n if flag:return 'kept'\n return mapping['absent']\nprint(f(True,{}))\ntry:f(False,{})\nexcept KeyError:print('missing')\n","kept\nmissing\n" },
        { "unselected-format","def f(flag,mapping):\n if flag:return 'kept'\n return f\"{mapping['absent']}\"\nprint(f(True,{}))\ntry:f(False,{})\nexcept KeyError:print('missing')\n","kept\nmissing\n" },
        { "changed-builtin","length=len\ndef f(flag,word):\n if flag:return word.upper()\n return length(word)\nprint(f(True,'ab'),f(False,'abc'))\nlength=lambda text:'changed'\nprint(f(False,'abc'))\ndel length\nprint(f(True,'kept'))\n","AB 3\nchanged\nKEPT\n" },
        { "custom-truth","events=[]\nclass Flag:\n def __bool__(self):events.append('truth');return False\ndef f(flag):\n if flag:return 'yes'\n return 'no'\nprint(f(Flag()),events)\n","no ['truth']\n" },
        { "joins-and-loops","def f(flag,word):\n if flag:word=word.upper()\n else:word=word.lower()\n return word\nprint(f(True,'Ab'),f(False,'Ab'))\ndef g(word):\n while word.islower():return word.upper()\n return word\nprint(g('ab'),g('AB'))\n","AB ab\nAB AB\n" },
        { "branch-forwarding","def f(flag,word):\n if flag:\n  local=word.lower()\n  return local.split()\n local=word.upper()\n return local.split()\nprint(f(True,'A B'),f(False,'a b'))\n","['a', 'b'] ['A', 'B']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task BranchHelpersPreserveSelectedWorkAndFreshResults(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualTruthSuspensionKeepsFundingUntilCancellationOrRefusalFinishes(bool refuse)
    {
        const string source = """
            class Flag:
                def __bool__(self):
                    with open('/cleanup','w') as target:
                        target.write('before')
                        target.flush()
                    with open('/value') as reader:
                        reader.read()
                    try:
                        if fail: storage='x'*4194304
                    finally:
                        return True
            def choose(flag):
                if flag:return []
                return []
            choose(Flag())
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        host.SeedFile("/value", "1");
        var started = refuse ? null : host.PauseReadUntilCancellation("/value");
        var run = script.RunAsync(host, new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            Globals = new Dictionary<string, object?> { ["fail"] = refuse }
        });
        try
        {
            if (started is not null)
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(run.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(result.Success);
            Assert.Equal(refuse ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.Cancelled,
                result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal("before", host.ReadText("/cleanup"));
        Assert.Equal(baseline, budget.ReservedBytes);
        var fresh = new DelayedLythonHost();
        fresh.SeedFile("/value", "1");
        using (var result = await script.RunAsync(fresh, new LythonRunOptions
            { Globals = new Dictionary<string, object?> { ["fail"] = false } }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.True(fresh.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
