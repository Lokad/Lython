using System.Runtime.InteropServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextWindowFundingTests
{
    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public async Task NextWindowAdmissionFailureKeepsPriorLineAndPrecedesHostRead(bool asynchronous, bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var host = new MockLythonHost();
            var text = new string('a', 63) + "\n";
            host.SeedFile("/input", text + new string('b', 40) + "\n");
            var context = new LythonRuntime.ExecutionContext(host,
                new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            using var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 64);
            var line = asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
            var position = reader.Position;
            var reads = host.RangedReadCount;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var reached = false;
            if (cancel) hook.AfterGrowth = _ => { reached = true; cancellation.Cancel(); };
            else hook.RefuseGrowth = _ => { reached = true; return true; };
            try
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    _ = asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
                });
                Assert.True(reached);
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                Assert.Equal(position, reader.Position);
                Assert.Equal(reads, host.RangedReadCount);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(text, line.AsString());
            }
            finally
            {
                hook.AfterGrowth = null;
                hook.RefuseGrowth = null;
                reader.Release();
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscardingUnpublishedLineCannotRefundAnotherLinesBacking(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var host = new MockLythonHost();
            var firstText = new string('a', 40) + "\n";
            var secondText = new string('b', 40) + "\n";
            host.SeedFile("/input", firstText + secondText);
            var context = new LythonRuntime.ExecutionContext(host, new() { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var baseline = governor.CurrentCommittedBytes;
            var file = new OpenedByteFile(context.State);
            await file.OpenAsync("/input", new(LythonFileAccess.Read), null, asynchronous);
            using var reader = new LythonRuntime.ExecutionContext.ChunkedTextFileReadState(file, context,
                LythonRuntime.TextEncodingMode.Utf8, LythonRuntime.TextErrorMode.Strict,
                LythonRuntime.TextNewlineMode.TranslateUniversal, 4096);
            var first = asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
            var second = asynchronous ? await reader.ReadLineAsync(-1) : reader.ReadLine(-1);
            reader.Release();
            Assert.Equal(firstText, first.AsString());
            governor.RefundUnpublishedValue(first);
            Assert.Equal(secondText, second.AsString());
            Assert.True(MemoryMarshal.TryGetArray(second.Utf8Bytes, out var storage));
            // Fund actual retained capacity, whether independent or shared.
            Assert.True(governor.CurrentCommittedBytes - baseline >= storage.Array!.LongLength + PyString.EstimateApproximateBytes(0));
            Assert.Equal(0, governor.CurrentReservedBytes);
            context.State.CloseResources();
            GC.KeepAlive(second);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
