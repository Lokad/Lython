using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BufferedLineAdmissionTests
{
    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task RefusedOrCancelledLineStorageDoesNotAdvanceTheReader(bool asynchronous, bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var host = new MockLythonHost();
            host.SeedFile("/input", "!first long café line\nsecond long retained line\n");
            var context = new LythonRuntime.ExecutionContext(host,
                new() { MemoryBudget = gate, CancellationToken = cancellation.Token });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            using var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 4096);
            var first = asynchronous ? await reader.ReadAsync(1) : reader.Read(1);
            var position = reader.Position;
            var reads = host.RangedReadCount;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            if (cancel) gate.AfterGrowth = _ => cancellation.Cancel();
            else gate.RefuseGrowth = _ => true;
            try
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    _ = asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
                });
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(position, reader.Position);
                Assert.Equal(reads, host.RangedReadCount);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal("!", first.AsString());
            }
            finally
            {
                gate.AfterGrowth = null;
                gate.RefuseGrowth = null;
                reader.Release();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
