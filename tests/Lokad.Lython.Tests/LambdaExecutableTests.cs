using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using System.Numerics;
using System.Reflection;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LambdaExecutableTests
{
    [Fact]
    public void DeniedClosureCaptureDoesNotPublishFundingMarksAndCanRetry()
    {
        var syntax = LythonFrontend.Compile("def outer(value):\n    return lambda:value").Script!;
        var module = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var outer = Assert.Single(module.Functions).CodeObject!;
        var lambda = Assert.Single(outer.Constants.OfType<ExecutableCreateLambda>()).CodeObject!;
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 1024 * 1024 });
        var values = new object[outer.LocalNames.Count];
        var cells = new LythonRuntime.ExecutableCell?[values.Length];
        var cell = new LythonRuntime.ExecutableCell(BigInteger.One);
        cells[outer.LocalNameToSlot["value"]] = cell;
        context.EnterExecutableSlots(new LythonRuntime.ExecutableFrameState(outer, values, cells, null));
        var capture = typeof(LythonRuntime).GetMethod("CaptureExecutableClosures", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ExecutableCodeObject, Context, LythonSourceSpan, LythonRuntime.ExecutableCell[]>>();
        var baseline = context.MemoryGovernor.CurrentAccountedBytes;
        var padding = 1024 * 1024 - baseline;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        try
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => capture(lambda, context, syntax.Statements[0].Span));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.False(cell.RetentionCharged);
            Assert.Equal(1024 * 1024, context.MemoryGovernor.CurrentAccountedBytes);
            context.MemoryGovernor.Release(padding);
            Assert.Same(cell, Assert.Single(capture(lambda, context, syntax.Statements[0].Span)));
            Assert.True(cell.RetentionCharged);
            Assert.True(context.MemoryGovernor.CurrentAccountedBytes > baseline);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally { context.LeaveExecutableSlots(null); }
    }

    [Theory]
    [InlineData("lambda value:value+1")]
    [InlineData("lambda value:f'{value:02d}'")]
    [InlineData("lambda target,values:target(*values)")]
    [InlineData("lambda value:lambda step:value+step")]
    public void LambdaBodiesAreExecutableWithoutForcedMirroring(string expression)
    {
        var syntax = LythonFrontend.Compile("result=" + expression).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        Check(code);
        Assert.NotEmpty(code.Constants.OfType<ExecutableCreateLambda>());

        static void Check(ExecutableCodeObject code)
        {
            Assert.Empty(code.ExpressionFallbacks);
            Assert.Empty(code.StatementFallbacks);
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
