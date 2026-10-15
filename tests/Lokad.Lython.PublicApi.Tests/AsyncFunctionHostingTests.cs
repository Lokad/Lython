using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class AsyncFunctionHostingTests
{
    private const string Prelude = "def note(message):\n    with open('events.txt','a',newline='') as target:\n        target.write(message+'\\n')\ndef read():\n    note('default')\n    with open('number.txt') as source:\n        return int(source.read())\n";
    // Exact stdout and event-file bytes independently checked under CPython.
    public static TheoryData<string, string, string, string> Programs => new()
    {
        { "decorator_default_order", "def factory():\n    note('decorator')\n    def decorate(function):\n        note('apply')\n        return function\n    return decorate\n@factory()\ndef f(x=read()):\n    return x+1\nprint(f())", "4\n", "decorator\ndefault\napply\n" },
        { "escaped_defaults_cells", "def make(seed):\n    note('make')\n    def inner(value=seed):\n        with open('number.txt') as source:\n            return value+seed+int(source.read())\n    seed+=1\n    return inner\nf=make(5)\nprint(f())", "14\n", "make\n" },
        { "yielding_property", "class Box:\n    @property\n    def value(self):\n        note('get')\n        with open('number.txt') as source:\n            return int(source.read())\ndef f(box):\n    return box.value+1\nprint(f(Box()))", "4\n", "get\n" },
        { "yielding_operator", "class Number:\n    def __add__(self,other):\n        note('add')\n        with open('number.txt') as source:\n            return int(source.read())+other\ndef f(value):\n    return value+1\nprint(f(Number()))", "4\n", "add\n" },
        { "annotated_fallback", "def f(value:int=read())->int:\n    return value\nprint(f(), f.__annotations__['value'] is int)", "3 True\n", "default\n" },
        { "generator_closure", "def make(seed):\n    def values(amount=read()):\n        yield seed+amount\n    seed+=1\n    return values\nvalues=make(4)\nprint(list(values()))", "[8]\n", "default\n" },
        { "failed_default_old_binding", "def f():\n    return 11\ndef fail():\n    note('fail')\n    raise ValueError('default')\ntry:\n    def f(x=fail()):\n        return x\nexcept ValueError:\n    note('caught')\nprint(f())", "11\n", "fail\ncaught\n" },
        { "defaults_once", "def f(a=read(),b=read()):\n    return a+b\nprint(f())", "6\n", "default\ndefault\n" },
        { "imports_preserve_slots", "note('import')\nmarker=9\nimport math\ndef f(value):\n    preserved=value+1\n    import operator\n    return operator.add(preserved,marker)\nprint(f(2))", "12\n", "import\n" },
        { "yielding_iadd", "class Number:\n    def __iadd__(self,other):\n        note('iadd')\n        with open('number.txt') as source:\n            return int(source.read())+other\ndef f(value):\n    value+=1\n    return value\nprint(f(Number()))", "4\n", "iadd\n" },
        { "yielding_iadd_fallback", "class Left:\n    def __iadd__(self,other):\n        note('iadd')\n        with open('number.txt') as source: source.read()\n        return NotImplemented\n    def __add__(self,other):\n        note('add')\n        with open('number.txt') as source: source.read()\n        return NotImplemented\nclass Right:\n    def __radd__(self,other):\n        note('radd')\n        with open('number.txt') as source: source.read()\n        return 42\nvalue=Left()\nvalue+=Right()\nprint(value)", "42\n", "iadd\nadd\nradd\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task DefinitionsAndYieldingProtocolsPreservePythonOrder(string name, string body, string stdout, string events)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Prelude + body, budget);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) { delayed.SeedFile("/number.txt", "3"); delayed.SeedFile("/events.txt", ""); }
            else { mock!.SeedFile("/number.txt", "3"); mock.SeedFile("/events.txt", ""); }
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(stdout, result.StandardOutput);
                Assert.Equal(events, delayed?.ReadText("/events.txt") ?? mock!.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationDuringDefinitionDrainsTheOwnerBeforeRefund(bool decorator)
    {
        const string wait = "def wait(value=0):\n    with open('out.txt','w') as target:\n        target.write('pending')\n    return value\n";
        var source = wait + (decorator ? "@wait\ndef f():\n    return 1" : "def f(value=wait()):\n    return value");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/out.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/out.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Equal("before", host.ReadText("/out.txt"));
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
