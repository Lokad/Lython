using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LoweredDispatchBoundaryTests
{
    [Theory]
    [InlineData("1 + 2", "3")]
    [InlineData("-4", "-4")]
    [InlineData("False and missing", "False")]
    [InlineData("True or missing", "True")]
    [InlineData("7 if True else missing", "7")]
    [InlineData("(1 + 2)", "3")]
    public void ExpressionCheckpointsKeepEvaluationOrderAndSpans(string source, string expected)
    {
        var expression = Expression(source);
        foreach (var directFunctionDispatch in new[] { false, true })
        {
            var context = Context(new LythonRunOptions { SourcePath = "/lowered.py" });
            context.UseSynchronousLoweredFunctionDispatch = directFunctionDispatch;
                var result = LythonRuntime.EvaluateLoweredExpression(expression, context);
                Assert.Equal(expected, result.ToString());
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }

    [Fact]
    public void ExpressionChecksCancellation()
    {
        foreach (var directFunctionDispatch in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var expression = Expression("1 + 2");
            var context = Context(new LythonRunOptions { CancellationToken = cancellation.Token });
            context.UseSynchronousLoweredFunctionDispatch = directFunctionDispatch;
            cancellation.Cancel();
            var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.EvaluateLoweredExpression(expression, context));
            Assert.Same(expression.Span, error.Span);
            Assert.Equal("execution canceled", error.Message);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinallyPreservesLoopControlReturnAliasingAndActiveException(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("""
            seen=[]
            try:
             for i in range(3):
              try:
               if i == 0: continue
               if i == 2: break
              finally: seen.append(i)
             else: seen.append(99)
             return seen
            finally: seen.append(7)
            """);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var context = Context(new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16384) });
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.Services.SetCurrentException(active);
        var statements = LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements;
        var signal = asynchronous
            ? await Assert.ThrowsAsync<LythonRuntime.ReturnSignal>(async () => await LythonRuntime.ExecuteStatementsAsync(statements, context))
            : Assert.Throws<LythonRuntime.ReturnSignal>(() => LythonRuntime.ExecuteStatements(statements, context));
        var returned = Assert.IsType<PyList>(signal.Value);
        Assert.Same(context.Variables["seen"], returned);
        Assert.Equal(new BigInteger[] { 0, 1, 2, 7 }, returned.ToArray().Cast<BigInteger>().ToArray());
        Assert.Same(active, context.Services.CurrentException);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LoweredExpression Expression(string source)
    {
        var frontend = LythonFrontend.Compile(source + "\n");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return Assert.IsType<LoweredExpressionStatement>(Assert.Single(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements)).Expression;
    }

    private static LythonRuntime.ExecutionContext Context(LythonRunOptions options) => new(new MockLythonHost(), options);
}
