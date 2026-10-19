using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OrdinaryUnpackingTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def run():\n x=0\n def read(): return x\n x,y=[7,8]\n return read(),y\nprint(run())", "(7, 8)\n" },
        { "def run():\n a,b=[1,2]\n return sorted(locals().items())\nprint(run())", "[('a', 1), ('b', 2)]\n" },
        { "box=[0,0]\nindex,box[index]=[1,9]\nprint(index,box)", "1 [0, 9]\n" },
        { "class Box: pass\nbox=Box()\na,*box.rest,c=[1,2,3,4]\nprint(a,box.rest,c)", "1 [2, 3] 4\n" },
        { "events=[]\npayload=[[],[]]\ndef rhs():\n events.append('rhs')\n return payload\na,b=both=rhs()\nprint(events,both is payload,a is payload[0],b is payload[1])", "['rhs'] True True True\n" },
        { "x=0\ndef run():\n global x\n x,y=[7,8]\n return y\nprint(run(),x)", "8 7\n" },
        { "def outer():\n x=0\n def inner():\n  nonlocal x\n  x,y=[7,8]\n  return y\n return inner(),x\nprint(outer())", "(8, 7)\n" },
        { "class Box: pass\nbox=Box()\n(chosen:=box).value,other=[1,2]\nprint(chosen is box,box.value,other)", "True 1 2\n" },
        { "class Box:\n a,b=[1,2]\nprint(Box.a,Box.b)", "1 2\n" },
        { "a,(b,*c),d=[1,[2,3,4],5]\nprint(a,b,c,d)", "1 2 [3, 4] 5\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task UnpackingUsesCurrentBindingsAndPreservesPythonEffects(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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

    [Theory]
    [InlineData("iter(range(1))", "ValueError")]
    [InlineData("1", "TypeError")]
    public async Task NestedFailureKeepsTargetSpanAndCallerFrame(string value, string errorType)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("def run(value):\n first,(left,right)=[9,value]\nrun(" + value + ")");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal(errorType, failure.ExceptionType);
            Assert.Equal(2, failure.Span?.Line);
            Assert.Equal(8, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == 3);
        }
    }

    [Fact]
    public async Task EscapingStarredValuesStayFundedAcrossExecutions()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("head,*tail,last=[0,['x'*4096],['y'*4096],1]\nreturn tail", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>().ToArray();
                Assert.Equal(2, rows.Length);
                for (var i = 0; i < rows.Length; i++)
                {
                    var cells = Assert.IsAssignableFrom<System.Collections.IEnumerable>(rows[i]).Cast<object?>();
                    Assert.Equal(new string(i == 0 ? 'x' : 'y', 4096), Assert.IsType<string>(Assert.Single(cells)));
                }
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.True(held > baseline);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/rhs.txt")]
    [InlineData("/receiver.txt")]
    [InlineData("/set.txt")]
    public async Task UnpackedValuesSurviveSuspensionAndCancelWithoutReplay(string? pausedPath)
    {
        const string source = "def mark(path,label):\n with open(path,'a') as output: output.write(label)\nclass Box:\n @property\n def value(self): return self.saved\n @value.setter\n def value(self,value):\n  mark('set.txt','S')\n  self.saved=value\nbox=Box()\npayload=['value']\ndef rhs():\n mark('rhs.txt','R')\n return [payload,2,3]\ndef receiver():\n mark('receiver.txt','C')\n return box\nreceiver().value,*rest=rhs()\nprint(box.saved is payload,rest)";
        var paths = new[] { "/rhs.txt", "/receiver.txt", "/set.txt" };
        var labels = new[] { "R", "C", "S" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in paths) host.SeedFile(path, "before");
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
                    Assert.Fail(early.Failure?.Message ?? "Unpacking completed before its controlled operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "True [2, 3]\n" : "", result.StandardOutput);
            var completed = pausedPath is null ? paths.Length : Array.IndexOf(paths, pausedPath);
            for (var i = 0; i < paths.Length; i++)
                Assert.Equal("before" + (i < completed ? labels[i] : ""), host.ReadText(paths[i]));
            if (entered is null) Assert.True(host.CompletedAsynchronously > 0);
            else Assert.True(entered.IsCompletedSuccessfully);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
