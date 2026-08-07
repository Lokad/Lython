using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    /// <summary>Provides the execution-mode-specific effects used by lowered statement dispatch.</summary>
    private interface ILoweredStatementExecution
    {
        /// <summary>Executes an import using the selected host-effect mode.</summary>
        ValueTask ExecuteImportAsync(LoweredImportStatement statement, ExecutionContext context);

        /// <summary>Creates and stores a function using the selected evaluation mode.</summary>
        ValueTask ExecuteFunctionDefinitionAsync(LoweredFunctionDefinitionStatement statement, ExecutionContext context);

        /// <summary>Creates and stores a class using the selected evaluation mode.</summary>
        ValueTask ExecuteClassDefinitionAsync(LoweredClassDefinitionStatement statement, ExecutionContext context);

        /// <summary>Executes an assignment using the selected evaluation mode.</summary>
        ValueTask ExecuteAssignmentAsync(LoweredAssignmentStatement statement, ExecutionContext context);

        /// <summary>Evaluates an expression using the selected host-effect mode.</summary>
        ValueTask<object> EvaluateExpressionAsync(LoweredExpression expression, ExecutionContext context);

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
