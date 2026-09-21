using System.Numerics;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum SpecialMethodInvocationKind
    {
        Missing,
        Invoked,
    }

    private readonly record struct SpecialMethodInvocation(
        SpecialMethodInvocationKind Kind,
        object Value)
    {
        public static SpecialMethodInvocation Missing => new(SpecialMethodInvocationKind.Missing, PyNone.Instance);

        public static SpecialMethodInvocation Invoked(object value) => new(SpecialMethodInvocationKind.Invoked, value);
    }

    private readonly record struct BinarySpecialMethodPair(string Left, string Right);

    private delegate ValueTask<SpecialMethodInvocation> BinarySpecialMethodInvoker(
        object target,
        string method,
        object argument,
        ExecutionContext context,
        LythonSourceSpan span);

    private delegate ValueTask<bool> TruthinessEvaluator(
        object value,
        ExecutionContext context,
        LythonSourceSpan span);

    private static object EvaluateBinaryOperator(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (TryEvaluateNumericProtocol(op, left, right, context, span, out var protocolResult))
        {
            return protocolResult;
        }

        return op switch
        {
            BinaryOperatorSyntax.Less => EvaluateRichComparison(left, right, "__lt__", "__gt__", context, span, static value => value < 0),
            BinaryOperatorSyntax.LessEqual => EvaluateRichComparison(left, right, "__le__", "__ge__", context, span, static value => value <= 0),
            BinaryOperatorSyntax.Greater => EvaluateRichComparison(left, right, "__gt__", "__lt__", context, span, static value => value > 0),
            BinaryOperatorSyntax.GreaterEqual => EvaluateRichComparison(left, right, "__ge__", "__le__", context, span, static value => value >= 0),
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, context, span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, context, span),
            BinaryOperatorSyntax.Equal => AreEqualWithProtocols(left, right, context, span),
            BinaryOperatorSyntax.NotEqual => AreNotEqualWithProtocols(left, right, context, span),
            _ => EvaluateBinaryOperatorWithoutProtocols(op, left, right, context, span),
        };
    }

    private static async ValueTask<object> EvaluateBinaryOperatorAsync(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var protocol = await EvaluateNumericProtocolAsync(op, left, right, context, span).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
        {
            return protocol.Value;
        }

        return op switch
        {
            BinaryOperatorSyntax.Less => await EvaluateRichComparisonAsync(left, right, "__lt__", "__gt__", context, span, static value => value < 0).ConfigureAwait(false),
            BinaryOperatorSyntax.LessEqual => await EvaluateRichComparisonAsync(left, right, "__le__", "__ge__", context, span, static value => value <= 0).ConfigureAwait(false),
            BinaryOperatorSyntax.Greater => await EvaluateRichComparisonAsync(left, right, "__gt__", "__lt__", context, span, static value => value > 0).ConfigureAwait(false),
            BinaryOperatorSyntax.GreaterEqual => await EvaluateRichComparisonAsync(left, right, "__ge__", "__le__", context, span, static value => value >= 0).ConfigureAwait(false),
            BinaryOperatorSyntax.In => await ContainsAsync(right, left, context, span).ConfigureAwait(false),
            BinaryOperatorSyntax.NotIn => !await ContainsAsync(right, left, context, span).ConfigureAwait(false),
            BinaryOperatorSyntax.Equal => await AreEqualWithProtocolsAsync(left, right, context, span).ConfigureAwait(false),
            BinaryOperatorSyntax.NotEqual => await AreNotEqualWithProtocolsAsync(left, right, context, span).ConfigureAwait(false),
            _ => EvaluateBinaryOperatorWithoutProtocols(op, left, right, context, span),
        };
    }

    private static object EvaluateBinaryOperatorWithoutProtocols(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
        => op switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, context, span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, context, span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, context, span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, context, span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, context, span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, context, span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, context, span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, context, span),
            BinaryOperatorSyntax.Is => AreIdentical(left, right),
            BinaryOperatorSyntax.IsNot => !AreIdentical(left, right),
            _ => throw new InvalidOperationException($"Unknown eager binary operator: {op}"),
        };

    private static object EvaluateUnaryOperator(
        UnaryOperatorSyntax op,
        object operand,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var method = UnarySpecialMethod(op);
        if (method is not null && TryInvokeUnarySpecialMethod(operand, method, context, span, out var protocolResult))
        {
            return protocolResult;
        }

        return EvaluateUnaryOperatorWithoutProtocols(op, operand, context, span);
    }

    private static async ValueTask<object> EvaluateUnaryOperatorAsync(
        UnaryOperatorSyntax op,
        object operand,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (op == UnaryOperatorSyntax.Not)
        {
            return !await IsTruthyAsync(operand, context, span).ConfigureAwait(false);
        }

        var method = UnarySpecialMethod(op);
        if (method is not null)
        {
            var protocol = await InvokeUnarySpecialMethodAsync(operand, method, context, span).ConfigureAwait(false);
            if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            {
                return protocol.Value;
            }
        }

        return EvaluateUnaryOperatorWithoutProtocols(op, operand, context, span);
    }

    private static object EvaluateUnaryOperatorWithoutProtocols(
        UnaryOperatorSyntax op,
        object operand,
        ExecutionContext context,
        LythonSourceSpan span)
        => op switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand, context, span),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, context, span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, context, span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, context, span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {op}"),
        };

    private static string? UnarySpecialMethod(UnaryOperatorSyntax op)
        => op switch
        {
            UnaryOperatorSyntax.Plus => "__pos__",
            UnaryOperatorSyntax.Minus => "__neg__",
            UnaryOperatorSyntax.BitwiseNot => "__invert__",
            _ => null,
        };

    private static bool TryEvaluateNumericProtocol(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        out object result)
    {
        // Sync twin of the async policy core below. The miss path runs per
        // binary operation, so routing it through an async state machine
        // allocates on every sync operator even though the sync invoker
        // below never suspends.
        if (op == BinaryOperatorSyntax.Modulo && left is Runtime.Text.PyString)
        {
            result = PyNone.Instance;
            return false;
        }

        if (!TryGetNumericProtocolMethods(op, out var methods))
        {
            result = PyNone.Instance;
            return false;
        }

        if (TryInvokeBinarySpecialMethod(left, methods.Left, right, context, span, out var leftValue) &&
            leftValue is not PyNotImplemented)
        {
            result = leftValue;
            return true;
        }

        if (TryInvokeBinarySpecialMethod(right, methods.Right, left, context, span, out var rightValue) &&
            rightValue is not PyNotImplemented)
        {
            result = rightValue;
            return true;
        }

        result = PyNone.Instance;
        return false;
    }

    private static bool TryGetNumericProtocolMethods(
        BinaryOperatorSyntax op,
        out BinarySpecialMethodPair methods)
    {
        BinarySpecialMethodPair? resolved = op switch
        {
            BinaryOperatorSyntax.Add => new BinarySpecialMethodPair("__add__", "__radd__"),
            BinaryOperatorSyntax.Subtract => new BinarySpecialMethodPair("__sub__", "__rsub__"),
            BinaryOperatorSyntax.Multiply => new BinarySpecialMethodPair("__mul__", "__rmul__"),
            BinaryOperatorSyntax.Divide => new BinarySpecialMethodPair("__truediv__", "__rtruediv__"),
            BinaryOperatorSyntax.FloorDivide => new BinarySpecialMethodPair("__floordiv__", "__rfloordiv__"),
            BinaryOperatorSyntax.Modulo => new BinarySpecialMethodPair("__mod__", "__rmod__"),
            BinaryOperatorSyntax.Power => new BinarySpecialMethodPair("__pow__", "__rpow__"),
            BinaryOperatorSyntax.BitwiseOr => new BinarySpecialMethodPair("__or__", "__ror__"),
            BinaryOperatorSyntax.BitwiseXor => new BinarySpecialMethodPair("__xor__", "__rxor__"),
            BinaryOperatorSyntax.BitwiseAnd => new BinarySpecialMethodPair("__and__", "__rand__"),
            BinaryOperatorSyntax.LeftShift => new BinarySpecialMethodPair("__lshift__", "__rlshift__"),
            BinaryOperatorSyntax.RightShift => new BinarySpecialMethodPair("__rshift__", "__rrshift__"),
            _ => (BinarySpecialMethodPair?)null,
        };
        if (resolved is not { } pair)
        {
            methods = default;
            return false;
        }

        methods = pair;
        return true;
    }

    private static ValueTask<SpecialMethodInvocation> EvaluateNumericProtocolAsync(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
        => EvaluateNumericProtocolCoreAsync(op, left, right, context, span, InvokeBinarySpecialMethodAsync);

    private static async ValueTask<SpecialMethodInvocation> EvaluateNumericProtocolCoreAsync(
        BinaryOperatorSyntax op,
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke)
    {
        if (op == BinaryOperatorSyntax.Modulo && left is Runtime.Text.PyString)
        {
            return SpecialMethodInvocation.Missing;
        }

        var methods = op switch
        {
            BinaryOperatorSyntax.Add => new BinarySpecialMethodPair("__add__", "__radd__"),
            BinaryOperatorSyntax.Subtract => new BinarySpecialMethodPair("__sub__", "__rsub__"),
            BinaryOperatorSyntax.Multiply => new BinarySpecialMethodPair("__mul__", "__rmul__"),
            BinaryOperatorSyntax.Divide => new BinarySpecialMethodPair("__truediv__", "__rtruediv__"),
            BinaryOperatorSyntax.FloorDivide => new BinarySpecialMethodPair("__floordiv__", "__rfloordiv__"),
            BinaryOperatorSyntax.Modulo => new BinarySpecialMethodPair("__mod__", "__rmod__"),
            BinaryOperatorSyntax.Power => new BinarySpecialMethodPair("__pow__", "__rpow__"),
            BinaryOperatorSyntax.BitwiseOr => new BinarySpecialMethodPair("__or__", "__ror__"),
            BinaryOperatorSyntax.BitwiseXor => new BinarySpecialMethodPair("__xor__", "__rxor__"),
            BinaryOperatorSyntax.BitwiseAnd => new BinarySpecialMethodPair("__and__", "__rand__"),
            BinaryOperatorSyntax.LeftShift => new BinarySpecialMethodPair("__lshift__", "__rlshift__"),
            BinaryOperatorSyntax.RightShift => new BinarySpecialMethodPair("__rshift__", "__rrshift__"),
            _ => (BinarySpecialMethodPair?)null,
        };
        if (methods is not { } resolvedMethods)
        {
            return SpecialMethodInvocation.Missing;
        }

        // A NotImplemented answer declines to the reflected slot like the other
        // protocol cores; when both sides decline the operator falls back instead
        // of leaking NotImplemented as a value.
        var leftInvocation = await invoke(left, resolvedMethods.Left, right, context, span).ConfigureAwait(false);
        if (leftInvocation.Kind == SpecialMethodInvocationKind.Invoked && leftInvocation.Value is not PyNotImplemented)
        {
            return leftInvocation;
        }

        var rightInvocation = await invoke(right, resolvedMethods.Right, left, context, span).ConfigureAwait(false);
        return rightInvocation.Kind == SpecialMethodInvocationKind.Invoked && rightInvocation.Value is not PyNotImplemented
            ? rightInvocation
            : SpecialMethodInvocation.Missing;
    }

    private static ValueTask<SpecialMethodInvocation> InvokeBinarySpecialMethod(
        object target,
        string method,
        object argument,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!TryResolveSpecialMethodCallable(target, method, context, span, out var callable))
        {
            return new ValueTask<SpecialMethodInvocation>(SpecialMethodInvocation.Missing);
        }

        return new ValueTask<SpecialMethodInvocation>(
            SpecialMethodInvocation.Invoked(CallableInvocation.InvokeUnary(callable, argument, span, context)));
    }

    private static async ValueTask<SpecialMethodInvocation> InvokeBinarySpecialMethodAsync(
        object target,
        string method,
        object argument,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!TryResolveSpecialMethodCallable(target, method, context, span, out var callable))
        {
            return SpecialMethodInvocation.Missing;
        }

        var value = await CallableInvocation.InvokeUnaryAsync(callable, argument, span, context).ConfigureAwait(false);
        return SpecialMethodInvocation.Invoked(value);
    }

    private static async ValueTask<SpecialMethodInvocation> InvokeUnarySpecialMethodAsync(
        object target,
        string method,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // A resolved non-callable member raises not-callable like CPython
        // instead of reading as missing (see the sync twin below).
        if (target is PyInstance unaryInstance &&
            unaryInstance.TryGetAttribute(method, context, span, out var unaryMember))
        {
            if (unaryMember is not ICallable unaryCallable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(unaryMember, context) + "' object is not callable", span);
            }

            var value = await unaryCallable.InvokeAsync([], span, context).ConfigureAwait(false);
            return SpecialMethodInvocation.Invoked(value);
        }

        return SpecialMethodInvocation.Missing;
    }

    private static bool TryResolveSpecialMethodCallable(
        object target,
        string method,
        ExecutionContext context,
        LythonSourceSpan span,
        [MaybeNullWhen(false)] out ICallable callable)
    {
        if (target is PyInstance instance &&
            instance.TryGetAttribute(method, context, span, out var member) &&
            member is ICallable resolved)
        {
            callable = resolved;
            return true;
        }

        callable = null;
        return false;
    }

    private static bool TryInvokeBinarySpecialMethod(
        object target,
        string method,
        object argument,
        ExecutionContext context,
        LythonSourceSpan span,
        out object result)
    {
        var invocation = InvokeBinarySpecialMethod(target, method, argument, context, span)
            .GetAwaiter()
            .GetResult();
        result = invocation.Value;
        return invocation.Kind == SpecialMethodInvocationKind.Invoked;
    }

    private static bool TryInvokeUnarySpecialMethod(
        object target,
        string method,
        ExecutionContext context,
        LythonSourceSpan span,
        out object result)
    {
        // A resolved non-callable member raises not-callable like CPython
        // instead of reading as missing (round/pow builtins do the same).
        if (target is PyInstance unaryInstance &&
            unaryInstance.TryGetAttribute(method, context, span, out var unaryMember))
        {
            if (unaryMember is not ICallable unaryCallable)
            {
                throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(unaryMember, context) + "' object is not callable", span);
            }

            result = unaryCallable.Invoke([], span, context);
            return true;
        }

        result = PyNone.Instance;
        return false;
    }

    private static bool AreEqualWithProtocols(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // Sync twin of the async core below. The miss path runs per
        // comparison, so routing it through an async state machine
        // allocates on every sync == even though the sync invoker and
        // truthiness checks below never suspend.
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return leftKey.CompareTo(rightKey, span, context) == 0;
        }

        // A NotImplemented answer declines like a missing slot (CPython reflected
        // dispatch): the root object slots always decline, so operators keep their
        // identity and relational fallbacks instead of reading NotImplemented as true.
        if (TryInvokeBinarySpecialMethod(left, "__eq__", right, context, span, out var leftValue) &&
            leftValue is not PyNotImplemented)
        {
            return IsTruthy(leftValue, context, span);
        }

        if (TryInvokeBinarySpecialMethod(right, "__eq__", left, context, span, out var rightValue) &&
            rightValue is not PyNotImplemented)
        {
            return IsTruthy(rightValue, context, span);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return AreEqual(left, right);
        }
    }

    private static ValueTask<bool> AreEqualWithProtocolsAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
        => AreEqualWithProtocolsCoreAsync(left, right, context, span, InvokeBinarySpecialMethodAsync, IsTruthyAsync);

    private static async ValueTask<bool> AreEqualWithProtocolsCoreAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return leftKey.CompareTo(rightKey, span, context) == 0;
        }

        // A NotImplemented answer declines like a missing slot (CPython reflected
        // dispatch): the root object slots always decline, so operators keep their
        // identity and relational fallbacks instead of reading NotImplemented as true.
        var invocation = await invoke(left, "__eq__", right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing || invocation.Value is PyNotImplemented)
        {
            invocation = await invoke(right, "__eq__", left, context, span).ConfigureAwait(false);
        }

        if (invocation.Kind == SpecialMethodInvocationKind.Invoked && invocation.Value is not PyNotImplemented)
        {
            return await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            // Async structural twin of the __eq__ members and AreEqual branches:
            // sequence elements and mapping values await element == so
            // suspending __eq__ (for example over delayed host reads) works in
            // nested positions. Keys stay structural until R13b.
            if (left is PyList leftList && right is PyList rightList)
            {
                return await ListsEqualAsync(leftList, rightList, context, span).ConfigureAwait(false);
            }

            if (PyTupleLike.TryGetItems(left, out var leftTupleItems) && PyTupleLike.TryGetItems(right, out var rightTupleItems))
            {
                return await TupleLikesEqualAsync(left, right, leftTupleItems, rightTupleItems, context, span).ConfigureAwait(false);
            }

            if (left is PyDeque leftDeque && right is PyDeque rightDeque)
            {
                return await DequesEqualAsync(leftDeque, rightDeque, context, span).ConfigureAwait(false);
            }

            var dictResult = await DictFamilyEqualAsync(left, right, context, span).ConfigureAwait(false);
            if (dictResult.HasValue)
            {
                return dictResult.Value;
            }

            return AreEqual(left, right);
        }
    }

    private static async ValueTask<bool> ListsEqualAsync(PyList left, PyList right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        using (PyStructuralGuard.EnterPair(left, right, span))
        {
            for (var i = 0; i < left.Count; i++)
            {
                PyStructuralGuard.NoteWork();
                if (!await ElementEqualsAsync(left[i], right[i], context, span).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static async ValueTask<bool> TupleLikesEqualAsync(object left, object right, IReadOnlyList<object> leftItems, IReadOnlyList<object> rightItems, ExecutionContext context, LythonSourceSpan span)
    {
        if (leftItems.Count != rightItems.Count)
        {
            return false;
        }

        using (PyStructuralGuard.EnterPair(left, right, span))
        {
            for (var i = 0; i < leftItems.Count; i++)
            {
                PyStructuralGuard.NoteWork();
                if (!await ElementEqualsAsync(leftItems[i], rightItems[i], context, span).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static async ValueTask<bool> DequesEqualAsync(PyDeque left, PyDeque right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        using (PyStructuralGuard.EnterPair(left, right, span))
        {
            using var leftItems = left.GetEnumerator();
            using var rightItems = right.GetEnumerator();
            while (leftItems.MoveNext())
            {
                _ = rightItems.MoveNext();
                PyStructuralGuard.NoteWork();
                if (!await ElementEqualsAsync(leftItems.Current, rightItems.Current, context, span).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static async ValueTask<bool> DictContentEqualAsync(
        int leftCount,
        IEnumerable<KeyValuePair<object, object>> leftPairs,
        int rightCount,
        Func<object, (bool Found, object? Value)> rightLookup,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (leftCount != rightCount)
        {
            return false;
        }

        foreach (var pair in leftPairs)
        {
            PyStructuralGuard.NoteWork();
            var (found, other) = rightLookup(pair.Key);
            if (!found || !await ElementEqualsAsync(pair.Value, other!, context, span).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<bool> ChainMapContentEqualAsync(
        PyChainMap leftChain,
        int rightCount,
        Func<object, (bool Found, object? Value)> rightLookup,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var leftKeys = leftChain.BuildMergedKeys();
        if (leftKeys.Count != rightCount)
        {
            return false;
        }

        foreach (var key in leftKeys)
        {
            PyStructuralGuard.NoteWork();
            if (!leftChain.TryGetStrictValue(key, out var leftValue))
            {
                return false;
            }

            var (found, other) = rightLookup(key);
            if (!found || !await ElementEqualsAsync(leftValue ?? PyNone.Instance, other!, context, span).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<bool> ChainMapContentEqualRightAsync(
        PyChainMap rightChain,
        int leftCount,
        Func<object, (bool Found, object? Value)> leftLookup,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var rightKeys = rightChain.BuildMergedKeys();
        if (rightKeys.Count != leftCount)
        {
            return false;
        }

        foreach (var key in rightKeys)
        {
            PyStructuralGuard.NoteWork();
            if (!rightChain.TryGetStrictValue(key, out var rightValue))
            {
                return false;
            }

            var (found, other) = leftLookup(key);
            if (!found || !await ElementEqualsAsync(other!, rightValue ?? PyNone.Instance, context, span).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<bool> CountersEqualAsync(PyCounter left, PyCounter right, ExecutionContext context, LythonSourceSpan span)
    {
        var keys = new HashSet<object>(left.Keys, PyValueComparer.Instance);
        keys.UnionWith(right.Keys);

        foreach (var key in keys)
        {
            PyStructuralGuard.NoteWork();
            var leftValue = left.TryGetValue(key, out var foundLeft) ? foundLeft : BigInteger.Zero;
            var rightValue = right.TryGetValue(key, out var foundRight) ? foundRight : BigInteger.Zero;
            if (!await ElementEqualsAsync(leftValue, rightValue, context, span).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    // Mirrors PyEquality dict-family dispatch; a null result falls back to
    // structural AreEqual. Key lookups stay synchronous and structural.
    private static async ValueTask<bool?> DictFamilyEqualAsync(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDict leftDict && right is PyDict rightDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await DictContentEqualAsync(
                    leftDict.Count,
                    leftDict,
                    rightDict.Count,
                    key => rightDict.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context,
                    span).ConfigureAwait(false);
            }
        }

        if (left is PyChainMap leftChain && right is PyChainMap rightChain)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                var leftKeys = leftChain.BuildMergedKeys();
                if (leftKeys.Count != rightChain.Count)
                {
                    return false;
                }

                foreach (var key in leftKeys)
                {
                    PyStructuralGuard.NoteWork();
                    if (!leftChain.TryGetStrictValue(key, out var leftValue))
                    {
                        return false;
                    }

                    if (!rightChain.TryGetStrictValue(key, out var rightValue))
                    {
                        return false;
                    }

                    if (!await ElementEqualsAsync(leftValue ?? PyNone.Instance, rightValue ?? PyNone.Instance, context, span).ConfigureAwait(false))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        if (left is PyChainMap leftMap && right is PyDict rightPlain)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualAsync(leftMap, rightPlain.Count,
                    key => rightPlain.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (left is PyChainMap leftDefaultMap && right is PyDefaultDict rightDefault)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualAsync(leftDefaultMap, rightDefault.Count,
                    key => rightDefault.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (left is PyChainMap leftCounterMap && right is PyCounter rightCounterMap)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualAsync(leftCounterMap, rightCounterMap.Count,
                    key => rightCounterMap.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (right is PyChainMap rightChainMap && left is PyDict leftPlain)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualRightAsync(rightChainMap, leftPlain.Count,
                    key => leftPlain.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (right is PyChainMap rightDefaultChain && left is PyDefaultDict leftDefaultOther)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualRightAsync(rightDefaultChain, leftDefaultOther.Count,
                    key => leftDefaultOther.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (right is PyChainMap rightCounterChain && left is PyCounter leftCounterOperand)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await ChainMapContentEqualRightAsync(rightCounterChain, leftCounterOperand.Count,
                    key => leftCounterOperand.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                    context, span).ConfigureAwait(false);
            }
        }

        if (left is PyCounter counterLeft && (right is PyDict || right is PyDefaultDict))
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await CounterDictContentEqualAsync(counterLeft, right, context, span).ConfigureAwait(false);
            }
        }

        if (right is PyCounter counterRight && (left is PyDict || left is PyDefaultDict))
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await CounterDictContentEqualAsync(counterRight, left, context, span).ConfigureAwait(false);
            }
        }

        if (left is PyDefaultDict leftDefaultDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await DefaultDictContentEqualAsync(leftDefaultDict, right, context, span).ConfigureAwait(false);
            }
        }

        if (right is PyDefaultDict rightDefaultDict)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await DefaultDictContentEqualAsync(rightDefaultDict, left, context, span).ConfigureAwait(false);
            }
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            using (PyStructuralGuard.EnterPair(left, right, span))
            {
                return await CountersEqualAsync(leftCounter, rightCounter, context, span).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static async ValueTask<bool> CounterDictContentEqualAsync(PyCounter counter, object other, ExecutionContext context, LythonSourceSpan span)
    {
        if (other is PyDict plain)
        {
            return await DictContentEqualAsync(
                counter.Count,
                counter.Items,
                plain.Count,
                key => plain.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                context, span).ConfigureAwait(false);
        }

        if (other is PyDefaultDict fellow)
        {
            return await DictContentEqualAsync(
                counter.Count,
                counter.Items,
                fellow.Count,
                key => fellow.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                context, span).ConfigureAwait(false);
        }

        return false;
    }

    private static async ValueTask<bool> DefaultDictContentEqualAsync(PyDefaultDict candidate, object other, ExecutionContext context, LythonSourceSpan span)
    {
        if (other is PyDict plain)
        {
            return await DictContentEqualAsync(
                candidate.Count,
                candidate.Items,
                plain.Count,
                key => plain.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                context, span).ConfigureAwait(false);
        }

        if (other is PyDefaultDict fellow)
        {
            return await DictContentEqualAsync(
                candidate.Count,
                candidate.Items,
                fellow.Count,
                key => fellow.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                context, span).ConfigureAwait(false);
        }

        if (other is PyCounter counter)
        {
            return await DictContentEqualAsync(
                candidate.Count,
                candidate.Items,
                counter.Count,
                key => counter.TryGetValue(key, out var value, context, span) ? (true, value) : (false, null),
                context, span).ConfigureAwait(false);
        }

        return false;
    }

    // Sequence membership consults the member __eq__ protocol like == does,
    // with CPython identity shortcut first (an identical object matches
    // without invoking __eq__). Sync twin of the async core below.
    internal static bool MembershipEquals(
        object item,
        object candidate,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (ReferenceEquals(item, candidate))
        {
            return true;
        }

        if (item is PyCmpKey itemKey && candidate is PyCmpKey candidateKey)
        {
            return itemKey.CompareTo(candidateKey, span, context) == 0;
        }

        if (TryInvokeBinarySpecialMethod(item, "__eq__", candidate, context, span, out var leftValue) &&
            leftValue is not PyNotImplemented)
        {
            return IsTruthy(leftValue, context, span);
        }

        if (TryInvokeBinarySpecialMethod(candidate, "__eq__", item, context, span, out var rightValue) &&
            rightValue is not PyNotImplemented)
        {
            return IsTruthy(rightValue, context, span);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return AreEqual(item, candidate);
        }
    }

    internal static ValueTask<bool> MembershipEqualsAsync(
        object item,
        object candidate,
        ExecutionContext context,
        LythonSourceSpan span)
        => MembershipEqualsCoreAsync(item, candidate, context, span, InvokeBinarySpecialMethodAsync, IsTruthyAsync);

    private static async ValueTask<bool> MembershipEqualsCoreAsync(
        object item,
        object candidate,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        if (ReferenceEquals(item, candidate))
        {
            return true;
        }

        if (item is PyCmpKey itemKey && candidate is PyCmpKey candidateKey)
        {
            return itemKey.CompareTo(candidateKey, span, context) == 0;
        }

        var invocation = await invoke(item, "__eq__", candidate, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing || invocation.Value is PyNotImplemented)
        {
            invocation = await invoke(candidate, "__eq__", item, context, span).ConfigureAwait(false);
        }

        if (invocation.Kind == SpecialMethodInvocationKind.Invoked && invocation.Value is not PyNotImplemented)
        {
            return await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return AreEqual(item, candidate);
        }
    }

    // Nested == positions (R13): CPython identity shortcut, then the full
    // == dispatch (reflected __eq__, NotImplemented, truthiness) with the
    // structural fallback. Container element loops and value comparisons
    // share this instead of context-free AreEqual, so [a] == [b] honors
    // custom __eq__ while scalar NaN keeps numeric inequality (no shortcut
    // at the top-level operator itself).
    internal static bool ElementEquals(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return AreEqualWithProtocols(left, right, context, span);
    }

    // Ambient twin for context-free AreEqual branches: dispatches only when
    // reached under an operator or member comparison that pushed an ambient
    // context. Comparer callbacks, hashing, and sorts observe suppression or
    // a null ambient and stay structural, so guest code never runs through
    // CLR comparer internals.
    internal static bool ElementEqualsAmbient(object left, object right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (PyStructuralGuard.GuestDispatchSuppressed)
        {
            return PyEquality.AreEqual(left, right);
        }

        var context = PyStructuralGuard.AmbientContext;
        var span = PyStructuralGuard.AmbientSpan;
        if (context is null || span is null)
        {
            return PyEquality.AreEqual(left, right);
        }

        return AreEqualWithProtocols(left, right, context, span);
    }

    internal static ValueTask<bool> ElementEqualsAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
        => ElementEqualsCoreAsync(left, right, context, span, InvokeBinarySpecialMethodAsync, IsTruthyAsync);

    private static async ValueTask<bool> ElementEqualsCoreAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return leftKey.CompareTo(rightKey, span, context) == 0;
        }

        var invocation = await invoke(left, "__eq__", right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing || invocation.Value is PyNotImplemented)
        {
            invocation = await invoke(right, "__eq__", left, context, span).ConfigureAwait(false);
        }

        if (invocation.Kind == SpecialMethodInvocationKind.Invoked && invocation.Value is not PyNotImplemented)
        {
            return await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return PyEquality.AreEqual(left, right);
        }
    }

    private static bool AreNotEqualWithProtocols(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // Sync twin of the async core below (same shape as == above).
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return leftKey.CompareTo(rightKey, span, context) != 0;
        }

        // != consults __ne__ first like CPython; a NotImplemented answer
        // declines to the reflected slot and then to the negated __eq__
        // protocol (which honors NotImplemented itself).
        if (TryInvokeBinarySpecialMethod(left, "__ne__", right, context, span, out var leftValue) &&
            leftValue is not PyNotImplemented)
        {
            return IsTruthy(leftValue, context, span);
        }

        if (TryInvokeBinarySpecialMethod(right, "__ne__", left, context, span, out var rightValue) &&
            rightValue is not PyNotImplemented)
        {
            return IsTruthy(rightValue, context, span);
        }

        return !AreEqualWithProtocols(left, right, context, span);
    }

    private static ValueTask<bool> AreNotEqualWithProtocolsAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span)
        => AreNotEqualWithProtocolsCoreAsync(left, right, context, span, InvokeBinarySpecialMethodAsync, IsTruthyAsync);

    private static async ValueTask<bool> AreNotEqualWithProtocolsCoreAsync(
        object left,
        object right,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return leftKey.CompareTo(rightKey, span, context) != 0;
        }

        // != consults __ne__ first like CPython; a NotImplemented answer
        // declines to the reflected slot and then to the negated __eq__
        // protocol (which honors NotImplemented itself).
        var invocation = await invoke(left, "__ne__", right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing || invocation.Value is PyNotImplemented)
        {
            invocation = await invoke(right, "__ne__", left, context, span).ConfigureAwait(false);
        }

        return invocation.Kind == SpecialMethodInvocationKind.Invoked && invocation.Value is not PyNotImplemented
            ? await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false)
            : !await AreEqualWithProtocolsCoreAsync(left, right, context, span, invoke, evaluateTruthiness).ConfigureAwait(false);
    }

    private static bool EvaluateRichComparison(
        object left,
        object right,
        string method,
        string reflectedMethod,
        ExecutionContext context,
        LythonSourceSpan span,
        Func<int, bool> fallback)
    {
        // Sync twin of the async core below. The miss path runs per ordered
        // comparison, so routing it through an async state machine allocates
        // on every sync <, <=, > and >= even though the sync invoker and
        // truthiness checks below never suspend.
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return fallback(leftKey.CompareTo(rightKey, span, context));
        }

        // R13b: set comparisons below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (left is PySet leftSet && right is PySet rightSet)
        {
            return method switch
            {
                "__lt__" => leftSet.IsProperSubsetOf(rightSet),
                "__le__" => leftSet.IsSubsetOf(rightSet),
                "__gt__" => leftSet.IsProperSupersetOf(rightSet),
                "__ge__" => leftSet.IsSupersetOf(rightSet),
                _ => false,
            };
        }

        // A NotImplemented answer declines like a missing slot (CPython reflected
        // dispatch): the root object slots always decline, so unsupported orderings
        // keep the relational fallback instead of reading NotImplemented as true.
        if (TryInvokeBinarySpecialMethod(left, method, right, context, span, out var leftValue) &&
            leftValue is not PyNotImplemented)
        {
            return IsTruthy(leftValue, context, span);
        }

        if (TryInvokeBinarySpecialMethod(right, reflectedMethod, left, context, span, out var rightValue) &&
            rightValue is not PyNotImplemented)
        {
            return IsTruthy(rightValue, context, span);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return CompareRelational(left, right, span, fallback, ComparisonSymbol(method));
        }
    }

    private static ValueTask<bool> EvaluateRichComparisonAsync(
        object left,
        object right,
        string method,
        string reflectedMethod,
        ExecutionContext context,
        LythonSourceSpan span,
        Func<int, bool> fallback)
        => EvaluateRichComparisonCoreAsync(
            left,
            right,
            new BinarySpecialMethodPair(method, reflectedMethod),
            context,
            span,
            fallback,
            InvokeBinarySpecialMethodAsync,
            IsTruthyAsync);

    private static async ValueTask<bool> EvaluateRichComparisonCoreAsync(
        object left,
        object right,
        BinarySpecialMethodPair methods,
        ExecutionContext context,
        LythonSourceSpan span,
        Func<int, bool> fallback,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        if (left is PyCmpKey leftKey && right is PyCmpKey rightKey)
        {
            return fallback(leftKey.CompareTo(rightKey, span, context));
        }

        // R13b: set comparisons below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (left is PySet leftSet && right is PySet rightSet)
        {
            return methods.Left switch
            {
                "__lt__" => leftSet.IsProperSubsetOf(rightSet),
                "__le__" => leftSet.IsSubsetOf(rightSet),
                "__gt__" => leftSet.IsProperSupersetOf(rightSet),
                "__ge__" => leftSet.IsSupersetOf(rightSet),
                _ => false,
            };
        }

        // A NotImplemented answer declines like a missing slot (CPython reflected
        // dispatch): the root object slots always decline, so unsupported orderings
        // keep the relational fallback instead of reading NotImplemented as true.
        var invocation = await invoke(left, methods.Left, right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing || invocation.Value is PyNotImplemented)
        {
            invocation = await invoke(right, methods.Right, left, context, span).ConfigureAwait(false);
        }

        if (invocation.Kind == SpecialMethodInvocationKind.Invoked && invocation.Value is not PyNotImplemented)
        {
            return await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return CompareRelational(left, right, span, fallback, ComparisonSymbol(methods.Left));
        }
    }

    private static string ComparisonSymbol(string method) => method switch
    {
        "__lt__" => "<",
        "__le__" => "<=",
        "__gt__" => ">",
        _ => ">=",
    };

    private static bool Contains(object container, object candidate, ExecutionContext context, LythonSourceSpan span)
    {
        // Sync twin of the async core below. The miss path runs per
        // membership check, so routing it through an async state machine
        // allocates on every sync in even though the sync invoker and
        // truthiness checks below never suspend.
        if (TryInvokeBinarySpecialMethod(container, "__contains__", candidate, context, span, out var value))
        {
            return IsTruthy(value, context, span);
        }

        using (PyStructuralGuard.PushAmbient(context, span))
        {
            return PyContainment.ContainsWithProtocols(container, candidate, context, span);
        }
    }

    private static ValueTask<bool> ContainsAsync(
        object container,
        object candidate,
        ExecutionContext context,
        LythonSourceSpan span)
        => ContainsCoreAsync(container, candidate, context, span, InvokeBinarySpecialMethodAsync, IsTruthyAsync);

    private static async ValueTask<bool> ContainsCoreAsync(
        object container,
        object candidate,
        ExecutionContext context,
        LythonSourceSpan span,
        BinarySpecialMethodInvoker invoke,
        TruthinessEvaluator evaluateTruthiness)
    {
        var invocation = await invoke(container, "__contains__", candidate, context, span).ConfigureAwait(false);
        return invocation.Kind == SpecialMethodInvocationKind.Invoked
            ? await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false)
            : await PyContainment.ContainsWithProtocolsAsync(container, candidate, context, span).ConfigureAwait(false);
    }

    private static ValueTask<bool> EvaluateTruthiness(object value, ExecutionContext context, LythonSourceSpan span)
        => new(IsTruthy(value, context, span));
}
