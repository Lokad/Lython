using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using System.Reflection;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LambdaExecutableTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosureCaptureTransfersAdmittedCellsOnlyAfterArrayAdmission(bool refuse)
    {
        var syntax = LythonFrontend.Compile("def outer(left, right):\n    return lambda:left+right",regexProvider:Utf8RegexProvider.Instance).Script!;
        var module = ExecutableScript.Compile(syntax).EntryPoint;
        var outer = Assert.Single(module.FunctionBindings()).CodeObject!;
        var lambda = Assert.Single(outer.Constants.OfType<ExecutableCreateLambda>()).CodeObject!;
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
        var governor = context.MemoryGovernor;
        var beforeFrame = governor.CurrentAccountedBytes;
        var cellBytes = 2 * LythonRuntime.ExecutableCell.StorageBytes;
        var stack = new LythonRuntime.ExecutableValueStorage(outer.ValueStorageCapacity, outer.FirstTemporarySlot,
            outer.LastOwnedTemporarySlot, context, null, cellBytes, null, null);
        var first = new LythonRuntime.ExecutableCell(BigInteger.One);
        var second = new LythonRuntime.ExecutableCell(BigInteger.Zero);
        stack.Values[outer.LocalNameToSlot["left"]].Object = first;
        stack.Values[outer.LocalNameToSlot["right"]].Object = second;
        context._execution = new() { Code = outer, Locals = stack.Values, Stack = stack };
        context.EnterExecutableSlots();
        var capture = typeof(LythonRuntime).GetMethod("CaptureExecutableClosures", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ExecutableCodeObject, Context, LythonSourceSpan, LythonRuntime.ExecutableCaptures>>();
        var baseline = governor.CurrentAccountedBytes;
        var span = syntax.Statements[0].Span;
        try
        {
            if (refuse)
            {
                var padding = 1024 * 1024 - baseline;
                governor.Reserve(padding, null);
                governor.Commit(padding);
                var failure = Assert.Throws<LythonRuntimeException>(() => capture(lambda, context, span));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(1024 * 1024, governor.CurrentAccountedBytes);
                governor.Release(padding);
                Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => capture(lambda, context, span)));
                Assert.False(first.RetentionCharged);
                Assert.False(second.RetentionCharged);
                Assert.Equal(baseline, governor.CurrentAccountedBytes);
                context._execution.Stack.ReleaseStorage();
                Assert.Equal(beforeFrame, governor.CurrentAccountedBytes);
            }
            else
            {
                var captured = capture(lambda, context, span);
                Assert.Same(first, captured.GetCell(lambda.ClosureNameToSlot["left"]));
                Assert.Same(second, captured.GetCell(lambda.ClosureNameToSlot["right"]));
                var arrayBytes = governor.CurrentAccountedBytes - baseline;
                Assert.InRange(arrayBytes, 1, LythonRuntime.ExecutableCell.StorageBytes - 1);
                context._execution.Stack.ReleaseStorage();
                Assert.Equal(beforeFrame + arrayBytes + cellBytes, governor.CurrentAccountedBytes);
            }
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        finally
        {
            context._execution.Stack.ReleaseStorage();
            context.LeaveExecutableSlots();
            context.State.CloseResources();
        }
    }

    [Theory]
    [InlineData("lambda value:value+1")]
    [InlineData("lambda value:f'{value:02d}'")]
    [InlineData("lambda target,values:target(*values)")]
    [InlineData("lambda value:lambda step:value+step")]
    public void LambdaBodiesAreExecutableWithoutForcedMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile("result=" + expression,regexProvider:Utf8RegexProvider.Instance).Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        Check(code);
        Assert.NotEmpty(code.Constants.OfType<ExecutableCreateLambda>());

        static void Check(ExecutableCodeObject code)
        {
            Assert.False(code.RequiresLocalVariableMirroring);
            foreach (var lambda in code.Constants.OfType<ExecutableCreateLambda>())
            {
                // Defaults-only preparation with a lowered body does not qualify.
                Assert.NotNull(lambda.CodeObject);
                Check(lambda.CodeObject);
            }
        }
    }
}
