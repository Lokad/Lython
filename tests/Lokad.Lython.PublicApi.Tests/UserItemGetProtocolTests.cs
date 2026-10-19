using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UserItemGetProtocolTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "shadow", "class Box:\n def __getitem__(self,key): return 'type'\nb=Box();b.__getitem__=lambda key:'instance'\nprint(b[0],b.__getitem__(0))", "type instance\n" },
        { "instance-only", "class Box: pass\nb=Box();b.__getitem__=lambda key:'instance'\ntry: print(b[0])\nexcept TypeError as e: print(str(e))", "'Box' object is not subscriptable\n" },
        { "getattr", "events=[]\nclass Box:\n def __getattr__(self,name): events.append(name);return lambda key:'fallback'\ntry: print(Box()[0])\nexcept TypeError: print('type-only')\nprint(events)", "type-only\n[]\n" },
        { "getattribute", "class Box:\n def __getattribute__(self,name): return lambda key:'override'\n def __getitem__(self,key): return 'type'\nb=Box();print(b[0],b.__getitem__(0))", "type override\n" },
        { "inheritance", "class Base:\n def __getitem__(self,key): return ('base',key)\nclass Child(Base): pass\nb=Child();b.__getitem__=lambda key:'instance'\nprint(b[3])", "('base', 3)\n" },
        { "classmethod", "class Box:\n @classmethod\n def __getitem__(cls,key): return cls.__name__,key\nprint(Box()[3])", "('Box', 3)\n" },
        { "staticmethod", "class Box:\n @staticmethod\n def __getitem__(key): return key\nprint(Box()[3])", "3\n" },
        { "descriptor", "events=[]\nclass Slot:\n def __get__(self,obj,owner):\n  events.append(owner.__name__)\n  return lambda key: (obj is box,key)\nclass Box: __getitem__=Slot()\nbox=Box();print(box[3],events)", "(True, 3) ['Box']\n" },
        { "slot-property", "events=[]\nclass Box:\n @property\n def __getitem__(self):\n  events.append('bind')\n  def get(key): events.append('call');return key\n  return get\nb=Box();key=[];print(b[key] is key,events)", "True ['bind', 'call']\n" },
        { "none", "class Box: __getitem__=None\ntry: Box()[0]\nexcept TypeError as e: print(str(e))", "'NoneType' object is not callable\n" },
        { "integer", "class Box: __getitem__=7\ntry: Box()[0]\nexcept TypeError as e: print(str(e))", "'int' object is not callable\n" },
        { "descriptor-none", "class Box:\n @property\n def __getitem__(self): return None\ntry: Box()[0]\nexcept TypeError as e: print(str(e))", "'NoneType' object is not callable\n" },
        { "descriptor-error", "class Box:\n @property\n def __getitem__(self): raise ValueError('bind')\ntry: Box()[0]\nexcept ValueError as e: print(str(e))", "bind\n" },
        { "order", "events=[]\nclass Box:\n @property\n def __getitem__(self):\n  events.append('bind')\n  return lambda key: events.append(('call',key))\ndef receiver(): events.append('receiver');return Box()\ndef key(): events.append('key');return 3\nreceiver()[key()]\nprint(events)", "['receiver', 'key', 'bind', ('call', 3)]\n" },
        { "augmented", "events=[]\nclass Box:\n def __getitem__(self,key): events.append(('get',key));return 1\n def __setitem__(self,key,value): events.append(('set',key,value))\nb=Box();b.__getitem__=lambda key:10\nb[3]+=2\nprint(events)", "[('get', 3), ('set', 3, 3)]\n" },
        { "raw-slice", "class Bound:\n def __index__(self): raise AssertionError('raw')\na=Bound();b=Bound();c=Bound()\nclass Box:\n def __getitem__(self,key): return key.start is a,key.stop is b,key.step is c\nprint(Box()[a:b:c])", "(True, True, True)\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImplicitItemReadsUseTypeSlots(string name, string source, string expected)
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
        foreach (var descriptor in new[] { false, true })
        foreach (var augmented in new[] { false, true })
        {
            foreach (var pause in new[] { null, "/bind.txt", "/call.txt" })
                yield return [descriptor, augmented, false, pause];
            if (augmented)
            {
                yield return [descriptor, true, false, "/rhs.txt"];
                yield return [descriptor, true, false, "/store.txt"];
            }
        }
        foreach (var pause in new[] { null, "/bind.txt", "/call.txt" })
            yield return [true, false, true, pause];
    }

    [Theory]
    [MemberData(nameof(SuspensionCases))]
    public async Task ItemBindingAndCallsSuspendWithoutReplay(bool descriptor, bool augmented, bool slice, string? pause)
    {
        const string definitions = """
            def mark(path):
             with open(path,'a') as output: output.write('x')
            payload=['old']
            delta=['new']
            token=[]
            def get(key):
             mark('call.txt')
             if isinstance(key,slice): assert (key.start,key.stop,key.step)==(1,2,None)
             else: assert key is token
             return payload
            def rhs():
             mark('rhs.txt')
             return delta
            """;
        var getter = descriptor
            ? "class Slot:\n def __get__(self,obj,owner):\n  mark('bind.txt')\n  return get\nclass Box:\n __getitem__=Slot()\n"
            : "class Box:\n @property\n def __getitem__(self):\n  mark('bind.txt')\n  return get\n";
        var setter = " def __setitem__(self,key,value):\n  mark('store.txt')\n  assert key is token and value is payload\n";
        var source = definitions + "\n" + getter + setter + "box=Box()\n"
            + (descriptor ? "box.__getitem__=lambda key:['wrong']\n" : "")
            + (augmented ? "box[token]+=rhs()\nresult=payload\n" : slice ? "result=box[1:2]\n" : "result=box[token]\n")
            + "assert result is payload\nwith open('result.txt','w') as output: output.write(str(result))";
        var paths = augmented ? new[] { "/bind.txt", "/call.txt", "/rhs.txt", "/store.txt" }
            : new[] { "/bind.txt", "/call.txt" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        foreach (var path in paths) host.SeedFile(path, "before");
        host.SeedFile("/result.txt", "original");
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
                    Assert.Fail(early.Failure?.Message ?? "Item read completed before the controlled callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? augmented ? "['old', 'new']" : "['old']" : "original", host.ReadText("/result.txt"));
            var completed = pause is null ? paths.Length : Array.IndexOf(paths, pause);
            Assert.True(completed >= 0);
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
    public async Task ReturnedKeyAliasesRemainFundedAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("class Box:\n def __getitem__(self,key): return key\npayload=['x'*4096]\nreturn Box()[payload]", budget);
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
    [InlineData("def run(obj):\n return obj[0]\nrun(object())", 2, 3)]
    [InlineData("class Box: __getitem__=None\ndef run(obj):\n return obj[0]\nrun(Box())", 3, 4)]
    public async Task ItemFailuresKeepTheirTargetAndCallerSpans(string source, int line, int callerLine)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            var failure = Assert.IsType<LythonRuntimeFailure>(result.Failure);
            Assert.Equal("TypeError", failure.ExceptionType);
            Assert.Equal(line, failure.Span?.Line);
            Assert.Equal(9, failure.Span?.Column);
            Assert.Contains(failure.StackTrace, frame => frame.FunctionName == "run" && frame.Span?.Line == callerLine);
        }
    }
}
