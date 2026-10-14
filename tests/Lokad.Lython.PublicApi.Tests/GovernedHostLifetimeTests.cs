using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GovernedHostLifetimeTests
{
    [Fact]
    public async Task NestedHostExecutionSharesTheActiveAllowanceAndReturnsItsScratch()
    {
        var budget = new LythonMemoryBudget(512000);
        var host = new MockLythonHost();
        host.UseSubprocessHandler(async (request, token) =>
        {
            Assert.NotNull(request.MemoryBudget);
            var retainedParent = budget.ReservedBytes;
            Assert.True(retainedParent >= 16000);
            using var child = request.MemoryBudget.CreateScope();
            using var output = await new LythonEngine().RunAsync("return 'y' * 16000\n", new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = child, ResultMemoryBudget = request.MemoryBudget }, token);
            Assert.True(output.Success);
            Assert.True(budget.ReservedBytes > retainedParent);
            Assert.Equal(16000, Assert.IsType<string>(output.ReturnValue).Length);
            output.Dispose();
            child.Dispose();
            Assert.Equal(retainedParent, budget.ReservedBytes);
            return new LythonSubprocessResult(0, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);
        });
        using var script = new LythonEngine().Compile("import subprocess\nheld = 'x' * 16000\nsubprocess.run(['nested'])\nreturn len(held)\n");
        using var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(16000), result.ReturnValue);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task OverlappingExecutionsRefuseBeforeAllocationAndCancellationWaitsForBorrowers()
    {
        var budget = new LythonMemoryBudget(100000);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new MockLythonHost();
        host.UseSubprocessHandler(async (request, token) =>
        {
            using var child = request.MemoryBudget!.CreateScope();
            Assert.True(child.TryReserve(64000, out _));
            var borrowed = new byte[64000];
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException)
            {
                cancelling.SetResult();
                await cleanup.Task;
            }
            GC.KeepAlive(borrowed);
            throw new OperationCanceledException(token);
        });
        using var cancellation = new CancellationTokenSource();
        using var first = new LythonEngine().Compile("import subprocess\nheld = 'x' * 16000\nsubprocess.run(['hold'])\n");
        var pending = first.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.InRange(budget.ReservedBytes, 80000, 100000);
            using var second = new LythonEngine().Compile("return 'z' * 32000\n");
            using var refused = await second.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget });
            Assert.False(refused.Success);
            Assert.Equal("MemoryError", refused.Failure?.ExceptionType);
            refused.Dispose();
            var held = budget.ReservedBytes;
            cancellation.Cancel();
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.Equal(held, budget.ReservedBytes);
        }
        finally { cancellation.Cancel(); cleanup.TrySetResult(); }
        using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(result.Success);
        result.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
