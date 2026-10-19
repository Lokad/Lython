using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RawDescriptorGetTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "property-class", "p=property(lambda self:1)\nprint(type(p) is property)", "True\n" },
        { "plain", "events=[]\nclass Slot:\n def __get__(self,obj,owner):\n  events.append((isinstance(self,Slot),obj is box,owner.__name__))\n  return 7\nclass Box: value=Slot()\nbox=Box()\ntry: print(box.value)\nexcept TypeError as e: print('error',str(e))\nprint(events)", "7\n[(True, True, 'Box')]\n" },
        { "property", "events=[]\nclass Slot:\n @property\n def __get__(self):\n  events.append('bind')\n  return lambda obj,owner:7\nclass Box: value=Slot()\nbox=Box()\ntry: print(box.value)\nexcept TypeError as e: print('error',str(e))\nprint(events)", "error 'property' object is not callable\n[]\n" },
        { "classmethod", "events=[]\nclass Slot:\n @classmethod\n def __get__(cls,obj,owner): events.append(cls.__name__);return 7\nclass Box: value=Slot()\nbox=Box()\ntry: print(box.value)\nexcept TypeError as e: print('error',str(e))\nprint(events)", "error 'classmethod' object is not callable\n[]\n" },
        { "staticmethod", "events=[]\nclass Slot:\n @staticmethod\n def __get__(descriptor,obj,owner):\n  events.append((isinstance(descriptor,Slot),obj is box,owner.__name__))\n  return 7\nclass Box: value=Slot()\nbox=Box()\ntry: print(box.value)\nexcept TypeError as e: print('error',str(e))\nprint(events)", "7\n[(True, True, 'Box')]\n" },
        { "callable-object", "events=[]\nclass Callback:\n def __call__(self,descriptor,obj,owner):\n  events.append((isinstance(descriptor,Slot),obj is box,owner.__name__))\n  return 7\nclass Slot: __get__=Callback()\nclass Box: value=Slot()\nbox=Box()\ntry: print(box.value)\nexcept TypeError as e: print('error',str(e))\nprint(events)", "7\n[(True, True, 'Box')]\n" },
        { "property-set-delete", "events=[]\nclass Slot:\n @property\n def __set__(self):\n  events.append('bind-set')\n  def set(obj,value): events.append(('set',value))\n  return set\n @property\n def __delete__(self):\n  events.append('bind-delete')\n  return lambda obj:events.append('delete')\nclass Box: value=Slot()\nbox=Box();box.value=7;del box.value\nprint(events)", "['bind-set', ('set', 7), 'bind-delete', 'delete']\n" },
        { "missing", "events=[]\nclass Slot:\n def __getattr__(self,name): events.append(name);return lambda *args:7\nd=Slot()\nclass Box: value=d\nprint(Box().value is d,events)", "True []\n" },
        { "instance-shadow", "class Slot:\n def __get__(self,obj,owner): return 7\nd=Slot();d.__get__=lambda *args:9\nclass Box: value=d\nb=Box();print(b.value,d.__get__(b,Box))", "7 9\n" },
        { "class-access", "class Slot:\n def __get__(self,obj,owner): return obj is None,owner.__name__\nclass Box: value=Slot()\nprint(Box.value)", "(True, 'Box')\n" },
        { "super-access", "class Slot:\n def __get__(self,obj,owner): return isinstance(obj,Child),owner.__name__\nclass Base: value=Slot()\nclass Child(Base):\n def read(self): return super().value\nprint(Child().read())", "(True, 'Child')\n" },
        { "inherited-getter", "class Base:\n def __get__(self,obj,owner): return type(self).__name__,owner.__name__\nclass Slot(Base): pass\nclass Box: value=Slot()\nprint(Box().value)", "('Slot', 'Box')\n" },
        { "bound-getter", "class Handler:\n def get(self,descriptor,obj,owner): return isinstance(descriptor,Slot),isinstance(obj,Box),owner.__name__\nh=Handler()\nclass Slot: __get__=h.get\nclass Box: value=Slot()\nprint(Box().value)", "(True, True, 'Box')\n" },
        { "attribute-fallback", "events=[]\nclass Slot:\n def __get__(self,obj,owner): events.append('get');raise AttributeError('missing')\nclass Box:\n value=Slot()\n def __getattr__(self,name): events.append(name);return 7\nprint(Box().value,events)", "7 ['get', 'value']\n" },
        { "call-result", "class Slot:\n def __get__(self,obj,owner): return lambda:obj.total\nclass Box: value=Slot()\nb=Box();b.total=7;print(b.value())", "7\n" },
        { "none", "class Slot: __get__=None\nclass Box: value=Slot()\ntry: Box().value\nexcept TypeError as e: print(str(e))", "'NoneType' object is not callable\n" },
        { "integer", "class Slot: __get__=7\nclass Box: value=Slot()\ntry: Box().value\nexcept TypeError as e: print(str(e))", "'int' object is not callable\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task GetterSlotsFollowPythonRawCallSemantics(string name, string source, string expected)
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
        foreach (var shape in new[] { "method", "staticmethod", "callable" })
        foreach (var item in new[] { false, true })
        {
            yield return [shape, item, null];
            yield return [shape, item, "/get.txt"];
            if (item) yield return [shape, true, "/item.txt"];
        }
    }

    [Theory]
    [MemberData(nameof(SuspensionCases))]
    public async Task RawGetterAndReturnedItemHandlerSuspendWithoutReplay(string shape, bool item, string? pause)
    {
        var definitions = """
            def mark(path):
             with open(path,'a') as output: output.write('x')
            payload=['value']
            token=[]
            def read(key):
             mark('item.txt')
             assert key is token
             return payload
            def emit(self,obj,owner):
             mark('get.txt')
             assert self is descriptor and obj is instance and owner is Box
             return RETURN_VALUE
            """.Replace("RETURN_VALUE", item ? "read" : "payload", StringComparison.Ordinal);
        var descriptor = shape switch
        {
            "method" => "class Descriptor: __get__=emit\n",
            "staticmethod" => "class Descriptor: __get__=staticmethod(emit)\n",
            _ => "class Getter:\n def __call__(self,descriptor,obj,owner): return emit(descriptor,obj,owner)\nclass Descriptor: __get__=Getter()\n"
        };
        var source = definitions + "\n" + descriptor + "descriptor=Descriptor()\nclass Box: "
            + (item ? "__getitem__" : "value") + "=descriptor\ninstance=Box()\nresult="
            + (item ? "instance[token]" : "instance.value")
            + "\nassert result is payload\nwith open('result.txt','w') as output: output.write('ok')";
        var paths = item ? new[] { "/get.txt", "/item.txt" } : new[] { "/get.txt" };
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
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
                    Assert.Fail(early.Failure?.Message ?? "Raw getter completed before the controlled callback suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pause is null, result.Success);
            Assert.Equal(pause is null ? "ok" : "original", host.ReadText("/result.txt"));
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
    public async Task RawGetterResultsRemainFundedAcrossExecutions(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("payload=['x'*4096]\nclass Descriptor:\n def __get__(self,obj,owner): return payload\nclass Box: value=Descriptor()\nreturn Box().value", budget);
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
    [InlineData("class Descriptor: __get__=None\nclass Box: value=Descriptor()\ndef run(obj):\n return obj.value\nrun(Box())", 4, 5)]
    [InlineData("class Descriptor:\n @property\n def __get__(self): raise AssertionError('must not bind')\nclass Box: value=Descriptor()\ndef run(obj):\n return obj.value\nrun(Box())", 6, 7)]
    public async Task InvalidRawGettersKeepTargetAndCallerSpans(string source, int line, int callerLine)
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
