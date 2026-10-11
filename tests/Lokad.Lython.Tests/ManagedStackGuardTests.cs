using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ManagedStackGuardTests
{
    [Theory]
    [InlineData("function")]
    [InlineData("interpreter")]
    [InlineData("pair")]
    [InlineData("single")]
    [InlineData("lowered")]
    [InlineData("lowered-async")]
    [InlineData("projection")]
    public void LowStackEntryFailsBeforePublishingDepthOrTraversal(string entry)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var state = new StructuralGuardState();
        var span = new LythonSourceSpan(4, 2, 1, 1);
        object left = new(), right = new();
        var expression = new LoweredCapturedExpression(new BooleanLiteralExpressionSyntax(true, span), true);
        var projected = new PyList([true]);
        Action enter = entry switch
        {
            "function" => () => context.EnterFunctionCall(span),
            "interpreter" => () => context.EnterInterpreterFrame(span),
            "pair" => () => PyStructuralGuard.EnterPair(state, left, right, span, context).Dispose(),
            "single" => () => PyStructuralGuard.EnterSingle(state, left, span, context).Dispose(),
            "lowered" => () => LythonRuntime.EvaluateLoweredExpression(expression, context),
            "lowered-async" => () => LythonRuntime.EvaluateLoweredExpressionAsync(expression, context).GetAwaiter().GetResult(),
            "projection" => () => PublicProjection.NormalizeValue(projected),
            _ => throw new ArgumentOutOfRangeException(nameof(entry))
        };
        Exception? failure = null;
        var refused = false;
        var thread = new Thread(() =>
        {
            try
            {
                WithLowStack(() =>
                {
                    refused = !RuntimeHelpers.TryEnsureSufficientExecutionStack();
                    try { enter(); }
                    catch (Exception error) { failure = error; }
                });
            }
            catch (Exception error) { failure = error; }
        }, 256 * 1024) { IsBackground = true };
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Low-stack test thread did not stop.");
        Assert.True(refused, "The real managed stack boundary was not reached.");
        if (entry == "projection")
            Assert.Contains("stack headroom", Assert.IsType<ProjectionException>(failure).Message, StringComparison.Ordinal);
        else
        {
            var error = Assert.IsType<LythonRuntimeException>(failure);
            Assert.Equal(entry is "pair" or "single" ? "RecursionError" : "RuntimeError", error.ExceptionType);
            Assert.Equal(span, error.Span);
        }
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, state.Depth);
        Assert.Null(state.Pairs);
        Assert.Null(state.Singles);

        // A failed entry must not poison subsequent work on a normal stack.
        enter();
        if (entry == "function") context.LeaveFunctionCall();
        if (entry == "interpreter") context.LeaveInterpreterFrame();
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, state.Depth);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int WithLowStack(Action action, int depth = 0)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            action();
            return 0;
        }
        if (depth == 512) throw new InvalidOperationException("Stack-pressure bound was not reached.");
        Span<byte> padding = stackalloc byte[4096];
        padding[0] = (byte)depth;
        padding[^1] = (byte)(depth + 1);
        var checksum = WithLowStack(action, depth + 1);
        return checksum + padding[0] + padding[^1];
    }
}
