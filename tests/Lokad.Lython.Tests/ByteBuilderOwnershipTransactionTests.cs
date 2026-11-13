using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ByteBuilderOwnershipTransactionTests
{
    private static readonly LythonSourceSpan Span = new(73, 3, 6, 4);

    [Theory]
    [InlineData(false), InlineData(true)]
    public void FailedGrowthRefundKeepsRetainedInputFunded(bool refusal)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var input = PyString.FromString(new string('R', 128), context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            using var builder = new GovernedByteBuilder(context.MemoryGovernor, Span, 8);
            builder.Append((byte)'a');
            var observed = false;
            hook.AfterShrink = _ =>
            {
                hook.AfterShrink = null;
                observed = true;
                if (refusal) context.MemoryGovernor.Reserve(2 * 1024 * 1024, Span);
                else throw new InvalidOperationException("controlled refund failure");
            };
            var error = Record.Exception(() => builder.Append(new byte[65]));
            Assert.True(observed);
            Assert.NotNull(error);
            if (refusal)
            {
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(Span, Assert.IsType<LythonRuntimeException>(error).Span);
            }
            else Assert.IsType<InvalidOperationException>(error);
            Assert.Equal(new byte[] { (byte)'a' }, builder.WrittenSpan.ToArray());
            builder.Release();
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(baseline, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(new string('R', 128), input.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void ReleaseRefundCannotReleaseAnotherOwnersFunding(bool throws)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var input = PyString.FromString(new string('R', 128), context.MemoryGovernor, Span);
            var baseline = budget.ReservedBytes;
            using var builder = new GovernedByteBuilder(context.MemoryGovernor, Span, 8);
            builder.Append((byte)'a');
            var observed = false;
            hook.AfterShrink = _ =>
            {
                hook.AfterShrink = null;
                observed = true;
                if (throws) throw new InvalidOperationException("controlled refund failure");
                builder.Release();
            };
            if (throws) Assert.Throws<InvalidOperationException>(builder.Release);
            else builder.Release();
            Assert.True(observed);
            builder.Release();
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(new string('R', 128), input.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
