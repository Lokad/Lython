using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SetOrderingProtocolTests
{
    private const string Keys = "events=[]\nclass Key:\n def __init__(self,n,label):self.n=n;self.label=label;self.h=n\n def __hash__(self):events.append('hash');return self.h\n def __eq__(self,other):events.append(self.label+'/'+other.label);return self.n==other.n\n";

    public static IEnumerable<object[]> Programs()
    {
        var cases = new (string Name, string Source, string Expected)[]
        {
            ("stored-hash-and-equality-direction", Keys + "left={Key(0,'L')}\nright={Key(0,'R'),Key(1,'extra')}\nevents.clear()\nprint(left<right,left<=right,right>left,right>=left)\nprint(events)", "True True True True\n['R/L', 'R/L', 'R/L', 'R/L']\n"),
            ("changed-hash-is-not-reinvoked", Keys + "key=Key(0,'L')\nleft={key}\nright={Key(0,'R'),Key(1,'extra')}\nkey.h=99\nevents.clear()\nprint(left<right,min([right,left]) is left,max([left,right]) is right)\nprint(events)", "True True True\n['R/L', 'R/L', 'R/L']\n"),
            ("count-and-identity-short-circuits", Keys + "left={Key(0,'L')}\nevents.clear()\nprint(left<=left,left<left,left>left,left<=set(),set()>=left)\nprint(events)", "True False False False False\n[]\n"),
            ("builtin-and-protocol-mixed", Keys + "left={2,Key(0,'L')}\nright={2,Key(0,'R'),3}\nevents.clear()\nprint(left<right,right>=left,left>{8})\nprint(events)", "True True False\n['R/L', 'R/L']\n"),
            ("tuple-and-collision-keys", "class Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return 0\n def __eq__(self,other):return self.n==other.n\nprint({(Key(1),)} < {(Key(1),),(Key(2),)}, {Key(1)} < {Key(2),Key(3)})", "True False\n"),
            ("mutated-dataclass-hash", "from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Key:\n n:int\na=Key(0)\nb=Key(0)\nleft={a}\nright={b,Key(1)}\na.n=9\nprint(left<right,left<=left)\nb.n=9\nprint(left<right)\nprint({(a,)} < {(b,),(Key(1),)})", "False True\nTrue\nTrue\n"),
            ("source-clear-keeps-python-shaped-error", "class Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):left.clear();return True\nleft={Key(0)}\nright={Key(0),Key(1)}\ntry:print(left<right)\nexcept RuntimeError:print('mutation')\nprint(len(left),len(right))", "mutation\n0 2\n"),
            ("candidate-clear-safe-snapshot", "class Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):right.clear();return False\nleft={Key(0)}\nright={Key(0),Key(1)}\nprint(left<right,len(left),len(right))", "False 1 0\n"),
            ("truth-error-releases-scratch", "class Truth:\n def __bool__(self):raise ValueError('bad truth')\nclass Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):return Truth()\nleft={Key(0)}\nright={Key(0),Key(1)}\ntry:print(left<right)\nexcept ValueError as error:print(str(error))\nprint(len(left),len(right))", "bad truth\n1 2\n"),
        };
        foreach (var item in cases)
        foreach (var asynchronous in new[] { false, true })
            yield return [item.Name, asynchronous, item.Source, item.Expected];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task RelationsReuseStoredHashesAndPythonEquality(string name, bool asynchronous, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
        foreach (var expression in new[] { "left<right", "left<=right", "right>left", "right>=left", "min([right,left]) is left", "max([left,right]) is right" })
        foreach (var truth in new[] { false, true })
        foreach (var cancel in new[] { false, true })
            yield return [expression, truth, cancel];
    }

    private static string HostSource(string expression, bool truth)
    {
        var boolean = truth ? "class Truth:\n def __bool__(self):\n  touch()\n  return True\n" : "";
        var equality = truth ? "return Truth()" : "touch();return self.n==other.n";
        return "def touch():\n with open('events.txt','a') as target:target.write('seen')\n" + boolean +
            "class Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):" + equality +
            "\nleft={Key(0)}\nright={Key(0),Key(1)}\nprint(" + expression + ")";
    }

    [Theory]
    [MemberData(nameof(HostCases))]
    public async Task EqualityAndTruthConversionAwaitHostIO(string expression, bool truth, bool cancel)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(HostSource(expression, truth), budget);
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
                    Assert.Fail(early.Failure?.Message ?? "Equality did not reach the pending write.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "True\n", result.StandardOutput);
            Assert.Equal(cancel ? "before" : "beforeseen", host.ReadText("/events.txt"));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
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
    public async Task RefusedEqualityAllocationRemainsTerminal(string expression, bool truth, bool asynchronous)
    {
        var body = HostSource(expression, truth).Replace(
            "with open('events.txt','a') as target:target.write('seen')", "print('entered');payload='x'*2000000");
        var source = "try:\n" + string.Join("\n", body.Split('\n').Select(line => " " + line)) +
            "\nexcept MemoryError:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceMutationIsDetectedAfterAwaitedEquality(bool truth)
    {
        var source = HostSource("left<right", truth).Replace(
            "with open('events.txt','a') as target:target.write('seen')",
            "left.clear();right.clear()\n with open('events.txt','a') as target:target.write('seen')");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost(); host.SeedFile("/events.txt", "before");
        using (var result = await script.RunAsync(host))
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Equal("Set changed size during iteration", result.Failure?.Message);
            Assert.Equal("beforeseen", host.ReadText("/events.txt"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
