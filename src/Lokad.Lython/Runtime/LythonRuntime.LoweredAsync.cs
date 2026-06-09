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
                await ExecuteLoweredStatementAsync(statement, context).ConfigureAwait(false);
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

    private static async ValueTask ExecuteLoweredStatementAsync(LoweredStatement statement, ExecutionContext context)
    {
        switch (statement)
        {
            case LoweredImportStatement importStatement:
                await ExecuteImportAsync(importStatement.Syntax, context).ConfigureAwait(false);
                return;
            case LoweredScopeDirectiveStatement:
                return;
            case LoweredFunctionDefinitionStatement functionDefinition:
                await ExecuteLoweredFunctionDefinitionAsync(functionDefinition, context).ConfigureAwait(false);
                return;
            case LoweredClassDefinitionStatement classDefinition:
                await ExecuteLoweredClassDefinitionAsync(classDefinition, context).ConfigureAwait(false);
                return;
            case LoweredAssignmentStatement assignment:
                await ExecuteLoweredAssignmentAsync(assignment, context).ConfigureAwait(false);
                return;
            case LoweredExpressionStatement expression:
                _ = await EvaluateLoweredExpressionAsync(expression.Expression, context).ConfigureAwait(false);
                return;
            case LoweredIfStatement ifStatement:
                await ExecuteLoweredIfStatementAsync(ifStatement, context).ConfigureAwait(false);
                return;
            case LoweredForStatement forStatement:
                await ExecuteLoweredForStatementAsync(forStatement, context).ConfigureAwait(false);
                return;
            case LoweredWhileStatement whileStatement:
                await ExecuteLoweredWhileStatementAsync(whileStatement, context).ConfigureAwait(false);
                return;
            case LoweredMatchStatement matchStatement:
                await ExecuteLoweredMatchStatementAsync(matchStatement, context).ConfigureAwait(false);
                return;
            case LoweredWithStatement withStatement:
                await ExecuteLoweredWithStatementAsync(withStatement, context).ConfigureAwait(false);
                return;
            case LoweredTryStatement tryStatement:
                await ExecuteLoweredTryStatementAsync(tryStatement, context).ConfigureAwait(false);
                return;
            case LoweredPassStatement:
                return;
            case LoweredBreakStatement:
                throw new BreakSignal();
            case LoweredContinueStatement:
                throw new ContinueSignal();
            case LoweredAssertStatement assertStatement:
                await ExecuteLoweredAssertStatementAsync(assertStatement, context).ConfigureAwait(false);
                return;
            case LoweredDeleteStatement deleteStatement:
                await ExecuteLoweredDeleteStatementAsync(deleteStatement, context).ConfigureAwait(false);
                return;
            case LoweredReturnStatement returnStatement:
                throw new ReturnSignal(returnStatement.Expression is null
                    ? PyNone.Instance
                    : RuntimeValue(await EvaluateLoweredExpressionAsync(returnStatement.Expression, context).ConfigureAwait(false)));
            case LoweredRaiseStatement raiseStatement:
                await ExecuteLoweredRaiseStatementAsync(raiseStatement, context).ConfigureAwait(false);
                return;
            case LoweredOtherStatement other:
                throw new InvalidOperationException($"Generic lowered statement fallback reached for supported execution: {other.Syntax.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered statement kind: {statement.GetType().Name}");
        }
    }

    private static async ValueTask ExecuteLoweredFunctionDefinitionAsync(LoweredFunctionDefinitionStatement functionDefinition, ExecutionContext context)
    {
        context.EnterInterpreterFrame(functionDefinition.Span);
        try
        {
            var syntax = functionDefinition.Syntax;
            var function = new PyFunction(
                syntax.Name,
                functionDefinition.Parameters,
                functionDefinition.Body,
                context.FunctionClosureContext,
                await BuildDefaultArgumentMapAsync(functionDefinition.Parameters, expression => EvaluateLoweredExpressionAsync(expression, context)).ConfigureAwait(false),
                ScopeDirectiveFactsCollector.ForFunction(syntax));
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
                classKeywordArguments[i] = new CallArgumentValue(argument.Name, await EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false));
            }

            var resolvedBases = ResolveClassBases(baseTypes, classDefinition.Span, context);
            ValidateClassKeywordArguments(classKeywordArguments, classDefinition.Span);

            var classContext = new ExecutionContext(context, classBodyScope: true);
            var signal = await ExecuteStatementsAsync(classDefinition.Body, classContext).ConfigureAwait(false);
            if (signal is not null)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a class body.", classDefinition.Span);
            }

            PyType type;
            try
            {
                type = new PyType(
                    classDefinition.Syntax.Name,
                    resolvedBases,
                    new Dictionary<string, object>(classContext.Variables, StringComparer.Ordinal));
            }
            catch (InvalidOperationException ex)
            {
                throw new LythonRuntimeException("TypeError", ex.Message, classDefinition.Span);
            }

            if (context.TryGetBuiltinType("type", out var metaType))
            {
                type.SetMetaType(metaType);
            }

            PyDataclass.Apply(type, classDefinition.Syntax, classContext.Variables, context, classDefinition.Span);
            type.InitializeClassMembers(context, classDefinition.Span);
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

            current = await callable.InvokeAsync([new CallArgumentValue(null, current)], span, context).ConfigureAwait(false);
        }

        return current;
    }

    private static async ValueTask ExecuteLoweredIfStatementAsync(LoweredIfStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var branch = IsTruthy(await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false))
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
            foreach (var item in ToSequence(iterable, statement.Iterable.Span))
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
            while (IsTruthy(await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false)))
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
            await ExecuteTryStatementAsync(statement, context).ConfigureAwait(false);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask ExecuteTryStatementAsync(LoweredTryStatement statement, ExecutionContext context)
    {
        ControlSignal? pendingControl = null;
        ReturnSignal? pendingReturn = null;
        LythonRuntimeException? pendingException = null;
        var ranWithoutException = false;

        try
        {
            pendingControl = await ExecuteStatementsAsync(statement.TryBody, context).ConfigureAwait(false);
            ranWithoutException = pendingControl is null;
            if (ranWithoutException && statement.ElseBody is not null)
            {
                pendingControl = await ExecuteStatementsAsync(statement.ElseBody, context).ConfigureAwait(false);
            }
        }
        catch (ReturnSignal signal)
        {
            pendingReturn = signal;
        }
        catch (LythonRuntimeException ex)
        {
            if (statement.Syntax.ExceptBody is not null &&
                (statement.Syntax.ExceptionTypeNames is null || statement.Syntax.ExceptionTypeNames.Any(name => string.Equals(name, ex.ExceptionType, StringComparison.Ordinal))))
            {
                var exceptContext = new ExecutionContext(context);
                var pyException = new PyException(ex.ExceptionType, ex.Message, ex.Payload ?? PyNone.Instance);
                if (statement.Syntax.ExceptionVariableName is not null)
                {
                    StoreName(statement.Syntax.ExceptionVariableName, pyException, exceptContext, statement.Span);
                }

                var previousException = context.Services.SetCurrentException(pyException);
                try
                {
                    pendingControl = await ExecuteStatementsAsync(statement.ExceptBody!, exceptContext).ConfigureAwait(false);
                }
                finally
                {
                    context.Services.SetCurrentException(previousException);
                }
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
                    var finalSignal = await ExecuteStatementsAsync(statement.FinallyBody, context).ConfigureAwait(false);
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

    private static async ValueTask ExecuteLoweredAssignmentAsync(LoweredAssignmentStatement assignment, ExecutionContext context)
    {
        context.EnterInterpreterFrame(assignment.Span);
        try
        {
            switch (assignment.Syntax)
            {
                case AssignmentStatementSyntax simple:
                    StoreName(simple.Name, await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false), context, assignment.Span);
                    return;
                case ChainedAssignmentStatementSyntax chained:
                    var chainedValue = await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false);
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
                        await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false),
                        augmented.Operator,
                        context,
                        augmented.Span));
                    return;
                case UnpackingAssignmentStatementSyntax unpacking:
                    AssignTargets(
                        unpacking.Targets,
                        await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false),
                        unpacking.Expression.Span,
                        context);
                    return;
                case SubscriptAssignmentStatementSyntax subscript:
                    await ExecuteLoweredSubscriptAssignmentAsync(
                            subscript,
                            assignment.Target!,
                            assignment.Index!,
                            await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case SliceAssignmentStatementSyntax slice:
                    await ExecuteLoweredSliceAssignmentAsync(
                            slice,
                            assignment.Target!,
                            assignment.Start,
                            assignment.End,
                            assignment.Step,
                            await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false),
                            context)
                        .ConfigureAwait(false);
                    return;
                case MemberAssignmentStatementSyntax memberAssignment:
                    var target = await EvaluateLoweredExpressionAsync(assignment.Target!, context).ConfigureAwait(false);
                    var value = await EvaluateLoweredExpressionAsync(assignment.Expression!, context).ConfigureAwait(false);
                    if (!PyMemberAccess.TryAssign(target, assignment.MemberName!, value, context, memberAssignment.Span))
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
                var subscriptTarget = await EvaluateLoweredExpressionAsync(assignment.Target!, context).ConfigureAwait(false);
                var index = await EvaluateLoweredExpressionAsync(assignment.Index!, context).ConfigureAwait(false);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = await EvaluateLoweredExpressionAsync(assignment.Target!, context).ConfigureAwait(false);
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
                var memberTarget = await EvaluateLoweredExpressionAsync(assignment.Target!, context).ConfigureAwait(false);
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

        switch (target)
        {
            case IMutablePySubscriptableValue subscriptable:
                subscriptable.SetSubscript(index, value, statement.Span);
                return;

            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, statement.Span), value);
                return;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                dict.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(dict.Count, statement.Span);
                return;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                defaultDict.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(defaultDict.Count, statement.Span);
                return;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                counter.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(counter.Count, statement.Span);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", statement.Span);
            case PyString:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", statement.Span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", statement.Span);
        }
    }

    internal static async ValueTask<object> EvaluateLoweredExpressionAsync(LoweredExpression expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            var value = expression switch
            {
                LoweredIdentifierExpression identifier => ResolveIdentifier(identifier.Identifier, context),
                LoweredStringLiteralExpression text => ValidateLoweredString(CreateString(text.Literal.Value, context, text.Span), context, text.Span),
                LoweredBytesLiteralExpression bytes => CreateBytes(bytes.Literal.Value.ToArray(), context, bytes.Span),
                LoweredIntegerLiteralExpression integer => ParseInteger(integer.Literal),
                LoweredFloatLiteralExpression floating => ParseFloat(floating.Literal),
                LoweredBooleanLiteralExpression boolean => boolean.Literal.Value,
                LoweredNoneLiteralExpression => PyNone.Instance,
                LoweredFormattedStringExpression formatted => await EvaluateLoweredFormattedStringAsync(formatted, context).ConfigureAwait(false),
                LoweredParenthesizedExpression parenthesized => await EvaluateLoweredExpressionAsync(parenthesized.Inner, context).ConfigureAwait(false),
                LoweredListLiteralExpression list => ValidateLoweredCollection(await CreateLoweredListLiteralAsync(list, context).ConfigureAwait(false), context, list.Span),
                LoweredListComprehensionExpression comprehension => await EvaluateLoweredListComprehensionAsync(comprehension, context).ConfigureAwait(false),
                LoweredGeneratorExpression generator => new PyGeneratorExpression(generator.Clauses, generator.ItemExpression, context, generator.Span),
                LoweredTupleLiteralExpression tuple => ValidateLoweredCollection(
                    await CreateTupleAsync(
                            tuple.Items.Count,
                            async i => RuntimeValue(await EvaluateLoweredExpressionAsync(tuple.Items[i], context).ConfigureAwait(false)),
                            context,
                            tuple.Span)
                        .ConfigureAwait(false),
                    context,
                    tuple.Span),
                LoweredSetLiteralExpression set => await EvaluateLoweredSetLiteralAsync(set, context).ConfigureAwait(false),
                LoweredDictLiteralExpression dict => await EvaluateLoweredDictLiteralAsync(dict, context).ConfigureAwait(false),
                LoweredDictComprehensionExpression comprehension => await EvaluateLoweredDictComprehensionAsync(comprehension, context).ConfigureAwait(false),
                LoweredMemberExpression member => await ResolveLoweredMemberAsync(member, context).ConfigureAwait(false),
                LoweredCallExpression call => await InvokeLoweredCallAsync(call, context).ConfigureAwait(false),
                LoweredSubscriptExpression subscript => await EvaluateLoweredSubscriptAsync(subscript, context).ConfigureAwait(false),
                LoweredSliceExpression slice => await EvaluateLoweredSliceAsync(slice, context).ConfigureAwait(false),
                LoweredBinaryExpression binary => await EvaluateLoweredBinaryAsync(binary, context).ConfigureAwait(false),
                LoweredChainedComparisonExpression chained => await EvaluateLoweredChainedComparisonAsync(chained, context).ConfigureAwait(false),
                LoweredUnaryExpression unary => await EvaluateLoweredUnaryAsync(unary, context).ConfigureAwait(false),
                LoweredConditionalExpression conditional => IsTruthy(await EvaluateLoweredExpressionAsync(conditional.Condition, context).ConfigureAwait(false))
                    ? await EvaluateLoweredExpressionAsync(conditional.Consequent, context).ConfigureAwait(false)
                    : await EvaluateLoweredExpressionAsync(conditional.Alternative, context).ConfigureAwait(false),
                LoweredAssignmentExpression assignment => await EvaluateLoweredAssignmentExpressionAsync(assignment, context).ConfigureAwait(false),
                LoweredLambdaExpression lambda => await CreateLoweredLambdaAsync(lambda, context).ConfigureAwait(false),
                LoweredOtherExpression other => throw new InvalidOperationException($"Generic lowered expression fallback reached for supported execution: {other.Expression.GetType().Name}"),
                _ => throw new InvalidOperationException($"Unknown lowered expression type: {expression.GetType().Name}")
            };

            context.ObserveValue(value, expression.Span);
            return value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<PyList> CreateLoweredListLiteralAsync(LoweredListLiteralExpression list, ExecutionContext context)
    {
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(await EvaluateLoweredExpressionAsync(list.Items[i], context).ConfigureAwait(false));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static async ValueTask<object> EvaluateLoweredDictLiteralAsync(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            var key = ValidateDictionaryKey(await EvaluateLoweredExpressionAsync(item.Key, context).ConfigureAwait(false), item.Key.Span, context.MemoryGovernor);
            var value = RuntimeValue(await EvaluateLoweredExpressionAsync(item.Value, context).ConfigureAwait(false));
            result.SetItem(key, value);
        }

        context.ObserveCollectionCount(result.Count, dict.Span);
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredSetLiteralAsync(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        var items = new PySet(context.MemoryGovernor, set.Span);
        foreach (var item in set.Items)
        {
            items.Add(ValidateSetItem(await EvaluateLoweredExpressionAsync(item, context).ConfigureAwait(false), item.Span, context.MemoryGovernor));
        }

        context.ObserveCollectionCount(items.Count, set.Span);
        return items;
    }

    private static async ValueTask<object> EvaluateLoweredFormattedStringAsync(LoweredFormattedStringExpression formatted, ExecutionContext context)
    {
        var builder = new Utf8ValueBuilder(context.MemoryGovernor, formatted.Span);
        foreach (var part in formatted.Parts)
        {
            switch (part)
            {
                case LoweredFormattedStringTextPart text:
                    builder.AppendString(text.Text);
                    break;
                case LoweredFormattedStringExpressionPart expression:
                    builder.Append(FormatInterpolatedStringPart(
                        await EvaluateLoweredExpressionAsync(expression.Expression, context).ConfigureAwait(false),
                        expression.Conversion,
                        expression.FormatSpecifier,
                        context,
                        formatted.Span));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown lowered formatted string part: {part.GetType().Name}");
            }
        }

        var value = builder.ToPyString();
        context.ObserveString(value, formatted.Span);
        return value;
    }

    private static async ValueTask<object> EvaluateLoweredListComprehensionAsync(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                context,
                async scope => result.Add(RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ItemExpression, scope).ConfigureAwait(false))))
            .ConfigureAwait(false);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static async ValueTask<object> EvaluateLoweredDictComprehensionAsync(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        await EvaluateLoweredComprehensionClausesAsync(
                comprehension.Clauses,
                0,
                context,
                async scope =>
                {
                    var key = ValidateDictionaryKey(
                        await EvaluateLoweredExpressionAsync(comprehension.KeyExpression, scope).ConfigureAwait(false),
                        comprehension.KeyExpression.Span,
                        scope.MemoryGovernor);
                    result.SetItem(key, RuntimeValue(await EvaluateLoweredExpressionAsync(comprehension.ValueExpression, scope).ConfigureAwait(false)));
                })
            .ConfigureAwait(false);

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static async ValueTask EvaluateLoweredComprehensionClausesAsync(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Func<ExecutionContext, ValueTask> emit)
    {
        var clause = clauses[index];
        var iterable = await EvaluateLoweredExpressionAsync(clause.Iterable, context).ConfigureAwait(false);

        foreach (var item in ToSequence(iterable, clause.Iterable.Span))
        {
            var scope = new ExecutionContext(context);
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null && !IsTruthy(await EvaluateLoweredExpressionAsync(clause.Condition, scope).ConfigureAwait(false)))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                await emit(scope).ConfigureAwait(false);
            }
            else
            {
                await EvaluateLoweredComprehensionClausesAsync(clauses, index + 1, scope, emit).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask<object> EvaluateLoweredSubscriptAsync(LoweredSubscriptExpression subscript, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(subscript.Target, context).ConfigureAwait(false);
        var index = await EvaluateLoweredExpressionAsync(subscript.Index, context).ConfigureAwait(false);
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, subscript.Span), context, subscript.Span);
        }

        return PyIndexing.ReadIndex(target, index, subscript.Span);
    }

    private static async ValueTask<object> EvaluateLoweredSliceAsync(LoweredSliceExpression slice, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(slice.Target, context).ConfigureAwait(false);
        var start = slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false);
        var end = slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false);
        var step = slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false);
        return PyIndexing.ReadSlice(target, start, end, step, slice.Span);
    }

    private static async ValueTask<object> ResolveLoweredMemberAsync(LoweredMemberExpression member, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(member.Target, context).ConfigureAwait(false);
        if (TryResolveRuntimeMember(target, member.Member.MemberName, context, member.Span, out var value))
        {
            return value;
        }

        throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span);
    }

    private static async ValueTask<object> EvaluateLoweredBinaryAsync(LoweredBinaryExpression binary, ExecutionContext context)
    {
        if (binary.Binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
            return IsTruthy(leftValue)
                ? leftValue
                : await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);
        }

        if (binary.Binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
            return !IsTruthy(leftValue)
                ? leftValue
                : await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);
        }

        var left = await EvaluateLoweredExpressionAsync(binary.Left, context).ConfigureAwait(false);
        var right = await EvaluateLoweredExpressionAsync(binary.Right, context).ConfigureAwait(false);

        return binary.Binary.Operator switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, binary.Span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, binary.Span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, binary.Span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, binary.Span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, binary.Span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, binary.Span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, binary.Span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, binary.Span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, binary.Span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, binary.Span),
            BinaryOperatorSyntax.Less => Compare(left, right, binary.Span) < 0,
            BinaryOperatorSyntax.LessEqual => Compare(left, right, binary.Span) <= 0,
            BinaryOperatorSyntax.Greater => Compare(left, right, binary.Span) > 0,
            BinaryOperatorSyntax.GreaterEqual => Compare(left, right, binary.Span) >= 0,
            BinaryOperatorSyntax.Is => ReferenceEquals(left, right),
            BinaryOperatorSyntax.IsNot => !ReferenceEquals(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, binary.Span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, binary.Span),
            BinaryOperatorSyntax.Equal => AreEqual(left, right),
            BinaryOperatorSyntax.NotEqual => !AreEqual(left, right),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binary.Binary.Operator}")
        };
    }

    private static async ValueTask<bool> EvaluateLoweredChainedComparisonAsync(LoweredChainedComparisonExpression chained, ExecutionContext context)
    {
        var left = await EvaluateLoweredExpressionAsync(chained.Operands[0], context).ConfigureAwait(false);
        for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
        {
            var right = await EvaluateLoweredExpressionAsync(chained.Operands[i + 1], context).ConfigureAwait(false);
            if (!EvaluateComparisonOperator(left, right, chained.ChainedComparison.Operators[i], chained.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static async ValueTask<object> EvaluateLoweredUnaryAsync(LoweredUnaryExpression unary, ExecutionContext context)
    {
        var operand = await EvaluateLoweredExpressionAsync(unary.Operand, context).ConfigureAwait(false);
        return unary.Unary.Operator switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, unary.Span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, unary.Span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, unary.Span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unary.Unary.Operator}")
        };
    }

    private static async ValueTask ExecuteLoweredAssertStatementAsync(LoweredAssertStatement statement, ExecutionContext context)
    {
        if (IsTruthy(await EvaluateLoweredExpressionAsync(statement.Condition, context).ConfigureAwait(false)))
        {
            return;
        }

        var message = statement.Message is null
            ? string.Empty
            : ToInterpolatedPyString(await EvaluateLoweredExpressionAsync(statement.Message, context).ConfigureAwait(false), context).AsString();
        throw new LythonRuntimeException("AssertionError", message, statement.Span);
    }

    private static async ValueTask ExecuteLoweredDeleteStatementAsync(LoweredDeleteStatement statement, ExecutionContext context)
    {
        switch (statement.Target.Syntax)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, statement.Span))
                {
                    throw new LythonRuntimeException("NameError", $"Name '{identifier.Name}' is not defined.", statement.Span);
                }

                return;

            case SubscriptExpressionSyntax:
                if (statement.Target is not LoweredSubscriptExpression subscript)
                {
                    break;
                }

                var target = await EvaluateLoweredExpressionAsync(subscript.Target, context).ConfigureAwait(false);
                var index = await EvaluateLoweredExpressionAsync(subscript.Index, context).ConfigureAwait(false);
                switch (target)
                {
                    case IDeletablePySubscriptableValue subscriptable:
                        subscriptable.DeleteSubscript(index, statement.Span);
                        return;
                    case IMutablePySequenceValue sequence:
                        sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, statement.Span));
                        return;
                    case PyDict dict:
                        if (!dict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;
                    case PyDefaultDict defaultDict:
                        if (!defaultDict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;
                    case PyCounter counter:
                        _ = counter.Remove(ValidateDictionaryKey(index, statement.Span));
                        return;
                    case PyTuple:
                        throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", statement.Span);
                    case PyString:
                        throw new LythonRuntimeException("TypeError", "String does not support item deletion.", statement.Span);
                    default:
                        throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", statement.Span);
                }

            case SliceExpressionSyntax:
                if (statement.Target is not LoweredSliceExpression slice)
                {
                    break;
                }

                ExecuteSliceDeletion(
                    await EvaluateLoweredExpressionAsync(slice.Target, context).ConfigureAwait(false),
                    slice.Start is null ? null : await EvaluateLoweredExpressionAsync(slice.Start, context).ConfigureAwait(false),
                    slice.End is null ? null : await EvaluateLoweredExpressionAsync(slice.End, context).ConfigureAwait(false),
                    slice.Step is null ? null : await EvaluateLoweredExpressionAsync(slice.Step, context).ConfigureAwait(false),
                    statement.Span);
                return;

            case MemberExpressionSyntax memberSyntax:
                if (statement.Target is not LoweredMemberExpression member)
                {
                    break;
                }

                var memberTarget = await EvaluateLoweredExpressionAsync(member.Target, context).ConfigureAwait(false);
                if (!PyMemberAccess.TryDelete(memberTarget, memberSyntax.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;
        }

        throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
    }

    private static async ValueTask ExecuteLoweredRaiseStatementAsync(LoweredRaiseStatement statement, ExecutionContext context)
    {
        var raised = await EvaluateLoweredExpressionAsync(statement.Expression, context).ConfigureAwait(false);
        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(statement.Span);
        }

        throw new LythonRuntimeException(instance.TypeName, instance.Message, statement.Span, payload: instance.Value);
    }

    private static async ValueTask<object> EvaluateLoweredAssignmentExpressionAsync(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = await EvaluateLoweredExpressionAsync(assignment.Expression, context).ConfigureAwait(false);
        StoreName(assignment.Assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static async ValueTask<object> CreateLoweredLambdaAsync(LoweredLambdaExpression lambda, ExecutionContext context)
    {
        var loweredParameters = lambda.Lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();
        return new LambdaFunction(
            loweredParameters,
            lambda.Body,
            context,
            await BuildDefaultArgumentMapAsync(loweredParameters, expression => EvaluateLoweredExpressionAsync(expression, context)).ConfigureAwait(false));
    }

    private static async ValueTask<object> InvokeLoweredCallAsync(LoweredCallExpression call, ExecutionContext context)
    {
        var target = await EvaluateLoweredExpressionAsync(call.Target, context).ConfigureAwait(false);
        return await InvokeCallableTargetAsync(
                target,
                call.Call.Target.Span,
                call.Span,
                context,
                () => ExpandLoweredCallArgumentsAsync(call.Arguments, context))
            .ConfigureAwait(false);
    }

    private static ValueTask<CallArgumentValue[]> ExpandLoweredCallArgumentsAsync(
        IReadOnlyList<LoweredCallArgument> arguments,
        ExecutionContext context)
        => CallExpansion.ExpandLoweredArgumentsAsync(arguments, context, EvaluateLoweredExpressionAsync);

    private static async ValueTask InvokeInitSubclassAsync(PyType type, CallArgumentValue[] keywordArguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (!type.TryLookupInMro("__init_subclass__", 1, out var rawMethod, out _))
        {
            return;
        }

        object candidate = rawMethod switch
        {
            IPyBindableCallable bindable => bindable.Bind(type),
            IPyDescriptor descriptor => descriptor.Get(type, type, context, span),
            _ => rawMethod
        };

        if (candidate is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "__init_subclass__ must be callable.", span);
        }

        _ = await callable.InvokeAsync(keywordArguments, span, context).ConfigureAwait(false);
    }
}
