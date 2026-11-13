using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ImmutableRepetitionTests
{
    private static readonly LythonSourceSpan Span = new(7, 12, 1, 8);

    public static IEnumerable<object[]> Kinds()
    {
        foreach (var kind in new[] { 0, 1, 2 })
        foreach (var asynchronous in new[] { false, true }) yield return [kind, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task UnownedInputStillRequiresAdmissionBeforeRepeatedStorage(int kind, bool asynchronous)
    {
        foreach (var owned in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(16 * 1024 * 1024);
            using (var owner = budget.CreateScope())
            {
                var gate = new AdmissionHookMemoryBudget(owner);
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = gate });
                var input = Input(kind, owned ? context.MemoryGovernor : null);
                var baseline = budget.ReservedBytes;
                long? beforeRefusal = null;
                var before = GC.GetAllocatedBytesForCurrentThread();
                gate.RefuseGrowth = _ => { beforeRefusal ??= GC.GetAllocatedBytesForCurrentThread() - before; return true; };
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Repeat(input, 131072, context, asynchronous));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(Span, error.Span);
                Assert.NotNull(beforeRefusal);
                Assert.True(beforeRefusal < 32768, $"Repeated storage allocated {beforeRefusal} bytes before refusal.");
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                context.State.CloseResources();
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task AdmissionCancellationReturnsUnpublishedStorage(int kind, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = gate, CancellationToken = cancellation.Token });
            var input = Input(kind, null);
            var baseline = budget.ReservedBytes;
            gate.AfterGrowth = _ => cancellation.Cancel();
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Repeat(input, 131072, context, asynchronous));
            Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task RepetitionPreservesChunkBoundariesAliasesAndRetainedFunding(int kind, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(16 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var input = Input(kind, null);
            var baseline = budget.ReservedBytes;
            var result = await Repeat(input, 4097, context, asynchronous);
            switch (result)
            {
                case PyBytes bytes:
                    Assert.Same(context.MemoryGovernor, bytes.OwnerMemoryGovernor);
                    Assert.Equal(Enumerable.Repeat(new byte[] { 97, 98, 99 }, 4097).SelectMany(x => x), bytes.Bytes.ToArray());
                    break;
                case PyString text:
                    Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
                    Assert.Equal(string.Concat(Enumerable.Repeat("é😀", 4097)), text.AsString());
                    break;
                case PyTuple tuple:
                    Assert.Same(context.MemoryGovernor, tuple.OwnerMemoryGovernor);
                    var original = Assert.IsType<PyTuple>(input);
                    Assert.Equal(4097, tuple.Count);
                    foreach (var value in tuple) Assert.Same(original[0], value);
                    break;
                default: Assert.Fail("Unexpected repeated value."); break;
            }
            Assert.True(budget.ReservedBytes > baseline);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Input(int kind, MemoryGovernor? governor) => kind switch
    {
        0 => governor is null ? new PyBytes([97, 98, 99]) : new PyBytes([97, 98, 99], governor, Span),
        1 => governor is null ? PyString.FromString("é😀") : PyString.FromString("é😀", governor, Span),
        _ => governor is null ? PyTuple.FromOwnedArray([new PyList()])
            : PyTuple.FromOwnedArray([new PyList()], governor, Span),
    };

    private static ValueTask<object> Repeat(object input, int count, LythonRuntime.ExecutionContext context, bool asynchronous)
        => asynchronous ? LythonRuntime.EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Multiply, input, new BigInteger(count), context, Span)
            : ValueTask.FromResult(LythonRuntime.EvaluateBinaryOperator(BinaryOperatorSyntax.Multiply, input, new BigInteger(count), context, Span));
}
