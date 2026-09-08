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

            var classContext = ExecutionContext.CreateClassBody(context);
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

            current = await CallableInvocation.InvokeUnaryAsync(callable, current, span, context).ConfigureAwait(false);
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
            await foreach (var item in ToSequenceAsync(iterable, statement.Iterable.Span, context).ConfigureAwait(false))
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
            switch (assignment)
            {
                case LoweredNameAssignmentStatement simple:
                    StoreName(simple.Assignment.Name, await EvaluateLoweredExpressionAsync(simple.Expression, context).ConfigureAwait(false), context, simple.Span);
                    return;
                case LoweredChainedAssignmentStatement chained:
                    var chainedValue = await EvaluateLoweredExpressionAsync(chained.Expression, context).ConfigureAwait(false);
                    foreach (var assignmentTarget in chained.Assignment.Targets)
                    {
                        AssignTarget(assignmentTarget, chainedValue, context);
                    }
                    return;
                case LoweredAnnotatedAssignmentStatement annotated:
                    if (annotated.Expression is not null)
                    {
                        StoreName(annotated.Assignment.Name, await EvaluateLoweredExpressionAsync(annotated.Expression, context).ConfigureAwait(false), context, annotated.Span);
                    }
                    return;
                case LoweredAugmentedAssignmentStatement augmented:
                    var augmentedTarget = await ResolveLoweredAugmentedAssignmentTargetAsync(augmented.Target, context).ConfigureAwait(false);
                    augmentedTarget.Store(EvaluateAugmentedAssignment(
                        augmentedTarget.CurrentValue,
                        await EvaluateLoweredExpressionAsync(augmented.Expression, context).ConfigureAwait(false),
                        augmented.Assignment.Operator,
                        context,
                        augmented.Span));
                    return;
                case LoweredUnpackingAssignmentStatement unpacking:
                    AssignTargets(
                        unpacking.Assignment.Targets,
                        await EvaluateLoweredExpressionAsync(unpacking.Expression, context).ConfigureAwait(false),
                        unpacking.Expression.Span,
                        context);
                    return;
                case LoweredSubscriptAssignmentStatement subscript:
                    await ExecuteLoweredSubscriptAssignmentAsync(
                            subscript.Assignment,
                            subscript.Receiver,
                            subscript.Index,
                            await EvaluateLoweredExpressionAsync(subscript.Expression, context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case LoweredSliceAssignmentStatement slice:
                    await ExecuteLoweredSliceAssignmentAsync(
                            slice.Assignment,
                            slice.Receiver,
                            slice.Start,
                            slice.End,
                            slice.Step,
                            await EvaluateLoweredExpressionAsync(slice.Expression, context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case LoweredMemberAssignmentStatement member:
                    var target = await EvaluateLoweredExpressionAsync(member.Receiver, context).ConfigureAwait(false);
                    var value = await EvaluateLoweredExpressionAsync(member.Expression, context).ConfigureAwait(false);
                    if (!PyMemberAccess.TryAssign(target, member.Assignment.MemberName, value, context, member.Span))
                    {
                        throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", member.Span);
                    }
                    return;
                default:
                    throw new InvalidOperationException($"Unsupported lowered assignment: {assignment.GetType().Name}");
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<AugmentedAssignmentTargetReference> ResolveLoweredAugmentedAssignmentTargetAsync(
        LoweredAugmentedAssignmentTarget target,
        ExecutionContext context)
    {
        switch (target)
        {
            case LoweredNameAugmentedAssignmentTarget name:
                return ResolveAugmentedAssignmentTarget(name.Target, context);

            case LoweredSubscriptAugmentedAssignmentTarget subscript:
                var subscriptTarget = await EvaluateLoweredExpressionAsync(subscript.Receiver, context).ConfigureAwait(false);
                var index = await EvaluateLoweredExpressionAsync(subscript.Index, context).ConfigureAwait(false);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case LoweredSliceAugmentedAssignmentTarget slice:
                var sliceTarget = await EvaluateLoweredExpressionAsync(slice.Receiver, context).ConfigureAwait(false);
                var start = slice.Start is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false);
                var end = slice.End is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false);
                var step = slice.Step is null
                    ? null
                    : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case LoweredMemberAugmentedAssignmentTarget member:
                var memberTarget = await EvaluateLoweredExpressionAsync(member.Receiver, context).ConfigureAwait(false);
                if (!TryResolveRuntimeMember(memberTarget, member.Target.MemberName, context, member.Span, out var memberValue))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, member.Target.MemberName, member.Span);
                }

                return new AugmentedAssignmentTargetReference(
                    memberValue,
                    value => SetMemberValue(memberTarget, member.Target.MemberName, value, member.Span, context));

            default:
                throw new InvalidOperationException($"Unsupported lowered augmented target: {target.GetType().Name}");
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
