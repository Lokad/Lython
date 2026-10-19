using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AugmentedMemberExecutionTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "order", "events=[]\nclass Box:\n @property\n def value(self): events.append('get'); return 1\n @value.setter\n def value(self,value): events.append(('set',value))\nbox=Box()\ndef receiver(): events.append('receiver'); return box\ndef rhs(): events.append('rhs'); return 2\nreceiver().value+=rhs()\nprint(events)", "['receiver', 'get', 'rhs', ('set', 3)]\n" },
        { "rebinding", "class Box: pass\nfirst=Box(); first.value=1\nsecond=Box(); second.value=10\ncurrent=first\ndef rhs():\n global current\n current=second\n return 2\ncurrent.value+=rhs()\nprint(first.value,second.value,current is second)", "3 10 True\n" },
        { "alias", "class Box: pass\nbox=Box(); box.value=[1]; alias=box.value\nbox.value+=[2]\nprint(alias,alias is box.value)", "[1, 2] True\n" },
        { "readonly-inplace", "class Box:\n @property\n def value(self): return payload\npayload=[1]\ntry: Box().value+=[2]\nexcept AttributeError: print(payload)", "[1, 2]\n" },
        { "fallback-add", "events=[]\nclass Value:\n def __iadd__(self,other): events.append('iadd'); return NotImplemented\n def __add__(self,other): events.append('add'); return 7\nclass Box: pass\nbox=Box();box.value=Value()\nbox.value+=2\nprint(events,box.value)", "['iadd', 'add'] 7\n" },
        { "raising-inplace", "events=[]\nclass Value:\n def __iadd__(self,other): events.append('iadd'); raise ValueError('bad')\nclass Box: pass\nbox=Box();original=Value();box.value=original\ndef rhs(): events.append('rhs'); return 2\ntry: box.value+=rhs()\nexcept ValueError: print(events,box.value is original)", "['rhs', 'iadd'] True\n" },
        { "getattr", "class Box:\n def __getattr__(self,name): return 1\nbox=Box();box.value+=2\nprint(box.value,vars(box))", "3 {'value': 3}\n" },
        { "missing-before-rhs", "class Box: pass\ndef rhs(): raise AssertionError('rhs ran')\ntry: Box().missing+=rhs()\nexcept AttributeError: print('missing')", "missing\n" },
        { "locals", "class Box: pass\nbox=Box();box.value=1\ndef run(box):\n delta=2\n box.value+=locals()['delta']\n return locals()['box'] is box\nprint(run(box),box.value)", "True 3\n" },
        { "yield-rhs", "class Box: pass\nbox=Box();box.value=1\ndef update(box):\n box.value+=(yield 'rhs')\n yield box.value\ng=update(box); print(next(g),g.send(2),box.value)", "rhs 3 3\n" },
        { "yield-receiver", "class Box: pass\nbox=Box();box.value=1\ndef update():\n (yield 'receiver').value+=2\n yield 'updated'\ng=update(); print(next(g),g.send(box),box.value)", "receiver updated 3\n" },
        { "nonlocal", "def outer():\n class Box: pass\n box=Box();box.value=1\n def run():\n  nonlocal box\n  box.value+=2\n run()\n return box.value\nprint(outer())", "3\n" },
        { "descriptor-replacement", "events=[]\nclass D:\n def __init__(self,name): self.name=name\n def __get__(self,obj,owner): events.append(('get',self.name)); return 1\n def __set__(self,obj,value): events.append(('set',self.name,value))\nclass C: value=D('old')\nobj=C()\ndef rhs(): C.value=D('new'); return 2\nobj.value+=rhs()\nprint(events)", "[('get', 'old'), ('set', 'new', 3)]\n" },
        { "inheritance", "class Base:\n @property\n def value(self): return self.saved\n @value.setter\n def value(self,value): self.saved=value\nclass Child(Base): pass\nbox=Child();box.saved=1\nbox.value+=2\nprint(box.value)", "3\n" },
        { "class-scope", "class Box: pass\nbox=Box();box.value=1\nclass Runner: box.value+=2\nprint(box.value)", "3\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task MemberUpdatesPreservePythonOrderAndAliases(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
        foreach (var path in new[] { null, "/receiver.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" })
            yield return [yielding, path];
    }

    [Theory]
    [MemberData(nameof(SuspensionCases))]
    public async Task MemberPhasesSuspendAndCancelWithoutReplay(bool yielding, string? pausedPath)
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
            class Box:
             @property
             def value(self):
              mark('get.txt')
              return payload
             @value.setter
             def value(self,value):
              mark('set.txt')
              assert value is payload
              self.saved=value
            box=Box()
            def receiver():
             mark('receiver.txt')
             return box
            def rhs():
             mark('rhs.txt')
             return delta
            """;
        var source = definitions + "\n" + (yielding
            ? "def update():\n (yield 'receiver').value+=(yield 'rhs')\n yield 'updated'\ng=update()\nassert next(g)=='receiver'\nassert g.send(receiver())=='rhs'\nassert g.send(rhs())=='updated'\n"
            : "receiver().value+=rhs()\n") + "assert box.saved is payload\nprint(payload.items)";
        var paths = new[] { "/receiver.txt", "/get.txt", "/rhs.txt", "/iadd.txt", "/set.txt" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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
                    Assert.Fail(early.Failure?.Message ?? "Member update finished before the controlled callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pausedPath is null, result.Success);
            Assert.Equal(pausedPath is null ? "['old', 'new']\n" : "", result.StandardOutput);
            var completed = pausedPath is null ? paths.Length : Array.IndexOf(paths, pausedPath);
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Box: pass\nbox=Box();box.value=[]\nalias=box.value\nbox.value+=['x'*4096]\nassert box.value is alias\nreturn alias", budget);
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
    [InlineData("def run(obj):\n obj.missing+=1\nrun(object())", 2, 3)]
    [InlineData("class C:\n @property\n def value(self): return 1\ndef run(obj):\n obj.value+=2\nrun(C())", 5, 6)]
    public async Task MemberFailuresKeepTargetAndCallerSpans(string source, int line, int callerLine)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal("AttributeError", failure.ExceptionType);
            Assert.Equal(line, failure.Span?.Line);
            Assert.Equal(2, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == callerLine);
        }
    }
}
