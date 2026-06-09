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
                ExecuteLoweredStatement(statement, context);
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

    private static void ExecuteLoweredStatement(LoweredStatement statement, ExecutionContext context)
    {
        switch (statement)
        {
            case LoweredImportStatement importStatement:
                ExecuteLoweredImport(importStatement, context);
                return;
            case LoweredScopeDirectiveStatement:
                return;
            case LoweredFunctionDefinitionStatement functionDefinition:
                ExecuteLoweredFunctionDefinition(functionDefinition, context);
                return;
            case LoweredClassDefinitionStatement classDefinition:
                ExecuteLoweredClassDefinition(classDefinition, context);
                return;
            case LoweredAssignmentStatement assignment:
                ExecuteLoweredAssignment(assignment, context);
                return;
            case LoweredExpressionStatement expression:
                _ = EvaluateLoweredExpression(expression.Expression, context);
                return;
            case LoweredIfStatement ifStatement:
                ExecuteLoweredIfStatement(ifStatement, context);
                return;
            case LoweredForStatement forStatement:
                ExecuteLoweredForStatement(forStatement, context);
                return;
            case LoweredWhileStatement whileStatement:
                ExecuteLoweredWhileStatement(whileStatement, context);
                return;
            case LoweredMatchStatement matchStatement:
                ExecuteLoweredMatchStatement(matchStatement, context);
                return;
            case LoweredWithStatement withStatement:
                ExecuteLoweredWithStatement(withStatement, context);
                return;
            case LoweredTryStatement tryStatement:
                ExecuteLoweredTryStatement(tryStatement, context);
                return;
            case LoweredPassStatement:
                return;
            case LoweredBreakStatement:
                throw new BreakSignal();
            case LoweredContinueStatement:
                throw new ContinueSignal();
            case LoweredAssertStatement assertStatement:
                ExecuteLoweredAssertStatement(assertStatement, context);
                return;
            case LoweredDeleteStatement deleteStatement:
                ExecuteLoweredDeleteStatement(deleteStatement, context);
                return;
            case LoweredReturnStatement returnStatement:
                throw new ReturnSignal(returnStatement.Expression is null
                    ? PyNone.Instance
                    : RuntimeValue(EvaluateLoweredExpression(returnStatement.Expression, context)));
            case LoweredRaiseStatement raiseStatement:
                ExecuteLoweredRaiseStatement(raiseStatement, context);
                return;
            case LoweredOtherStatement other:
                throw new InvalidOperationException($"Generic lowered statement fallback reached for supported execution: {other.Syntax.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered statement kind: {statement.GetType().Name}");
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
            var function = new PyFunction(
                syntax.Name,
                functionDefinition.Parameters,
                functionDefinition.Body,
                context.FunctionClosureContext,
                BuildDefaultArgumentMap(functionDefinition.Parameters, expression => EvaluateLoweredExpression(expression, context)),
                ScopeDirectiveFactsCollector.ForFunction(syntax));
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
                classKeywordArguments[i] = new CallArgumentValue(argument.Name, EvaluateLoweredExpression(argument.Expression, context));
            }
            var resolvedBases = ResolveClassBases(baseTypes, classDefinition.Span, context);
            ValidateClassKeywordArguments(classKeywordArguments, classDefinition.Span);

            var classContext = new ExecutionContext(context, classBodyScope: true);
            var signal = ExecuteStatements(classDefinition.Body, classContext);
            if (signal is not null)
            {
                throw new LythonRuntimeException("RuntimeError", "Loop control cannot escape a class body.", classDefinition.Span);
            }

            StoreClassAnnotations(classDefinition.Syntax, classContext.Variables, classContext);

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

            PyDataclass.Apply(type, classDefinition.Syntax, classContext.Variables, classContext, classDefinition.Span);
            type.InitializeClassMembers(context, classDefinition.Span);
            InvokeInitSubclass(type, classKeywordArguments, classDefinition.Span, context);
            StoreName(classDefinition.Syntax.Name, ApplyDecorators(type, classDefinition.Decorators, classDefinition.Span, context), context, classDefinition.Span);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static void StoreClassAnnotations(ClassDefinitionStatementSyntax syntax, Dictionary<string, object> members, ExecutionContext context)
    {
        PyDict? annotations = null;
        foreach (var statement in syntax.Body.OfType<AnnotatedAssignmentStatementSyntax>())
        {
            annotations ??= new PyDict(context.MemoryGovernor, syntax.Span);
            annotations.SetItem(PyString.FromString(statement.Name), PyDataclass.CreateAnnotationValue(statement.Annotation));
        }

        if (annotations is not null)
        {
            members["__annotations__"] = annotations;
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

            current = callable.Invoke([new CallArgumentValue(null, current)], span, context);
        }

        return current;
    }

    private static void ExecuteLoweredIfStatement(LoweredIfStatement statement, ExecutionContext context)
    {
        context.EnterInterpreterFrame(statement.Span);
        try
        {
            var branch = IsTruthy(EvaluateLoweredExpression(statement.Condition, context))
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
            foreach (var item in ToSequence(iterable, statement.Iterable.Span))
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
            while (IsTruthy(EvaluateLoweredExpression(statement.Condition, context)))
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
            switch (assignment.Syntax)
            {
                case AssignmentStatementSyntax simple:
                    StoreName(simple.Name, EvaluateLoweredExpression(assignment.Expression!, context), context, assignment.Span);
                    return;
                case ChainedAssignmentStatementSyntax chained:
                    var chainedValue = EvaluateLoweredExpression(assignment.Expression!, context);
                    foreach (var assignmentTarget in chained.Targets)
                    {
                        AssignTarget(assignmentTarget, chainedValue, context);
                    }
                    return;
                case AnnotatedAssignmentStatementSyntax annotated:
                    if (assignment.Expression is not null)
                    {
                        StoreName(annotated.Name, EvaluateLoweredExpression(assignment.Expression, context), context, assignment.Span);
                    }
                    return;
                case AugmentedAssignmentStatementSyntax augmented:
                    var augmentedTarget = ResolveLoweredAugmentedAssignmentTarget(augmented, assignment, context);
                    augmentedTarget.Store(EvaluateAugmentedAssignment(
                        augmentedTarget.CurrentValue,
                        EvaluateLoweredExpression(assignment.Expression!, context),
                        augmented.Operator,
                        context,
                        augmented.Span));
                    return;
                case UnpackingAssignmentStatementSyntax unpacking:
                    AssignTargets(
                        unpacking.Targets,
                        EvaluateLoweredExpression(assignment.Expression!, context),
                        unpacking.Expression.Span,
                        context);
                    return;
                case SubscriptAssignmentStatementSyntax subscript:
                    ExecuteLoweredSubscriptAssignment(
                        subscript,
                        assignment.Target!,
                        assignment.Index!,
                        EvaluateLoweredExpression(assignment.Expression!, context),
                        context);
                    return;
                case SliceAssignmentStatementSyntax slice:
                    ExecuteLoweredSliceAssignment(
                        slice,
                        assignment.Target!,
                        assignment.Start,
                        assignment.End,
                        assignment.Step,
                        EvaluateLoweredExpression(assignment.Expression!, context),
                        context);
                    return;
                case MemberAssignmentStatementSyntax memberAssignment:
                    var target = EvaluateLoweredExpression(assignment.Target!, context);
                    var value = EvaluateLoweredExpression(assignment.Expression!, context);
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
        AugmentedAssignmentStatementSyntax statement,
        LoweredAssignmentStatement assignment,
        ExecutionContext context)
    {
        switch (statement.Target)
        {
            case NameAssignmentTargetSyntax:
                return ResolveAugmentedAssignmentTarget(statement.Target, context);

            case SubscriptAssignmentTargetSyntax subscript:
                var subscriptTarget = EvaluateLoweredExpression(assignment.Target!, context);
                var index = EvaluateLoweredExpression(assignment.Index!, context);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = EvaluateLoweredExpression(assignment.Target!, context);
                var start = assignment.Start is null ? null : EvaluateLoweredExpression(assignment.Start, context);
                var end = assignment.End is null ? null : EvaluateLoweredExpression(assignment.End, context);
                var step = assignment.Step is null ? null : EvaluateLoweredExpression(assignment.Step, context);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case MemberAssignmentTargetSyntax member:
                var memberTarget = EvaluateLoweredExpression(assignment.Target!, context);
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

    private static void ExecuteLoweredSubscriptAssignment(
        SubscriptAssignmentStatementSyntax statement,
        LoweredExpression targetExpression,
        LoweredExpression indexExpression,
        object value,
        ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(targetExpression, context);
        var index = EvaluateLoweredExpression(indexExpression, context);

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

    internal static object EvaluateLoweredExpression(LoweredExpression expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            return expression switch
            {
                LoweredIdentifierExpression identifier => ResolveIdentifier(identifier.Identifier, context),
                LoweredStringLiteralExpression text => ValidateLoweredString(CreateString(text.Literal.Value, context, text.Span), context, text.Span),
                LoweredBytesLiteralExpression bytes => CreateBytes(bytes.Literal.Value.ToArray(), context, bytes.Span),
                LoweredIntegerLiteralExpression integer => ParseInteger(integer.Literal),
                LoweredFloatLiteralExpression floating => ParseFloat(floating.Literal),
                LoweredBooleanLiteralExpression boolean => boolean.Literal.Value,
                LoweredNoneLiteralExpression => PyNone.Instance,
                LoweredFormattedStringExpression formatted => EvaluateLoweredFormattedString(formatted, context),
                LoweredParenthesizedExpression parenthesized => EvaluateLoweredExpression(parenthesized.Inner, context),
                LoweredListLiteralExpression list => ValidateLoweredCollection(CreateLoweredListLiteral(list, context), context, list.Span),
                LoweredListComprehensionExpression comprehension => EvaluateLoweredListComprehension(comprehension, context),
                LoweredGeneratorExpression generator => new PyGeneratorExpression(generator.Clauses, generator.ItemExpression, context, generator.Span),
                LoweredTupleLiteralExpression tuple => ValidateLoweredCollection(
                    CreateTuple(
                        tuple.Items.Count,
                        i => RuntimeValue(EvaluateLoweredExpression(tuple.Items[i], context)),
                        context,
                        tuple.Span),
                    context,
                    tuple.Span),
                LoweredSetLiteralExpression set => EvaluateLoweredSetLiteral(set, context),
                LoweredDictLiteralExpression dict => EvaluateLoweredDictLiteral(dict, context),
                LoweredDictComprehensionExpression comprehension => EvaluateLoweredDictComprehension(comprehension, context),
                LoweredMemberExpression member => ResolveLoweredMember(member, context),
                LoweredCallExpression call => InvokeLoweredCall(call, context),
                LoweredSubscriptExpression subscript => EvaluateLoweredSubscript(subscript, context),
                LoweredSliceExpression slice => EvaluateLoweredSlice(slice, context),
                LoweredBinaryExpression binary => EvaluateLoweredBinary(binary, context),
                LoweredChainedComparisonExpression chained => EvaluateLoweredChainedComparison(chained, context),
                LoweredUnaryExpression unary => EvaluateLoweredUnary(unary, context),
                LoweredConditionalExpression conditional => IsTruthy(EvaluateLoweredExpression(conditional.Condition, context))
                    ? EvaluateLoweredExpression(conditional.Consequent, context)
                    : EvaluateLoweredExpression(conditional.Alternative, context),
                LoweredAssignmentExpression assignment => EvaluateLoweredAssignmentExpression(assignment, context),
                LoweredLambdaExpression lambda => CreateLoweredLambda(lambda, context),
                LoweredOtherExpression other => throw new InvalidOperationException($"Generic lowered expression fallback reached for supported execution: {other.Expression.GetType().Name}"),
                _ => throw new InvalidOperationException($"Unknown lowered expression type: {expression.GetType().Name}")
            };
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

    private static PyList CreateLoweredListLiteral(LoweredListLiteralExpression list, ExecutionContext context)
    {
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(EvaluateLoweredExpression(list.Items[i], context));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static object EvaluateLoweredDictLiteral(LoweredDictLiteralExpression dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            var key = ValidateDictionaryKey(EvaluateLoweredExpression(item.Key, context), item.Key.Span, context.MemoryGovernor);
            var value = RuntimeValue(EvaluateLoweredExpression(item.Value, context));
            result.SetItem(key, value);
        }

        context.ObserveCollectionCount(result.Count, dict.Span);
        return result;
    }

    private static object EvaluateLoweredSetLiteral(LoweredSetLiteralExpression set, ExecutionContext context)
    {
        var items = new PySet(context.MemoryGovernor, set.Span);
        foreach (var item in set.Items)
        {
            items.Add(ValidateSetItem(EvaluateLoweredExpression(item, context), item.Span, context.MemoryGovernor));
        }

        context.ObserveCollectionCount(items.Count, set.Span);
        return items;
    }

    private static object EvaluateLoweredFormattedString(LoweredFormattedStringExpression formatted, ExecutionContext context)
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
                        EvaluateLoweredExpression(expression.Expression, context),
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

    private static object EvaluateLoweredListComprehension(LoweredListComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope => result.Add(RuntimeValue(EvaluateLoweredExpression(comprehension.ItemExpression, scope))));

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateLoweredDictComprehension(LoweredDictComprehensionExpression comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        EvaluateLoweredComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope =>
            {
                var key = ValidateDictionaryKey(EvaluateLoweredExpression(comprehension.KeyExpression, scope), comprehension.KeyExpression.Span, scope.MemoryGovernor);
                result.SetItem(key, RuntimeValue(EvaluateLoweredExpression(comprehension.ValueExpression, scope)));
            });

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static void EvaluateLoweredComprehensionClauses(
        IReadOnlyList<LoweredComprehensionClause> clauses,
        int index,
        ExecutionContext context,
        Action<ExecutionContext> emit)
    {
        var clause = clauses[index];
        var iterable = EvaluateLoweredExpression(clause.Iterable, context);

        foreach (var item in ToSequence(iterable, clause.Iterable.Span))
        {
            var scope = new ExecutionContext(context);
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null && !IsTruthy(EvaluateLoweredExpression(clause.Condition, scope)))
            {
                continue;
            }

            if (index == clauses.Count - 1)
            {
                emit(scope);
            }
            else
            {
                EvaluateLoweredComprehensionClauses(clauses, index + 1, scope, emit);
            }
        }
    }

    private static PyString ValidateLoweredString(PyString text, ExecutionContext context, LythonSourceSpan span)
    {
        context.ObserveString(text, span);
        return text;
    }

    private static T ValidateLoweredCollection<T>(T collection, ExecutionContext context, LythonSourceSpan span)
        where T : IReadOnlyCollection<object>
    {
        context.ObserveCollectionCount(collection.Count, span);
        return collection;
    }

    private static object EvaluateLoweredSubscript(LoweredSubscriptExpression subscript, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(subscript.Target, context);
        var index = EvaluateLoweredExpression(subscript.Index, context);
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, subscript.Span), context, subscript.Span);
        }

        return PyIndexing.ReadIndex(target, index, subscript.Span);
    }

    private static object EvaluateLoweredSlice(LoweredSliceExpression slice, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(slice.Target, context);
        var start = slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context);
        var end = slice.End is null ? null : EvaluateLoweredExpression(slice.End, context);
        var step = slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context);
        return PyIndexing.ReadSlice(target, start, end, step, slice.Span);
    }

    private static object ResolveLoweredMember(LoweredMemberExpression member, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(member.Target, context);
        if (TryResolveRuntimeMember(target, member.Member.MemberName, context, member.Span, out var value))
        {
            return value;
        }

        throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span);
    }

    private static object EvaluateLoweredBinary(LoweredBinaryExpression binary, ExecutionContext context)
    {
        if (binary.Binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateLoweredExpression(binary.Left, context);
            return IsTruthy(leftValue)
                ? leftValue
                : EvaluateLoweredExpression(binary.Right, context);
        }

        if (binary.Binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateLoweredExpression(binary.Left, context);
            return !IsTruthy(leftValue)
                ? leftValue
                : EvaluateLoweredExpression(binary.Right, context);
        }

        var left = EvaluateLoweredExpression(binary.Left, context);
        var right = EvaluateLoweredExpression(binary.Right, context);

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

    private static bool EvaluateLoweredChainedComparison(LoweredChainedComparisonExpression chained, ExecutionContext context)
    {
        var left = EvaluateLoweredExpression(chained.Operands[0], context);
        for (var i = 0; i < chained.ChainedComparison.Operators.Count; i++)
        {
            var right = EvaluateLoweredExpression(chained.Operands[i + 1], context);
            if (!EvaluateComparisonOperator(left, right, chained.ChainedComparison.Operators[i], chained.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static object EvaluateLoweredUnary(LoweredUnaryExpression unary, ExecutionContext context)
    {
        var operand = EvaluateLoweredExpression(unary.Operand, context);
        return unary.Unary.Operator switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, unary.Span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, unary.Span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, unary.Span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unary.Unary.Operator}")
        };
    }

    private static void ExecuteLoweredAssertStatement(LoweredAssertStatement statement, ExecutionContext context)
    {
        if (IsTruthy(EvaluateLoweredExpression(statement.Condition, context)))
        {
            return;
        }

        var message = statement.Message is null
            ? string.Empty
            : ToInterpolatedPyString(EvaluateLoweredExpression(statement.Message, context), context).AsString();
        throw new LythonRuntimeException("AssertionError", message, statement.Span);
    }

    private static void ExecuteLoweredDeleteStatement(LoweredDeleteStatement statement, ExecutionContext context)
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

                var target = EvaluateLoweredExpression(subscript.Target, context);
                var index = EvaluateLoweredExpression(subscript.Index, context);
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
                    EvaluateLoweredExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateLoweredExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateLoweredExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateLoweredExpression(slice.Step, context),
                    statement.Span);
                return;

            case MemberExpressionSyntax memberSyntax:
                if (statement.Target is not LoweredMemberExpression member)
                {
                    break;
                }

                var memberTarget = EvaluateLoweredExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, memberSyntax.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;
        }

        throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
    }

    private static void ExecuteLoweredRaiseStatement(LoweredRaiseStatement statement, ExecutionContext context)
    {
        var raised = EvaluateLoweredExpression(statement.Expression, context);
        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(statement.Span);
        }

        throw new LythonRuntimeException(instance.TypeName, instance.Message, statement.Span, payload: instance.Value);
    }

    private static object CreateLoweredLambda(LoweredLambdaExpression lambda, ExecutionContext context)
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
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
    }

    private static object EvaluateLoweredAssignmentExpression(LoweredAssignmentExpression assignment, ExecutionContext context)
    {
        var value = EvaluateLoweredExpression(assignment.Expression, context);
        StoreName(assignment.Assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static object InvokeLoweredCall(LoweredCallExpression call, ExecutionContext context)
    {
        var target = EvaluateLoweredExpression(call.Target, context);
        return InvokeCallableTarget(
            target,
            call.Call.Target.Span,
            call.Span,
            context,
            () => ExpandLoweredCallArguments(call.Arguments, context));
    }

    private static CallArgumentValue[] ExpandLoweredCallArguments(
        IReadOnlyList<LoweredCallArgument> arguments,
        ExecutionContext context)
        => CallExpansion.ExpandLoweredArguments(arguments, context, EvaluateLoweredExpression);

    private static void ValidateClassKeywordArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument.Name, "metaclass", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "class(..., metaclass=...) is not supported by Lython.",
                    span);
            }
        }
    }

    private static void InvokeInitSubclass(PyType type, CallArgumentValue[] keywordArguments, LythonSourceSpan span, ExecutionContext context)
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

        _ = callable.Invoke(keywordArguments, span, context);
    }
}
