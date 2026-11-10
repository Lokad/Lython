using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class EnumerateIndexAdmissionTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan Span = new(12, 3, 2, 4);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedWideIncrementDoesNotAllocateItsMagnitude(bool asynchronous)
    {
        // Warm native/exception bookkeeping separately. Refusal is terminal;
        // each observation owns a fresh execution and its own iterator.
        foreach (var measured in new[] { false, true })
        {
            const long limit = 8 * 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using (var owner = budget.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                    new LythonRunOptions { MemoryBudget = owner });
                var governor = context.MemoryGovernor;
                var start = (BigInteger)LythonRuntime.OwnFreshInteger(BigInteger.One << 524288, governor, Span);
                var source = new FiniteItems();
                var iterator = new PyEnumerateIterator(source, start, Span, context);
                Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 8192, out var blocker));
                using (blocker)
                {
                    var funded = budget.ReservedBytes;
                    var committed = governor.CurrentCommittedBytes;
                    LythonRuntimeException? failure = null;
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    try
                    {
                        if (asynchronous)
                        {
                            var pending = iterator.TryMoveNextAsync();
                            Assert.True(pending.IsCompleted);
                            await pending;
                        }
                        else iterator.TryMoveNext(out _);
                    }
                    catch (LythonRuntimeException error) { failure = error; }
                    var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                    if (measured) output.WriteLine($"Wide enumerate refusal allocated {allocated} bytes; async={asynchronous}.");
                    Assert.NotNull(failure);
                    Assert.Same(governor.AdmissionFailure, failure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                    Assert.Equal(Span, failure.Span);
                    Assert.Equal(1, source.Pulls);
                    Assert.Equal(committed, governor.CurrentCommittedBytes);
                    Assert.Equal(0, governor.CurrentReservedBytes);
                    Assert.Equal(funded, budget.ReservedBytes);
                    if (measured) Assert.True(allocated < 32768, $"Refused index increment allocated {allocated} bytes.");
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
            Assert.True(budget.TryReserve(limit, out var later));
            later.Dispose();
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WideIndexAliasesStayFundedAndExhaustionNeedsNoUnusedIncrement(bool asynchronous, bool negative)
    {
        const long limit = 8 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var governor = context.MemoryGovernor;
            var start = (BigInteger)LythonRuntime.OwnFreshInteger((negative ? -BigInteger.One : BigInteger.One) << 524288, governor, Span);
            var source = new FiniteItems(2);
            var iterator = new PyEnumerateIterator(source, start, Span, context);
            var first = await Pull();
            var second = await Pull();
            Assert.True(first.HasValue);
            Assert.True(second.HasValue);
            Assert.Equal(start, Assert.IsType<PyTuple>(first.Value)[0]);
            Assert.Equal(start + 1, Assert.IsType<PyTuple>(second.Value)[0]);
            Assert.True(budget.ReservedBytes >= 3L * start.GetByteCount());
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out var blocker));
            using (blocker)
            {
                Assert.False((await Pull()).HasValue);
                Assert.Null(governor.AdmissionFailure);
                Assert.Equal(3, source.Pulls);
                Assert.Equal(start, Assert.IsType<PyTuple>(first.Value)[0]);
                Assert.Equal(start + 1, Assert.IsType<PyTuple>(second.Value)[0]);
            }
            ValueTask<PyIterationResult> Pull() => asynchronous ? iterator.TryMoveNextAsync()
                : ValueTask.FromResult(iterator.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class FiniteItems(int count = 1) : PyIteratorBase
    {
        public int Pulls { get; private set; }
        public override bool TryMoveNext(out object value)
        {
            value = PyNone.Instance;
            return ++Pulls <= count;
        }
        public override ValueTask<PyIterationResult> TryMoveNextAsync()
            => ValueTask.FromResult(TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End);
        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<one item>");
    }
}
