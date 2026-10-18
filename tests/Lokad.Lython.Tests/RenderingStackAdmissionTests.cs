using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RenderingStackAdmissionTests
{
    [Theory]
    [InlineData("list")]
    [InlineData("tuple")]
    [InlineData("dict")]
    public void ChildRenderingRequiresAdmissionAndUnwinds(string kind)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var dictionary = new PyDict(); dictionary.SetItem(BigInteger.One, BigInteger.One);
        object value = kind switch { "list" => new PyList([BigInteger.One]), "tuple" => new PyTuple([BigInteger.One]), _ => dictionary };
        context.Limits.CurrentInterpreterDepth = LythonRuntime.ExecutionLimits.MaxInterpreterDepth - 1;
        try
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => PyRendering.ToReprPyString(value, new PyRenderingContext(context)));
            Assert.Equal(LythonFailureReason.InterpreterStackLimit, failure.Attribution?.Reason);
            Assert.Equal(LythonRuntime.ExecutionLimits.MaxInterpreterDepth, failure.Attribution?.Limit);
            Assert.Equal(LythonRuntime.ExecutionLimits.MaxInterpreterDepth - 1, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { context.Limits.CurrentInterpreterDepth = 0; }
        owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
    }
}
