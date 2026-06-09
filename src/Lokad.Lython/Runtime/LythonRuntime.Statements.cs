using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void ExecuteStatement(StatementSyntax statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            switch (statement)
            {
                case ImportStatementSyntax importStatement:
                    ExecuteImport(importStatement, context);
                    return;

                case AssignmentStatementSyntax assignmentStatement:
                    context.Variables[assignmentStatement.Name] = EvaluateExpression(assignmentStatement.Expression, context);
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
                    var branch = IsTruthy(EvaluateExpression(ifStatement.Condition, context))
                        ? ifStatement.ThenStatements
                        : ifStatement.ElseStatements;

                    if (branch is not null)
                    {
                        foreach (var nested in branch)
                        {
                            ExecuteStatement(nested, context);
                        }
                    }
                    return;

                case ForStatementSyntax forStatement:
                    var iterable = EvaluateExpression(forStatement.Iterable, context);
                    var broke = false;
                    foreach (var item in ToSequence(iterable, forStatement.Iterable.Span))
                    {
                        AssignLoopTarget(forStatement.Target, item, forStatement.Iterable.Span, context);
                        var signal = ExecuteStatements(forStatement.Body, context);
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

                    if (!broke && forStatement.ElseStatements is not null)
                    {
                        foreach (var nested in forStatement.ElseStatements)
                        {
                            ExecuteStatement(nested, context);
                        }
                    }
                    return;

                case WhileStatementSyntax whileStatement:
                    var whileBroke = false;
                    while (IsTruthy(EvaluateExpression(whileStatement.Condition, context)))
                    {
                        var signal = ExecuteStatements(whileStatement.Body, context);
                        if (signal is ContinueSignal)
                        {
                            continue;
                        }

                        if (signal is BreakSignal)
                        {
                            whileBroke = true;
                            break;
                        }
                    }

                    if (!whileBroke && whileStatement.ElseStatements is not null)
                    {
                        foreach (var nested in whileStatement.ElseStatements)
                        {
                            ExecuteStatement(nested, context);
                        }
                    }
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
                    var loweredParameters = functionDefinition.Parameters
                        .Select(parameter => new LoweredFunctionParameter(
                            parameter.Name,
                            parameter.Kind,
                            parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                            parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
                        .ToArray();
                    var function = new PyFunction(
                        functionDefinition.Name,
                        loweredParameters,
                        LoweredScript.Lower(new ScriptSyntax(functionDefinition.Body)).Statements,
                        context.FunctionClosureContext,
                        BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
                    context.Variables[functionDefinition.Name] = ApplyDecorators(function, functionDefinition.Decorators.Select(LoweredScript.LowerStandaloneExpression).ToArray(), functionDefinition.Span, context);
                    return;

                case ClassDefinitionStatementSyntax classDefinition:
                    var loweredBases = classDefinition.Bases.Select(LoweredScript.LowerStandaloneExpression).ToArray();
                    ExecuteLoweredClassDefinition(
                        new LoweredClassDefinitionStatement(
                            classDefinition,
                            classDefinition.Decorators.Select(LoweredScript.LowerStandaloneExpression).ToArray(),
                            loweredBases,
                            classDefinition.KeywordArguments.Select(argument => new LoweredCallArgument(argument.Name, LoweredScript.LowerStandaloneExpression(argument.Value))).ToArray(),
                            LoweredScript.Lower(new ScriptSyntax(classDefinition.Body)).Statements),
                        context);
                    return;

                case ReturnStatementSyntax returnStatement:
                    throw new ReturnSignal(returnStatement.Expression is null
                        ? PyNone.Instance
                        : RuntimeValue(EvaluateExpression(returnStatement.Expression, context)));

                case RaiseStatementSyntax raiseStatement:
                    var raised = EvaluateExpression(raiseStatement.Expression, context);
                    if (raised is not PyException instance)
                    {
                        throw RuntimeErrors.RaiseExpectsException(raiseStatement.Span);
                    }

                    throw new LythonRuntimeException(instance.TypeName, instance.Message, raiseStatement.Span, payload: instance.Value);

                case TryStatementSyntax tryStatement:
                    ExecuteTryStatement(
                        new LoweredTryStatement(
                            tryStatement,
                            LoweredScript.Lower(new ScriptSyntax(tryStatement.TryBody)).Statements,
                            tryStatement.ExceptBody is null ? null : LoweredScript.Lower(new ScriptSyntax(tryStatement.ExceptBody)).Statements,
                            tryStatement.ElseBody is null ? null : LoweredScript.Lower(new ScriptSyntax(tryStatement.ElseBody)).Statements,
                            tryStatement.FinallyBody is null ? null : LoweredScript.Lower(new ScriptSyntax(tryStatement.FinallyBody)).Statements),
                        context);
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

    private static void ExecuteTryStatement(LoweredTryStatement statement, ExecutionContext context)
    {
        ControlSignal? pendingControl = null;
        ReturnSignal? pendingReturn = null;
        LythonRuntimeException? pendingException = null;
        var ranWithoutException = false;

        try
        {
            pendingControl = ExecuteStatements(statement.TryBody, context);
            ranWithoutException = pendingControl is null;
            if (ranWithoutException && statement.ElseBody is not null)
            {
                pendingControl = ExecuteStatements(statement.ElseBody, context);
            }
        }
        catch (ReturnSignal signal)
        {
            pendingReturn = signal;
        }
        catch (LythonRuntimeException ex)
        {
            if (statement.ExceptBody is not null &&
                (statement.Syntax.ExceptionTypeNames is null || statement.Syntax.ExceptionTypeNames.Any(name => string.Equals(name, ex.ExceptionType, StringComparison.Ordinal))))
            {
                var exceptContext = new ExecutionContext(context);
                if (statement.Syntax.ExceptionVariableName is not null)
                {
                    exceptContext.Variables[statement.Syntax.ExceptionVariableName] = new PyException(ex.ExceptionType, ex.Message, ex.Payload ?? PyNone.Instance);
                }

                pendingControl = ExecuteStatements(statement.ExceptBody, exceptContext);
            }
            else
            {
                pendingException = ex;
            }
        }

        finally
        {
            if (statement.FinallyBody is not null)
            {
                try
                {
                    var finalSignal = ExecuteStatements(statement.FinallyBody, context);
                    if (finalSignal is not null)
                    {
                        pendingControl = finalSignal;
                        pendingReturn = null;
                        pendingException = null;
                    }
                }
                catch (ReturnSignal signal)
                {
                    pendingReturn = signal;
                    pendingControl = null;
                    pendingException = null;
                }
                catch (LythonRuntimeException ex)
                {
                    pendingException = ex;
                    pendingControl = null;
                    pendingReturn = null;
                }
            }
        }

        if (pendingException is not null)
        {
            throw pendingException;
        }

        if (pendingReturn is not null)
        {
            throw pendingReturn;
        }

        if (pendingControl is not null)
        {
            throw pendingControl;
        }
    }
}
