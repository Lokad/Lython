using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using System.Numerics;

namespace Lokad.Lython.Tests;

public sealed class IterationWorkspaceLifetimeTests
{
    [Fact]
    public void RefusedTransferStopsBeforeInvokingTheReplacement()
    {
        const string source = """
            convert = int
            def quantity(row):
                return convert(row['quantity'])
            """;
        var executable = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        var root = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = root.CreateScope();
        var budget = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget });
        try
        {
            LythonRuntime.ExecuteExecutableCodeObject(executable, context, default, null);
            var callback = Assert.IsType<PyExecutableFunction>(context.Variables["quantity"]);
            var replacement = new CallWitness();
            var span = new LythonSourceSpan(0, 0, 1, 1);
            Assert.True(callback.CanBorrowIterationStorage(context, span));
            LythonRuntime.StoreName("convert", replacement, callback.DefinitionContext, span);
            var row = new PyDict(context.MemoryGovernor, span);
            row.SetItem(PyString.FromString("quantity"), new BigInteger(17));
            var bytes = MemoryGovernor.WorkspaceBytes(callback.CodeObject.ValueStorageCapacity);
            budget.RefuseGrowth = growth => growth == bytes;

            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                LythonRuntime.IterationOperands storage = default;
                storage[0] = LythonRuntime.ExecutableSlot.FromObject(row);
                LythonRuntime.StartIterationCallback(callback, storage, 1, borrowStorage: true, span, context,
                    asynchronous: true, out _);
            });
            Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(bytes, error.Attribution?.Requested);
            Assert.False(replacement.Invoked);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        finally { context.State.CloseResources(); }
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LateBoundCallbackKeepsOperandsThroughSuspension(bool starmap, bool cancel)
    {
        var invocation = starmap ? "starmap(quantity, [(row,)])" : "map(quantity, [row])";
        var source = $$"""
            from itertools import starmap
            convert = int
            count = 0
            kept = ['alias']
            row = {'quantity': kept}

            def quantity(row):
                return convert(row['quantity'])

            def replacement(value):
                global count
                count += 1
                with open('data.txt') as stream:
                    text = stream.read()
                return value, text

            convert = replacement
            value, text = next({{invocation}})
            print(value is kept, text, count)
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/data.txt", "read");
        var reading = cancel ? host.PauseReadUntilCancellation("/data.txt") : null;
        var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.BeforeFileClose = () =>
        {
            closing.TrySetResult();
            return new ValueTask(release.Task);
        };

        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            if (reading is not null)
            {
                await reading.WaitAsync(TimeSpan.FromSeconds(5));
                cancellation.Cancel();
            }
            await closing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            release.TrySetResult();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(!cancel, result.Success);
            if (cancel)
            {
                Assert.Contains("execution canceled", result.Failure?.Message ?? "");
                Assert.Empty(result.StandardOutput);
            }
            else Assert.Equal("True read 1\n", result.StandardOutput);
            Assert.Equal(1, host.ClosedFileCount);
        }
        finally
        {
            cancellation.Cancel();
            release.TrySetResult();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class CallWitness : LythonRuntime.ICallable
    {
        internal bool Invoked { get; private set; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Invoked = true;
            return PyNone.Instance;
        }
    }
}
