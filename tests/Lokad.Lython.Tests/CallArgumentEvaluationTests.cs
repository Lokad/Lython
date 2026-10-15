using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CallArgumentEvaluationTests
{
    [Fact]
    public void SyntaxArgumentsRunBeforeRejectingANonCallableTarget()
    {
        var syntax = LythonFrontend.Compile("target(*[(marker:=1)])").Script!;
        var expression = Assert.IsType<CallExpressionSyntax>(Assert.IsType<ExpressionStatementSyntax>(Assert.Single(syntax.Statements)).Expression);
        var evaluate = typeof(LythonRuntime).GetMethod("EvaluateExpression", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Func<ExpressionSyntax, Context, object>>();
        var context = new Context(new MockLythonHost(), new LythonRunOptions());
        context.Variables["target"] = BigInteger.Zero;
        context.Variables["marker"] = BigInteger.Zero;
        var failure = Assert.Throws<LythonRuntimeException>(() => evaluate(expression, context));
        Assert.Equal("TypeError", failure.ExceptionType);
        Assert.Equal(expression.Target.Span, failure.Span);
        Assert.Equal(BigInteger.One, context.Variables["marker"]);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
