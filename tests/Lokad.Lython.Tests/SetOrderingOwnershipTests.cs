using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SetOrderingOwnershipTests
{
    [Fact]
    public async Task SharedAllowanceRefusesComparisonScratchBeforeEquality()
    {
        const string source = "events=[]\nclass Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):events.append('entered');return self.n==other.n\nleft={Key(0)}\nright={Key(0),Key(1)}";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        var left = Assert.IsType<PySet>(context.Variables["left"]);
        var right = Assert.IsType<PySet>(context.Variables["right"]);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out var occupied));
        using (occupied)
        {
            var baseline = budget.ReservedBytes;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await left.IsSubsetOfAsync(right, context, new LythonSourceSpan(0, 1, 1, 1)));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            Assert.Empty(Assert.IsType<PyList>(context.Variables["events"]));
            Assert.Equal(1, left.Count);
            Assert.Equal(2, right.Count);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearingSetsKeepsSuspendedComparisonStorageFunded(bool truth)
    {
        var callback = "left.clear();right.clear()\n with open('hold.txt','w') as target:target.write('held')\n return True";
        var source = (truth ? "class Truth:\n def __bool__(self):\n  " + callback.Replace("\n", "\n ") + "\n" : "") +
            "class Key:\n def __init__(self,n):self.n=n\n def __hash__(self):return self.n\n def __eq__(self,other):\n  " +
            (truth ? "return Truth()" : callback.Replace("\n", "\n ")) +
            "\nleft={Key(0)}\nright={Key(0),Key(1)}\nreturn left<right";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/hold.txt");
        var context = new Context(host, new LythonRunOptions
            { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var frontend = LythonFrontend.Compile(source);
        Assert.True(frontend.Script is not null, string.Join("; ", frontend.Diagnostics.Select(d => d.Message)));
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        Exception? failure;
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(entered, first);
            Assert.False(pending.IsCompleted);
            Assert.Empty(Assert.IsType<PySet>(context.Variables["left"]));
            Assert.Empty(Assert.IsType<PySet>(context.Variables["right"]));
            // Both backing stores have been relinquished. Copied source and
            // candidate entries still belong to the pending comparison.
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 96);
        }
        finally
        {
            cancellation.Cancel();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
