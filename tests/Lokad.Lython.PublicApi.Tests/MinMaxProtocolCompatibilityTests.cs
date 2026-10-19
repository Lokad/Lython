using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MinMaxProtocolCompatibilityTests
{
    public static IEnumerable<object[]> Programs()
    {
        var cases = new (string Name, string Source, string Expected)[]
        {
            ("guest-ordering", "class Key:\n def __init__(self,n):self.n=n\n def __lt__(self,other):return self.n<other.n\n def __gt__(self,other):return self.n>other.n\na=Key(2)\nb=Key(1)\nprint(min([a,b]).n,max(a,b).n,min(a,b).n,max([a,b]).n)", "1 2 1 2\n"),
            ("set-partial-order", "print(min([{1,2},{1}]),max({1},{1,2}))\na={1}\nb={2}\nprint(min([a,b]) is a,max(a,b) is a)", "{1} {1, 2}\nTrue True\n"),
            ("strict-first-tie", "class Key:\n def __lt__(self,other):return False\n def __gt__(self,other):return False\na=Key()\nb=Key()\nprint(min([a,b]) is a,max(a,b) is a)", "True True\n"),
            ("reflected-subclass-direction", "events=[]\nclass A:\n def __lt__(self,other):events.append('A<');return NotImplemented\n def __gt__(self,other):events.append('A>');return NotImplemented\nclass B(A):\n def __lt__(self,other):events.append('B<');return True\n def __gt__(self,other):events.append('B>');return True\na=A()\nb=B()\nprint(min([b,a]) is a,max(b,a) is a,events)", "True True ['B>', 'B<']\n"),
            ("key-once-in-order", "events=[]\ndef key(v):\n events.append(v)\n return {1:2,2:0,3:1}[v]\nprint(min([1,2,3],key=key),events)\nevents.clear()\nprint(max(1,2,3,key=key),events)", "2 [1, 2, 3]\n1 [1, 2, 3]\n"),
            ("empty-default-invalid-key", "marker=[]\nprint(min([],key=1,default=marker) is marker,max([],key=1,default=marker) is marker)\nfor consume in [min,max]:\n try:consume([1],key=1)\n except TypeError:print('key')\n try:consume([])\n except ValueError:print('empty')", "True True\nkey\nempty\nkey\nempty\n"),
            ("retained-iterator-after-error", "for consume in [min,max]:\n values=iter([2,'bad',1])\n try:consume(values)\n except TypeError:print(next(values))", "1\n1\n"),
            ("numeric-nan-and-alias-controls", "import math\nnan=float('nan')\nprint(min([2.0,nan,1.0]),math.isnan(max([nan,1.0])))\nfirst=1\nprint(min(range(3)),max(range(3)),min([first,True]) is first,max(first,True) is first)\nwide=1<<256\nprint(min([float('inf'),wide]) is wide,math.isinf(max(wide,float('inf'))))", "1.0 True\n0 2 True True\nTrue True\n"),
            ("rich-result-truthiness", "events=[]\nclass Truth:\n def __init__(self,value):self.value=value\n def __bool__(self):events.append('truth');return self.value\nclass Key:\n def __init__(self,n):self.n=n\n def __lt__(self,other):events.append('lt');return Truth(self.n<other.n)\n def __gt__(self,other):events.append('gt');return Truth(self.n>other.n)\na=Key(2)\nb=Key(1)\nprint(min([a,b]) is b,max(a,b) is a,events)", "True True ['lt', 'truth', 'gt', 'truth']\n"),
            ("wrapped-comparator", "from functools import cmp_to_key\ndef compare(a,b):return (a>b)-(a<b)\nprint(min([2,1],key=cmp_to_key(compare)),max(2,1,key=cmp_to_key(compare)))", "1 2\n"),
        };
        foreach (var item in cases)
        foreach (var asynchronous in new[] { false, true })
            yield return [item.Name, asynchronous, item.Source, item.Expected];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ExtremaPreservePythonOrderingAndEffects(string name, bool asynchronous, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> HostCases()
    {
        foreach (var operation in new[] { "min", "max" })
        foreach (var stage in new[] { "iter", "compare", "truth", "cmpkey" })
        foreach (var multiple in stage == "iter" ? new[] { false } : new[] { false, true })
        foreach (var cancel in new[] { false, true })
            yield return [operation, stage, multiple, cancel];
    }

    private static string HostSource(string operation, string stage, bool multiple)
    {
        const string touch = "def touch():\n with open('events.txt','a') as target:target.write('seen')\n";
        if (stage == "iter")
            return touch + "class Values:\n def __iter__(self):\n  touch()\n  return iter([2,1])\nprint(" + operation + "(Values()))";
        if (stage == "cmpkey")
            return touch + "from functools import cmp_to_key\ndef compare(a,b):\n touch()\n return (a>b)-(a<b)\nprint(" + operation +
                (multiple ? "(2,1,key=cmp_to_key(compare)))" : "([2,1],key=cmp_to_key(compare)))");
        var truth = stage == "truth"
            ? "class Truth:\n def __init__(self,value):self.value=value\n def __bool__(self):\n  touch()\n  return self.value\n"
            : "";
        var less = stage == "truth" ? "return Truth(self.value<other.value)" : "touch();return self.value<other.value";
        var greater = stage == "truth" ? "return Truth(self.value>other.value)" : "touch();return self.value>other.value";
        return touch + truth + "class Key:\n def __init__(self,value):self.value=value\n def __lt__(self,other):" + less +
            "\n def __gt__(self,other):" + greater + "\na=Key(2)\nb=Key(1)\nprint(" + operation +
            (multiple ? "(a,b).value)" : "([a,b]).value)");
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task GuestProtocolsAwaitIOAndCancellation(string operation, string stage, bool multiple, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(HostSource(operation, stage, multiple), budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/events.txt", "before");
        using var cancellation = new CancellationTokenSource();
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "The guest protocol did not reach the pending write.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : operation == "min" ? "1\n" : "2\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeseen", host.ReadText("/events.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task RefusedProtocolAllocationRemainsTerminal(string operation, string stage, bool multiple, bool asynchronous)
    {
        var body = HostSource(operation, stage, multiple).Replace(
            "with open('events.txt','a') as target:target.write('seen')", "print('entered');payload='x'*2000000");
        var source = "try:\n" + string.Join("\n", body.Split('\n').Select(line => " " + line)) +
            "\nexcept MemoryError:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(3, result.Failure?.Span?.Line);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
