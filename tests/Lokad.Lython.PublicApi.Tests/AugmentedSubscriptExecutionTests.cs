using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedSubscriptExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "counts", "counts={'a':0,'b':0}\nfor word in ['a','b','a']: counts[word]+=1\nprint(counts)", "{'a': 2, 'b': 1}\n" },
        { "locals", "def run(values,key):\n delta=3\n values[key]+=locals()['delta']\n return values\nprint(run([1,2],1))", "[1, 5]\n" },
        { "captured-index", "index=[0]\nvalues=[[1],[2]]\ndef rhs():\n index[0]=1\n return [3]\nvalues[index[0]]+=rhs()\nprint(values,index)", "[[1, 3], [2]] [1]\n" },
        { "index-recoercion", "events=[]\nclass Key:\n def __index__(self): events.append(self.value);return self.value\nk=Key();k.value=0\nvalues=[1,10]\ndef rhs(): k.value=1;return 2\nvalues[k]+=rhs()\nprint(values,events)", "[1, 3] [0, 1]\n" },
        { "inplace-alias", "values=[[1]]\nalias=values[0]\nvalues[0]+=[2]\nprint(alias,alias is values[0])", "[1, 2] True\n" },
        { "raw-key", "class Key:\n def __index__(self): raise AssertionError('converted')\nk=Key();values={k:1};values[k]+=2\nprint(values[k])", "3\n" },
        { "raw-slice-key", "class Box:\n def __getitem__(self,key): self.key=key;return [1]\n def __setitem__(self,key,value): print(key is self.key,value)\nk=slice('a','b','c');Box()[k]+=[2]", "True [1, 2]\n" },
        { "missing-before-rhs", "def rhs(): raise AssertionError('rhs ran')\ndef run(items,key): items[key]+=rhs()\ntry: run({},'missing')\nexcept KeyError: print('missing')", "missing\n" },
        { "tuple-inplace", "value=[];items=(value,)\ntry: items[0]+=[2]\nexcept TypeError: print(value,items[0] is value)", "[2] True\n" },
        { "setter-replacement", "events=[]\nclass Box:\n def __getitem__(self,key): events.append('get');return 1\n def __setitem__(self,key,value): events.append('old')\ndef setter(self,key,value): events.append(('new',value))\ndef rhs(): Box.__setitem__=setter;return 2\nBox()[0]+=rhs()\nprint(events)", "['get', ('new', 3)]\n" },
        { "nonlocal", "def outer():\n values=[1]\n def run():\n  nonlocal values\n  values[0]+=2\n run()\n return values\nprint(outer())", "[3]\n" },
        { "class-scope", "values=[1]\nclass Runner: values[0]+=2\nprint(values)", "[3]\n" },
        { "yield-rhs", "values=[1]\ndef update():\n values[0]+=(yield 'rhs')\n yield values[0]\ng=update();print(next(g),g.send(2),values)", "rhs 3 [3]\n" },
        { "yield-header", "values=[1]\ndef update():\n (yield 'receiver')[(yield 'key')]+=(yield 'rhs')\n yield 'done'\ng=update();print(next(g),g.send(values),g.send(0),g.send(2),values)", "receiver key rhs done [3]\n" },
        { "yield-store", "values=[1]\ndef update():\n (yield 'receiver')[(yield 'key')]=2\n yield 'done'\ng=update();print(next(g),g.send(values),g.send(0),values)", "receiver key done [2]\n" },
        { "protected-error", "values=[1]\ntry: values[5]+=2\nexcept IndexError: values[0]+=3\nprint(values)", "[4]\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task SubscriptUpdatesFollowPythonOrderAndAliases(string name, string source, string expected)
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
        foreach (var path in new[] { null, "/receiver.txt", "/key.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" })
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
            token=[]
            class Box:
             def __getitem__(self,key):
              mark('get.txt')
              assert key is token
              return payload
             def __setitem__(self,key,value):
              mark('set.txt')
              assert key is token and value is payload
              self.saved=value
            box=Box()
            def receiver():
             mark('receiver.txt')
             return box
            def key():
             mark('key.txt')
             return token
            def rhs():
             mark('rhs.txt')
             return delta
            """;
        var source = definitions + "\n" + (yielding
            ? "def update():\n (yield 'receiver')[(yield 'key')]+=(yield 'rhs')\n yield 'updated'\ng=update()\nassert next(g)=='receiver'\nassert g.send(receiver())=='key'\nassert g.send(key())=='rhs'\nassert g.send(rhs())=='updated'\n"
            : "receiver()[key()]+=rhs()\n") + "assert box.saved is payload\nprint(payload.items)";
        var paths = new[] { "/receiver.txt", "/key.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" };
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
        using var script = new LythonEngine().Compile("values=[[]]\nalias=values[0]\nvalues[0]+=['x'*4096]\nassert values[0] is alias\nreturn alias", budget);
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
    [InlineData("def run(obj):\n obj[5]+=1\nrun([1])", "IndexError", 2, 3)]
    [InlineData("def run(obj):\n obj[0]+=2\nrun((1,))", "TypeError", 2, 3)]
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
