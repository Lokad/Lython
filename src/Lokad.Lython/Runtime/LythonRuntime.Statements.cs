using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void ExecuteStatement(StatementSyntax statement, ExecutionContext context)
    {
        // Every syntax-level statement owns an interpreter frame, including nested
        // statements. Control-flow signals deliberately unwind through this boundary.
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            switch (statement)
            {
                case ImportStatementSyntax importStatement:
                    ExecuteImport(importStatement, context);
                    return;

                case ScopeDirectiveStatementSyntax:
                    return;

                case AssignmentStatementSyntax assignmentStatement:
                    StoreName(assignmentStatement.Name, EvaluateExpression(assignmentStatement.Expression, context), context, assignmentStatement.Span);
                    return;

                case ChainedAssignmentStatementSyntax chainedAssignmentStatement:
                    ExecuteChainedAssignment(chainedAssignmentStatement, context);
                    return;

                case AnnotatedAssignmentStatementSyntax annotatedAssignmentStatement:
                    ExecuteAnnotatedAssignment(annotatedAssignmentStatement, context);
                    return;

                case AugmentedAssignmentStatementSyntax augmentedAssignmentStatement:
                    ExecuteAugmentedAssignment(augmentedAssignmentStatement, context);
                    return;

                case UnpackingAssignmentStatementSyntax unpackingAssignmentStatement:
                    ExecuteUnpackingAssignment(unpackingAssignmentStatement, context);
                    return;

                case SubscriptAssignmentStatementSyntax subscriptAssignmentStatement:
                    ExecuteSubscriptAssignment(subscriptAssignmentStatement, context);
                    return;

                case SliceAssignmentStatementSyntax sliceAssignmentStatement:
                    ExecuteSliceAssignment(sliceAssignmentStatement, context);
                    return;

                case MemberAssignmentStatementSyntax memberAssignmentStatement:
                    ExecuteMemberAssignment(memberAssignmentStatement, context);
                    return;

                case ExpressionStatementSyntax expressionStatement:
                    _ = EvaluateExpression(expressionStatement.Expression, context);
                    return;

                case WithStatementSyntax withStatement:
                    ExecuteWithStatement(withStatement, null, null, context);
                    return;

                case IfStatementSyntax ifStatement:
                    ExecuteIfStatement(ifStatement, context);
                    return;

                case ForStatementSyntax forStatement:
                    ExecuteForStatement(forStatement, context);
                    return;

                case WhileStatementSyntax whileStatement:
                    ExecuteWhileStatement(whileStatement, context);
                    return;

                case MatchStatementSyntax matchStatement:
                    ExecuteMatchStatement(matchStatement, context);
                    return;

                case PassStatementSyntax:
                    return;

                case BreakStatementSyntax:
                    throw new BreakSignal();

                case ContinueStatementSyntax:
                    throw new ContinueSignal();

                case AssertStatementSyntax assertStatement:
                    ExecuteAssertStatement(assertStatement, context);
                    return;

                case DeleteStatementSyntax deleteStatement:
                    ExecuteDeleteStatement(deleteStatement, context);
                    return;

                case FunctionDefinitionStatementSyntax functionDefinition:
                    ExecuteFunctionDefinition(functionDefinition, context);
                    return;

                case ClassDefinitionStatementSyntax classDefinition:
                    ExecuteClassDefinition(classDefinition, context);
                    return;

                case ReturnStatementSyntax returnStatement:
                    throw new ReturnSignal(returnStatement.Expression is null
                        ? PyNone.Instance
                        : RuntimeValue(EvaluateExpression(returnStatement.Expression, context)));

                case RaiseStatementSyntax raiseStatement:
                    ExecuteRaiseStatement(raiseStatement, context);
                    return;

                case TryStatementSyntax tryStatement:
                    ExecuteTryStatementSyntax(tryStatement, context);
                    return;

                default:
                    throw new InvalidOperationException($"Unknown statement type: {statement.GetType().Name}");
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void ExecuteIfStatement(IfStatementSyntax statement, ExecutionContext context)
    {
        var branch = IsTruthy(EvaluateExpression(statement.Condition, context), context, statement.Condition.Span)
            ? statement.ThenStatements
            : statement.ElseStatements;

        if (branch is null)
        {
            return;
        }

        foreach (var nested in branch)
        {
            ExecuteStatement(nested, context);
        }
    }

    private static void ExecuteForStatement(ForStatementSyntax statement, ExecutionContext context)
    {
        var iterable = EvaluateExpression(statement.Iterable, context);
        var broke = false;
        foreach (var item in ToSequence(iterable, statement.Iterable.Span, context))
        {
            AssignLoopTarget(statement.Target, item, statement.Iterable.Span, context);
            var signal = ExecuteStatements(statement.Body, context);
            if (signal is ContinueSignal)
            {
                continue;
            }

            if (signal is BreakSignal)
            {
                broke = true;
                break;
            }
        }

        // Python's loop else-clause runs only after natural exhaustion.
        if (!broke && statement.ElseStatements is not null)
        {
            foreach (var nested in statement.ElseStatements)
            {
                ExecuteStatement(nested, context);
            }
        }
    }

    private static void ExecuteWhileStatement(WhileStatementSyntax statement, ExecutionContext context)
    {
        var broke = false;
        while (IsTruthy(EvaluateExpression(statement.Condition, context), context, statement.Condition.Span))
        {
            var signal = ExecuteStatements(statement.Body, context);
            if (signal is ContinueSignal)
            {
                continue;
            }

            if (signal is BreakSignal)
            {
                broke = true;
                break;
            }
        }

        if (!broke && statement.ElseStatements is not null)
        {
            foreach (var nested in statement.ElseStatements)
            {
                ExecuteStatement(nested, context);
            }
        }
    }

    private static void ExecuteFunctionDefinition(FunctionDefinitionStatementSyntax statement, ExecutionContext context)
    {
        var loweredParameters = statement.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();
        var function = new PyFunction(
            statement.Name,
            loweredParameters,
            LoweredScript.Lower(new ScriptSyntax(statement.Body)).Statements,
            context.FunctionClosureContext,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)),
            ScopeDirectiveFactsCollector.ForFunction(statement));
        ChargeFunctionValue(context, statement.Span);
        ChargeClosureRetention(context.FunctionClosureContext, context.FunctionClosureContext.Variables.Count, context.MemoryGovernor, statement.Span);
        StoreName(
            statement.Name,
            ApplyDecorators(function, statement.Decorators.Select(LoweredScript.LowerStandaloneExpression).ToArray(), statement.Span, context),
            context,
            statement.Span);
    }

    private static void ExecuteClassDefinition(ClassDefinitionStatementSyntax statement, ExecutionContext context)
    {
        var loweredBases = statement.Bases.Select(LoweredScript.LowerStandaloneExpression).ToArray();
        ExecuteLoweredClassDefinition(
            new LoweredClassDefinitionStatement(
                statement,
                statement.Decorators.Select(LoweredScript.LowerStandaloneExpression).ToArray(),
                loweredBases,
                statement.KeywordArguments.Select(argument => new LoweredCallArgument(CallArgumentForm.Keyword(argument.Name), LoweredScript.LowerStandaloneExpression(argument.Value))).ToArray(),
                LoweredScript.Lower(new ScriptSyntax(statement.Body)).Statements),
            context);
    }

    private static void ExecuteRaiseStatement(RaiseStatementSyntax statement, ExecutionContext context)
    {
        var raised = EvaluateExpression(statement.Expression, context);
        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(statement.Span);
        }

        throw new LythonRuntimeException(instance.Identity, instance.Message, statement.Span, null, instance.Value);
    }

    private static void ExecuteTryStatementSyntax(TryStatementSyntax statement, ExecutionContext context)
        => ExecuteTryStatement(
            new LoweredTryStatement(
                statement,
                LoweredScript.Lower(new ScriptSyntax(statement.TryBody)).Statements,
                statement.ExceptBody is null ? null : LoweredScript.Lower(new ScriptSyntax(statement.ExceptBody)).Statements,
                statement.ElseBody is null ? null : LoweredScript.Lower(new ScriptSyntax(statement.ElseBody)).Statements,
                statement.FinallyBody is null ? null : LoweredScript.Lower(new ScriptSyntax(statement.FinallyBody)).Statements),
            context);

    private static void ExecuteTryStatement(LoweredTryStatement statement, ExecutionContext context)
    {
        static ValueTask<ControlSignal?> ExecuteSynchronously(
            IReadOnlyList<LoweredStatement> statements,
            ExecutionContext executionContext)
            => new(ExecuteStatements(statements, executionContext));

        ExecuteTryStatementCoreAsync(statement, context, ExecuteSynchronously).GetAwaiter().GetResult();
    }
}
