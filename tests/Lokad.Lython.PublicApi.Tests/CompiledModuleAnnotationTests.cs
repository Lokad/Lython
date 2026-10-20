using System.Collections;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CompiledModuleAnnotationTests
{
    [Fact]
    public async Task SuppliedAnnotationNamespaceAndAliasesSurviveInitialization()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("print(__annotations__ is supplied)\nitem:int\nprint(list(__annotations__),__annotations__['item'] is int)", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var supplied = new Dictionary<string, object?> { ["seed"] = "kept" };
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["__annotations__"] = supplied, ["supplied"] = supplied } };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True\n['seed', 'item'] True\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal("kept", Assert.Single(supplied).Value);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string, string, string> Programs => new()
    {
        { "simple-module-values", "counter:int=7\nother:str\nprint(counter,list(__annotations__),__annotations__['counter'] is int,__annotations__['other'] is str)", "7 ['counter', 'other'] True True\n" },
        { "value-setter-annotation-order", "events=[]\nclass Box:\n def __setattr__(self,name,value):events.append('setter');object.__setattr__(self,name,value)\ndef value():events.append('value');return 7\ndef annotation():events.append('annotation');return int\nbox=Box()\nbox.item:annotation()=value()\nresult:annotation()=value()\nprint(events,list(__annotations__),result,box.item)", "['value', 'setter', 'annotation', 'value', 'annotation'] ['result'] 7 7\n" },
        { "replaced-module-mapping", "events=[]\nclass Registry:\n def __setitem__(self,key,value):events.append((key,value is int))\n__annotations__=Registry()\nx:int=7\nprint(x,events)", "7 [('x', True)]\n" },
        { "postponed-module", "from __future__ import annotations\nevents=[]\ndef annotation():events.append('annotation');return int\nx:annotation()=7\nprint(x,__annotations__['x'],events)", "7 annotation() []\n" },
        { "conditional-annotation-setup", "if False:unused:int\nprint(__annotations__)", "{}\n" },
        { "setup-before-first-statement", "print(__annotations__)\nfor item in []:\n unused:int\nprint(__annotations__)", "{}\n{}\n" },
        { "dead-handler-setup", "print(__annotations__)\ntry:pass\nexcept ValueError:unused:int\nprint(__annotations__)", "{}\n{}\n" },
        { "nested-functions-excluded", "def helper():\n unused:int\ntry:print(__annotations__)\nexcept NameError:print('absent')", "absent\n" },
        { "nested-classes-excluded", "class Box:\n item:int\ntry:print(__annotations__)\nexcept NameError:print('absent')\nprint(list(Box.__annotations__))", "absent\n['item']\n" },
        { "failed-value-before-annotation", "events=[]\ndef fail():events.append('value');raise ValueError('stop')\ndef annotation():events.append('annotation');return int\ntry:x:annotation()=fail()\nexcept ValueError:pass\nprint(events,__annotations__)", "['value'] {}\n" },
        { "annotation-only-target-reads", "events=[]\nclass Box:pass\nbox=Box()\ndef receiver():events.append('base');return box\ndef annotation():events.append('annotation');return int\nreceiver().item:annotation()\nprint(events,__annotations__)", "['base', 'annotation'] {}\n" },
        { "complete-annotated-csv-report", "import csv\nimport io\n\nclass Total:\n    def __init__(self):\n        self.amount = 0\n\n    def add(self, amount):\n        self.amount += amount\n\n    def __format__(self, spec):\n        return format(self.amount, spec)\n\ntotals:dict[str,Total] = {}\naccepted:int = 0\nsource = io.StringIO('kind,amount\\nred,3\\nblue,2\\nred,-1\\nblue,5\\n')\nfor row in csv.DictReader(source):\n    amount = int(row['amount'])\n    if amount >= 0:\n        kind = row['kind']\n        if kind not in totals:\n            totals[kind] = Total()\n        totals[kind].add(amount)\n        accepted += 1\ntarget = io.StringIO()\ntarget.write('kind,total\\n')\nfor kind in sorted(totals):\n    target.write(f'{kind},{totals[kind]:04d}\\n')\nprint(target.getvalue(), end='')\nprint(accepted)\n", "kind,total\nblue,0007\nred,0003\n3\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ModuleMetadataFollowsPythonOrderAndBindings(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
    [InlineData("value:(lambda:Kind)", "helper.__annotations__['value']()")]
    [InlineData("def factory():\n def read():return Kind\n return read\nvalue:factory()", "helper.__annotations__['value']()")]
    [InlineData("value:(lambda:Kind for _ in [0])", "next(helper.__annotations__['value'])()")]
    public async Task ImportedMetadataKeepsFinalGlobals(string declaration, string expression)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import helper\nprint(" + expression + " is str)", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helper", "/helper.py" },
        };
        foreach (var asynchronous in new[] { false, true })
        {
            var source = "Kind=int\n" + declaration + "\nKind=str";
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/helper.py", source);
            else mock!.SeedFile("/helper.py", source);
            using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("True\n", result.StandardOutput);
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    private const string CallbackSource = """
def value():
 with open('value.txt','a') as target:target.write('value')
 return 7
def annotation():
 with open('annotation.txt','a') as target:target.write('annotation')
 return int
class Registry:
 def __setitem__(self,key,value):
  with open('mapping.txt','a') as target:target.write(key)
__annotations__=Registry()
item:annotation()=value()
print('after')
""";

    [Theory]
    [InlineData("value", false)]
    [InlineData("annotation", false)]
    [InlineData("mapping", false)]
    [InlineData("value", true)]
    [InlineData("annotation", true)]
    [InlineData("mapping", true)]
    public async Task CancellationDuringMetadataCallbacksDrainsBeforeRefund(string phase, bool classBody)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = classBody ? CallbackSource.Replace("__annotations__=Registry()\nitem:annotation()=value()",
            "class Box:\n __annotations__=Registry()\n item:annotation()=value()") : CallbackSource;
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var phases = new[] { "value", "annotation", "mapping" };
        foreach (var name in phases) host.SeedFile("/" + name + ".txt", "before");
        var entered = host.PauseWriteUntilCancellation("/" + phase + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Execution finished before the selected metadata callback suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            for (var i = 0; i < phases.Length; i++)
                Assert.Equal(i < Array.IndexOf(phases, phase) ? "before" + phases[i] : "before", host.ReadText("/" + phases[i] + ".txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RefusedMetadataStorageRemainsTerminal(bool mapping, bool classBody)
    {
        const string common = """
def refuse():
 print('before')
 try:unused=[0]*8192
 except BaseException:pass
""";
        const string store = """
class Registry:
 def __setitem__(self,key,value):refuse()
""";
        var assignment = "item:" + (mapping ? "int" : "annotation()") + "=7";
        var body = (mapping ? "__annotations__=Registry()\n" : "") + assignment;
        if (classBody) body = "class Box:\n " + body.Replace("\n", "\n ");
        var source = common + "\n" + (mapping ? store : "def annotation():refuse();return int\n") +
            "\ntry:\n " + body.Replace("\n", "\n ") + "\nexcept BaseException:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal("before\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedMetadataStaysFundedAcrossSharedExecutions(bool classBody)
    {
        const string moduleSource = """
def annotation():
 with open('annotation.txt','a') as target:target.write('annotation')
 return 'é'*4096
item:annotation()=7
return __annotations__
""";
        var source = classBody ? moduleSource.Replace("item:annotation()=7\nreturn __annotations__",
            "class Box:item:annotation()=7\nreturn Box.__annotations__") : moduleSource;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var mock = new MockLythonHost();
        mock.SeedFile("/annotation.txt", "before");
        using (var first = script.Run(mock))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, Assert.IsAssignableFrom<IDictionary>(first.ReturnValue)["item"]);
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var host = new DelayedLythonHost();
            host.SeedFile("/annotation.txt", "before");
            using (var second = await script.RunAsync(host))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, Assert.IsAssignableFrom<IDictionary>(second.ReturnValue)["item"]);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypedModuleCsvJobPublishesCompleteReport(bool helper)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "RepresentativeScripts", helper ? "csv-summary-helpers.py" : "csv-summary.py"))
            .Replace("totals = {}", "totals:dict[str,int] = {}")
            .Replace("accepted = 0", "accepted:int = 0");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            const string input = "kind,amount,note\nred,3,x\nblue,2,y\nred,-1,z\nblue,5,w\n";
            if (delayed is not null) delayed.SeedFile("/input.csv", input);
            else mock!.SeedFile("/input.csv", input);
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("3\n", result.StandardOutput);
                Assert.Equal("kind,total\nblue,7\nred,3\n", delayed?.ReadText("/report.csv") ?? mock!.ReadText("/report.csv"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnnotationsKeepManagedRecursionProtection(bool classBody)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var source = classBody ? "def recurse():return recurse()\nclass Box:item:recurse()" :
            "item:int\ndef recurse():return recurse()\nrecurse()";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxRecursionDepth = 16 };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Contains(result.FailureAttribution?.Reason, new LythonFailureReason?[]
                    { LythonFailureReason.RecursionDepthLimit, LythonFailureReason.InterpreterStackLimit });
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
