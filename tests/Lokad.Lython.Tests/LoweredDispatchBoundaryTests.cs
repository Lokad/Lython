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
        var context = Context(new LythonRunOptions { SourcePath = "/lowered.py" });
        var result = LythonRuntime.EvaluateLoweredExpression(expression, context);
        Assert.Equal(expected, result.ToString());
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ExpressionChecksCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var expression = Expression("1 + 2");
        var context = Context(new LythonRunOptions { CancellationToken = cancellation.Token });
        cancellation.Cancel();
        var error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.EvaluateLoweredExpression(expression, context));
        Assert.Same(expression.Span, error.Span);
        Assert.Equal("execution canceled", error.Message);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
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
            """,regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var context = Context(new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16384) });
        // The test observes the namespace alias after the execution frame leaves.
        context.MirrorLocalStores = true;
        var active = new PyException("ValueError", "outer", PyNone.Instance);
        context.State.SetCurrentException(active);
        var code = ExecutableScript.Compile(frontend.Script.RequireNotNull()).EntryPoint;
        var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var returned = Assert.IsType<PyList>(result);
        Assert.Same(context.Variables["seen"], returned);
        Assert.Equal(new BigInteger[] { 0, 1, 2, 7 }, returned.ToArray().Cast<BigInteger>().ToArray());
        Assert.Same(active, context.State.CurrentException);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LoweredExpression Expression(string source)
    {
        var frontend = LythonFrontend.Compile(source + "\n",regexProvider:Utf8RegexProvider.Instance);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var statement = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(frontend.Script.RequireNotNull().Statements));
        return LoweredScript.LowerExpression(statement.Expression);
    }

    private static LythonRuntime.ExecutionContext Context(LythonRunOptions options) => new(new MockLythonHost(), options,regexProvider:Utf8RegexProvider.Instance);
}
