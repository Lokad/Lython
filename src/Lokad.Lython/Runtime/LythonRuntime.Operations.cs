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

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static MemoryGovernor.TemporaryMemoryReservation? GuardIntegerAddition(
        BigInteger left, BigInteger right, MemoryGovernor governor, LythonSourceSpan span)
    {
        // Ordinary counters need no separate magnitude admission. Seven signed
        // bytes per operand cannot produce a result outside the bounded 64-bit tier.
        if (left.GetByteCount() < 8 && right.GetByteCount() < 8) return null;
        var resultBits = RuntimeMemoryEstimates.SaturatingAdd(Math.Max(
            RuntimeMemoryEstimates.GetMagnitudeBitLength(left),
            RuntimeMemoryEstimates.GetMagnitudeBitLength(right)), 1);
        if (resultBits <= 64) return null;
        // Admit result and computation overlap together. The shared addition
        // commits the result from this reservation; no second admission follows
        // arithmetic. Remaining scratch is refunded when the reservation ends.
        var resultBytes = RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(
            RuntimeMemoryEstimates.SaturatingAdd(resultBits, 1));
        return governor.ReserveTemporary(RuntimeMemoryEstimates.SaturatingMultiply(2, resultBytes), span);
    }

    private static MemoryGovernor.TemporaryMemoryReservation? GuardIntegerPower(PyNumber lhs, PyNumber rhs, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (lhs.IsFloat || rhs.IsFloat || rhs.Integer < BigInteger.Zero)
        {
            return null;
        }

        var baseBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs.Integer);
        var exponent = rhs.Integer;
        if (exponent.IsZero || baseBits <= 1) return null;
        var resultBits = exponent > long.MaxValue ? long.MaxValue
            : RuntimeMemoryEstimates.SaturatingMultiply(baseBits, (long)exponent);

        return GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), governor, span);
    }

    private static MemoryGovernor.TemporaryMemoryReservation? GuardIntegerLeftShift(BigInteger lhs, BigInteger rhs, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (rhs < BigInteger.Zero || lhs.IsZero || rhs.IsZero)
        {
            return null;
        }

        var lhsBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs);
        var shiftBits = rhs > long.MaxValue ? long.MaxValue : (long)rhs;
        var resultBits = RuntimeMemoryEstimates.SaturatingAdd(lhsBits, shiftBits);
        return GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), governor, span);
    }

    private static MemoryGovernor.TemporaryMemoryReservation GuardIntegerResultBytes(long estimatedBytes, MemoryGovernor governor, LythonSourceSpan? span)
    {
        // Admit once and hold the working storage throughout computation. The
        // caller funds any retained magnitude before this scratch is released.
        return governor.ReserveTemporary(estimatedBytes, span);
    }

    // Major magnitudes stay conservatively charged to the execution owner.
    private static object OwnHeapInteger(object value, MemoryGovernor governor, LythonSourceSpan? span)
        => OwnFreshInteger(value, governor, span);

    // Fresh arithmetic, parsed and copied magnitudes have no identity ledger.
    internal static object OwnFreshInteger(object box, MemoryGovernor governor, LythonSourceSpan? span,
        MemoryGovernor.TemporaryMemoryReservation? storage = null)
    {
        if (box is BigInteger integer &&
            RuntimeMemoryEstimates.GetMagnitudeBitLength(integer) > 64)
        {
            var bytes = RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer);
            if (storage is not null) storage.Commit(bytes);
            else
            {
                governor.Reserve(bytes, span);
                governor.Commit(bytes);
            }
        }

        return box;
    }

    // Preflight for text-parsed magnitudes: deny on digit scale before the
    // conversion allocates the limbs. Bits-per-digit upper-bounds the base.
    internal static MemoryGovernor.TemporaryMemoryReservation GuardIntegerParseBytes(int textLength, int bitsPerDigit, MemoryGovernor governor, LythonSourceSpan? span)
    {
        return GuardIntegerResultBytes(
            RuntimeMemoryEstimates.SaturatingMultiply(2, RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(
                RuntimeMemoryEstimates.SaturatingMultiply(textLength, bitsPerDigit))),
            governor,
            span);
    }

    internal static int IntegerParseBitsPerDigit(int numberBase)
        => numberBase <= 2 ? 1 : numberBase <= 4 ? 2 : numberBase <= 8 ? 3 : numberBase <= 16 ? 4 : 6;

    private static IEnumerable<KeyValuePair<object, object>>? MergeUnionPairs(object value) => value switch
    {
        PyDict dict => dict,
        PyDefaultDict defaultdict => defaultdict.Items,
        PyCounter counter => counter.Items,
        _ => null,
    };

    private static bool IsInPlaceMergeOperand(object value)
        => value is PyDict or PyDefaultDict or PyCounter or PyChainMap;

    private static IEnumerable<KeyValuePair<object, object>> InPlaceMergePairs(object mapping, LythonSourceSpan span) => mapping switch
    {
        PyDict dict => dict,
        PyDefaultDict defaultdict => defaultdict.Items,
        PyCounter counter => counter.Items,
        PyChainMap chainMap => MergeChainMapMergePairs(chainMap, span),
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static IEnumerable<KeyValuePair<object, object>> MergeChainMapMergePairs(PyChainMap chainMap, LythonSourceSpan span)
    {
        foreach (var key in chainMap.BuildMergedKeys())
        {
            yield return new KeyValuePair<object, object>(key, chainMap.GetSubscript(key, span));
        }
    }

    private static void MergeCounterUnionInPlace(PyCounter counter, object source, LythonSourceSpan span)
    {
        foreach (var pair in InPlaceMergePairs(source, span))
        {
            MergeCounterUnionPair(counter, pair.Key, pair.Value, span);
        }

        PurgeCounterNonPositive(counter, span);
    }

    // Union keeps the incoming count only when strictly greater, so ties
    // keep the incumbent object exactly like CPython (if other_count >
    // count: self[elem] = other_count).
    private static void MergeCounterUnionPair(PyCounter counter, object key, object otherCount, LythonSourceSpan span)
    {
        var current = counter.TryGetValue(key, out var found) ? found : BigInteger.Zero;
        if (CompareCounterCounts(otherCount, current, span, ">") > 0)
        {
            counter.SetItem(key, otherCount);
        }
    }

    // Purge pre-existing non-positive counts like CPython, surfacing
    // comparison failures for non-numeric occupants.
    // Multiset ordering shared by the Counter comparison dunders: every
    // key on either side compares with missing counts as zero through the
    // full <= operator, exactly like CPython (element failures propagate).
    internal static bool MultisetLessEqual(PyCounter left, PyCounter right, ExecutionContext context, LythonSourceSpan span)
    {
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        var keys = new HashSet<object>(left.Keys, PyValueComparer.Instance);
        keys.UnionWith(right.Keys);

        foreach (var key in keys)
        {
            var leftCount = left.TryGetValue(key, out var foundLeft) ? foundLeft : BigInteger.Zero;
            var rightCount = right.TryGetValue(key, out var foundRight) ? foundRight : BigInteger.Zero;
            // N11: ordering returns raw results; counter comparison truth-tests.
            if (!IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.LessEqual, leftCount, rightCount, context, span), context, span))
            {
                return false;
            }
        }

        return true;
    }

    private static void PurgeCounterNonPositive(PyCounter counter, LythonSourceSpan span)
    {
        var stale = new List<object>();
        foreach (var pair in counter.Items)
        {
            if (CompareCounterCounts(pair.Value, BigInteger.Zero, span, ">") <= 0)
            {
                stale.Add(pair.Key);
            }
        }

        foreach (var key in stale)
        {
            counter.Remove(key);
        }
    }

    private static object EvaluateBitwiseOr(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean | rightBoolean;
        }

        // R13b: mapping merges below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return UnionCounters(leftCounter, rightCounter, span, context);
        }

        if (left is PyChainMap leftChainMap && right is PyDict or PyDefaultDict or PyCounter or PyChainMap)
        {
            var first = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in leftChainMap.FirstMap.Items)
            {
                first.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in InPlaceMergePairs(right, span))
            {
                first.SetItem(pair.Key, pair.Value);
            }

            return new PyChainMap(leftChainMap.Maps.Skip(1).Prepend(first), context, span);
        }

        if (right is PyChainMap && left is PyDict or PyDefaultDict or PyCounter)
        {
            var merged = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in InPlaceMergePairs(left, span))
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in InPlaceMergePairs(right, span))
            {
                merged.SetItem(pair.Key, pair.Value);
            }
            return new PyChainMap([merged], context, span);
        }

        if (left is PyDefaultDict || right is PyDefaultDict)
        {
            var leftPairs = MergeUnionPairs(left);
            var rightPairs = MergeUnionPairs(right);
            if (leftPairs is null || rightPairs is null)
            {
                throw RuntimeErrors.UnsupportedOperands(operation ?? "|", left, right, span);
            }

            var factory = left is PyDefaultDict leftDefault ? leftDefault.DefaultFactory : ((PyDefaultDict)right).DefaultFactory;
            var unionResult = new PyDefaultDict(factory, context.MemoryGovernor, span);
            var merged = unionResult.InnerDict;
            foreach (var pair in leftPairs)
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in rightPairs)
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            return unionResult;
        }

        if ((left is PyCounter && right is PyDict) || (left is PyDict && right is PyCounter))
        {
            var plain = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in MergeUnionPairs(left).RequireNotNull())
            {
                plain.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in MergeUnionPairs(right).RequireNotNull())
            {
                plain.SetItem(pair.Key, pair.Value);
            }
            return plain;
        }

        if (left is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView || right is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView)
        {
            // Dict views combine as sets like CPython; other iterables
            // materialize with the usual validation and governance.
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
            try
            {
                result.UnionWith(rightSet);
            }
            catch
            {
                result.ReleaseUnpublishedStorage();
                throw;
            }
            return result;
        }

        if (left is PyDict leftDict && right is PyDict rightDict)
        {
            var merged = new PyDict(leftDict, context.MemoryGovernor, span);
            foreach (var pair in rightDict)
            {
                merged.SetItem(pair.Key, pair.Value);
            }
            return merged;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "|", left, right, span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseOr(lhs, rhs), context.MemoryGovernor, span);
    }

    // Counter branches keep their combining lambda in a callee: the lambda
    // captures span, so Roslyn instantiates its closure on every call of the
    // enclosing operator even when the operands take the fast integer path.
    private static object UnionCounters(PyCounter leftCounter, PyCounter rightCounter, LythonSourceSpan span, ExecutionContext context)
        => BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) >= 0 ? lhs : rhs, keepPositiveOnly: true, span, context);

    private static object IntersectCounters(PyCounter leftCounter, PyCounter rightCounter, LythonSourceSpan span, ExecutionContext context)
        => BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) < 0 ? lhs : rhs, keepPositiveOnly: true, span, context);

    private static object EvaluateBitwiseXor(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean ^ rightBoolean;
        }
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);

        if (left is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView || right is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView)
        {
            // Dict views combine as sets like CPython; other iterables
            // materialize with the usual validation and governance.
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
            try
            {
                result.SymmetricExceptWith(rightSet);
            }
            catch
            {
                result.ReleaseUnpublishedStorage();
                throw;
            }
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "^", left, right, span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseXor(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateBitwiseAnd(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean & rightBoolean;
        }

        // R13b: set/counter regions below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return IntersectCounters(leftCounter, rightCounter, span, context);
        }

        if (left is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView || right is DictKeysView or DictItemsView or ChainMapKeysView or ChainMapItemsView)
        {
            // Dict views combine as sets like CPython; other iterables
            // materialize with the usual validation and governance.
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
            try
            {
                result.IntersectWith(rightSet);
            }
            catch
            {
                result.ReleaseUnpublishedStorage();
                throw;
            }
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "&", left, right, span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseAnd(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateLeftShift(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "<<", left, right, span);
        }

        try
        {
            using var integerAdmission = GuardIntegerLeftShift(lhs, rhs, context.MemoryGovernor, span);
            return OwnHeapInteger(PyNumberOps.LeftShift(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (PyNegativeShiftException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateRightShift(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? ">>", left, right, span);
        }

        try
        {
            return OwnHeapInteger(PyNumberOps.RightShift(lhs, rhs), context.MemoryGovernor, span);
        }
        catch (PyNegativeShiftException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException ex)
        {
            throw new LythonRuntimeException("OverflowError", ex.Message, span);
        }
    }

    private static object EvaluateUnaryPlus(object operand, ExecutionContext context, LythonSourceSpan span)
    {
        if (operand is bool boolean)
        {
            return OwnHeapInteger(boolean ? BigInteger.One : BigInteger.Zero, context.MemoryGovernor, span);
        }

        if (operand is PyCounter positiveCounter)
        {
            return BuildCounterUnaryResult(positiveCounter, negative: false, span, context);
        }

        if (operand is PyTimedelta)
        {
            return operand;
        }

        if (operand is PyDecimal positiveDecimal)
        {
            var policy = context.DecimalContext;
            var value = positiveDecimal.Value == 0m && policy.Rounding != DecimalRoundingMode.Floor ? 0m : positiveDecimal.Value;
            return OwnDecimalValue(PyDecimalOps.ApplyContext(new PyDecimal(value, positiveDecimal.Exponent), policy, context, span), context, span);
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: false, context, span, out var positiveNormalDist))
        {
            return positiveNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out _))
        {
            throw RuntimeErrors.BadUnaryOperand("+", operand, span);
        }

        return operand;
    }

    private static object EvaluateUnaryMinus(object operand, ExecutionContext context, LythonSourceSpan span)
    {
        if (operand is PyCounter negativeCounter)
        {
            return BuildCounterUnaryResult(negativeCounter, negative: true, span, context);
        }

        if (operand is PyTimedelta)
        {
            return PyDateTimeOps.Negate(operand, context, span);
        }

        if (operand is PyDecimal decimalValue)
        {
            var policy = context.DecimalContext;
            var value = decimalValue.Value == 0m && policy.Rounding != DecimalRoundingMode.Floor ? 0m : decimal.Negate(decimalValue.Value);
            return OwnDecimalValue(PyDecimalOps.ApplyContext(new PyDecimal(value, decimalValue.Exponent), policy, context, span), context, span);
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: true, context, span, out var negativeNormalDist))
        {
            return negativeNormalDist;
        }

        if (!PyNumberOps.TryAsNumber(operand, out var numeric))
        {
            throw RuntimeErrors.BadUnaryOperand("-", operand, span);
        }

        return OwnHeapInteger(PyNumberOps.Negate(numeric), context.MemoryGovernor, span);
    }

    private static PyCounter BuildCounterUnaryResult(
        PyCounter source,
        bool negative,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        var result = new PyCounter(source.OwnerMemoryGovernor ?? context.MemoryGovernor, span);
        foreach (var pair in source.Items)
        {
            context.CheckExecution(span);
            var count = ExpectCounterCount(pair.Value, span);
            var comparison = CompareCounterCounts(count, BigInteger.Zero, span);
            if (negative ? comparison >= 0 : comparison <= 0)
            {
                continue;
            }

            result.SetItem(pair.Key, negative
                ? SubtractCounterCounts(BigInteger.Zero, count, span, result.OwnerMemoryGovernor, context)
                : count);
        }
        return result;
    }

    private static PyCounter BuildCounterBinaryResult(
        PyCounter left,
        PyCounter right,
        Func<object, object, object> combine,
        bool keepPositiveOnly,
        LythonSourceSpan span,
        ExecutionContext context)
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

    internal static object AddCounterCounts(object left, object right, LythonSourceSpan span, MemoryGovernor? governor, ExecutionContext context)
    {
        left = ExpectCounterCount(left, span);
        right = ExpectCounterCount(right, span);
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Add(left, right, span, "+", context), governor, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return !lhs.IsFloat && !rhs.IsFloat
            ? AddExactIntegers(lhs.Integer, rhs.Integer, governor, span)
            : PyNumberOps.Add(lhs, rhs);
    }

    private static object SubtractCounterCounts(object left, object right, LythonSourceSpan span, MemoryGovernor? governor, ExecutionContext context)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Subtract(left, right, span, "-", context), governor, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Subtract(lhs, rhs);
    }

    private static int CompareCounterCounts(object left, object right, LythonSourceSpan span, string? operation = null)
        => PyComparison.Compare(left, right, span, operation);

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
            throw RuntimeErrors.BadUnaryOperand("~", operand, span);
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

        var result = left.Concat(right, context.MemoryGovernor, span);
        return result;
    }

    private static PyBytes ConcatBytes(PyBytes left, PyBytes right, ExecutionContext context, LythonSourceSpan span)
    {
        var leftSpan = left.Bytes;
        var rightSpan = right.Bytes;
        var total = (long)leftSpan.Length + rightSpan.Length;
        if (total > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Bytes concatenation is too large.", span);
        }

        context.CheckExecution(span);
        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes((int)total), span);
        var combined = new byte[(int)total];
        leftSpan.CopyTo(combined);
        rightSpan.CopyTo(combined.AsSpan(leftSpan.Length));
        return new PyBytes(combined, context.MemoryGovernor, span, storage);
    }

    internal static PyString OwnMethodResult(PyString result, PyString receiver, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is null || result.OwnerMemoryGovernor is not null ||
            ReferenceEquals(result, receiver) || ReferenceEquals(result, PyString.Empty))
        {
            return result;
        }

        var owned = PyString.FromString(result.AsString(), governor, span);
        return owned;
    }

    // Path values wrap a governed string payload; the wrapper itself retains a
    // small object header beside that payload, so own both together.
    internal static PyPath OwnPathResult(PyString raw, PyString receiver, MemoryGovernor? governor, LythonSourceSpan? span, bool isPure = false)
    {
        if (governor is null)
        {
            return new PyPath(raw, isPure);
        }

        var owned = OwnMethodResult(raw, receiver, governor, span);
        governor.Reserve(64L, span);
        governor.Commit(64L);
        var result = new PyPath(owned, isPure);
        return result;
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

        var repeated = text.Repeat((int)count, context.MemoryGovernor, span);
        GC.KeepAlive(context);
        return repeated;
    }

    private static PyBytes RepeatBytes(PyBytes value, BigInteger count, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero || value.Length == 0)
        {
            return new PyBytes([]);
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Bytes repetition is too large.", span);
        }

        var total = (long)value.Length * (long)count;
        if (total > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Bytes repetition is too large.", span);
        }

        var source = value.Bytes;
        var combined = new byte[(int)total];
        for (var offset = 0; offset < combined.Length; offset += source.Length)
        {
            source.CopyTo(combined.AsSpan(offset));
        }

        return value.OwnerMemoryGovernor is { } governor
            ? new PyBytes(combined, governor, span)
            : new PyBytes(combined);
    }

    private static PyList RepeatList(PyList list, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        var repeatCount = ToListRepeatCount(count, span);
        if (repeatCount == 0 || list.Count == 0)
        {
            var emptyList = new PyList([], context.MemoryGovernor, span);
            return emptyList;
        }

        var totalLength = (long)list.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        context.CheckExecution(span);
        var result = new PyList((int)totalLength, context.MemoryGovernor, span);
        try
        {
            // The source and private destination are distinct native lists; no
            // guest protocol runs here, so preserve references without snapshots.
            for (var i = 0; i < repeatCount; i++)
            {
                for (var item = 0; item < list.Count; item++)
                {
                    context.CheckExecution(span);
                    result.Add(list[item]);
                }
            }
            context.CheckExecution(span);
            return result;
        }
        catch
        {
            result.DiscardUnpublishedStorage();
            throw;
        }
    }

    private static PyTuple RepeatTuple(IReadOnlyList<object> tuple, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero || tuple.Count == 0)
        {
            var emptyTuple = new PyTuple([], context.MemoryGovernor, span);
            return emptyTuple;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Tuple repetition is too large.", span);
        }

        var repeatCount = (int)count;
        var totalLength = (long)tuple.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Tuple repetition is too large.", span);
        }

        // N02: deny before the sized allocation; bound the output count.
        { _ = (int)totalLength; context.CheckExecution(span); }
        using var preflight34788 = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes((int)totalLength), span);
        var items = new object[(int)totalLength];
        var filled = 0;
        for (var i = 0; i < repeatCount; i++)
        {
            for (var j = 0; j < tuple.Count; j++)
            {
                items[filled++] = tuple[j];
                if ((filled & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }

            { _ = (i + 1) * tuple.Count; context.CheckExecution(span); }
        }

        // Fresh array transfers without copying (escape proof: allocated here,
        // never retained elsewhere).
        var repeated = PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
        return repeated;
    }

    private static PyDeque RepeatDeque(PyDeque source, BigInteger count, ExecutionContext context, LythonSourceSpan span)
        => source.Repeat(count, context.MemoryGovernor, span, context);

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
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, dict.Span);
        foreach (var item in dict.Items)
        {
            if (item is DictionaryUnpackingItemSyntax unpacking)
            {
                var mapping = RuntimeValue(EvaluateExpression(unpacking.Mapping, context));
                foreach (var pair in EnumerateMappingItems(mapping, context, unpacking.Span))
                {
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Span), RuntimeValue(pair.Value));
                    context.CheckExecution(dict.Span);
                }

                continue;
            }

            var keyValue = (DictionaryKeyValueItemSyntax)item;
            var key = RuntimeValue(EvaluateExpression(keyValue.Key, context));
            var value = RuntimeValue(EvaluateExpression(keyValue.Value, context));
            result.SetItem(ValidateDictionaryKey(key, keyValue.Key.Span), value);
            context.CheckExecution(dict.Span);
        }
        return result;
    }

    private static object EvaluateSetLiteral(SetLiteralExpressionSyntax set, ExecutionContext context)
    {
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, set.Span);
        var result = new PySet(context.MemoryGovernor, set.Span);
        try
        {
            for (var i = 0; i < set.Items.Count; i++)
            {
                var value = RuntimeValue(EvaluateExpression(set.Items[i].Expression, context));
                AddSetLiteralItem(result, value, set.Items[i].IsUnpacking, set.Items[i].Span, set.Span, context);
            }
        }
        catch
        {
            result.ReleaseUnpublishedStorage();
            throw;
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
        result.Add(ValidateSetItem(value, itemSpan));
        context.CheckExecution(setSpan);
    }

    // Typed mapping-absence signal (N16): non-mappings report false without throwing,
    // so callers recognize them by result instead of comparing English message text.
    // Guest failures from keys()/iteration still throw and propagate untouched, even
    // with identical wording. The public TypeError identity and message for display
    // callers flow through EnumerateMappingItems below.
    internal static bool TryEnumerateMappingItems(
        object mapping,
        ExecutionContext context,
        LythonSourceSpan span,
        [NotNullWhen(true)] out IEnumerable<KeyValuePair<object, object>>? items)
    {
        switch (mapping)
        {
            case PyDict dict:
                items = dict.Items;
                return true;
            case PyDefaultDict defaultDict:
                items = defaultDict.Items;
                return true;
            case PyCounter counter:
                items = counter.Items;
                return true;
            case PyChainMap chainMap:
                {
                    // The private merge remains funded through every pair consumer.
                    items = chainMap.EnumerateMappingKeys(context, span).Select(key =>
                        new KeyValuePair<object, object>(key, chainMap.GetSubscript(key, span)));
                    return true;
                }
            case PyInstance instance:
                if (!TryResolveRuntimeMember(instance, "keys", context, span, out var keysMember))
                {
                    break;
                }

                var keys = InvokeCallableTarget(keysMember, span, span, context, () => []);
                items = ToSequence(keys, span, context).Select(key =>
                    new KeyValuePair<object, object>(key, GetUserItem(instance, key, context, span)));
                return true;
        }

        items = null;
        return false;
    }

    internal static IEnumerable<KeyValuePair<object, object>> EnumerateMappingItems(
        object mapping,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (TryEnumerateMappingItems(mapping, context, span, out var items))
        {
            return items;
        }

        throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.OperandTypeName(mapping) + "' object is not a mapping", span);
    }

}
