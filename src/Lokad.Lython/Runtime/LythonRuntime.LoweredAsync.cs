using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static async ValueTask<ControlSignal?> ExecuteStatementsAsync(IReadOnlyList<LoweredStatement> statements, ExecutionContext context)
    {
        try
        {
            foreach (var statement in statements)
            {
                await DispatchLoweredStatementAsync(statement, context, AsynchronousLoweredStatementExecution.Instance).ConfigureAwait(false);
            }

            return null;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        catch (ControlSignal signal)
        {
            return signal;
        }
    }

    private static async ValueTask ExecuteLoweredFunctionDefinitionAsync(LoweredFunctionDefinitionStatement functionDefinition, ExecutionContext context)
    {
        context.EnterInterpreterFrame(functionDefinition.Span);
        try
        {
            var syntax = functionDefinition.Syntax;
            var function = CreateLoweredFunction(
                functionDefinition,
                context,
                await BuildDefaultArgumentMapAsync(functionDefinition.Parameters, expression => EvaluateLoweredExpressionAsync(expression, context)).ConfigureAwait(false));
            StoreName(
                syntax.Name,
                await ApplyDecoratorsAsync(function, functionDefinition.Decorators, functionDefinition.Span, context).ConfigureAwait(false),
                context,
                functionDefinition.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredClassDefinitionAsync(LoweredClassDefinitionStatement classDefinition, ExecutionContext context)
    {
        context.EnterInterpreterFrame(classDefinition.Span);
        try
        {
            var baseTypes = new object[classDefinition.Bases.Count];
            for (var i = 0; i < classDefinition.Bases.Count; i++)
            {
                baseTypes[i] = await EvaluateLoweredExpressionAsync(classDefinition.Bases[i], context).ConfigureAwait(false);
            }

            var classKeywordArguments = new CallArgumentValue[classDefinition.KeywordArguments.Count];
            for (var i = 0; i < classDefinition.KeywordArguments.Count; i++)
            {
                var argument = classDefinition.KeywordArguments[i];
                classKeywordArguments[i] = CallArgumentValue.Keyword(argument.KeywordName, await EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false));
            }

            var resolvedBases = ResolveClassBases(baseTypes, classDefinition.Span, context);
            ValidateClassKeywordArguments(classKeywordArguments, classDefinition.Span);

            var classContext = new ExecutionContext(context, classBodyScope: true);
            var signal = await ExecuteStatementsAsync(classDefinition.Body, classContext).ConfigureAwait(false);
            if (signal is not null)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a class body.", classDefinition.Span);
            }

            var type = CreateLoweredClassType(classDefinition, resolvedBases, classContext, context);
            await InvokeInitSubclassAsync(type, classKeywordArguments, classDefinition.Span, context).ConfigureAwait(false);
            StoreName(
                classDefinition.Syntax.Name,
                await ApplyDecoratorsAsync(type, classDefinition.Decorators, classDefinition.Span, context).ConfigureAwait(false),
                context,
                classDefinition.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<object> ApplyDecoratorsAsync(object value, IReadOnlyList<LoweredExpression> decorators, LythonSourceSpan span, ExecutionContext context)
    {
        object current = value;
        for (var i = decorators.Count - 1; i >= 0; i--)
        {
            var decorator = await EvaluateLoweredExpressionAsync(decorators[i], context).ConfigureAwait(false);
            if (decorator is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "Decorator expression must evaluate to a callable.", span);
            }

            current = await callable.InvokeAsync([CallArgumentValue.Positional(current)], span, context).ConfigureAwait(false);
        }

        return current;
    }

    private static async ValueTask ExecuteLoweredIfStatementAsync(LoweredIfStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var branch = await IsTruthyAsync(
                    await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false),
                    context,
                    statement.Condition.Span)
                .ConfigureAwait(false)
                ? statement.ThenStatements
                : statement.ElseStatements;

            if (branch is not null)
            {
                var signal = await ExecuteStatementsAsync(branch, context).ConfigureAwait(false);
                if (signal is not null)
                {
                    throw signal;
                }
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredForStatementAsync(LoweredForStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var syntax = statement.Syntax;
            var iterable = await EvaluateLoweredExpressionAsync(statement.Iterable, context).ConfigureAwait(false);
            var broke = false;
            await foreach (var item in ToSequenceAsync(iterable, statement.Iterable.Span).ConfigureAwait(false))
            {
                AssignLoopTarget(syntax.Target, item, statement.Iterable.Span, context);
                var signal = await ExecuteStatementsAsync(statement.Body, context).ConfigureAwait(false);
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
                var signal = await ExecuteStatementsAsync(statement.ElseStatements, context).ConfigureAwait(false);
                if (signal is not null)
                {
                    throw signal;
                }
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredWhileStatementAsync(LoweredWhileStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var broke = false;
            while (await IsTruthyAsync(
                    await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false),
                    context,
                    statement.Condition.Span)
                .ConfigureAwait(false))
            {
                var signal = await ExecuteStatementsAsync(statement.Body, context).ConfigureAwait(false);
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
                var signal = await ExecuteStatementsAsync(statement.ElseStatements, context).ConfigureAwait(false);
                if (signal is not null)
                {
                    throw signal;
                }
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredMatchStatementAsync(LoweredMatchStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            ExecuteMatch(statement.Syntax, await EvaluateLoweredExpressionAsync(statement.Subject, context).ConfigureAwait(false), context);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredWithStatementAsync(LoweredWithStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            await ExecuteWithStatementAsync(statement.Syntax, statement.ContextExpression, statement.Body, context).ConfigureAwait(false);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredTryStatementAsync(LoweredTryStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            await ExecuteTryStatementCoreAsync(statement, context, ExecuteStatementsAsync).ConfigureAwait(false);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteLoweredAssignmentAsync(LoweredAssignmentStatement assignment, ExecutionContext context)
    {
        context.EnterInterpreterFrame(assignment.Span);
        try
        {
            switch (assignment.Syntax)
            {
                case AssignmentStatementSyntax simple:
                    StoreName(simple.Name, await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false), context, assignment.Span);
                    return;
                case ChainedAssignmentStatementSyntax chained:
                    var chainedValue = await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false);
                    foreach (var assignmentTarget in chained.Targets)
                    {
                        AssignTarget(assignmentTarget, chainedValue, context);
                    }
                    return;
                case AnnotatedAssignmentStatementSyntax annotated:
                    if (assignment.Expression is not null)
                    {
                        StoreName(annotated.Name, await EvaluateLoweredExpressionAsync(assignment.Expression, context).ConfigureAwait(false), context, assignment.Span);
                    }
                    return;
                case AugmentedAssignmentStatementSyntax augmented:
                    var augmentedTarget = await ResolveLoweredAugmentedAssignmentTargetAsync(augmented, assignment, context).ConfigureAwait(false);
                    augmentedTarget.Store(EvaluateAugmentedAssignment(
                        augmentedTarget.CurrentValue,
                        await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false),
                        augmented.Operator,
                        context,
                        augmented.Span));
                    return;
                case UnpackingAssignmentStatementSyntax unpacking:
                    AssignTargets(
                        unpacking.Targets,
                        await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false),
                        unpacking.Expression.Span,
                        context);
                    return;
                case SubscriptAssignmentStatementSyntax subscript:
                    await ExecuteLoweredSubscriptAssignmentAsync(
                            subscript,
                            assignment.Target.RequireNotNull(),
                            assignment.Index.RequireNotNull(),
                            await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case SliceAssignmentStatementSyntax slice:
                    await ExecuteLoweredSliceAssignmentAsync(
                            slice,
                            assignment.Target.RequireNotNull(),
                            assignment.Start,
                            assignment.End,
                            assignment.Step,
                            await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case MemberAssignmentStatementSyntax memberAssignment:
                    var target = await EvaluateLoweredExpressionAsync(assignment.Target.RequireNotNull(), context).ConfigureAwait(false);
                    var value = await EvaluateLoweredExpressionAsync(assignment.Expression.RequireNotNull(), context).ConfigureAwait(false);
                    if (!PyMemberAccess.TryAssign(target, assignment.MemberName.RequireNotNull(), value, context, memberAssignment.Span))
                    {
                        throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", memberAssignment.Span);
                    }
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported lowered assignment fallback: {assignment.Syntax.GetType().Name}");
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<AugmentedAssignmentTargetReference> ResolveLoweredAugmentedAssignmentTargetAsync(
        AugmentedAssignmentStatementSyntax statement,
        LoweredAssignmentStatement assignment,
        ExecutionContext context)
    {
        switch (statement.Target)
        {
            case NameAssignmentTargetSyntax:
                return ResolveAugmentedAssignmentTarget(statement.Target, context);

            case SubscriptAssignmentTargetSyntax subscript:
                var subscriptTarget = await EvaluateLoweredExpressionAsync(assignment.Target.RequireNotNull(), context).ConfigureAwait(false);
                var index = await EvaluateLoweredExpressionAsync(assignment.Index.RequireNotNull(), context).ConfigureAwait(false);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = await EvaluateLoweredExpressionAsync(assignment.Target.RequireNotNull(), context).ConfigureAwait(false);
                var start = assignment.Start is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(assignment.Start, context).ConfigureAwait(false);
                var end = assignment.End is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(assignment.End, context).ConfigureAwait(false);
                var step = assignment.Step is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(assignment.Step, context).ConfigureAwait(false);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case MemberAssignmentTargetSyntax member:
                var memberTarget = await EvaluateLoweredExpressionAsync(assignment.Target.RequireNotNull(), context).ConfigureAwait(false);
                if (!TryResolveRuntimeMember(memberTarget, member.MemberName, context, member.Span, out var memberValue))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, member.MemberName, member.Span);
                }

                return new AugmentedAssignmentTargetReference(
                    memberValue,
                    value => SetMemberValue(memberTarget, member.MemberName, value, member.Span, context));

            default:
                throw new LythonRuntimeException("TypeError", "Unsupported augmented assignment target.", statement.Target.Span);
        }
    }

    private static async ValueTask ExecuteLoweredSliceAssignmentAsync(
        SliceAssignmentStatementSyntax statement,
        LoweredExpression targetExpression,
        LoweredExpression? startExpression,
        LoweredExpression? endExpression,
        LoweredExpression? stepExpression,
        object value,
        ExecutionContext context)
    {
        ExecuteSliceAssignment(
            await EvaluateLoweredExpressionAsync(targetExpression, context).ConfigureAwait(false),
            startExpression is null ? null : await EvaluateLoweredExpressionAsync(startExpression, context).ConfigureAwait(false),
            endExpression is null ? null : await EvaluateLoweredExpressionAsync(endExpression, context).ConfigureAwait(false),
            stepExpression is null ? null : await EvaluateLoweredExpressionAsync(stepExpression, context).ConfigureAwait(false),
            value,
            statement.Span,
            context);
    }

    private static async ValueTask ExecuteLoweredSubscriptAssignmentAsync(
        SubscriptAssignmentStatementSyntax statement,
        LoweredExpression targetExpression,
        LoweredExpression indexExpression,
        object value,
        ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(targetExpression, context).ConfigureAwait(false);
        var index = await EvaluateLoweredExpressionAsync(indexExpression, context).ConfigureAwait(false);
        SetSubscriptValue(target, index, value, statement.Span, context);
    }
}
