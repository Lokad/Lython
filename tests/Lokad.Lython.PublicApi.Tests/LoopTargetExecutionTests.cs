using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LoopTargetExecutionTests
{
    public static TheoryData<string, string> Programs => new()
    {
        { "def generate():\n result=[x+y for x,y in (yield 'rows')]\n yield result\ng=generate()\nprint(next(g),g.send([(1,2)]))", "rows [3]\n" },
        { "x=0\ndef run():\n global x\n for x,y in [(1,2),(3,4)]: pass\n return y\nprint(run(),x)", "4 3\n" },
        { "def outer():\n x=0\n def run():\n  nonlocal x\n  for x,y in [(1,2),(3,4)]: pass\n  return y\n return run(),x\nprint(outer())", "(4, 3)\n" },
        { "events=[]\nfor x,y in [(1,2),(3,4)]:\n try:\n  if x==1: continue\n  break\n finally: events.append(x)\nprint(events,x,y)", "[1, 3] 3 4\n" },
        { "box=[0,0]\nfor index,box[index] in [(1,9)]: pass\nprint(index,box)", "1 [0, 9]\n" },
        { "class Box: pass\nbox=Box()\nfor a,*box.rest in [[1,2,3]]: pass\nprint(a,box.rest)", "1 [2, 3]\n" },
        { "x='global'\ndef run():\n global x\n return [x for x,y in [(1,2)]]\nprint(run(),x)", "[1] global\n" },
        { "def outer():\n x='enclosing'\n def run():\n  nonlocal x\n  return [x for x,y in [(1,2)]]\n return run(),x\nprint(outer())", "([1], 'enclosing')\n" },
        { "functions=[lambda:x for x,y in [(1,2),(3,4)]]\nprint(functions[0](),functions[1]())", "3 3\n" },
        { "last=0\nvalues=[(last:=x+y) for x,y in [(1,2),(3,4)]]\nprint(values,last)", "[3, 7] 7\n" },
        { "def run():\n x='caller'\n result=[locals()['x'] for x,y in [(1,2)]]\n return result,x\nprint(run())", "([1], 'caller')\n" },
        { "x='module'\nclass C:\n x='class'\n values=[x for x,y in [(1,2)]]\nprint(C.x,C.values,x)", "class [1] module\n" },
        { "x='outer'\nresult=[(x,[x for x,y in [(10,20)]],x) for x,y in [(1,2),(3,4)]]\nprint(result,x)", "[(1, [10], 1), (3, [10], 3)] outer\n" },
        { "def run():\n functions=[]\n for x,y in [(1,2),(3,4)]:\n  def read(): return x\n  functions.append(read)\n return [f() for f in functions]\nprint(run())", "[3, 3]\n" },
        { "values=iter(range(1))\nx=y='old'\ntry:\n for x,(y,z) in [(9,values)]: pass\nexcept ValueError: print(x,y,next(values,'end'))", "9 old end\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LoopTargetsPreserveScopedPythonBindings(string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
    [InlineData("def run():\n return [None for (yield 'target').value in [1]]")]
    [InlineData("def run():\n return {None for (yield 'target').value in [1]}")]
    [InlineData("def run():\n return {0:None for (yield 'target').value in [1]}")]
    public void YieldInsideComprehensionTargetRemainsRejected(string source)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 42)]
    public async Task NestedTargetErrorsKeepTheirSpanAndCaller(bool comprehension, int column)
    {
        var source = comprehension ? "def run(values):\n return [first for first,(left,right) in values]\nrun([(9,iter(range(1)))])"
            : "def run(values):\n for first,(left,right) in values: pass\nrun([(9,iter(range(1)))])";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal("ValueError", failure.ExceptionType);
            Assert.Equal(2, failure.Span?.Line);
            Assert.Equal(column, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == 3);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapingRestListsRemainFundedAfterTheLoopScopeEnds(bool comprehension)
    {
        var source = "pairs=[(0,['x'*4096])]\n" + (comprehension
            ? "result=[rest for head,*rest in pairs]\n"
            : "result=[]\nfor head,*rest in pairs: result.append(rest)\n") +
            "assert result[0][0] is pairs[0][1]\nreturn result";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var rows = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>();
                var rest = Assert.IsAssignableFrom<System.Collections.IEnumerable>(Assert.Single(rows)).Cast<object?>();
                var payload = Assert.IsAssignableFrom<System.Collections.IEnumerable>(Assert.Single(rest)).Cast<object?>();
                Assert.Equal(new string('x', 4096), Assert.IsType<string>(Assert.Single(payload)));
                var held = budget.ReservedBytes;
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.True(held > baseline);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "/rows.txt")]
    [InlineData(false, "/receiver.txt")]
    [InlineData(false, "/bind.txt")]
    [InlineData(false, "/set.txt")]
    [InlineData(true, null)]
    [InlineData(true, "/rows.txt")]
    [InlineData(true, "/receiver.txt")]
    [InlineData(true, "/bind.txt")]
    [InlineData(true, "/set.txt")]
    public async Task TargetCallbacksSuspendAndCancelWithoutReplay(bool comprehension, string? pausedPath)
    {
        const string definitions = "def mark(path,label):\n with open(path,'a') as output: output.write(label)\nclass Box:\n @property\n def __setitem__(self):\n  mark('bind.txt','B')\n  def assign(key,value):\n   mark('set.txt','S')\n   self.saved=value\n  return assign\nbox=Box()\npayload=['value']\ndef rows():\n mark('rows.txt','R')\n return [(payload,2,3)]\ndef receiver():\n mark('receiver.txt','C')\n return box\n";
        var source = definitions + (comprehension
            ? "result=[(box.saved is payload,rest) for receiver()[0],*rest in rows()]\nprint(result)"
            : "for receiver()[0],*rest in rows():\n print(box.saved is payload,rest)");
        var paths = new[] { "/rows.txt", "/receiver.txt", "/bind.txt", "/set.txt" };
        var labels = new[] { "R", "C", "B", "S" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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
                    Assert.Fail(early.Failure?.Message ?? "Target callbacks finished before the controlled operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            var expected = pausedPath is not null ? "" : comprehension ? "[(True, [2, 3])]\n" : "True [2, 3]\n";
            Assert.Equal(expected, result.StandardOutput);
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
