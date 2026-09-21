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
    private static object EvaluateAssignmentExpression(AssignmentExpressionSyntax assignment, ExecutionContext context)
    {
        var value = EvaluateExpression(assignment.Expression, context);
        StoreName(assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static object EvaluateUnary(UnaryExpressionSyntax unary, ExecutionContext context)
    {
        var operand = EvaluateExpression(unary.Operand, context);
        return EvaluateUnaryOperator(unary.Operator, operand, context, unary.Span);
    }

    private static BigInteger ParseInteger(IntegerLiteralExpressionSyntax integer)
    {
        return PyNumberOps.ParseInteger(integer.ValueText);
    }

    private static double ParseFloat(FloatLiteralExpressionSyntax floating)
    {
        return PyNumberOps.ParseFloat(floating.ValueText);
    }

    private static object EvaluateAdd(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Add(left, right, span, operation ?? "+"), context, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return ConcatStrings(leftText, rightText, context, span);
        }

        if (left is PyBytes leftBytes && right is PyBytes rightBytes)
        {
            var joinedBytes = ConcatBytes(leftBytes, rightBytes, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(joinedBytes, joinedBytes.CommittedStorageBytes);
            return joinedBytes;
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            var governor = leftList.OwnerMemoryGovernor ?? rightList.OwnerMemoryGovernor;
            var allocationSpan = leftList.AllocationSpan ?? rightList.AllocationSpan;
            var result = governor is null
                ? new PyList(leftList)
                : new PyList(leftList, governor, allocationSpan);
            result.AddRange(rightList);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
            return result;
        }

        if (PyTupleLike.TryGetItems(left, out var leftItems) && PyTupleLike.TryGetItems(right, out var rightItems))
        {
            var (leftGovernor, leftSpan) = TupleLikeOwnership(left);
            var (rightGovernor, rightSpan) = TupleLikeOwnership(right);
            var governor = leftGovernor ?? rightGovernor;
            var allocationSpan = leftSpan ?? rightSpan;
            var joined = governor is null
                ? new PyTuple(leftItems.Concat(rightItems))
                : new PyTuple(leftItems.Concat(rightItems), governor, allocationSpan);
            context.Services.State.CallTemporaries.TrackFreshMutable(joined, joined.CommittedStorageBytes);
            return joined;
        }

        if (left is PyDeque leftDeque && right is PyDeque rightDeque)
        {
            // Concatenation keeps the left maxlen with append discipline
            // (bounded eviction), mirroring the deque slice path.
            var dequeGovernor = leftDeque.OwnerMemoryGovernor ?? rightDeque.OwnerMemoryGovernor;
            var dequeSpan = leftDeque.AllocationSpan ?? rightDeque.AllocationSpan;
            var joined = leftDeque.Iterate().Concat(rightDeque.Iterate());
            var joinedDeque = dequeGovernor is null ? new PyDeque(joined, leftDeque.MaxLength) : new PyDeque(joined, leftDeque.MaxLength, dequeGovernor, dequeSpan);
            if (dequeGovernor is not null)
            {
                context.Services.State.CallTemporaries.TrackFreshMutable(joinedDeque, joinedDeque.CommittedStorageBytes);
            }
            return joinedDeque;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return AddCounters(leftCounter, rightCounter, span, context);
        }

        if (left is PyString)
        {
            throw RuntimeErrors.ConcatError("str", right, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Add(left, right, context, span, operation);
        }

        if (StatisticsModule.TryAddNormalDist(left, right, span, out var normalDistSum))
        {
            return StatisticsModule.OwnNormalDist(normalDistSum, context, span);
        }

        if (left is PyList)
        {
            throw RuntimeErrors.ConcatError("list", right, span);
        }

        if (left is PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject or LythonRuntime.TimeStructTimeValue)
        {
            throw RuntimeErrors.ConcatError("tuple", right, span);
        }

        if (left is PyString)
        {
            throw RuntimeErrors.ConcatError("str", right, span);
        }

        if (left is PyBytes)
        {
            throw RuntimeErrors.CantConcatToBytes(right, span);
        }

        if (left is PyDeque)
        {
            throw RuntimeErrors.ConcatError("deque", right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "+", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.Add(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    // Counter branches keep their combining lambda in a callee: the lambda
    // captures span/context, so Roslyn instantiates its closure on every
    // call of the enclosing operator even when the operands take the fast
    // numeric path below.
    private static object AddCounters(PyCounter leftCounter, PyCounter rightCounter, LythonSourceSpan span, ExecutionContext context)
        => BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => AddCounterCounts(lhs, rhs, span, leftCounter.OwnerMemoryGovernor ?? rightCounter.OwnerMemoryGovernor, context.Services.State.CallTemporaries), keepPositiveOnly: true, span, context);

    private static object SubtractCounters(PyCounter leftCounter, PyCounter rightCounter, LythonSourceSpan span, ExecutionContext context)
        => BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => SubtractCounterCounts(lhs, rhs, span, leftCounter.OwnerMemoryGovernor ?? rightCounter.OwnerMemoryGovernor, context.Services.State.CallTemporaries), keepPositiveOnly: true, span, context);

    internal static object AddRuntimeValues(object left, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAdd(left, right, context, span);

    private static object EvaluateSubtract(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView || right is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView)
        {
            left = SetMembers.AsSetOperand(left, span, context);
            right = SetMembers.AsSetOperand(right, span, context);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.ExceptWith(rightSet);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
            return result;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return SubtractCounters(leftCounter, rightCounter, span, context);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Subtract(left, right, span, operation ?? "-"), context, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Subtract(left, right, context, span, operation);
        }

        if (StatisticsModule.TrySubtractNormalDist(left, right, span, out var normalDistDifference))
        {
            return StatisticsModule.OwnNormalDist(normalDistDifference, context, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "-", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.Subtract(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateMultiply(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Multiply(left, right, span, operation ?? "*"), context, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && TryRepeatCount(right, context, span, out var rightCount))
        {
            return RepeatString(leftText, rightCount, context, span);
        }

        if (PyStringOps.TryAsString(right, out var rightText) && TryRepeatCount(left, context, span, out var leftCount))
        {
            return RepeatString(rightText, leftCount, context, span);
        }

        if (left is PyList leftList && TryRepeatCount(right, context, span, out var rightRepeatCount))
        {
            return RepeatList(leftList, rightRepeatCount, context, span);
        }

        if (right is PyList rightList && TryRepeatCount(left, context, span, out var leftRepeatCount))
        {
            return RepeatList(rightList, leftRepeatCount, context, span);
        }

        if (left is PyBytes leftBytes && TryRepeatCount(right, context, span, out var rightByteCount))
        {
            var repeatedBytes = RepeatBytes(leftBytes, rightByteCount, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(repeatedBytes, repeatedBytes.CommittedStorageBytes);
            return repeatedBytes;
        }

        if (right is PyBytes rightBytes && TryRepeatCount(left, context, span, out var leftByteCount))
        {
            var repeatedBytes = RepeatBytes(rightBytes, leftByteCount, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(repeatedBytes, repeatedBytes.CommittedStorageBytes);
            return repeatedBytes;
        }

        if (left is PyDeque leftDeque && TryRepeatCount(right, context, span, out var rightDequeRepeatCount))
        {
            return RepeatDeque(leftDeque, rightDequeRepeatCount, context, span);
        }

        if (right is PyDeque rightDeque && TryRepeatCount(left, context, span, out var leftDequeRepeatCount))
        {
            return RepeatDeque(rightDeque, leftDequeRepeatCount, context, span);
        }

        if (PyTupleLike.TryGetItems(left, out var repeatLeft) && TryRepeatCount(right, context, span, out var rightTupleRepeatCount))
        {
            return RepeatTuple(repeatLeft, rightTupleRepeatCount, context, span);
        }

        if (PyTupleLike.TryGetItems(right, out var repeatRight) && TryRepeatCount(left, context, span, out var leftTupleRepeatCount))
        {
            return RepeatTuple(repeatRight, leftTupleRepeatCount, context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Multiply(left, right, context, span, operation);
        }

        if (StatisticsModule.TryMultiplyNormalDist(left, right, span, out var normalDistProduct))
        {
            return StatisticsModule.OwnNormalDist(normalDistProduct, context, span);
        }

        if (IsSequenceOperand(left) && !IsIntLikeOperand(right))
        {
            throw RuntimeErrors.MultiplySequenceError(right, span);
        }

        if (IsSequenceOperand(right) && !IsIntLikeOperand(left))
        {
            throw RuntimeErrors.MultiplySequenceError(left, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "*", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.Multiply(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    // Repeat counts coerce through __index__ like CPython; a failing __index__
    // raises its shaped error while plain non-integers simply decline so the
    // caller falls through to its mismatch error.
    private static bool TryRepeatCount(object value, ExecutionContext context, LythonSourceSpan span, out BigInteger count)
    {
        if (value is BigInteger integer)
        {
            count = integer;
            return true;
        }

        if (value is bool flag)
        {
            count = flag ? BigInteger.One : BigInteger.Zero;
            return true;
        }

        if (value is PyInstance)
        {
            return PyNumberOps.TryAsInteger(CoerceIndexProtocol(value, context, span), out count);
        }

        count = default;
        return false;
    }

    private static bool IsSequenceOperand(object value)
        => value is PyList or PyTuple or PyNamedTupleObject or PyTypingNamedTupleObject or LythonRuntime.TimeStructTimeValue or PyString or PyBytes or PyDeque;

    private static (MemoryGovernor? Governor, LythonSourceSpan? Span) TupleLikeOwnership(object value) => value switch
    {
        PyTuple tuple => (tuple.OwnerMemoryGovernor, tuple.AllocationSpan),
        PyNamedTupleObject named => (named.OwnerMemoryGovernor, named.AllocationSpan),
        PyTypingNamedTupleObject typingNamed => (typingNamed.OwnerMemoryGovernor, typingNamed.AllocationSpan),
        _ => (null, null),
    };

    private static bool IsIntLikeOperand(object value)
        => value is BigInteger or int or bool;

    private static object EvaluateDivide(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Divide(left, right, span, operation ?? "/"), context, span);
        }

        if (left is PyPath leftPath)
        {
            if (right is PyPath rightPath)
            {
                return OwnPathResult(PathOps.Join(leftPath.Value, rightPath.Value), leftPath.Value, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
            }

            if (PyStringOps.TryAsString(right, out var rightText))
            {
                return OwnPathResult(PathOps.Join(leftPath.Value, rightText), leftPath.Value, context.MemoryGovernor, span, context.Services.State.CallTemporaries);
            }
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Divide(left, right, context, span, operation);
        }

        if (StatisticsModule.TryDivideNormalDist(left, right, span, out var normalDistQuotient))
        {
            return StatisticsModule.OwnNormalDist(normalDistQuotient, context, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "/", left, right, span);
        }

        try
        {
            return PyNumberOps.TrueDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException(
                "ZeroDivisionError",
                left is double || right is double ? "float division by zero" : "division by zero",
                span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateFloorDivide(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.FloorDivide(left, right, span, operation ?? "//"), context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            var floored = PyDateTimeOps.FloorDivide(left, right, context, span, operation);
            return floored is BigInteger flooredInteger
                ? OwnHeapInteger(flooredInteger, context.MemoryGovernor, context.Services.State.CallTemporaries, span)
                : floored;
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "//", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.FloorDivide(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException(
                "ZeroDivisionError",
                left is double || right is double ? "float floor division by zero" : "integer division or modulo by zero",
                span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateModulo(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyString template)
        {
            return FormatPercentString(template, right, context, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Modulo(left, right, span, operation ?? "%"), context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Modulo(left, right, context, span, operation);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "%", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.Modulo(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException(
                "ZeroDivisionError",
                left is double || right is double ? "float modulo by zero" : "integer modulo by zero",
                span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluatePower(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Power(left, right, span, operation ?? "** or pow()"), context, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "** or pow()", left, right, span);
        }

        try
        {
            if (lhs.IsZero && (rhs.IsFloat ? rhs.Floating < 0 : rhs.Integer < BigInteger.Zero))
            {
                throw new LythonRuntimeException("ZeroDivisionError", "0.0 cannot be raised to a negative power", span);
            }

            var leftValue = lhs.IsFloat ? lhs.Floating : PyNumberOps.ToDoubleChecked(lhs);
            var rightValue = rhs.IsFloat ? rhs.Floating : PyNumberOps.ToDoubleChecked(rhs);
            if (leftValue < 0 && double.IsFinite(rightValue) && rightValue != Math.Truncate(rightValue))
            {
                throw new LythonRuntimeException("TypeError", "complex results are not supported by Lython", span);
            }

            GuardIntegerPower(lhs, rhs, context.MemoryGovernor, span);
            return OwnHeapInteger(PyNumberOps.Power(lhs, rhs), context.MemoryGovernor, context.Services.State.CallTemporaries, span);
        }
        catch (OverflowException ex) when (ex.Message == "int too large to convert to float")
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }
    }
}
