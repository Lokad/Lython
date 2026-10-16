using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedSliceExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "native", "items=[0,1,2,3];alias=items\nitems[1:3]+=[4]\nprint(items,alias is items)", "[0, 1, 2, 4, 3] True\n" },
        { "raw-key", "class Box:\n def __getitem__(self,key): self.key=key;print(key.start,key.stop,key.step);return [1]\n def __setitem__(self,key,value): print(key is self.key,value)\nBox()['a':'b':'c']+=[2]", "a b c\nTrue [1, 2]\n" },
        { "recoercion", "events=[]\nclass Index:\n def __init__(self,name,value):self.name=name;self.value=value\n def __index__(self):events.append(self.name);return self.value\na=Index('start',0);b=Index('stop',1);c=Index('step',1);items=[1,2,3]\ndef rhs():a.value=1;b.value=2;return [4]\nitems[a:b:c]+=rhs()\nprint(items,events)", "[1, 1, 4, 3] ['step', 'start', 'stop', 'step', 'start', 'stop']\n" },
        { "rhs-resize", "items=[0,1,2,3]\ndef rhs():items.clear();return [4]\nitems[1:3]+=rhs()\nprint(items)", "[1, 2, 4]\n" },
        { "extended-failure", "items=[0,1,2,3]\ntry:items[::2]+=[4]\nexcept ValueError:print(items)", "[0, 1, 2, 3]\n" },
        { "yield-headers", "items=[1,2,3]\ndef update():\n (yield 'receiver')[(yield 'start'):(yield 'stop'):(yield 'step')]+=(yield 'rhs')\n yield 'done'\ng=update();print(next(g),g.send(items),g.send(0),g.send(1),g.send(1),g.send([4]),items)", "receiver start stop step rhs done [1, 4, 2, 3]\n" },
        { "yield-store", "items=[1,2,3]\ndef update():\n (yield 'receiver')[(yield 'start'):(yield 'stop')]=[4]\n yield 'done'\ng=update();print(next(g),g.send(items),g.send(0),g.send(1),items)", "receiver start stop done [4, 2, 3]\n" },
        { "protected", "items=[0,1,2,3]\ntry:items[::2]+=[4]\nexcept ValueError:items[1:2]+=[5]\nprint(items)", "[0, 1, 5, 2, 3]\n" },
        { "locals", "def run(values,start):\n delta=[4]\n values[start:1]+=locals()['delta']\n return values\nprint(run([1,2,3],0))", "[1, 4, 2, 3]\n" },
        { "nonlocal", "def outer():\n values=[1,2,3]\n def run():\n  nonlocal values\n  values[:1]+=[4]\n run()\n return values\nprint(outer())", "[1, 4, 2, 3]\n" },
        { "immutable", "items=(1,2)\ntry:items[:1]+=(3,)\nexcept TypeError:print(items)", "(1, 2)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SliceUpdatesFollowPythonOrderAndAliases(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name);
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

    public static IEnumerable<object?[]> SuspensionCases()
    {
        foreach (var yielding in new[] { false, true })
        foreach (var path in new[] { null, "/receiver.txt", "/start.txt", "/stop.txt", "/step.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" })
            yield return [yielding, path];
    }

    [Theory]
    [MemberData(nameof(SuspensionCases))]
    public async Task UpdatePhasesSuspendAndCancelWithoutReplay(bool yielding, string? pause)
    {
        const string definitions = """
            def mark(path):
             with open(path,'a') as output: output.write('x')
            class Value:
             def __init__(self): self.items=['old']
             def __iadd__(self,other):
              mark('iadd.txt')
              assert other is delta
              self.items.extend(other)
              return self
            payload=Value()
            delta=['new']
            begin=[]
            end=[]
            stride=[]
            class Box:
             def __getitem__(self,key):
              mark('get.txt')
              assert key.start is begin and key.stop is end and key.step is stride
              self.key=key
              return payload
             def __setitem__(self,key,value):
              mark('set.txt')
              assert key is self.key and value is payload
              self.saved=value
            box=Box()
            def receiver():
             mark('receiver.txt')
             return box
            def start():
             mark('start.txt')
             return begin
            def stop():
             mark('stop.txt')
             return end
            def step():
             mark('step.txt')
             return stride
            def rhs():
             mark('rhs.txt')
             return delta
            """;
        var source = definitions + "\n" + (yielding
            ? "def update():\n (yield 'receiver')[(yield 'start'):(yield 'stop'):(yield 'step')]+=(yield 'rhs')\n yield 'updated'\ng=update()\nassert next(g)=='receiver'\nassert g.send(receiver())=='start'\nassert g.send(start())=='stop'\nassert g.send(stop())=='step'\nassert g.send(step())=='rhs'\nassert g.send(rhs())=='updated'\n"
            : "receiver()[start():stop():step()]+=rhs()\n") + "assert box.saved is payload\nprint(payload.items)";
        var paths = new[] { "/receiver.txt", "/start.txt", "/stop.txt", "/step.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in paths) host.SeedFile(path, "before");
        var entered = pause is null ? null : host.PauseWriteUntilCancellation(pause);
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
                    Assert.Fail(early.Failure?.Message ?? "Update finished before the controlled callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? "['old', 'new']\n" : "", result.StandardOutput);
            var completed = pause is null ? paths.Length : Array.IndexOf(paths, pause);
            for (var i = 0; i < paths.Length; i++) Assert.Equal("before" + (i < completed ? "x" : ""), host.ReadText(paths[i]));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatedAliasesRemainFundedAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("values=[]\nalias=values\nvalues[:]+=['x'*4096]\nassert values is alias\nreturn alias", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(retained.Success, retained.Failure?.Message);
            var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(retained.ReturnValue).Cast<object?>();
            Assert.Equal(new string('x', 4096), Assert.IsType<string>(Assert.Single(values)));
            var held = budget.ReservedBytes;
            using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
            Assert.True(held > baseline);
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("def run(obj):\n obj[5:6:0]+=[1]\nrun([1])", "ValueError", 2, 3)]
    [InlineData("def run(obj):\n obj[:]+=(2,)\nrun((1,))", "TypeError", 2, 3)]
    public async Task FailuresKeepTargetAndCallerSpans(string source, string type, int line, int callerLine)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal(type, failure.ExceptionType);
            Assert.Equal(line, failure.Span?.Line);
            Assert.Equal(2, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == callerLine);
        }
    }
}
