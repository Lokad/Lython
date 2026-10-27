using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TypingAliasAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0,0,0,0);

    [Fact]
    public void CanonicalArgumentsNeedNoLargeFactoryOrGetArgsCopy()
    {
        const int count = 32768;
        var inputs = new LythonMemoryBudget();
        var operations = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var operationOwner = operations.CreateScope())
        {
            var governor = new MemoryGovernor(inputOwner);
            var arguments = new PyTuple(Enumerable.Range(0,count).Select(i => (object)new BigInteger(i)),governor,Span);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions { MemoryBudget=operationOwner });
            using var ambient = PyStructuralGuard.PushAmbient(context,Span);
            var definition = PyTyping.Definition("Tuple");
            _ = definition.GetSubscript(new PyTuple([BigInteger.One],context.MemoryGovernor,Span),Span);
            context.TryGetBuiltin("tuple",out _);
            var inputBalance = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var alias = Assert.IsType<PyGenericAlias>(definition.GetSubscript(arguments,Span));
            var retained = operations.ReservedBytes;
            var inspected = PyTyping.GetArgs([CallArgumentValue.Positional(alias)],Span,context);
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread()-before < count*IntPtr.Size);
#endif
            Assert.Same(arguments,alias.Arguments);
            Assert.Same(arguments,inspected);
            Assert.Equal(inputBalance,inputs.ReservedBytes);
            Assert.Equal(retained,operations.ReservedBytes);
            Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
        }
        Assert.Equal(0,inputs.ReservedBytes);
        Assert.Equal(0,operations.ReservedBytes);
    }

    [Fact]
    public void RenderingRefusesBeforeTheMajorOutputOrClrConversion()
    {
        const int length = 262144;
        var inputs = new LythonMemoryBudget();
        var operations = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var operationOwner = operations.CreateScope())
        {
            var inputContext = new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions { MemoryBudget=inputOwner });
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),new LythonRunOptions { MemoryBudget=operationOwner });
            var definition = PyTyping.Definition("List");
            using (PyStructuralGuard.PushAmbient(inputContext,Span))
                _ = PyRendering.ToReprPyString(definition.GetSubscript(PyString.FromString("small",inputContext.MemoryGovernor,Span),Span),new(inputContext));
            var payload = PyString.FromString(new string('x',length),inputContext.MemoryGovernor,Span);
            object alias;
            using (PyStructuralGuard.PushAmbient(inputContext,Span)) alias = definition.GetSubscript(payload,Span);
            var inputBalance = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() => PyRendering.ToReprPyString(alias,new(context)));
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread()-before < length);
#endif
            Assert.Same(error,context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance,error.Attribution?.Reason);
            Assert.Equal(inputBalance,inputs.ReservedBytes);
            Assert.Equal(0,context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0,operations.ReservedBytes);
        }
        Assert.Equal(0,inputs.ReservedBytes);
    }
}
