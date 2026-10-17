using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AnnotationOnlyLocalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclarationOnlyRecursionKeepsManagedAdmission(bool nested)
    {
        var source = nested
            ? "def helper(value):\n unused:int\n def step(next_value):return helper(next_value)\n return step(value+1)\nhelper(0)"
            : "def helper(value):\n value:int\n return helper(value+1)\nhelper(0)";
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

    public static TheoryData<string, string, string> Programs => new()
    {
        { "simple-local", "def parse_amount(value:str)->int:\n result:int\n result=int(value)\n return result\nprint([parse_amount(x) for x in ['7','-2','11']])", "[7, -2, 11]\n" },
        { "closure-local", "def outer():\n value:int\n def inner():return value\n value=7\n return inner\nprint(outer()())", "7\n" },
        { "closure-unbound", "def outer():\n value:int\n return lambda:value\ntry:print(outer()())\nexcept NameError:print('free-cell-unbound')", "free-cell-unbound\n" },
        { "unevaluated-annotation", "def annotation():raise ValueError('annotation-ran')\ndef helper():\n value:annotation()\n value=7\n return value\nprint(helper())", "7\n" },
        { "target-effects-only", "events=[]\nclass Box:\n def __getattr__(self,name):events.append('getattr');return 7\n def __getitem__(self,index):events.append('getitem');return 7\nbox=Box()\ndef base():events.append('base');return box\ndef key():events.append('key');return 0\ndef annotation():events.append('annotation');return int\ndef helper():\n base().value:annotation()\n base()[key()]:annotation()\n return 7\nprint(helper(),events)", "7 ['base', 'base', 'key']\n" },
        { "target-error-order", "events=[]\ndef base():events.append('base');raise ValueError('base')\ndef key():events.append('key');return 0\ndef annotation():events.append('annotation');return int\ndef helper():\n base()[key()]:annotation()\ntry:helper()\nexcept ValueError as error:print(str(error),events)", "base ['base']\n" },
        { "nonlocal-control", "def outer():\n value:int\n value=7\n def update():\n  nonlocal value\n  value+=2\n update()\n return value\nprint(outer())", "9\n" },
        { "protected-return", "events=[]\ndef helper():\n value:int\n try:\n  value=7\n  return value\n finally:events.append('cleanup')\nprint(helper(),events)", "7 ['cleanup']\n" },
        { "generator-control", "events=[]\ndef values():\n value:int\n value=7\n yield value\nprint(list(values()),events)", "[7] []\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LocalDeclarationsPreservePythonScopesAndEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
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

    private const string TargetIo = """
class Box:
 def __getattr__(self,name):return record('lookup',7)
 def __getitem__(self,index):return record('lookup',7)
def record(stage,value):
 with open(stage+'.txt','a') as target:target.write(stage)
 return value
def base():return record('base',Box())
def key():return record('index',9999)
def annotation():return record('annotation',int)
def helper():
 unused:int
 base().value:annotation()
 base()[key()]:annotation()
 return record('body',7)
print(helper())
""";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetIoEvaluatesBasesAndIndexWithoutLookupOrAnnotation(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(TargetIo, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        foreach (var stage in new[] { "base", "index", "annotation", "lookup", "body" })
        {
            if (delayed is not null) delayed.SeedFile("/" + stage + ".txt", "before");
            else mock!.SeedFile("/" + stage + ".txt", "before");
        }
        using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("7\n", result.StandardOutput);
            foreach (var stage in new[] { "base", "index", "annotation", "lookup", "body" })
            {
                var suffix = stage == "base" ? "basebase" : stage is "index" or "body" ? stage : "";
                Assert.Equal("before" + suffix, delayed?.ReadText("/" + stage + ".txt") ?? mock!.ReadText("/" + stage + ".txt"));
            }
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("index")]
    [InlineData("body")]
    public async Task TargetIoCancellationStopsAndDrainsBeforeRefund(string pausedStage)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(TargetIo, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        string[] stages = ["base", "index", "annotation", "lookup", "body"];
        foreach (var stage in stages) host.SeedFile("/" + stage + ".txt", "before");
        var entered = host.PauseWriteUntilCancellation("/" + pausedStage + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Execution completed before target I/O suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            foreach (var stage in stages)
            {
                var suffix = stage == "base" && pausedStage != "base" ? "basebase"
                    : stage == "index" && pausedStage == "body" ? "index" : "";
                Assert.Equal("before" + suffix, host.ReadText("/" + stage + ".txt"));
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedTargetStorageCannotRunIndexAnnotationOrBody(bool asynchronous)
    {
        const string source = """
def base():
 print('base-before')
 try:unused=[0]*8192
 except MemoryError:pass
 return []
def key():print('index');return 0
def annotation():print('annotation');return int
def helper():
 unused:int
 base()[key()]:annotation()
 print('body')
helper()
""";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 64 * 1024 };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Equal("base-before\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task EscapingClosureValueStaysFundedAcrossExecutions()
    {
        const string source = """
def produce():
 value:object
 value=['a'*4096]
 def read():return value
 return read
reader=produce()
return reader()
""";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var first = script.Run(new MockLythonHost()))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var values = Assert.IsAssignableFrom<System.Collections.IEnumerable>(first.ReturnValue).Cast<object?>().ToArray();
            Assert.Equal(new string('a', 4096), Assert.Single(values));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            using (var second = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
