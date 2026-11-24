using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CallArgumentEvaluationTests
{
    [Fact]
    public void LoweredArgumentsRunBeforeRejectingANonCallableTarget()
    {
        var syntax = LythonFrontend.Compile("target(*[(marker:=1)])",regexProvider:Utf8RegexProvider.Instance).Script!;
        var expression = Assert.IsType<CallExpressionSyntax>(Assert.IsType<ExpressionStatementSyntax>(Assert.Single(syntax.Statements)).Expression);
        var context = new Context(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        context.Variables["target"] = BigInteger.Zero;
        context.Variables["marker"] = BigInteger.Zero;
        var lowered = LoweredScript.LowerExpression(expression);
        var failure = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.EvaluateLoweredExpression(lowered, context));
        Assert.Equal("TypeError", failure.ExceptionType);
        Assert.Equal(expression.Target.Span, failure.Span);
        Assert.Equal(BigInteger.One, context.Variables["marker"]);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
