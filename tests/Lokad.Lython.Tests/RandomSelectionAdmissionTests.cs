using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class RandomSelectionAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false), InlineData(true)]
    public void DenialPrecedesCountCopyOrWideRangeArithmetic(bool range)
    {
        foreach (var measured in new[] { false, true })
        {
            var root = new LythonMemoryBudget(32768);
            var inputs = new LythonMemoryBudget();
            using (var inputOwner = inputs.CreateScope())
            using (var owner = root.CreateScope())
            {
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                var state = new PyRandomState();
                var module = new LythonRuntime.RandomModule(state);
                var inputGovernor = new MemoryGovernor(inputOwner);
                object[] args;
                if (range)
                {
                    var start = BigInteger.One << 262144;
                    args = [start, start + 1];
                    inputGovernor.Reserve(131200, Span); inputGovernor.Commit(131200);
                }
                else
                {
                    var values = Enumerable.Repeat<object>(BigInteger.One, 16384).ToArray();
                    args = [new PyList(values, inputGovernor, Span), BigInteger.Zero, new PyList(values, inputGovernor, Span)];
                }
                Assert.True(module.TryGetMember(range ? "randrange" : "sample", out var member));
                var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
                var call = range ? args.Select(CallArgumentValue.Positional).ToArray()
                    : new[] { CallArgumentValue.Positional(args[0]), CallArgumentValue.Positional(args[1]), CallArgumentValue.Keyword("counts", args[2]) };
                var balance = root.ReservedBytes;
                var inputBalance = inputs.ReservedBytes;
                var snapshot = state.Snapshot();
                var before = GC.GetAllocatedBytesForCurrentThread();
                var error = Assert.Throws<LythonRuntimeException>(() => callable.Invoke(call, Span, context));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(snapshot, state.Snapshot());
                Assert.Equal(balance, root.ReservedBytes);
                Assert.Equal(inputBalance, inputs.ReservedBytes);
#if !DEBUG
                if (measured) Assert.True(allocated < 32768, $"Refused operation allocated {allocated} bytes.");
#endif
            }
            Assert.Equal(0, root.ReservedBytes);
            Assert.Equal(0, inputs.ReservedBytes);
        }
    }
}
