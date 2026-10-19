using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ComparisonValueTests
{
    [Fact]
    public async Task EscapingComparisonAliasRemainsFundedAfterItsOriginalBindingChanges()
    {
        const string source = "value='x'*4096\nclass N:\n def __gt__(self,other):return value\nanswer=0<1<N()\nvalue=None\nreturn answer";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new string('x', 4096), Assert.IsType<string>(retained.ReturnValue));
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "/compare.txt")]
    [InlineData(false, "/truth.txt")]
    [InlineData(true, null)]
    [InlineData(true, "/compare.txt")]
    public async Task ComparisonAndIntermediateTruthCanSuspendAndCancel(bool final, string? pausedPath)
    {
        const string definitions = "class Answer:\n def __bool__(self):\n  with open('truth.txt','a') as output:output.write('truth')\n  return True\nclass N:\n def __lt__(self,other):\n  with open('compare.txt','a') as output:output.write('compare')\n  return Answer()\n __gt__=__lt__\n";
        var source = definitions + (final ? "print(type(0<1<N()).__name__)" : "print(N()<1<2)");
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/compare.txt", "before");
        host.SeedFile("/truth.txt", "before");
        var entered = pausedPath is null ? null : host.PauseWriteUntilCancellation(pausedPath);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Comparison completed without reaching the paused host call.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pausedPath is null, result.Success);
            Assert.Equal(pausedPath is null ? final ? "Answer\n" : "True\n" : "", result.StandardOutput);
            Assert.Equal(pausedPath == "/compare.txt" ? "before" : "beforecompare", host.ReadText("/compare.txt"));
            Assert.Equal(pausedPath is null && !final ? "beforetruth" : "before", host.ReadText("/truth.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    // Expected output checked independently with isolated CPython 3.13.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "unary_bool", "import operator\nfor value in (True,False):\n for answer in (+value,operator.pos(value)):\n  print(answer,type(answer).__name__)", "1 int\n1 int\n0 int\n0 int\n" },
        { "last_list", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nprint(0<1<N())", "[1]\n" },
        { "first_empty", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[]\ndef tail():\n print(\"unexpected\")\n return 2\nprint(N()<1<tail())", "[]\n" },
        { "first_none", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=None\nprint(N()<1<2)", "None\n" },
        { "first_dict", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult={}\nprint(N()<1<2)", "{}\n" },
        { "first_identity", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[]\nanswer=N()<1<2\nprint(answer is result)", "True\n" },
        { "last_identity", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nanswer=0<1<N()\nprint(answer is result)", "True\n" },
        { "last_no_bool", "class Result:\n def __bool__(self):raise ValueError(\"unexpected truth\")\nclass N:\n def __gt__(self,other):return Result()\nprint(type(0<1<N()).__name__)", "Result\n" },
        { "first_truth", "events=[]\nclass Result:\n def __bool__(self):\n  events.append(\"truth\")\n  return True\nclass N:\n def __lt__(self,other):\n  events.append(\"compare\")\n  return Result()\ndef tail():\n events.append(\"tail\")\n return 2\nprint(N()<1<tail(),events)", "True ['compare', 'truth', 'tail']\n" },
        { "first_false", "events=[]\nclass Result:\n def __bool__(self):\n  events.append(\"truth\")\n  return False\nclass N:\n def __lt__(self,other):return Result()\nanswer=N()<1<2\nprint(type(answer).__name__,events)", "Result ['truth']\n" },
        { "middle_once", "events=[]\ndef item(value):\n events.append(value)\n return value\nprint(item(0)<item(1)<item(2),events)", "True [0, 1, 2]\n" },
        { "nested", "print(0<(0<1<2)<3, 0<1<2<3,3<2<1<0)", "True True False\n" },
        { "helper", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\ndef compare():return 0<1<N()\nprint(compare())", "[1]\n" },
        { "lambda", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\ncompare=lambda:0<1<N()\nprint(compare())", "[1]\n" },
        { "comprehension", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nprint([0<1<N() for i in range(2)])", "[[1], [1]]\n" },
        { "generator", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\ndef values():\n yield 0<1<N()\nprint(list(values()))", "[[1]]\n" },
        { "append_result", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nanswer=0<1<N()\nanswer.append(2)\nprint(answer)", "[1, 2]\n" },
        { "index_result", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nanswer=N()<1\nprint(answer[0])", "1\n" },
        { "mutate_operand", "events=[]\nleft=0\ndef middle():\n global left\n left=100\n return 1\nprint(left<middle()<2,left)", "True 100\n" },
        { "truth_error", "class Result:\n def __bool__(self):return 1\nclass N:\n def __lt__(self,other):return Result()\ntry:print(N()<1<2)\nexcept TypeError:print(\"TypeError\")", "TypeError\n" },
        { "membership", "class N:\n def __contains__(self,other):return [1]\nanswer=1 in N()\nprint(answer,type(answer).__name__,0<1 in N())", "True bool True\n" },
        { "raw_single", "class N:\n def __lt__(self,other):return result\n def __gt__(self,other):return result\nresult=[1]\nprint(N()<1,1<N())", "[1] [1]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ComparisonsPreservePythonValues(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
}
