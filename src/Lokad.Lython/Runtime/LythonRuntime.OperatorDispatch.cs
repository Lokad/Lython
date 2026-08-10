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
            BinaryOperatorSyntax.NotEqual => !AreEqualWithProtocols(left, right, context, span),
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
            BinaryOperatorSyntax.NotEqual => !await AreEqualWithProtocolsAsync(left, right, context, span).ConfigureAwait(false),
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
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, context, span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, span),
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
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, span),
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
        // The sync invoker always returns an already-completed ValueTask, so the
        // shared policy core cannot suspend or block the synchronous runtime.
        var invocation = EvaluateNumericProtocolCoreAsync(op, left, right, context, span, InvokeBinarySpecialMethod)
            .GetAwaiter()
            .GetResult();
        result = invocation.Value;
        return invocation.Kind == SpecialMethodInvocationKind.Invoked;
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

        var leftInvocation = await invoke(left, resolvedMethods.Left, right, context, span).ConfigureAwait(false);
        return leftInvocation.Kind == SpecialMethodInvocationKind.Invoked
            ? leftInvocation
            : await invoke(right, resolvedMethods.Right, left, context, span).ConfigureAwait(false);
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
            SpecialMethodInvocation.Invoked(callable.Invoke([CallArgumentValue.Positional(argument)], span, context)));
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

        var value = await callable.InvokeAsync([CallArgumentValue.Positional(argument)], span, context).ConfigureAwait(false);
        return SpecialMethodInvocation.Invoked(value);
    }

    private static async ValueTask<SpecialMethodInvocation> InvokeUnarySpecialMethodAsync(
        object target,
        string method,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!TryResolveSpecialMethodCallable(target, method, context, span, out var callable))
        {
            return SpecialMethodInvocation.Missing;
        }

        var value = await callable.InvokeAsync([], span, context).ConfigureAwait(false);
        return SpecialMethodInvocation.Invoked(value);
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
        if (TryResolveSpecialMethodCallable(target, method, context, span, out var callable))
        {
            result = callable.Invoke([], span, context);
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
        => AreEqualWithProtocolsCoreAsync(left, right, context, span, InvokeBinarySpecialMethod, EvaluateTruthiness)
            .GetAwaiter()
            .GetResult();

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

        var invocation = await invoke(left, "__eq__", right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing)
        {
            invocation = await invoke(right, "__eq__", left, context, span).ConfigureAwait(false);
        }

        return invocation.Kind == SpecialMethodInvocationKind.Invoked
            ? await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false)
            : AreEqual(left, right);
    }

    private static bool EvaluateRichComparison(
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
                InvokeBinarySpecialMethod,
                EvaluateTruthiness)
            .GetAwaiter()
            .GetResult();

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

        var invocation = await invoke(left, methods.Left, right, context, span).ConfigureAwait(false);
        if (invocation.Kind == SpecialMethodInvocationKind.Missing)
        {
            invocation = await invoke(right, methods.Right, left, context, span).ConfigureAwait(false);
        }

        return invocation.Kind == SpecialMethodInvocationKind.Invoked
            ? await evaluateTruthiness(invocation.Value, context, span).ConfigureAwait(false)
            : CompareRelational(left, right, span, fallback);
    }

    private static bool Contains(object container, object candidate, ExecutionContext context, LythonSourceSpan span)
        => ContainsCoreAsync(container, candidate, context, span, InvokeBinarySpecialMethod, EvaluateTruthiness)
            .GetAwaiter()
            .GetResult();

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
            : PyContainment.Contains(container, candidate, span);
    }

    private static ValueTask<bool> EvaluateTruthiness(object value, ExecutionContext context, LythonSourceSpan span)
        => new(IsTruthy(value, context, span));
}
