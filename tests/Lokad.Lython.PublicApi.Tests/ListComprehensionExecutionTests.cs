using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class ListComprehensionExecutionTests
{
    // Independent CPython goldens; these also exercise helpers and module slots.
    public static TheoryData<string, string> Programs => new()
    {
        { "x=7\nprint([x for x in [x]], x)", "[7] 7\n" },
        { "x=7\ndef f():\n    global x\n    return [x for x in range(3)]\nprint(f(),x)", "[0, 1, 2] 7\n" },
        { "def f():\n    x=7\n    def g():\n        nonlocal x\n        return [x for x in range(3)]\n    return g(),x\nprint(f())", "([0, 1, 2], 7)\n" },
        { "print([a+sum(b) for a,*b in [(1,2,3),(4,5)]])", "[6, 9]\n" },
        { "x=7\ny=8\ntry:\n    values=[10//(x-y) for x in range(3) for y in range(3)]\nexcept ZeroDivisionError:\n    print(x,y)\nprint([x for x in range(2)],x,y)", "7 8\n[0, 1] 7 8\n" },
        { "def f():\n    total=0\n    values=[(total:=total+x+y) for x in range(3) for y in range(x) if y<2]\n    return total,values\nprint(f())", "(6, [1, 3, 6])\n" },
        { "x=7\nprint([[x for x in range(2)] for x in range(3)],x)", "[[0, 1], [0, 1], [0, 1]] 7\n" },
        { "x=7\nreaders=[lambda:x for x in range(3)]\nprint([read() for read in readers],x)", "[2, 2, 2] 7\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task LexicalBindingsAndAbruptCleanupMatchPython(string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
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

    [Fact]
    public async Task YieldingBodyKeepsPublishedWritesAndFunding()
    {
        const string source = "def emit(x):\n    with open('out.txt','a') as target:\n        target.write(str(x))\n    return x\nx=99\nvalues=[emit(x) for x in range(3)]\nprint(values,x)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/out.txt", "");
        using (var result = await script.RunAsync(host, new LythonRunOptions { }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[0, 1, 2] 99\n", result.StandardOutput);
            Assert.Equal("012", host.ReadText("/out.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationDrainsTheYieldingBodyBeforeReturningFunding()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("def emit(x):\n    with open('out.txt','w') as target:\n        target.write(str(x))\n    return x\nvalues=[emit(x) for x in range(3)]", budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
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
