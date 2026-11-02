using System.Text;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class GovernedOutputCompletionTests
{
    [Theory]
    [InlineData(4, false), InlineData(32, false)]
    [InlineData(4, true), InlineData(32, true)]
    public void SnapshotAndConsumedStringRemainIndependentOfBuilderReuse(int capacity, bool governed)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var builder = governed ? new GovernedByteBuilder(context.MemoryGovernor, null, capacity)
                : new GovernedByteBuilder(capacity);
            builder.Append(Encoding.UTF8.GetBytes("σé"));
            var snapshot = builder.ToPyString();
            builder.Truncate(0);
            builder.Append(Encoding.UTF8.GetBytes("éσ"));
            var consumed = builder.ToPyStringAndRelease();
            Assert.Equal(0, builder.Length);
            Assert.Equal(0, builder.CommittedCapacity);
            builder.AppendAscii("next");
            Assert.Equal("σé", snapshot.AsString());
            Assert.Equal("éσ", consumed.AsString());
            Assert.Equal(2, consumed.Length);
            builder.Release();
            if (governed)
            {
                Assert.True(budget.ReservedBytes >= snapshot.CommittedOwnedBytes + consumed.CommittedOwnedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(4, false), InlineData(32, false)]
    [InlineData(4, true), InlineData(32, true)]
    public void ConsumedBytesRemainFundedAndUnchangedDuringBuilderReuse(int capacity, bool rawArray)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var builder = new GovernedByteBuilder(context.MemoryGovernor, null, capacity);
            builder.Append([0, 255, 1, 128]);
            ReadOnlyMemory<byte> result = rawArray ? builder.ToArrayAndRelease() : builder.ToPyBytesAndRelease().Memory;
            var retained = budget.ReservedBytes;
            Assert.True(retained >= PyBytes.EstimateApproximateBytes(4));
            Assert.Equal(0, builder.CommittedCapacity);
            builder.AppendAscii("next");
            builder.Release();
            Assert.Equal(new byte[] { 0, 255, 1, 128 }, result.ToArray());
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0), InlineData(1), InlineData(2)]
    public void RefusedCompletionPreservesExistingOwnershipUntilCleanup(int kind)
    {
        const long capacity = 1024 * 1024;
        var budget = new LythonMemoryBudget(capacity);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var builder = new GovernedByteBuilder(context.MemoryGovernor, null, 8192);
            builder.AppendRepeated((byte)'a', 8192);
            Assert.True(budget.TryReserve(capacity - budget.ReservedBytes, out var blocker));
            using (blocker)
            {
                var failure = Assert.Throws<LythonRuntimeException>(() =>
                {
                    if (kind == 0) builder.ToPyStringAndRelease();
                    else if (kind == 1) builder.ToPyBytesAndRelease();
                    else builder.ToArrayAndRelease();
                });
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                // String/raw completion leaves its still-owned working input
                // intact on refusal. Bytes completion already consumes on failure.
                Assert.Equal(kind == 1 ? 0 : 8192, builder.Length);
                Assert.Equal(kind == 1 ? 0 : 8192, builder.CommittedCapacity);
                builder.Release();
                Assert.Equal(0, context.MemoryGovernor.CurrentAccountedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
