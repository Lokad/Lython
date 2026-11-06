using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class TextOperationProvenanceTests
{
    [Fact]
    public void RefusedUnicodeIndexStorageReportsTheRequestingOperation()
    {
        const long limit = 65536;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var birth = new LythonSourceSpan(1, 1, 1, 9);
            var operation = new LythonSourceSpan(7, 3, 7, 11);
            var text = PyString.FromString(new string('é', 64), context.MemoryGovernor, birth);
            var baseline = budget.ReservedBytes;
            var storage = text.CommittedOwnedBytes;
            using (context.MemoryGovernor.ReserveTemporary(limit - baseline, null))
            using (PyStructuralGuard.PushAmbient(context, operation))
            {
                var error = Assert.Throws<LythonRuntimeException>(() => text.Index(0));
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(operation, error.Span);
                Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(storage, text.CommittedOwnedBytes);
                Assert.Equal(limit, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
