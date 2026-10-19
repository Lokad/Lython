using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AnnotatedHelperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnnotatedRecursionKeepsManagedAdmissionAndCleanup(bool nested)
    {
        var source = nested
            ? "def helper(value:int)->int:\n def step(next_value:int)->int:return helper(next_value)\n return step(value+1)\nhelper(0)"
            : "def helper(value:int)->int:return helper(value+1)\nhelper(0)";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxRecursionDepth = 16 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.RecursionDepthLimit, result.FailureAttribution?.Reason);
                Assert.Equal(16, result.FailureAttribution?.Limit);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task RetainedAnnotationAliasRemainsFunded()
    {
        const string source = "payload='x'*4096\ndef helper(value:payload)->int:return value+1\npayload=None\nreturn helper.__annotations__['value']";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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

    [Fact]
    public async Task RefusedAnnotationStorageStopsBeforeDecoratorPublication()
    {
        const string source = "def decorate(function):\n with open('events.txt','a') as output:output.write('published')\n return function\nhelper='before'\nprint('ready')\n@decorate\ndef helper(value:'x'*262144)->int:return value\nprint('after')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/events.txt", "before");
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(80 * 1024) };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("ready\n", result.StandardOutput);
                Assert.Equal("before", host.ReadText("/events.txt"));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("default")]
    [InlineData("annotation")]
    [InlineData("decorate")]
    [InlineData("body")]
    public async Task HeaderAndBodyIoPreserveOrderCancellationAndOwnership(string? pausedStage)
    {
        const string source = "def read(stage,value):\n with open(stage+'.txt','a') as output:output.write(stage)\n return value\ndef decorate(function):\n read('decorate',0)\n return function\n@decorate\ndef helper(value:read('annotation',int)=read('default',3))->int:\n return read('body',value)+1\nprint(helper())";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        string[] stages = ["default", "annotation", "decorate", "body"];
        foreach (var stage in stages) host.SeedFile("/" + stage + ".txt", "before");
        var entered = pausedStage is null ? null : host.PauseWriteUntilCancellation("/" + pausedStage + ".txt");
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
                    Assert.Fail(early.Failure?.Message ?? "Definition completed before the selected operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(pausedStage is null, result.Success);
            if (pausedStage is not null) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal(pausedStage is null ? "4\n" : "", result.StandardOutput);
            var completed = pausedStage is null ? stages.Length : Array.IndexOf(stages, pausedStage);
            for (var i = 0; i < stages.Length; i++)
                Assert.Equal("before" + (i < completed ? stages[i] : ""), host.ReadText("/" + stages[i] + ".txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    // Expected stdout checked independently with isolated CPython 3.13.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "simple", "def f(value:int)->int:return value+1\nprint(f(2),f.__annotations__['value'] is int,f.__annotations__['return'] is int)", "3 True True\n" },
        { "eager_binding", "Kind=int\ndef f(value:Kind)->Kind:return value\nKind=str\nprint(f(2),f.__annotations__['value'] is int)", "2 True\n" },
        { "definition_order", "events=[]\ndef annotation():\n events.append('annotation')\n return int\ndef default():\n events.append('default')\n return 2\ndef decorator():\n events.append('decorator')\n def apply(function):\n  events.append('apply')\n  return function\n return apply\n@decorator()\ndef f(value:annotation()=default())->annotation():return value+1\nprint(f(),events)", "3 ['decorator', 'default', 'annotation', 'annotation', 'apply']\n" },
        { "failed_annotation", "def f():return 'old'\ntry:\n def f(value:missing)->int:return value\nexcept NameError:print(f())", "old\n" },
        { "failed_return", "def f():return 'old'\ntry:\n def f(value:int)->missing:return value\nexcept NameError:print(f())", "old\n" },
        { "default_before_annotation", "events=[]\ndef default():\n events.append('default')\n raise ValueError()\ndef annotation():\n events.append('annotation')\n return int\ntry:\n def f(value:annotation()=default())->int:return value\nexcept ValueError:print(events)", "['default']\n" },
        { "nested_closure", "def make(seed):\n Kind=int\n def f(value:Kind=2)->Kind:return seed+value\n seed+=1\n Kind=str\n return f\nf=make(3)\nprint(f(),f.__annotations__['value'] is int)", "6 True\n" },
        { "nonlocal", "def make(seed):\n def f(value:int)->int:\n  nonlocal seed\n  seed+=value\n  return seed\n return f\nf=make(3)\nprint(f(2),f(4))", "5 9\n" },
        { "postponed", "from __future__ import annotations\nevents=[]\ndef annotation():\n events.append('annotation')\n return int\ndef f(value:annotation())->missing:return value+1\nprint(f(2),f.__annotations__,events)", "3 {'value': 'annotation()', 'return': 'missing'} []\n" },
        { "docstring", "def f(value:int)->int:\n 'helper documentation'\n return value+1\nprint(f(2),f.__doc__)", "3 helper documentation\n" },
        { "parameter_shapes", "def f(value:int=2,/,*,scale:int=3)->int:return value*scale\nprint(f(),f(4,scale=5),list(f.__annotations__))", "6 20 ['value', 'scale', 'return']\n" },
        { "metadata_alias", "def f(value:int)->int:return value+1\nannotations=f.__annotations__\nannotations['extra']=123\nprint(f(2),annotations is f.__annotations__,f.__annotations__['extra'])", "3 True 123\n" },
        { "recursion", "def factorial(value:int)->int:\n if value<2:return 1\n return value*factorial(value-1)\nprint(factorial(6))", "720\n" },
        { "none_return", "def f(value:int)->None:pass\nprint(f(2),f.__annotations__['return'] is None)", "None True\n" },
        { "metadata_not_enforcement", "def helper(value:int)->int:return value\nprint(helper('text'))", "text\n" },
        { "locals", "def helper(value:int)->dict:return locals()\nprint(sorted(helper(3).items()))", "[('value', 3)]\n" },
        { "default_updates_cell", "def make(seed):\n def default():\n  nonlocal seed\n  seed+=1\n  return 2\n def helper(value:int=default())->int:return seed+value\n return helper\nprint(make(3)())", "6\n" },
        { "nested_recursion", "def make():\n def factorial(value:int)->int:\n  if value<2:return 1\n  return value*factorial(value-1)\n return factorial\nprint(make()(6))", "720\n" },
        { "escaping_annotation_lambda", "def make(seed):\n def helper(value:(lambda:seed))->int:return value\n seed+=1\n return helper.__annotations__['value']\nreader=make(3)\nprint(reader())", "4\n" },
        { "annotation_globals", "seed=3\ndef helper(value:globals()['seed'])->int:return value\nseed=4\nprint(helper.__annotations__['value'],globals()['seed'])", "3 4\n" },
        { "annotation_locals", "def make(seed):\n def helper(value:locals()['seed'])->int:return value\n return helper\nprint(make(3).__annotations__['value'])", "3\n" },
        { "annotation_nonlocal", "def make(seed):\n def annotation():\n  nonlocal seed\n  seed+=1\n  return seed\n def helper(value:annotation())->int:return seed+value\n return helper\nfunction=make(3)\nprint(function(2),function.__annotations__['value'])", "6 4\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task HelpersPreservePythonAnnotationsAndEffects(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
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
}
