using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>Provides the execution-mode-specific effects used by lowered expression dispatch.</summary>
    private interface ILoweredExpressionExecution
    {
        ValueTask<object> UnpackAnnotationAsync(object value, ExecutionContext context, LythonSourceSpan span);
        /// <summary>Evaluates an expression recursively in the selected host-effect mode.</summary>
        ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context);

        /// <summary>Evaluates truthiness in the selected host-effect mode.</summary>
        ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span);

        /// <summary>Evaluates an interpolated string.</summary>
        ValueTask<object> EvaluateFormattedAsync(LoweredFormattedStringExpression expression, ExecutionContext context);

        /// <summary>Evaluates a list display, including unpacking.</summary>
        ValueTask<object> EvaluateListLiteralAsync(LoweredListLiteralExpression expression, ExecutionContext context);

        /// <summary>Evaluates a list comprehension.</summary>
        ValueTask<object> EvaluateListComprehensionAsync(LoweredListComprehensionExpression expression, ExecutionContext context);

        /// <summary>Evaluates a tuple display, including unpacking.</summary>
        ValueTask<object> EvaluateTupleLiteralAsync(LoweredTupleLiteralExpression expression, ExecutionContext context);

        /// <summary>Evaluates a set display, including unpacking.</summary>
        ValueTask<object> EvaluateSetLiteralAsync(LoweredSetLiteralExpression expression, ExecutionContext context);

        /// <summary>Evaluates a set comprehension.</summary>
        ValueTask<object> EvaluateSetComprehensionAsync(LoweredSetComprehensionExpression expression, ExecutionContext context);

        /// <summary>Evaluates a dictionary display, including unpacking.</summary>
        ValueTask<object> EvaluateDictionaryLiteralAsync(LoweredDictLiteralExpression expression, ExecutionContext context);

        /// <summary>Evaluates a dictionary comprehension.</summary>
        ValueTask<object> EvaluateDictionaryComprehensionAsync(LoweredDictComprehensionExpression expression, ExecutionContext context);

        /// <summary>Resolves a member access.</summary>
        ValueTask<object> ResolveMemberAsync(LoweredMemberExpression expression, ExecutionContext context);

        /// <summary>Invokes a lowered call.</summary>
        ValueTask<object> InvokeCallAsync(LoweredCallExpression expression, ExecutionContext context);

        /// <summary>Evaluates a subscript access.</summary>
        ValueTask<object> EvaluateSubscriptAsync(LoweredSubscriptExpression expression, ExecutionContext context);

        /// <summary>Evaluates a slice access.</summary>
        ValueTask<object> EvaluateSliceAsync(LoweredSliceExpression expression, ExecutionContext context);

        /// <summary>Materializes a slice in a composite subscription key.</summary>
        ValueTask<object> EvaluateSliceValueAsync(LoweredSliceValueExpression expression, ExecutionContext context);

        /// <summary>Evaluates a binary operation with mode-appropriate short circuiting.</summary>
        ValueTask<object> EvaluateBinaryAsync(LoweredBinaryExpression expression, ExecutionContext context);

        /// <summary>Evaluates a chained comparison.</summary>
        ValueTask<object> EvaluateChainedComparisonAsync(LoweredChainedComparisonExpression expression, ExecutionContext context);

        /// <summary>Evaluates a unary operation.</summary>
        ValueTask<object> EvaluateUnaryAsync(LoweredUnaryExpression expression, ExecutionContext context);

        /// <summary>Evaluates and stores an assignment expression.</summary>
        ValueTask<object> EvaluateAssignmentAsync(LoweredAssignmentExpression expression, ExecutionContext context);

        /// <summary>Creates a lambda using the selected execution mode.</summary>
        ValueTask<object> CreateLambdaAsync(LoweredLambdaExpression expression, ExecutionContext context);
    }

    /// <summary>Provides the execution-mode-specific effects used by lowered statement dispatch.</summary>
    private sealed class SynchronousLoweredExpressionExecution : ILoweredExpressionExecution
    {
        public ValueTask<object> UnpackAnnotationAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => LythonRuntime.UnpackAnnotationAsync(value, span, context, asynchronous: false);
        public static readonly SynchronousLoweredExpressionExecution Instance = new();

        public ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context)
            => new(EvaluateLoweredExpression(expression, context));

        public ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => new(IsTruthy(value, context, span));

        public ValueTask<object> EvaluateFormattedAsync(LoweredFormattedStringExpression expression, ExecutionContext context)
            => new(EvaluateLoweredFormattedString(expression, context));

        public ValueTask<object> EvaluateListLiteralAsync(LoweredListLiteralExpression expression, ExecutionContext context)
            => new(CreateLoweredListLiteral(expression, context));

        public ValueTask<object> EvaluateListComprehensionAsync(LoweredListComprehensionExpression expression, ExecutionContext context)
            => new(EvaluateLoweredListComprehension(expression, context));

        public ValueTask<object> EvaluateTupleLiteralAsync(LoweredTupleLiteralExpression expression, ExecutionContext context)
            => new(CreateLoweredTupleLiteral(expression, context));

        public ValueTask<object> EvaluateSetLiteralAsync(LoweredSetLiteralExpression expression, ExecutionContext context)
            => new(EvaluateLoweredSetLiteral(expression, context));

        public ValueTask<object> EvaluateSetComprehensionAsync(LoweredSetComprehensionExpression expression, ExecutionContext context)
            => new(EvaluateLoweredSetComprehension(expression, context));

        public ValueTask<object> EvaluateDictionaryLiteralAsync(LoweredDictLiteralExpression expression, ExecutionContext context)
            => new(EvaluateLoweredDictLiteral(expression, context));

        public ValueTask<object> EvaluateDictionaryComprehensionAsync(LoweredDictComprehensionExpression expression, ExecutionContext context)
            => new(EvaluateLoweredDictComprehension(expression, context));

        public ValueTask<object> ResolveMemberAsync(LoweredMemberExpression expression, ExecutionContext context)
            => new(ResolveLoweredMember(expression, context));

        public ValueTask<object> InvokeCallAsync(LoweredCallExpression expression, ExecutionContext context)
            => new(InvokeLoweredCall(expression, context));

        public ValueTask<object> EvaluateSubscriptAsync(LoweredSubscriptExpression expression, ExecutionContext context)
            => new(EvaluateLoweredSubscript(expression, context));

        public ValueTask<object> EvaluateSliceAsync(LoweredSliceExpression expression, ExecutionContext context)
            => new(EvaluateLoweredSlice(expression, context));

        public ValueTask<object> EvaluateSliceValueAsync(LoweredSliceValueExpression expression, ExecutionContext context)
            => new(EvaluateLoweredSliceValue(expression, context));

        public ValueTask<object> EvaluateBinaryAsync(LoweredBinaryExpression expression, ExecutionContext context)
            => new(EvaluateLoweredBinary(expression, context));

        public ValueTask<object> EvaluateChainedComparisonAsync(LoweredChainedComparisonExpression expression, ExecutionContext context)
            => new(EvaluateLoweredChainedComparison(expression, context));

        public ValueTask<object> EvaluateUnaryAsync(LoweredUnaryExpression expression, ExecutionContext context)
            => new(EvaluateLoweredUnary(expression, context));

        public ValueTask<object> EvaluateAssignmentAsync(LoweredAssignmentExpression expression, ExecutionContext context)
            => new(EvaluateLoweredAssignmentExpression(expression, context));

        public ValueTask<object> CreateLambdaAsync(LoweredLambdaExpression expression, ExecutionContext context)
            => new(CreateLoweredLambda(expression, context));

    }

    private sealed class AsynchronousLoweredExpressionExecution : ILoweredExpressionExecution
    {
        public ValueTask<object> UnpackAnnotationAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => LythonRuntime.UnpackAnnotationAsync(value, span, context, asynchronous: true);
        public static readonly AsynchronousLoweredExpressionExecution Instance = new();

        public ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context)
            => EvaluateLoweredExpressionAsync(expression, context);

        public ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => LythonRuntime.IsTruthyAsync(value, context, span);

        public async ValueTask<object> EvaluateFormattedAsync(LoweredFormattedStringExpression expression, ExecutionContext context)
            => await EvaluateLoweredFormattedStringAsync(expression, context).ConfigureAwait(false);

        public async ValueTask<object> EvaluateListLiteralAsync(LoweredListLiteralExpression expression, ExecutionContext context)
            => await CreateLoweredListLiteralAsync(expression, context).ConfigureAwait(false);

        public ValueTask<object> EvaluateListComprehensionAsync(LoweredListComprehensionExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredListComprehensionAsync(expression, context);

        public async ValueTask<object> EvaluateTupleLiteralAsync(LoweredTupleLiteralExpression expression, ExecutionContext context)
            => await CreateLoweredTupleLiteralAsync(expression, context).ConfigureAwait(false);

        public ValueTask<object> EvaluateSetLiteralAsync(LoweredSetLiteralExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredSetLiteralAsync(expression, context);

        public ValueTask<object> EvaluateSetComprehensionAsync(LoweredSetComprehensionExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredSetComprehensionAsync(expression, context);

        public ValueTask<object> EvaluateDictionaryLiteralAsync(LoweredDictLiteralExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredDictLiteralAsync(expression, context);

        public ValueTask<object> EvaluateDictionaryComprehensionAsync(LoweredDictComprehensionExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredDictComprehensionAsync(expression, context);

        public ValueTask<object> ResolveMemberAsync(LoweredMemberExpression expression, ExecutionContext context)
            => LythonRuntime.ResolveLoweredMemberAsync(expression, context);

        public ValueTask<object> InvokeCallAsync(LoweredCallExpression expression, ExecutionContext context)
            => LythonRuntime.InvokeLoweredCallAsync(expression, context);

        public ValueTask<object> EvaluateSubscriptAsync(LoweredSubscriptExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredSubscriptAsync(expression, context);

        public ValueTask<object> EvaluateSliceAsync(LoweredSliceExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredSliceAsync(expression, context);

        public ValueTask<object> EvaluateSliceValueAsync(LoweredSliceValueExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredSliceValueAsync(expression, context);

        public ValueTask<object> EvaluateBinaryAsync(LoweredBinaryExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredBinaryAsync(expression, context);

        public ValueTask<object> EvaluateChainedComparisonAsync(LoweredChainedComparisonExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredChainedComparisonAsync(expression, context);

        public ValueTask<object> EvaluateUnaryAsync(LoweredUnaryExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredUnaryAsync(expression, context);

        public ValueTask<object> EvaluateAssignmentAsync(LoweredAssignmentExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredAssignmentExpressionAsync(expression, context);

        public ValueTask<object> CreateLambdaAsync(LoweredLambdaExpression expression, ExecutionContext context)
            => LythonRuntime.CreateLoweredLambdaAsync(expression, context);

    }
}
