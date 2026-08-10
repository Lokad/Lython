using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>Provides the execution-mode-specific effects used by lowered expression dispatch.</summary>
    private interface ILoweredExpressionExecution
    {
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
    private interface ILoweredStatementExecution : ILoweredExpressionExecution
    {
        /// <summary>Executes an import using the selected host-effect mode.</summary>
        ValueTask ExecuteImportAsync(LoweredImportStatement statement, ExecutionContext context);

        /// <summary>Creates and stores a function using the selected evaluation mode.</summary>
        ValueTask ExecuteFunctionDefinitionAsync(LoweredFunctionDefinitionStatement statement, ExecutionContext context);

        /// <summary>Creates and stores a class using the selected evaluation mode.</summary>
        ValueTask ExecuteClassDefinitionAsync(LoweredClassDefinitionStatement statement, ExecutionContext context);

        /// <summary>Executes an assignment using the selected evaluation mode.</summary>
        ValueTask ExecuteAssignmentAsync(LoweredAssignmentStatement statement, ExecutionContext context);

        /// <summary>Executes a conditional statement using the selected evaluation mode.</summary>
        ValueTask ExecuteIfAsync(LoweredIfStatement statement, ExecutionContext context);

        /// <summary>Executes a for statement using the selected iteration mode.</summary>
        ValueTask ExecuteForAsync(LoweredForStatement statement, ExecutionContext context);

        /// <summary>Executes a while statement using the selected evaluation mode.</summary>
        ValueTask ExecuteWhileAsync(LoweredWhileStatement statement, ExecutionContext context);

        /// <summary>Executes a match statement using the selected evaluation mode.</summary>
        ValueTask ExecuteMatchAsync(LoweredMatchStatement statement, ExecutionContext context);

        /// <summary>Executes a with statement using the selected context-manager mode.</summary>
        ValueTask ExecuteWithAsync(LoweredWithStatement statement, ExecutionContext context);

        /// <summary>Executes a try statement using the selected block execution mode.</summary>
        ValueTask ExecuteTryAsync(LoweredTryStatement statement, ExecutionContext context);

        /// <summary>Executes an assertion using the selected evaluation mode.</summary>
        ValueTask ExecuteAssertAsync(LoweredAssertStatement statement, ExecutionContext context);

        /// <summary>Executes a deletion using the selected evaluation mode.</summary>
        ValueTask ExecuteDeleteAsync(LoweredDeleteStatement statement, ExecutionContext context);

        /// <summary>Executes a raise statement using the selected evaluation mode.</summary>
        ValueTask ExecuteRaiseAsync(LoweredRaiseStatement statement, ExecutionContext context);
    }

    private sealed class SynchronousLoweredStatementExecution : ILoweredStatementExecution
    {
        public static readonly SynchronousLoweredStatementExecution Instance = new();

        public ValueTask ExecuteImportAsync(LoweredImportStatement statement, ExecutionContext context)
        {
            ExecuteLoweredImport(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteFunctionDefinitionAsync(LoweredFunctionDefinitionStatement statement, ExecutionContext context)
        {
            ExecuteLoweredFunctionDefinition(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteClassDefinitionAsync(LoweredClassDefinitionStatement statement, ExecutionContext context)
        {
            ExecuteLoweredClassDefinition(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteAssignmentAsync(LoweredAssignmentStatement statement, ExecutionContext context)
        {
            ExecuteLoweredAssignment(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context)
            => new(EvaluateLoweredExpression(expression, context));

        public ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => new(IsTruthy(value, context, span));

        public ValueTask<object> EvaluateFormattedAsync(LoweredFormattedStringExpression expression, ExecutionContext context)
            => new(EvaluateLoweredFormattedString(expression, context));

        public ValueTask<object> EvaluateListLiteralAsync(LoweredListLiteralExpression expression, ExecutionContext context)
            => new(ValidateLoweredCollection(CreateLoweredListLiteral(expression, context), context, expression.Span));

        public ValueTask<object> EvaluateListComprehensionAsync(LoweredListComprehensionExpression expression, ExecutionContext context)
            => new(EvaluateLoweredListComprehension(expression, context));

        public ValueTask<object> EvaluateTupleLiteralAsync(LoweredTupleLiteralExpression expression, ExecutionContext context)
            => new(ValidateLoweredCollection(CreateLoweredTupleLiteral(expression, context), context, expression.Span));

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

        public ValueTask ExecuteIfAsync(LoweredIfStatement statement, ExecutionContext context)
        {
            ExecuteLoweredIfStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteForAsync(LoweredForStatement statement, ExecutionContext context)
        {
            ExecuteLoweredForStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteWhileAsync(LoweredWhileStatement statement, ExecutionContext context)
        {
            ExecuteLoweredWhileStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteMatchAsync(LoweredMatchStatement statement, ExecutionContext context)
        {
            ExecuteLoweredMatchStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteWithAsync(LoweredWithStatement statement, ExecutionContext context)
        {
            ExecuteLoweredWithStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteTryAsync(LoweredTryStatement statement, ExecutionContext context)
        {
            ExecuteLoweredTryStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteAssertAsync(LoweredAssertStatement statement, ExecutionContext context)
        {
            ExecuteLoweredAssertStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteDeleteAsync(LoweredDeleteStatement statement, ExecutionContext context)
        {
            ExecuteLoweredDeleteStatement(statement, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask ExecuteRaiseAsync(LoweredRaiseStatement statement, ExecutionContext context)
        {
            ExecuteLoweredRaiseStatement(statement, context);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class AsynchronousLoweredStatementExecution : ILoweredStatementExecution
    {
        public static readonly AsynchronousLoweredStatementExecution Instance = new();

        public ValueTask ExecuteImportAsync(LoweredImportStatement statement, ExecutionContext context)
            => LythonRuntime.ExecuteImportAsync(statement.Syntax, context);

        public ValueTask ExecuteFunctionDefinitionAsync(LoweredFunctionDefinitionStatement statement, ExecutionContext context)
            => ExecuteLoweredFunctionDefinitionAsync(statement, context);

        public ValueTask ExecuteClassDefinitionAsync(LoweredClassDefinitionStatement statement, ExecutionContext context)
            => ExecuteLoweredClassDefinitionAsync(statement, context);

        public ValueTask ExecuteAssignmentAsync(LoweredAssignmentStatement statement, ExecutionContext context)
            => ExecuteLoweredAssignmentAsync(statement, context);

        public ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context)
            => EvaluateLoweredExpressionAsync(expression, context);

        public ValueTask<bool> IsTruthyAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => LythonRuntime.IsTruthyAsync(value, context, span);

        public async ValueTask<object> EvaluateFormattedAsync(LoweredFormattedStringExpression expression, ExecutionContext context)
            => await EvaluateLoweredFormattedStringAsync(expression, context).ConfigureAwait(false);

        public async ValueTask<object> EvaluateListLiteralAsync(LoweredListLiteralExpression expression, ExecutionContext context)
            => ValidateLoweredCollection(await CreateLoweredListLiteralAsync(expression, context).ConfigureAwait(false), context, expression.Span);

        public ValueTask<object> EvaluateListComprehensionAsync(LoweredListComprehensionExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredListComprehensionAsync(expression, context);

        public async ValueTask<object> EvaluateTupleLiteralAsync(LoweredTupleLiteralExpression expression, ExecutionContext context)
            => ValidateLoweredCollection(await CreateLoweredTupleLiteralAsync(expression, context).ConfigureAwait(false), context, expression.Span);

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

        public ValueTask<object> EvaluateBinaryAsync(LoweredBinaryExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredBinaryAsync(expression, context);

        public async ValueTask<object> EvaluateChainedComparisonAsync(LoweredChainedComparisonExpression expression, ExecutionContext context)
            => await LythonRuntime.EvaluateLoweredChainedComparisonAsync(expression, context).ConfigureAwait(false);

        public ValueTask<object> EvaluateUnaryAsync(LoweredUnaryExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredUnaryAsync(expression, context);

        public ValueTask<object> EvaluateAssignmentAsync(LoweredAssignmentExpression expression, ExecutionContext context)
            => LythonRuntime.EvaluateLoweredAssignmentExpressionAsync(expression, context);

        public ValueTask<object> CreateLambdaAsync(LoweredLambdaExpression expression, ExecutionContext context)
            => LythonRuntime.CreateLoweredLambdaAsync(expression, context);

        public ValueTask ExecuteIfAsync(LoweredIfStatement statement, ExecutionContext context)
            => ExecuteLoweredIfStatementAsync(statement, context);

        public ValueTask ExecuteForAsync(LoweredForStatement statement, ExecutionContext context)
            => ExecuteLoweredForStatementAsync(statement, context);

        public ValueTask ExecuteWhileAsync(LoweredWhileStatement statement, ExecutionContext context)
            => ExecuteLoweredWhileStatementAsync(statement, context);

        public ValueTask ExecuteMatchAsync(LoweredMatchStatement statement, ExecutionContext context)
            => ExecuteLoweredMatchStatementAsync(statement, context);

        public ValueTask ExecuteWithAsync(LoweredWithStatement statement, ExecutionContext context)
            => ExecuteLoweredWithStatementAsync(statement, context);

        public ValueTask ExecuteTryAsync(LoweredTryStatement statement, ExecutionContext context)
            => ExecuteLoweredTryStatementAsync(statement, context);

        public ValueTask ExecuteAssertAsync(LoweredAssertStatement statement, ExecutionContext context)
            => ExecuteLoweredAssertStatementAsync(statement, context);

        public ValueTask ExecuteDeleteAsync(LoweredDeleteStatement statement, ExecutionContext context)
            => ExecuteLoweredDeleteStatementAsync(statement, context);

        public ValueTask ExecuteRaiseAsync(LoweredRaiseStatement statement, ExecutionContext context)
            => ExecuteLoweredRaiseStatementAsync(statement, context);
    }
}
