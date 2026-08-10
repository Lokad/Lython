using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Text.RegularExpressions;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static ControlSignal? ExecuteStatements(IReadOnlyList<LoweredStatement> statements, ExecutionContext context)
    {
        try
        {
            foreach (var statement in statements)
            {
                DispatchLoweredStatementAsync(statement, context, SynchronousLoweredStatementExecution.Instance).GetAwaiter().GetResult();
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

    private static void ExecuteLoweredImport(LoweredImportStatement statement, ExecutionContext context)
    {
        ExecuteImport(statement.Syntax, context);
    }

    private static void ExecuteLoweredFunctionDefinition(LoweredFunctionDefinitionStatement functionDefinition, ExecutionContext context)
    {
        context.EnterInterpreterFrame(functionDefinition.Span);
        try
        {
            var syntax = functionDefinition.Syntax;
            var function = CreateLoweredFunction(
                functionDefinition,
                context,
                BuildDefaultArgumentMap(functionDefinition.Parameters, expression => EvaluateLoweredExpression(expression, context)));
            StoreName(syntax.Name, ApplyDecorators(function, functionDefinition.Decorators, functionDefinition.Span, context), context, functionDefinition.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void ExecuteLoweredClassDefinition(LoweredClassDefinitionStatement classDefinition, ExecutionContext context)
    {
        context.EnterInterpreterFrame(classDefinition.Span);
        try
        {
            var baseTypes = new object[classDefinition.Bases.Count];
            for (var i = 0; i < classDefinition.Bases.Count; i++)
            {
                baseTypes[i] = EvaluateLoweredExpression(classDefinition.Bases[i], context);
            }

            var classKeywordArguments = new CallArgumentValue[classDefinition.KeywordArguments.Count];
            for (var i = 0; i < classDefinition.KeywordArguments.Count; i++)
            {
                var argument = classDefinition.KeywordArguments[i];
                classKeywordArguments[i] = CallArgumentValue.Keyword(argument.KeywordName, EvaluateLoweredExpression(argument.Expression, context));
            }
            var resolvedBases = ResolveClassBases(baseTypes, classDefinition.Span, context);
            ValidateClassKeywordArguments(classKeywordArguments, classDefinition.Span);

            var classContext = new ExecutionContext(context, classBodyScope: true);
            var signal = ExecuteStatements(classDefinition.Body, classContext);
            if (signal is not null)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a class body.", classDefinition.Span);
            }

            var type = CreateLoweredClassType(classDefinition, resolvedBases, classContext, context);
            InvokeInitSubclass(type, classKeywordArguments, classDefinition.Span, context);
            StoreName(classDefinition.Syntax.Name, ApplyDecorators(type, classDefinition.Decorators, classDefinition.Span, context), context, classDefinition.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static object ApplyDecorators(object value, IReadOnlyList<LoweredExpression> decorators, LythonSourceSpan span, ExecutionContext context)
    {
        object current = value;
        for (var i = decorators.Count - 1; i >= 0; i--)
        {
            var decorator = EvaluateLoweredExpression(decorators[i], context);
            if (decorator is not LythonRuntime.ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "Decorator expression must evaluate to a callable.", span);
            }

            current = callable.Invoke([CallArgumentValue.Positional(current)], span, context);
        }

        return current;
    }

    private static void ExecuteLoweredIfStatement(LoweredIfStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var branch = IsTruthy(EvaluateLoweredExpression(statement.Condition, context), context, statement.Condition.Span)
                ? statement.ThenStatements
                : statement.ElseStatements;

            if (branch is not null)
            {
                var signal = ExecuteStatements(branch, context);
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

    private static void ExecuteLoweredForStatement(LoweredForStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var syntax = statement.Syntax;
            var iterable = EvaluateLoweredExpression(statement.Iterable, context);
            var broke = false;
            foreach (var item in ToSequence(iterable, statement.Iterable.Span, context))
            {
                AssignLoopTarget(syntax.Target, item, statement.Iterable.Span, context);
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
                var signal = ExecuteStatements(statement.ElseStatements, context);
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

    private static void ExecuteLoweredWhileStatement(LoweredWhileStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var broke = false;
            while (IsTruthy(EvaluateLoweredExpression(statement.Condition, context), context, statement.Condition.Span))
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
                var signal = ExecuteStatements(statement.ElseStatements, context);
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

    private static void ExecuteLoweredMatchStatement(LoweredMatchStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            ExecuteMatch(statement.Syntax, EvaluateLoweredExpression(statement.Subject, context), context);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void ExecuteLoweredWithStatement(LoweredWithStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            ExecuteWithStatement(statement.Syntax, statement.ContextExpression, statement.Body, context);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void ExecuteLoweredTryStatement(LoweredTryStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            ExecuteTryStatement(statement, context);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void ExecuteLoweredAssignment(LoweredAssignmentStatement assignment, ExecutionContext context)
    {
        context.EnterInterpreterFrame(assignment.Span);
        try
        {
            switch (assignment)
            {
                case LoweredNameAssignmentStatement simple:
                    StoreName(simple.Assignment.Name, EvaluateLoweredExpression(simple.Expression, context), context, simple.Span);
                    return;
                case LoweredChainedAssignmentStatement chained:
                    var chainedValue = EvaluateLoweredExpression(chained.Expression, context);
                    foreach (var assignmentTarget in chained.Assignment.Targets)
                    {
                        AssignTarget(assignmentTarget, chainedValue, context);
                    }
                    return;
                case LoweredAnnotatedAssignmentStatement annotated:
                    if (annotated.Expression is not null)
                    {
                        StoreName(annotated.Assignment.Name, EvaluateLoweredExpression(annotated.Expression, context), context, annotated.Span);
                    }
                    return;
                case LoweredAugmentedAssignmentStatement augmented:
                    var augmentedTarget = ResolveLoweredAugmentedAssignmentTarget(augmented.Target, context);
                    augmentedTarget.Store(EvaluateAugmentedAssignment(
                        augmentedTarget.CurrentValue,
                        EvaluateLoweredExpression(augmented.Expression, context),
                        augmented.Assignment.Operator,
                        context,
                        augmented.Span));
                    return;
                case LoweredUnpackingAssignmentStatement unpacking:
                    AssignTargets(
                        unpacking.Assignment.Targets,
                        EvaluateLoweredExpression(unpacking.Expression, context),
                        unpacking.Expression.Span,
                        context);
                    return;
                case LoweredSubscriptAssignmentStatement subscript:
                    ExecuteLoweredSubscriptAssignment(
                        subscript.Assignment,
                        subscript.Receiver,
                        subscript.Index,
                        EvaluateLoweredExpression(subscript.Expression, context),
                        context);
                    return;
                case LoweredSliceAssignmentStatement slice:
                    ExecuteLoweredSliceAssignment(
                        slice.Assignment,
                        slice.Receiver,
                        slice.Start,
                        slice.End,
                        slice.Step,
                        EvaluateLoweredExpression(slice.Expression, context),
                        context);
                    return;
                case LoweredMemberAssignmentStatement member:
                    var target = EvaluateLoweredExpression(member.Receiver, context);
                    var value = EvaluateLoweredExpression(member.Expression, context);
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

    private static void ExecuteLoweredSliceAssignment(
        SliceAssignmentStatementSyntax statement,
        LoweredExpression targetExpression,
        LoweredExpression? startExpression,
        LoweredExpression? endExpression,
        LoweredExpression? stepExpression,
        object value,
        ExecutionContext context)
    {
        ExecuteSliceAssignment(
            EvaluateLoweredExpression(targetExpression, context),
            startExpression is null ? null : EvaluateLoweredExpression(startExpression, context),
            endExpression is null ? null : EvaluateLoweredExpression(endExpression, context),
            stepExpression is null ? null : EvaluateLoweredExpression(stepExpression, context),
            value,
            statement.Span,
            context);
    }

    private static AugmentedAssignmentTargetReference ResolveLoweredAugmentedAssignmentTarget(
        LoweredAugmentedAssignmentTarget target,
        ExecutionContext context)
    {
        switch (target)
        {
            case LoweredNameAugmentedAssignmentTarget name:
                return ResolveAugmentedAssignmentTarget(name.Target, context);

            case LoweredSubscriptAugmentedAssignmentTarget subscript:
                var subscriptTarget = EvaluateLoweredExpression(subscript.Receiver, context);
                var index = EvaluateLoweredExpression(subscript.Index, context);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case LoweredSliceAugmentedAssignmentTarget slice:
                var sliceTarget = EvaluateLoweredExpression(slice.Receiver, context);
                var start = slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context);
                var end = slice.End is null ? null : EvaluateLoweredExpression(slice.End, context);
                var step = slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case LoweredMemberAugmentedAssignmentTarget member:
                var memberTarget = EvaluateLoweredExpression(member.Receiver, context);
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

    private static void ExecuteLoweredSubscriptAssignment(
        SubscriptAssignmentStatementSyntax statement,
        LoweredExpression targetExpression,
        LoweredExpression indexExpression,
        object value,
        ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(targetExpression, context);
        var index = EvaluateLoweredExpression(indexExpression, context);
        SetSubscriptValue(target, index, value, statement.Span, context);
    }
}
