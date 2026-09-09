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
    private static void GuardIntegerPower(PyNumber lhs, PyNumber rhs, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (lhs.IsFloat || rhs.IsFloat || rhs.Integer < BigInteger.Zero)
        {
            return;
        }

        var baseBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs.Integer);
        var exponent = rhs.Integer;
        var resultBits = baseBits switch
        {
            0 => 0,
            1 => 1,
            _ when exponent > long.MaxValue => long.MaxValue,
            _ => RuntimeMemoryEstimates.SaturatingMultiply(baseBits, (long)exponent)
        };

        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), governor, span);
    }

    private static void GuardIntegerLeftShift(BigInteger lhs, BigInteger rhs, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (rhs < BigInteger.Zero)
        {
            return;
        }

        var lhsBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs);
        var shiftBits = rhs > long.MaxValue ? long.MaxValue : (long)rhs;
        var resultBits = RuntimeMemoryEstimates.SaturatingAdd(lhsBits, shiftBits);
        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), governor, span);
    }

    private static void GuardIntegerResultBytes(long estimatedBytes, MemoryGovernor governor, LythonSourceSpan span)
    {
        // Preflight only: fail before materializing a giant result. Durable
        // ownership of the actual result happens at the arithmetic sites below.
        governor.EnsureCanReserve(estimatedBytes, span);
    }

    // Integers above the inline range retain heap magnitude storage with no owner
    // tracking after this point; own them durably. Inline-range values, floats
    // and aliased inputs stay free.
    private static object OwnHeapInteger(object value, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (value is BigInteger integer && RuntimeMemoryEstimates.GetMagnitudeBitLength(integer) > 64)
        {
            var bytes = RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer);
            governor.Reserve(bytes, span);
            governor.Commit(bytes);
        }

        return value;
    }

    private static object EvaluateBitwiseOr(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean | rightBoolean;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) >= 0 ? lhs : rhs, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.UnionWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '|'.", span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseOr(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateBitwiseXor(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean ^ rightBoolean;
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.SymmetricExceptWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '^'.", span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseXor(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateBitwiseAnd(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean & rightBoolean;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) <= 0 ? lhs : rhs, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.IntersectWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '&'.", span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseAnd(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateLeftShift(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }

        try
        {
            GuardIntegerLeftShift(lhs, rhs, context.MemoryGovernor, span);
            return OwnHeapInteger(PyNumberOps.LeftShift(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }
    }

    private static object EvaluateRightShift(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.RightShift(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }
    }

    private static object EvaluateUnaryPlus(object operand, LythonSourceSpan span)
    {
        if (operand is PyCounter positiveCounter)
        {
            return BuildCounterUnaryResult(positiveCounter, count => count, keepPositiveOnly: true, span);
        }

        if (operand is PyDecimal)
        {
            return operand;
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: false, out var positiveNormalDist))
        {
            return positiveNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out _))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return operand;
    }

    private static object EvaluateUnaryMinus(object operand, ExecutionContext context, LythonSourceSpan span)
    {
        if (operand is PyCounter negativeCounter)
        {
            return BuildCounterUnaryResult(negativeCounter, count => NegateCounterCount(count, span, negativeCounter.OwnerMemoryGovernor), keepPositiveOnly: true, span);
        }

        if (operand is PyTimedelta)
        {
            return PyDateTimeOps.Negate(operand, span);
        }

        if (operand is PyDecimal decimalValue)
        {
            return OwnDecimalValue(new PyDecimal(-decimalValue.Value, decimalValue.Exponent), context, span);
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: true, out var negativeNormalDist))
        {
            return negativeNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out var numeric))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return OwnHeapInteger(PyNumberOps.Negate(numeric), context.MemoryGovernor, span);
    }

    private static PyCounter BuildCounterUnaryResult(
        PyCounter source,
        Func<object, object> transform,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(source, null, span);
        foreach (var pair in source.Items)
        {
            var count = transform(ExpectCounterCount(pair.Value, span));
            if (keepPositiveOnly && CompareCounterCounts(count, BigInteger.Zero, span) <= 0)
            {
                continue;
            }

            result.SetItem(pair.Key, count);
        }

        return result;
    }

    private static PyCounter BuildCounterBinaryResult(
        PyCounter left,
        PyCounter right,
        Func<object, object, object> combine,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(left, right, span);
        var seen = new HashSet<object>(PyValueComparer.Instance);
        foreach (var pair in left.Items)
        {
            seen.Add(pair.Key);
            AddResult(pair.Key, pair.Value, right.GetCount(pair.Key));
        }

        foreach (var pair in right.Items)
        {
            if (seen.Add(pair.Key))
            {
                AddResult(pair.Key, BigInteger.Zero, pair.Value);
            }
        }

        return result;

        void AddResult(object key, object leftCount, object rightCount)
        {
            var count = combine(ExpectCounterCount(leftCount, span), ExpectCounterCount(rightCount, span));
            if (!keepPositiveOnly || CompareCounterCounts(count, BigInteger.Zero, span) > 0)
            {
                result.SetItem(key, count);
            }
        }
    }

    internal static object AddCounterCounts(object left, object right, LythonSourceSpan span, MemoryGovernor? governor)
    {
        left = ExpectCounterCount(left, span);
        right = ExpectCounterCount(right, span);
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Add(left, right, span), governor, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Add(lhs, rhs);
    }

    private static object SubtractCounterCounts(object left, object right, LythonSourceSpan span, MemoryGovernor? governor)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Subtract(left, right, span), governor, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Subtract(lhs, rhs);
    }

    private static object NegateCounterCount(object value, LythonSourceSpan span, MemoryGovernor? governor)
        => value is PyDecimal decimalValue
            ? OwnDecimalValue(new PyDecimal(-decimalValue.Value, decimalValue.Exponent), governor, span)
            : PyNumberOps.TryAsNumber(value, out var number)
                ? PyNumberOps.Negate(number)
                : throw new LythonRuntimeException("TypeError", "Counter mapping values must be numeric.", span);

    private static int CompareCounterCounts(object left, object right, LythonSourceSpan span)
        => PyComparison.Compare(left, right, span);

    private static PyCounter CreateCounterResult(PyCounter left, PyCounter? right, LythonSourceSpan span)
    {
        var governor = left.OwnerMemoryGovernor ?? right?.OwnerMemoryGovernor;
        var allocationSpan = left.AllocationSpan ?? right?.AllocationSpan ?? span;
        return governor is null ? new PyCounter() : new PyCounter(governor, allocationSpan);
    }

    private static object EvaluateBitwiseNot(object operand, ExecutionContext context, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(operand, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not an integer.", span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseNot(integer), context.MemoryGovernor, span);
    }

    private static bool TryGetNumericOperands(object left, object right, out PyNumber lhs, out PyNumber rhs)
    {
        if (PyNumberOps.TryAsNumber(left, out lhs) && PyNumberOps.TryAsNumber(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static bool TryGetIntegerOperands(object left, object right, out BigInteger lhs, out BigInteger rhs)
    {
        if (PyNumberOps.TryAsInteger(left, out lhs) && PyNumberOps.TryAsInteger(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static PyString ConcatStrings(PyString left, PyString right, ExecutionContext context, LythonSourceSpan span)
    {
        var resultLength = RuntimeMemoryEstimates.SaturatingAdd(left.Length, right.Length);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return left.Concat(right, context.MemoryGovernor, span);
    }

    private static PyString RepeatString(PyString text, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return PyString.Empty;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "String repetition is too large.", span);
        }

        var resultLength = RuntimeMemoryEstimates.SaturatingMultiply(text.Length, (long)count);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return text.Repeat((int)count, context.MemoryGovernor, span);
    }

    private static PyList RepeatList(PyList list, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        var repeatCount = ToListRepeatCount(count, span);
        if (repeatCount == 0 || list.Count == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        var totalLength = (long)list.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        for (var i = 0; i < repeatCount; i++)
        {
            result.AddRange(list);
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static int ToListRepeatCount(BigInteger count, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return 0;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        return (int)count;
    }

    private static object EvaluateDictLiteral(DictLiteralExpressionSyntax dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is DictionaryUnpackingItemSyntax unpacking)
            {
                var mapping = RuntimeValue(EvaluateExpression(unpacking.Mapping, context));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Span, context.MemoryGovernor), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (DictionaryKeyValueItemSyntax)item;
            result.SetItem(
                ValidateDictionaryKey(EvaluateExpression(keyValue.Key, context), keyValue.Key.Span, context.MemoryGovernor),
                RuntimeValue(EvaluateExpression(keyValue.Value, context)));
            context.ObserveCollectionCount(result.Count, dict.Span);
        }

        return result;
    }

    private static object EvaluateSetLiteral(SetLiteralExpressionSyntax set, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, set.Span);
        for (var i = 0; i < set.Items.Count; i++)
        {
            var value = RuntimeValue(EvaluateExpression(set.Items[i].Expression, context));
            AddSetLiteralItem(result, value, set.Items[i].IsUnpacking, set.Items[i].Span, set.Span, context);
        }

        return result;
    }

    private static void AddSetLiteralItem(
        PySet result,
        object value,
        bool isUnpacking,
        LythonSourceSpan itemSpan,
        LythonSourceSpan setSpan,
        ExecutionContext context)
    {
        if (!isUnpacking)
        {
            AddSetLiteralValue(result, value, itemSpan, setSpan, context);
            return;
        }

        foreach (var item in ToSequence(value, itemSpan, context))
        {
            AddSetLiteralValue(result, RuntimeValue(item), itemSpan, setSpan, context);
        }
    }

    private static void AddSetLiteralValue(
        PySet result,
        object value,
        LythonSourceSpan itemSpan,
        LythonSourceSpan setSpan,
        ExecutionContext context)
    {
        result.Add(ValidateSetItem(value, itemSpan, context.MemoryGovernor));
        context.ObserveCollectionCount(result.Count, setSpan);
    }

    private static IEnumerable<KeyValuePair<object, object>> EnumerateMappingItems(
        object mapping,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        switch (mapping)
        {
            case PyDict dict:
                return dict.Items;
            case PyDefaultDict defaultDict:
                return defaultDict.Items;
            case PyCounter counter:
                return counter.Items;
            case PyChainMap chainMap:
            {
                // The merged list plus dedup set peak beside the governed destination,
                // like the keys()/values()/items() views; hold the same transient estimate.
                using var scratch = context.MemoryGovernor.ReserveTemporary(chainMap.EstimateMergeScratchBytes(), span);
                return chainMap.Iterate().Select(key =>
                    new KeyValuePair<object, object>(key, chainMap.GetSubscript(key, span)));
            }
            case PyInstance instance:
                if (!TryResolveRuntimeMember(instance, "keys", context, span, out var keysMember))
                {
                    break;
                }

                var keys = InvokeCallableTarget(keysMember, span, span, context, () => []);
                return ToSequence(keys, span, context).Select(key =>
                    new KeyValuePair<object, object>(key, GetUserItem(instance, key, context, span)));
        }

        throw new LythonRuntimeException("TypeError", "Object is not a mapping.", span);
    }

}
