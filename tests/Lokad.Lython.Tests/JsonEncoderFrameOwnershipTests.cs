using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonEncoderFrameOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FrameGrowthUnwindsWithoutInvalidatingPublishedChunks(bool asynchronous, bool cancel)
    {
        var budget = new LythonMemoryBudget(16 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            {
                MemoryBudget = hook,
                CancellationToken = cancellation.Token
            });
            object nested = PyNone.Instance;
            for (var depth = 0; depth < 10; depth++)
                nested = new PyList([nested]);
            var span = new LythonSourceSpan(1, 1, 1, 2);
            var iterator = new LythonRuntime.JsonEncodeIterator(nested, LythonRuntime.JsonDumpOptions.Default, context, span);
            async ValueTask<PyIterationResult> Next() => asynchronous
                ? await iterator.TryMoveNextAsync() : iterator.TryMoveNext();

            var prefix = new List<PyString>();
            for (var index = 0; index < 4; index++)
                prefix.Add(Assert.IsType<PyString>((await Next()).Value));
            var before = context.MemoryGovernor.CurrentAccountedBytes;

            // The next container opens beyond the existing frame capacity.
            // Small result allocations cannot trigger the fault.
            var growths = 0;
            if (cancel)
                hook.AfterGrowth = bytes =>
                {
                    if (bytes <= 256) return;
                    growths++;
                    cancellation.Cancel();
                };
            else
                hook.RefuseGrowth = bytes =>
                {
                    if (bytes <= 256) return false;
                    growths++;
                    return true;
                };

            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await Next(); });
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                failure.Attribution?.Reason);
            Assert.Equal(1, growths);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(context.MemoryGovernor.CurrentAccountedBytes < before);
            Assert.All(prefix, chunk => Assert.Equal("[", chunk.AsString()));
        }

        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(16 * 1024, out var reused));
        reused!.Dispose();
    }
}
