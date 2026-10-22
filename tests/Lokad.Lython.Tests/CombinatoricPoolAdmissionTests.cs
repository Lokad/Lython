using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CombinatoricPoolAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in new[] { "product", "combinations", "combinations_with_replacement", "permutations" })
            foreach (var asynchronous in new[] { false, true })
                foreach (var known in new[] { false, true })
                    yield return [name, asynchronous, known];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void InputCapacityIsAdmittedBeforeProportionalAllocation(string name, bool asynchronous, bool known)
    {
        var module = (PyModule)typeof(LythonRuntime).GetNestedType("ItertoolsModule", BindingFlags.NonPublic)!
            .GetField("Instance")!.GetValue(null)!;
        Assert.True(module.TryGetMember(name, out var member));
        var factory = Assert.IsAssignableFrom<LythonRuntime.ICallable>(member);
        CallArgumentValue[] Args(object input) => name == "product"
            ? [CallArgumentValue.Positional(input)]
            : [CallArgumentValue.Positional(input), CallArgumentValue.Positional(new BigInteger(2))];
        // Initialize signatures outside the allocation window.
        factory.Invoke(Args(new PyTuple(Array.Empty<object>())), Span,
            new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions()));

        var inputs = new LythonMemoryBudget();
        var operation = new LythonMemoryBudget(32768);
        using (var inputOwner = inputs.CreateScope())
        using (var owner = operation.CreateScope())
        {
            var governor = new MemoryGovernor(inputOwner);
            object source = known
                ? new PyTuple(Enumerable.Repeat<object>(BigInteger.One, 25000), governor, Span)
                : new PyRange(BigInteger.Zero, new BigInteger(25000), BigInteger.One, governor, Span);
            var arguments = Args(source);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var inputBaseline = inputs.ReservedBytes;
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (!asynchronous) factory.Invoke(arguments, Span, context);
                else
                {
                    var pending = factory.InvokeAsync(arguments, Span, context);
                    Assert.True(pending.IsCompleted); // These inputs do not suspend.
                    pending.GetAwaiter().GetResult();
                }
            });
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 131072);
#endif
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(inputBaseline, inputs.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, operation.ReservedBytes);
        }
        Assert.Equal(0, inputs.ReservedBytes);
        Assert.Equal(0, operation.ReservedBytes);
    }
}
