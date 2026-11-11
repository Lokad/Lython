using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExceptionArgumentAdmissionTests
{
    [Fact]
    public void RefusedExceptionArgumentsDoNotAllocateTheirBackingFirst()
    {
        const long limit = 2 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            Assert.True(context.TryGetBuiltin("Exception", out var value));
            var callable = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
            var span = new LythonSourceSpan(0, 1, 0, 1);
            _ = callable.Invoke([], span, context); // Warm the constructor and its tuple factory.
            var arguments = new CallArgumentValue[4096];
            Array.Fill(arguments, CallArgumentValue.Positional(PyNone.Instance));
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out var blocker));
            using (blocker)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                LythonRuntimeException? failure = null;
                try { _ = callable.Invoke(arguments, span, context); }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.NotNull(failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
                Assert.Same(span, failure.Span);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                // The refused call can construct its error, but must not first
                // allocate a value vector proportional to the prebuilt inputs.
#if !DEBUG
                Assert.True(allocated < 24 * 1024, $"Refused arguments allocated {allocated} bytes.");
#endif
                Assert.All(arguments, argument => Assert.Same(PyNone.Instance, argument.Value));
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
