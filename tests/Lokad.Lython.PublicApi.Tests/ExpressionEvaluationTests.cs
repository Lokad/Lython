using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpressionEvaluationTests
{
    public static IEnumerable<object[]> Cases => new object[][]
    {
        new object[] { "eager_reads", "x=10\ndef change():\n global x\n x=40\n return 2\na=x+change()\nb=change()+x\nprint(a,b,x)", "12 42 40\n" },
        new object[] { "protocol_rebind", "class Left:\n def __add__(self,other):\n  global right\n  right=99\n  return other\nleft=Left()\nright={'value':7}\nresult=left+right\nprint(result['value'],right)", "7 99\n" },
        new object[] { "closure_store", "def outer():\n total=0\n def add(n):\n  nonlocal total\n  total+=n\n  return total\n for n in range(5): add(n)\n return total\nprint(outer())", "10\n" },
        new object[] { "dictionary_values", "d={'value':4}\na=d['value']+2\nb=2+d['value']\nd['value']=-a\nprint(a,b,d['value'])", "6 6 -6\n" },
        new object[] { "operators", "a=3\nb=5\nx=a*b\ny=b<<a\nz=-x\na+=b\nb-=3\nprint(x,y,z,a,b)", "15 40 -15 8 2\n" },
        new object[] { "unbound_read", "def f():\n for value in []: pass\n try: result=value+1\n except UnboundLocalError: print('unbound')\n return 2\nprint(f())", "unbound\n2\n" },
        new object[] { "visible_locals", "def f():\n value=4\n value=value+3\n print(locals()['value'])\nf()", "7\n" },
        new object[] { "generator", "def values():\n n=0\n while n<3:\n  n+=1\n  yield n\nprint(list(values()))", "[1, 2, 3]\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExpressionsKeepCaptureOrderBindingsAndCleanup(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { MemoryBudget = budget };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CapturedAliasSurvivesProtocolRebindingAndActualSuspension()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(SuspendingAddition, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/pause.txt", "ready");
            using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("7 99\n", result.StandardOutput);
                Assert.True(host.CompletedAsynchronously > 0);
            }
            Assert.Equal(1, host.ClosedFileCount);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SuspendedOperandsKeepOwnersUntilCancellationAndAwaitedCleanup()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var script = new LythonEngine().Compile(SuspendingAddition, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/pause.txt", "ready");
        var entered = host.PauseReadUntilCancellation("/pause.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.True(budget.ReservedBytes > baseline);
            script.Dispose();
            Assert.True(budget.ReservedBytes > 0);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.False(result.Success);
            Assert.Equal("", result.StandardOutput);
            Assert.Contains("execution canceled", result.Failure?.Message);
            Assert.Equal(1, host.ClosedFileCount);
        }
        finally
        {
            cancellation.Cancel();
            try { (result ?? await pending.WaitAsync(TimeSpan.FromSeconds(30))).Dispose(); }
            finally { script.Dispose(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusalInsideOperatorCannotPublishAReplacementReturn(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            class Left:
                def __add__(self, other):
                    try: bytes(200000)
                    finally: return 123
            left=Left()
            print('before')
            value=left+1
            print('after',value)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("before\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string SuspendingAddition = """
        class Left:
            def __add__(self, other):
                global right
                right=99
                with open('pause.txt') as source:
                    source.read()
                return other
        left=Left()
        right={'value':7}
        result=left+right
        print(result['value'],right)
        """;
}
