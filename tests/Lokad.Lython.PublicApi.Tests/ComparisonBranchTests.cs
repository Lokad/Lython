using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonBranchTests
{
    [Theory]
    [InlineData(null), InlineData("/compare"), InlineData("/truth")]
    public async Task ComparisonAndTruthKeepRealSuspensionAndCleanup(string? cancelAt)
    {
        const string source = """
            events=[]
            class Verdict:
                def __bool__(self):
                    with open('/truth') as source:
                        events.append(source.read())
                    return True
            class Left:
                def __lt__(self,other):
                    with open('/compare') as source:
                        events.append(source.read())
                    return Verdict()
            if Left()<0:events.append('branch')
            print(events)
            print('done')
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/compare", "comparison");
            host.SeedFile("/truth", "truth");
            using var cancellation = new CancellationTokenSource();
            var reached = cancelAt is null ? null : host.PauseReadUntilCancellation(cancelAt);
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            if (reached is not null)
            {
                await reached.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                if (cancelAt is null)
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("['comparison', 'truth', 'branch']\ndone\n", result.StandardOutput);
                }
                else
                {
                    Assert.False(result.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                    Assert.DoesNotContain("done", result.StandardOutput);
                }
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(cancelAt == "/compare" ? 1 : 2, host.ClosedFileCount);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    // Independently captured with isolated CPython 3.13.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "native-truth-and-escaped-values", "pairs=[(0,0),(True,1),(2**100,float(2**100)),(2**100+1,float(2**100)),(float('nan'),0),(float('inf'),10**400),(-float('inf'),-10**400)]\nfor a,b in pairs:\n print(1 if a<b else 0,1 if a<=b else 0,1 if a>b else 0,1 if a>=b else 0,1 if a==b else 0,1 if a!=b else 0)\n print(a<b,a<=b,a>b,a>=b,a==b,a!=b)\ncount=0\nwhile count<3:count+=1\nprint(count,[x for x in range(5) if x>=2])\n", "0 1 0 1 1 0\nFalse True False True True False\n0 1 0 1 1 0\nFalse True False True True False\n0 1 0 1 1 0\nFalse True False True True False\n0 0 1 1 0 1\nFalse False True True False True\n0 0 0 0 0 1\nFalse False False False False True\n0 0 1 1 0 1\nFalse False True True False True\n1 1 0 0 0 1\nTrue True False False False True\n3 [2, 3, 4]\n" },
        { "raw-results-reflection-and-order", "events=[]\nclass Verdict:\n def __bool__(self):\n  events.append('truth')\n  return True\nverdict=Verdict()\nclass Left:\n def __lt__(self,other):\n  events.append('left')\n  return verdict\n def __eq__(self,other):\n  events.append('eq')\n  return verdict\nclass Right(Left):\n def __gt__(self,other):\n  events.append('reflected')\n  return NotImplemented\nleft=Left()\nright=Right()\nstored=left<right\nprint(stored is verdict,events)\nevents.clear()\nif left<right:events.append('branch')\nprint(events)\nevents.clear()\nif left==right:events.append('equal branch')\nprint(events)\ndef value(label,result):\n events.append(label)\n return result\nevents.clear()\nprint(value('first',3)<value('middle',2)<value('last',4),events)\nevents.clear()\nif value('first',1)<value('middle',2)<value('last',4):events.append('chain branch')\nprint(events)\n", "True ['reflected', 'left']\n['reflected', 'left', 'truth', 'branch']\n['eq', 'truth', 'equal branch']\nFalse ['first', 'middle']\n['first', 'middle', 'last', 'chain branch']\n" },
        { "protected-failures-and-loops", "events=[]\nclass Verdict:\n def __bool__(self):\n  raise ValueError('truth failure')\nclass Left:\n def __lt__(self,other):return Verdict()\ntry:\n if Left()<0:events.append('wrong')\nexcept ValueError as error:\n events.append(str(error))\nfinally:\n events.append('cleanup')\ndef check(value):\n try:\n  if value<0:return 'negative'\n  return 'nonnegative'\n finally:\n  events.append('function cleanup')\nprint(check(-1),check(0))\nfor index in range(4):\n if index<2:continue\n events.append(index)\nprint(events)\n", "negative nonnegative\n['truth failure', 'cleanup', 'function cleanup', 'function cleanup', 2, 3]\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ComparisonConditionsPreservePythonResults(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
