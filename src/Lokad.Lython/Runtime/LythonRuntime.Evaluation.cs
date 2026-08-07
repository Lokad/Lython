using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object EvaluateExpression(ExpressionSyntax expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            var value = expression switch
            {
                IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
                StringLiteralExpressionSyntax literal => CreateString(literal.Value, context, literal.Span),
                IntegerLiteralExpressionSyntax integer => ParseInteger(integer),
                FloatLiteralExpressionSyntax floating => ParseFloat(floating),
                BooleanLiteralExpressionSyntax boolean => boolean.Value,
                NoneLiteralExpressionSyntax => PyNone.Instance,
                FormattedStringExpressionSyntax formatted => EvaluateFormattedString(formatted, context),
                ListLiteralExpressionSyntax list => CreateListLiteral(list, context),
                ListComprehensionExpressionSyntax listComprehension => EvaluateListComprehension(listComprehension, context),
                GeneratorExpressionSyntax generator => EvaluateGeneratorExpression(generator, context),
                DictLiteralExpressionSyntax dict => EvaluateDictLiteral(dict, context),
                SetLiteralExpressionSyntax set => EvaluateSetLiteral(set, context),
                SetComprehensionExpressionSyntax setComprehension => EvaluateSetComprehension(setComprehension, context),
                DictComprehensionExpressionSyntax dictComprehension => EvaluateDictComprehension(dictComprehension, context),
                TupleLiteralExpressionSyntax tuple => CreateTupleLiteral(tuple, context),
                ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Inner, context),
                BytesLiteralExpressionSyntax bytes => CreateBytes(bytes.Value.ToArray(), context, bytes.Span),
                MemberExpressionSyntax member => ResolveMember(member, context),
                CallExpressionSyntax call => InvokeCall(call, context),
                SubscriptExpressionSyntax subscript => EvaluateSubscript(subscript, context),
                SliceExpressionSyntax slice => EvaluateSlice(slice, context),
                BinaryExpressionSyntax binary => EvaluateBinary(binary, context),
                ChainedComparisonExpressionSyntax chainedComparison => EvaluateChainedComparison(chainedComparison, context),
                UnaryExpressionSyntax unary => EvaluateUnary(unary, context),
                ConditionalExpressionSyntax conditional => EvaluateConditional(conditional, context),
                AssignmentExpressionSyntax assignment => EvaluateAssignmentExpression(assignment, context),
                LambdaExpressionSyntax lambda => CreateLambda(lambda, context),
                _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
            };

            context.ObserveValue(value, expression.Span);
            return value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or RegexParseException)
        {
            var runtime = new LythonRuntimeException("RuntimeError", ex.Message, expression.Span);
            runtime.SetSourcePathIfMissing(context.SourcePath);
            throw runtime;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static PyList CreateListLiteral(ListLiteralExpressionSyntax list, ExecutionContext context)
    {
        if (list.UnpackingFlags.Any(flag => flag))
        {
            var expanded = new PyList([], context.MemoryGovernor, list.Span);
            for (var i = 0; i < list.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateExpression(list.Items[i], context));
                if (!list.UnpackingFlags[i])
                {
                    expanded.Add(value);
                    context.ObserveCollectionCount(expanded.Count, list.Span);
                    continue;
                }

                foreach (var item in ToSequence(value, list.Items[i].Span, context))
                {
                    expanded.Add(RuntimeValue(item));
                    context.ObserveCollectionCount(expanded.Count, list.Span);
                }
            }

            return expanded;
        }

        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(EvaluateExpression(list.Items[i], context));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static PyTuple CreateTupleLiteral(TupleLiteralExpressionSyntax tuple, ExecutionContext context)
    {
        if (!tuple.UnpackingFlags.Any(flag => flag))
        {
            return CreateTuple(
                tuple.Items.Count,
                i => RuntimeValue(EvaluateExpression(tuple.Items[i], context)),
                context,
                tuple.Span);
        }

        var expanded = new List<object>();
        for (var i = 0; i < tuple.Items.Count; i++)
        {
            var value = RuntimeValue(EvaluateExpression(tuple.Items[i], context));
            if (!tuple.UnpackingFlags[i])
            {
                EnsureTupleExpansionCapacity(expanded.Count + 1, context, tuple.Span);
                expanded.Add(value);
                continue;
            }

            foreach (var item in ToSequence(value, tuple.Items[i].Span, context))
            {
                EnsureTupleExpansionCapacity(expanded.Count + 1, context, tuple.Span);
                expanded.Add(RuntimeValue(item));
            }
        }

        context.ObserveCollectionCount(expanded.Count, tuple.Span);
        return new PyTuple(expanded, context.MemoryGovernor, tuple.Span);
    }

    private static void EnsureTupleExpansionCapacity(int count, ExecutionContext context, LythonSourceSpan span)
    {
        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        context.ObserveCollectionCount(count, span);
    }

    private static object EvaluateBinary(BinaryExpressionSyntax binary, ExecutionContext context)
    {
        if (binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue.RequireNotNull()
                : EvaluateExpression(binary.Right, context).RequireNotNull();
        }

        if (binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return !IsTruthy(leftValue, context, binary.Left.Span)
                ? leftValue.RequireNotNull()
                : EvaluateExpression(binary.Right, context).RequireNotNull();
        }

        var left = EvaluateExpression(binary.Left, context);
        var right = EvaluateExpression(binary.Right, context);
        if (TryEvaluateNumericProtocol(binary.Operator, left, right, context, binary.Span, out var protocolResult))
        {
            return protocolResult;
        }

        return binary.Operator switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, binary.Span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, binary.Span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, binary.Span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, binary.Span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, binary.Span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, context, binary.Span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, binary.Span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, binary.Span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, binary.Span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, binary.Span),
            BinaryOperatorSyntax.Less => EvaluateRichComparison(left, right, "__lt__", "__gt__", context, binary.Span, static value => value < 0),
            BinaryOperatorSyntax.LessEqual => EvaluateRichComparison(left, right, "__le__", "__ge__", context, binary.Span, static value => value <= 0),
            BinaryOperatorSyntax.Greater => EvaluateRichComparison(left, right, "__gt__", "__lt__", context, binary.Span, static value => value > 0),
            BinaryOperatorSyntax.GreaterEqual => EvaluateRichComparison(left, right, "__ge__", "__le__", context, binary.Span, static value => value >= 0),
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, context, binary.Span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, context, binary.Span),
            BinaryOperatorSyntax.Equal => AreEqualWithProtocols(left, right, context, binary.Span),
            BinaryOperatorSyntax.NotEqual => !AreEqualWithProtocols(left, right, context, binary.Span),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binary.Operator}")
        };
    }

    private static void ExecuteAssertStatement(AssertStatementSyntax statement, ExecutionContext context)
    {
        if (IsTruthy(EvaluateExpression(statement.Condition, context), context, statement.Condition.Span))
        {
            return;
        }

        var message = statement.Message is null
            ? string.Empty
            : ToInterpolatedPyString(EvaluateExpression(statement.Message, context), context).AsString();
        throw new LythonRuntimeException("AssertionError", message, statement.Span);
    }

    private static void ExecuteDeleteStatement(DeleteStatementSyntax statement, ExecutionContext context)
    {
        switch (statement.Target)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, statement.Span))
                {
                    throw new LythonRuntimeException("NameError", $"Name '{identifier.Name}' is not defined.", statement.Span);
                }

                return;

            case SubscriptExpressionSyntax subscript:
                var target = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
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

                    case PyInstance instance:
                        InvokeItemMutation(instance, "__delitem__", [new CallArgumentValue(null, index)], context, statement.Span);
                        return;

                    case PyTuple:
                        throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", statement.Span);

                    case string:
                        throw new LythonRuntimeException("TypeError", "String does not support item deletion.", statement.Span);

                    default:
                        throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", statement.Span);
                }

            case SliceExpressionSyntax slice:
                ExecuteSliceDeletion(
                    EvaluateExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateExpression(slice.Step, context),
                    statement.Span);
                return;

            case MemberExpressionSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, member.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;

            default:
                throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
        }
    }

    private static bool EvaluateChainedComparison(ChainedComparisonExpressionSyntax chainedComparison, ExecutionContext context)
    {
        var left = EvaluateExpression(chainedComparison.Operands[0], context);
        for (var i = 0; i < chainedComparison.Operators.Count; i++)
        {
            var right = EvaluateExpression(chainedComparison.Operands[i + 1], context);
            if (!EvaluateComparisonOperator(left, right, chainedComparison.Operators[i], context, chainedComparison.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static bool EvaluateComparisonOperator(object left, object right, BinaryOperatorSyntax op, ExecutionContext context, LythonSourceSpan span)
    {
        return op switch
        {
            BinaryOperatorSyntax.Less => EvaluateRichComparison(left, right, "__lt__", "__gt__", context, span, static value => value < 0),
            BinaryOperatorSyntax.LessEqual => EvaluateRichComparison(left, right, "__le__", "__ge__", context, span, static value => value <= 0),
            BinaryOperatorSyntax.Greater => EvaluateRichComparison(left, right, "__gt__", "__lt__", context, span, static value => value > 0),
            BinaryOperatorSyntax.GreaterEqual => EvaluateRichComparison(left, right, "__ge__", "__le__", context, span, static value => value >= 0),
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, span),
            BinaryOperatorSyntax.Equal => AreEqualWithProtocols(left, right, context, span),
            BinaryOperatorSyntax.NotEqual => !AreEqualWithProtocols(left, right, context, span),
            _ => throw new InvalidOperationException($"Unsupported chained comparison operator: {op}")
        };
    }

    private static object CreateLambda(LambdaExpressionSyntax lambda, ExecutionContext context)
    {
        var loweredParameters = lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();
        return new LambdaFunction(
            loweredParameters,
            LoweredScript.LowerStandaloneExpression(lambda.Body),
            context,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
    }

    private static void ExecuteAugmentedAssignment(AugmentedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = ResolveAugmentedAssignmentTarget(statement.Target, context);
        var right = EvaluateExpression(statement.Expression, context);
        var updated = EvaluateAugmentedAssignment(target.CurrentValue, right, statement.Operator, context, statement.Span);
        target.Store(updated);
    }

    private sealed record AugmentedAssignmentTargetReference(object CurrentValue, Action<object> Store);

    private static void ExecuteAnnotatedAssignment(AnnotatedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        if (statement.Expression is not null)
        {
            StoreName(statement.Name, EvaluateExpression(statement.Expression, context), context, statement.Span);
        }
    }

    private static void ExecuteChainedAssignment(ChainedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var value = EvaluateExpression(statement.Expression, context);
        foreach (var target in statement.Targets)
        {
            AssignTarget(target, value, context);
        }
    }

    private static void ExecuteSubscriptAssignment(SubscriptAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var index = EvaluateExpression(statement.Index, context);
        var value = EvaluateExpression(statement.Expression, context);

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

            case PyInstance instance:
                InvokeItemMutation(
                    instance,
                    "__setitem__",
                    [new CallArgumentValue(null, index), new CallArgumentValue(null, value)],
                    context,
                    statement.Span);
                return;

            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", statement.Span);

            case string:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", statement.Span);

            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", statement.Span);
        }
    }

    private static void InvokeItemMutation(
        PyInstance instance,
        string methodName,
        CallArgumentValue[] arguments,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!instance.TryGetAttribute(methodName, context, span, out var member) || member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"'{instance.Type.Name}' object does not support item mutation", span);
        }

        _ = callable.Invoke(arguments, span, context);
    }

    private static void ExecuteSliceAssignment(SliceAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var start = statement.Start is null ? null : EvaluateExpression(statement.Start, context);
        var end = statement.End is null ? null : EvaluateExpression(statement.End, context);
        var step = statement.Step is null ? null : EvaluateExpression(statement.Step, context);
        var value = EvaluateExpression(statement.Expression, context);
        ExecuteSliceAssignment(target, start, end, step, value, statement.Span, context);
    }

    private static void ExecuteMemberAssignment(MemberAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var value = EvaluateExpression(statement.Expression, context);
        if (!PyMemberAccess.TryAssign(target, statement.MemberName, value, context, statement.Span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", statement.Span);
        }
    }

    private static void AssignTarget(AssignmentTargetSyntax target, object value, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                StoreName(name.Name, value, context, name.Span);
                return;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                AssignTargets(unpacking.Targets, value, unpacking.Span, context);
                return;
            case SubscriptAssignmentTargetSyntax subscript:
                AssignSubscriptTarget(subscript, value, context);
                return;
            case SliceAssignmentTargetSyntax slice:
                AssignSliceTarget(slice, value, context);
                return;
            case MemberAssignmentTargetSyntax member:
                AssignMemberTarget(member, value, context);
                return;
            default:
                throw new InvalidOperationException($"Unsupported assignment target syntax: {target.GetType().Name}");
        }
    }

    private static void AssignSubscriptTarget(SubscriptAssignmentTargetSyntax subscript, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(subscript.Target, context);
        var index = EvaluateExpression(subscript.Index, context);
        SetSubscriptValue(target, index, value, subscript.Span, context);
    }

    private static void SetSubscriptValue(object target, object index, object value, LythonSourceSpan span, ExecutionContext context)
    {
        switch (target)
        {
            case IMutablePySubscriptableValue subscriptable:
                subscriptable.SetSubscript(index, value, span);
                return;

            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, span), value);
                return;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                dict.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                defaultDict.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, span);
                counter.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyInstance instance:
                InvokeItemMutation(
                    instance,
                    "__setitem__",
                    [new CallArgumentValue(null, index), new CallArgumentValue(null, value)],
                    context,
                    span);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", span);
            case string:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", span);
        }
    }

    private static void AssignSliceTarget(SliceAssignmentTargetSyntax slice, object value, ExecutionContext context)
    {
        ExecuteSliceAssignment(
            EvaluateExpression(slice.Target, context),
            slice.Start is null ? null : EvaluateExpression(slice.Start, context),
            slice.End is null ? null : EvaluateExpression(slice.End, context),
            slice.Step is null ? null : EvaluateExpression(slice.Step, context),
            value,
            slice.Span,
            context);
    }

    private static void AssignMemberTarget(MemberAssignmentTargetSyntax member, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(member.Target, context);
        SetMemberValue(target, member.MemberName, value, member.Span, context);
    }

    private static void SetMemberValue(object target, string memberName, object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (!PyMemberAccess.TryAssign(target, memberName, value, context, span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", span);
        }
    }

    private static AugmentedAssignmentTargetReference ResolveAugmentedAssignmentTarget(AssignmentTargetSyntax target, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                var currentValue = ResolveName(name.Name, name.Span, context);

                return new AugmentedAssignmentTargetReference(
                    currentValue,
                    value => StoreName(name.Name, value, context, name.Span));

            case SubscriptAssignmentTargetSyntax subscript:
                var subscriptTarget = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = EvaluateExpression(slice.Target, context);
                var start = slice.Start is null ? null : EvaluateExpression(slice.Start, context);
                var end = slice.End is null ? null : EvaluateExpression(slice.End, context);
                var step = slice.Step is null ? null : EvaluateExpression(slice.Step, context);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case MemberAssignmentTargetSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!TryResolveRuntimeMember(memberTarget, member.MemberName, context, member.Span, out var memberValue))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, member.MemberName, member.Span);
                }

                return new AugmentedAssignmentTargetReference(
                    memberValue,
                    value => SetMemberValue(memberTarget, member.MemberName, value, member.Span, context));

            default:
                throw new LythonRuntimeException("TypeError", "Unsupported augmented assignment target.", target.Span);
        }
    }

    private static object ReadSubscriptValue(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        if (target is PyInstance instance)
        {
            return GetUserItem(instance, index, context, span);
        }

        return PyIndexing.ReadIndex(target, CoerceIndexProtocol(index, context, span), span);
    }

    private static void ExecuteSliceAssignment(
        object target,
        object? start,
        object? end,
        object? step,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (target is PyList list)
        {
            list.AttachMemoryGovernor(context.MemoryGovernor, span);
            var values = ToSequence(value, span, context).ToArray();
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.SetSlice(bounds, values, span);
            context.ObserveCollectionCount(list.Count, span);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice assignment.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice assignment.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice assignment.", span);
    }

    private static void ExecuteSliceDeletion(
        object target,
        object? start,
        object? end,
        object? step,
        LythonSourceSpan span)
    {
        if (target is PyList list)
        {
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.DeleteSlice(bounds);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice deletion.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice deletion.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice deletion.", span);
    }

    private static void ExecuteUnpackingAssignment(UnpackingAssignmentStatementSyntax statement, ExecutionContext context)
    {
        AssignTargets(statement.Targets, EvaluateExpression(statement.Expression, context), statement.Expression.Span, context);
    }

    private static void ExecuteWithStatement(
        WithStatementSyntax statement,
        LoweredExpression? loweredContextExpression,
        IReadOnlyList<LoweredStatement>? loweredBody,
        ExecutionContext context)
    {
        _ = PyContextManagers.ExecuteWith(
            loweredContextExpression is null
                ? EvaluateExpression(statement.ContextExpression, context)
                : EvaluateLoweredExpression(loweredContextExpression, context),
            statement.VariableName,
            loweredBody ?? LoweredScript.Lower(new ScriptSyntax(statement.Body)).Statements,
            statement.Span,
            statement.ContextExpression.Span,
            context);
    }

    private static async ValueTask ExecuteWithStatementAsync(
        WithStatementSyntax statement,
        LoweredExpression loweredContextExpression,
        IReadOnlyList<LoweredStatement> loweredBody,
        ExecutionContext context)
    {
        _ = await PyContextManagers.ExecuteWithAsync(
                await EvaluateLoweredExpressionAsync(loweredContextExpression, context).ConfigureAwait(false),
                statement.VariableName,
                loweredBody,
                statement.Span,
                statement.ContextExpression.Span,
                context)
            .ConfigureAwait(false);
    }

    private static object EvaluateAugmentedAssignment(
        object currentValue,
        object right,
        AugmentedAssignmentOperatorSyntax op,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var inPlaceMethod = op switch
        {
            AugmentedAssignmentOperatorSyntax.Add => "__iadd__",
            AugmentedAssignmentOperatorSyntax.Subtract => "__isub__",
            AugmentedAssignmentOperatorSyntax.Multiply => "__imul__",
            AugmentedAssignmentOperatorSyntax.Divide => "__itruediv__",
            AugmentedAssignmentOperatorSyntax.FloorDivide => "__ifloordiv__",
            AugmentedAssignmentOperatorSyntax.Modulo => "__imod__",
            AugmentedAssignmentOperatorSyntax.Power => "__ipow__",
            AugmentedAssignmentOperatorSyntax.BitwiseOr => "__ior__",
            AugmentedAssignmentOperatorSyntax.BitwiseXor => "__ixor__",
            AugmentedAssignmentOperatorSyntax.BitwiseAnd => "__iand__",
            AugmentedAssignmentOperatorSyntax.LeftShift => "__ilshift__",
            AugmentedAssignmentOperatorSyntax.RightShift => "__irshift__",
            _ => null,
        };
        if (inPlaceMethod is not null &&
            TryInvokeBinarySpecialMethod(currentValue, inPlaceMethod, right, context, span, out var inPlaceResult))
        {
            return inPlaceResult;
        }

        if (op == AugmentedAssignmentOperatorSyntax.Add &&
            currentValue is PyList currentList)
        {
            currentList.AddRange(ToSequence(right, span, context));
            return currentList;
        }

        if (op == AugmentedAssignmentOperatorSyntax.Multiply &&
            currentValue is PyList multipliedList &&
            right is BigInteger repeatCount)
        {
            multipliedList.RepeatInPlace(ToListRepeatCount(repeatCount, span), span);
            context.ObserveCollectionCount(multipliedList.Count, span);
            return multipliedList;
        }

        if (currentValue is PySet currentSet && right is PySet rightSet)
        {
            switch (op)
            {
                case AugmentedAssignmentOperatorSyntax.BitwiseOr:
                    currentSet.UnionWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseAnd:
                    currentSet.IntersectWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseXor:
                    currentSet.SymmetricExceptWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.Subtract:
                    currentSet.ExceptWith(rightSet);
                    return currentSet;
            }
        }

        return op switch
        {
            AugmentedAssignmentOperatorSyntax.Add => EvaluateAdd(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Subtract => EvaluateSubtract(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Multiply => EvaluateMultiply(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Divide => EvaluateDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.FloorDivide => EvaluateFloorDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Modulo => EvaluateModulo(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Power => EvaluatePower(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.LeftShift => EvaluateLeftShift(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.RightShift => EvaluateRightShift(currentValue, right, span),
            _ => throw new InvalidOperationException($"Unsupported augmented assignment operator: {op}")
        };
    }

    private static object EvaluateConditional(ConditionalExpressionSyntax conditional, ExecutionContext context)
    {
        return IsTruthy(EvaluateExpression(conditional.Condition, context), context, conditional.Condition.Span)
            ? EvaluateExpression(conditional.Consequent, context)
            : EvaluateExpression(conditional.Alternative, context);
    }

    private static void ExecuteMatchStatement(MatchStatementSyntax statement, ExecutionContext context)
    {
        var subject = EvaluateExpression(statement.Subject, context);
        ExecuteMatch(statement, subject, context);
    }

    private static void ExecuteMatch(MatchStatementSyntax statement, object subject, ExecutionContext context)
    {
        foreach (var matchCase in statement.Cases)
        {
            var bindings = new Dictionary<string, object>(StringComparer.Ordinal);
            if (!TryMatchPattern(matchCase.Pattern, subject, context, bindings))
            {
                continue;
            }

            if (matchCase.Guard is not null)
            {
                var guardContext = new ExecutionContext(context);
                foreach (var pair in bindings)
                {
                    guardContext.Variables[pair.Key] = pair.Value;
                }

                if (!IsTruthy(EvaluateExpression(matchCase.Guard, guardContext)))
                {
                    continue;
                }
            }

            foreach (var pair in bindings)
            {
                StoreName(pair.Key, pair.Value, context, matchCase.Span);
            }

            var signal = ExecuteStatements(matchCase.Body, context);
            if (signal is not null)
            {
                throw signal;
            }

            return;
        }
    }

    private static bool TryMatchPattern(PatternSyntax pattern, object subject, ExecutionContext context, Dictionary<string, object> bindings)
    {
        switch (pattern)
        {
            case MatchValuePatternSyntax valuePattern:
                return AreEqual(subject, EvaluateExpression(valuePattern.Expression, context));

            case MatchSingletonPatternSyntax singletonPattern:
                return singletonPattern.Value switch
                {
                    MatchSingletonKind.None => subject is PyNone,
                    MatchSingletonKind.True => subject is bool boolean && boolean,
                    MatchSingletonKind.False => subject is bool boolean && !boolean,
                    _ => false
                };

            case MatchCapturePatternSyntax capturePattern:
                return TryBindPatternName(capturePattern.Name, subject, bindings);

            case MatchWildcardPatternSyntax:
                return true;

            case MatchSequencePatternSyntax sequencePattern:
                return TryMatchSequencePattern(sequencePattern, subject, context, bindings);

            case MatchMappingPatternSyntax mappingPattern:
                return TryMatchMappingPattern(mappingPattern, subject, context, bindings);

            case MatchClassPatternSyntax classPattern:
                return TryMatchClassPattern(classPattern, subject, context, bindings);

            case MatchStarPatternSyntax starPattern:
                return starPattern.Name is null || TryBindPatternName(starPattern.Name, subject, bindings);

            case MatchAsPatternSyntax asPattern:
                return TryMatchPattern(asPattern.Pattern, subject, context, bindings)
                    && TryBindPatternName(asPattern.Name, subject, bindings);

            case MatchOrPatternSyntax orPattern:
                foreach (var candidate in orPattern.Patterns)
                {
                    var branchBindings = new Dictionary<string, object>(bindings, StringComparer.Ordinal);
                    if (!TryMatchPattern(candidate, subject, context, branchBindings))
                    {
                        continue;
                    }

                    bindings.Clear();
                    foreach (var pair in branchBindings)
                    {
                        bindings[pair.Key] = pair.Value;
                    }

                    return true;
                }

                return false;

            default:
                throw new InvalidOperationException($"Unknown pattern type: {pattern.GetType().Name}");
        }
    }

    private static bool TryMatchSequencePattern(
        MatchSequencePatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (!TryGetPatternSequence(subject, out var items))
        {
            return false;
        }

        var starIndex = pattern.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => pair.item is MatchStarPatternSyntax).index;
        var hasStar = pattern.Items.Any(item => item is MatchStarPatternSyntax);

        if (!hasStar)
        {
            if (items.Count != pattern.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < pattern.Items.Count; i++)
            {
                if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        var beforeCount = starIndex;
        var afterCount = pattern.Items.Count - starIndex - 1;
        if (items.Count < beforeCount + afterCount)
        {
            return false;
        }

        for (var i = 0; i < beforeCount; i++)
        {
            if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
            {
                return false;
            }
        }

        var starPattern = (MatchStarPatternSyntax)pattern.Items[starIndex];
        var starItems = items.Skip(beforeCount).Take(items.Count - beforeCount - afterCount).ToArray();
        if (starPattern.Name is not null &&
            !TryBindPatternName(
                starPattern.Name,
                new PyList(starItems, context.MemoryGovernor, pattern.Span),
                bindings))
        {
            return false;
        }

        for (var i = 0; i < afterCount; i++)
        {
            var patternIndex = starIndex + 1 + i;
            var itemIndex = items.Count - afterCount + i;
            if (!TryMatchPattern(pattern.Items[patternIndex], items[itemIndex], context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchMappingPattern(
        MatchMappingPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (subject is not PyDict dict)
        {
            return false;
        }

        var matchedKeys = new HashSet<object>(new PyValueComparer());
        foreach (var item in pattern.Items)
        {
            var key = ValidateDictionaryKey(EvaluateExpression(item.Key, context), item.Key.Span, context.MemoryGovernor);
            if (!dict.TryGetValue(key, out var value))
            {
                return false;
            }

            matchedKeys.Add(key);
            if (!TryMatchPattern(item.Pattern, value, context, bindings))
            {
                return false;
            }
        }

        if (pattern.RestName is not null)
        {
            var rest = new PyDict(context.MemoryGovernor, pattern.Span);
            foreach (var pair in dict)
            {
                if (!matchedKeys.Contains(pair.Key))
                {
                    rest.SetItem(pair.Key, pair.Value);
                }
            }

            if (!TryBindPatternName(pattern.RestName, rest, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchClassPattern(
        MatchClassPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        var classValue = TryResolvePatternClassValue(pattern.ClassExpression, context);
        if (classValue is PyType runtimeType)
        {
            if (subject is not PyInstance instance || !instance.Type.IsSubtypeOf(runtimeType))
            {
                return false;
            }

            if (pattern.PositionalPatterns.Count != 0)
            {
                if (!runtimeType.TryGetMatchArgs(out var matchArgs))
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' does not define __match_args__ for positional class patterns.",
                        pattern.Span);
                }

                if (pattern.PositionalPatterns.Count > matchArgs.Count)
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' accepts {matchArgs.Count} positional class pattern argument(s), {pattern.PositionalPatterns.Count} given.",
                        pattern.Span);
                }

                for (var i = 0; i < pattern.PositionalPatterns.Count; i++)
                {
                    if (!PyMemberAccess.TryResolve(subject, matchArgs[i], context, pattern.Span, out var memberValue) ||
                        !TryMatchPattern(pattern.PositionalPatterns[i], RuntimeValue(memberValue), context, bindings))
                    {
                        return false;
                    }
                }
            }

            foreach (var keyword in pattern.KeywordPatterns)
            {
                if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
                {
                    return false;
                }

                if (!TryMatchPattern(keyword.Pattern, RuntimeValue(memberValue), context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetPatternClassName(pattern.ClassExpression, out var className))
        {
            throw new LythonRuntimeException(
                "TypeError",
                "match class patterns require a simple class name or dotted class name.",
                pattern.Span);
        }

        if (!DoesSubjectMatchPatternClass(className, subject))
        {
            return false;
        }

        if (pattern.PositionalPatterns.Count != 0)
        {
            return className switch
            {
                "list" when subject is PyList => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                "tuple" when subject is PyTuple => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                _ => throw new LythonRuntimeException(
                    "TypeError",
                    $"match class pattern '{className}(...)' does not support positional subpatterns in Lython.",
                    pattern.Span)
            };
        }

        foreach (var keyword in pattern.KeywordPatterns)
        {
            if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
            {
                return false;
            }

            memberValue = RuntimeValue(memberValue);
            if (!TryMatchPattern(keyword.Pattern, memberValue, context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPatternSequence(object subject, out IReadOnlyList<object> items)
    {
        switch (subject)
        {
            case PyList list:
                items = list.ToArray();
                return true;
            case PyTuple tuple:
                items = tuple.ToArray();
                return true;
            default:
                items = Array.Empty<object>();
                return false;
        }
    }

    private static bool TryBindPatternName(string name, object value, Dictionary<string, object> bindings)
    {
        value = RuntimeValue(value);
        if (bindings.TryGetValue(name, out var existing))
        {
            return AreEqual(existing, value);
        }

        bindings[name] = value;
        return true;
    }

    private static bool TryGetPatternClassName(ExpressionSyntax expression, out string className)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                className = identifier.Name;
                return true;
            case MemberExpressionSyntax member when TryGetPatternClassName(member.Target, out var prefix):
                className = prefix + "." + member.MemberName;
                return true;
            default:
                className = string.Empty;
                return false;
        }
    }

    private static object? TryResolvePatternClassValue(ExpressionSyntax expression, ExecutionContext context)
    {
        return expression switch
        {
            IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
            MemberExpressionSyntax member => ResolveMember(member, context),
            _ => null
        };
    }

    private static bool DoesSubjectMatchPatternClass(string className, object subject)
    {
        return className switch
        {
            "list" => subject is PyList,
            "tuple" => subject is PyTuple,
            "dict" => subject is PyDict,
            "set" => subject is PySet,
            "str" => subject is PyString or string,
            "bytes" => subject is PyBytes,
            "bool" => subject is bool,
            "int" => subject is BigInteger or int or bool,
            "float" => subject is double,
            "pathlib.Path" => subject is PyPath,
            "datetime.timedelta" => subject is PyTimedelta,
            "datetime.date" => subject is PyDate,
            "datetime.time" => subject is PyTime,
            "datetime.datetime" => subject is PyDateTime,
            "datetime.timezone" => subject is PyTimezone,
            "statistics.NormalDist" => subject is StatisticsModule.PyNormalDist,
            "random.Random" => subject is RandomModule.PyRandom,
            "re.Match" => subject is ReMatchObject,
            "re.Pattern" => subject is RePatternObject,
            _ => false
        };
    }

    private static IReadOnlyList<PyType> ResolveClassBases(object[] baseValues, LythonSourceSpan span, ExecutionContext context)
    {
        if (baseValues.Length == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        var bases = new List<PyType>(baseValues.Length);
        foreach (var baseValue in baseValues)
        {
            if (baseValue is PyTypingAlias { IsInertClassBase: true })
            {
                continue;
            }

            if (baseValue is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "Class bases must be user-defined Lython classes.", span);
            }

            bases.Add(type);
        }

        if (bases.Count == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        return bases;
    }

    private static object EvaluateAssignmentExpression(AssignmentExpressionSyntax assignment, ExecutionContext context)
    {
        var value = EvaluateExpression(assignment.Expression, context);
        StoreName(assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static object EvaluateUnary(UnaryExpressionSyntax unary, ExecutionContext context)
    {
        var operand = EvaluateExpression(unary.Operand, context);
        var method = unary.Operator switch
        {
            UnaryOperatorSyntax.Plus => "__pos__",
            UnaryOperatorSyntax.Minus => "__neg__",
            UnaryOperatorSyntax.BitwiseNot => "__invert__",
            _ => null,
        };
        if (method is not null && TryInvokeUnarySpecialMethod(operand, method, context, unary.Span, out var protocolResult))
        {
            return protocolResult;
        }

        return unary.Operator switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand, context, unary.Span),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, unary.Span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, unary.Span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, unary.Span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unary.Operator}")
        };
    }

    private static BigInteger ParseInteger(IntegerLiteralExpressionSyntax integer)
    {
        return PyNumberOps.ParseInteger(integer.ValueText);
    }

    private static double ParseFloat(FloatLiteralExpressionSyntax floating)
    {
        return PyNumberOps.ParseFloat(floating.ValueText);
    }

    private static object EvaluateAdd(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Add(left, right, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return ConcatStrings(leftText, rightText, context, span);
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            var governor = leftList.OwnerMemoryGovernor ?? rightList.OwnerMemoryGovernor;
            var allocationSpan = leftList.AllocationSpan ?? rightList.AllocationSpan;
            var result = governor is null
                ? new PyList(leftList)
                : new PyList(leftList.ToArray(), governor, allocationSpan);
            result.AddRange(rightList);
            return result;
        }

        if (left is PyTuple leftTuple && right is PyTuple rightTuple)
        {
            var governor = leftTuple.OwnerMemoryGovernor ?? rightTuple.OwnerMemoryGovernor;
            var allocationSpan = leftTuple.AllocationSpan ?? rightTuple.AllocationSpan;
            return governor is null
                ? new PyTuple(leftTuple.Concat(rightTuple))
                : new PyTuple(leftTuple.Concat(rightTuple), governor, allocationSpan);
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => AddCounterCounts(lhs, rhs, span), keepPositiveOnly: true, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Add(left, right, span);
        }

        if (StatisticsModule.TryAddNormalDist(left, right, span, out var normalDistSum))
        {
            return normalDistSum;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '+'.", span);
        }

        return PyNumberOps.Add(lhs, rhs);
    }

    internal static object AddRuntimeValues(object left, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAdd(left, right, context, span);

    private static object EvaluateSubtract(object left, object right, LythonSourceSpan span)
    {
        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.ExceptWith(rightSet);
            return result;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => SubtractCounterCounts(lhs, rhs, span), keepPositiveOnly: true, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Subtract(left, right, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Subtract(left, right, span);
        }

        if (StatisticsModule.TrySubtractNormalDist(left, right, span, out var normalDistDifference))
        {
            return normalDistDifference;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '-'.", span);
        }

        return PyNumberOps.Subtract(lhs, rhs);
    }

    private static object EvaluateMultiply(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Multiply(left, right, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && right is BigInteger rightCount)
        {
            return RepeatString(leftText, rightCount, context, span);
        }

        if (PyStringOps.TryAsString(right, out var rightText) && left is BigInteger leftCount)
        {
            return RepeatString(rightText, leftCount, context, span);
        }

        if (left is PyList leftList && right is BigInteger rightRepeatCount)
        {
            return RepeatList(leftList, rightRepeatCount, context, span);
        }

        if (right is PyList rightList && left is BigInteger leftRepeatCount)
        {
            return RepeatList(rightList, leftRepeatCount, context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Multiply(left, right, span);
        }

        if (StatisticsModule.TryMultiplyNormalDist(left, right, span, out var normalDistProduct))
        {
            return normalDistProduct;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '*'.", span);
        }

        return PyNumberOps.Multiply(lhs, rhs);
    }

    private static object EvaluateDivide(object left, object right, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Divide(left, right, span);
        }

        if (left is PyPath leftPath)
        {
            if (right is PyPath rightPath)
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightPath.Value));
            }

            if (PyStringOps.TryAsString(right, out var rightText))
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightText));
            }
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Divide(left, right, span);
        }

        if (StatisticsModule.TryDivideNormalDist(left, right, span, out var normalDistQuotient))
        {
            return normalDistQuotient;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '/'.", span);
        }

        try
        {
            return PyNumberOps.TrueDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "division by zero", span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateFloorDivide(object left, object right, LythonSourceSpan span)
    {
        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.FloorDivide(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '//'.", span);
        }

        try
        {
            return PyNumberOps.FloorDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluateModulo(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyString template)
        {
            return FormatPercentString(template, right, context, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Modulo(left, right, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Modulo(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '%'.", span);
        }

        try
        {
            return PyNumberOps.Modulo(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ZeroDivisionError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluatePower(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Power(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }

        try
        {
            if (lhs.IsZero && (rhs.IsFloat ? rhs.Floating < 0 : rhs.Integer < BigInteger.Zero))
            {
                throw new LythonRuntimeException("ZeroDivisionError", "0.0 cannot be raised to a negative power", span);
            }

            var leftValue = lhs.IsFloat ? lhs.Floating : (double)lhs.Integer;
            var rightValue = rhs.IsFloat ? rhs.Floating : (double)rhs.Integer;
            if (leftValue < 0 && double.IsFinite(rightValue) && rightValue != Math.Truncate(rightValue))
            {
                throw new LythonRuntimeException("TypeError", "complex results are not supported by Lython", span);
            }

            GuardIntegerPower(lhs, rhs, context, span);
            return PyNumberOps.Power(lhs, rhs);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }
    }

}
