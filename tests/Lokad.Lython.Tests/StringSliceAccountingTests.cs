using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class StringSliceAccountingTests
{
    [Theory]
    [InlineData('x', 1)]
    [InlineData('x', 2)]
    [InlineData('é', 1)]
    [InlineData('é', 2)]
    public void AdmissionPrecedesAllocatingTheSlice(char scalar, int step)
    {
        var span = new LythonSourceSpan(2, 3, 4, 5);
        const int characters = 128 * 1024;
        const long limit = 1024 * 1024;
        var source = PyString.FromString(new string(scalar, characters));
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var warmup = PyString.FromString(new string(scalar, 8));
            Assert.IsType<PyString>(PyIndexing.ReadSliceCore(warmup, 0, 4, step, span, context));
            var afterWarmup = budget.ReservedBytes;
            using (context.MemoryGovernor.ReserveTemporary(limit - afterWarmup, span))
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var error = Assert.Throws<LythonRuntimeException>(() =>
                    PyIndexing.ReadSliceCore(source, 1, characters, step, span, context));
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Equal(span, error.Span);
                // Ordinary exception storage is small relative to one attempted
                // payload. Leave ample headroom for the test and failure path.
                Assert.True(allocated < 16 * 1024, $"Refused slice allocated {allocated} bytes.");
            }
            Assert.Equal(afterWarmup, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
