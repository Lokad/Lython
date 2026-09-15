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
        var keys = new HashSet<object>(left.Keys, PyValueComparer.Instance);
        keys.UnionWith(right.Keys);

        foreach (var key in keys)
        {
            var leftCount = left.TryGetValue(key, out var foundLeft) ? foundLeft : BigInteger.Zero;
            var rightCount = right.TryGetValue(key, out var foundRight) ? foundRight : BigInteger.Zero;
            if (!(bool)EvaluateBinaryOperator(BinaryOperatorSyntax.LessEqual, leftCount, rightCount, context, span))
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

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) >= 0 ? lhs : rhs, keepPositiveOnly: true, span);
        }

        if (left is PyChainMap leftChainMap && right is PyDict or PyDefaultDict or PyCounter or PyChainMap)
        {
            var first = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in leftChainMap.Maps[0])
            {
                first.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in InPlaceMergePairs(right, span))
            {
                first.SetItem(pair.Key, pair.Value);
            }

            var maps = new List<PyDict> { first };
            for (var i = 1; i < leftChainMap.Maps.Count; i++)
            {
                maps.Add(leftChainMap.Maps[i]);
            }

            return new PyChainMap(maps);
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

            return new PyChainMap([merged]);
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
            var merged = new PyDict(context.MemoryGovernor, span);
            foreach (var pair in leftPairs)
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            foreach (var pair in rightPairs)
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            context.MemoryGovernor.Reserve(64L, span);
            context.MemoryGovernor.Commit(64L);
            var unionResult = new PyDefaultDict(factory, merged);
            context.Services.State.CallTemporaries.TrackFreshMutable(unionResult, unionResult.CommittedStorageBytes);
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
            result.UnionWith(rightSet);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
            return result;
        }

        if (left is PyDict leftDict && right is PyDict rightDict)
        {
            var merged = new PyDict(leftDict, context.MemoryGovernor, span);
            foreach (var pair in rightDict)
            {
                merged.SetItem(pair.Key, pair.Value);
            }

            context.Services.State.CallTemporaries.TrackFreshMutable(merged, merged.CommittedStorageBytes);
            return merged;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw RuntimeErrors.UnsupportedOperands(operation ?? "|", left, right, span);
        }

        return OwnHeapInteger(PyNumberOps.BitwiseOr(lhs, rhs), context.MemoryGovernor, span);
    }

    private static object EvaluateBitwiseXor(object left, object right, ExecutionContext context, LythonSourceSpan span, string? operation = null)
    {
        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return leftBoolean ^ rightBoolean;
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
            result.SymmetricExceptWith(rightSet);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
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

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => CompareCounterCounts(lhs, rhs, span) < 0 ? lhs : rhs, keepPositiveOnly: true, span);
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
            result.IntersectWith(rightSet);
            context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
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
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }
    }

    private static object EvaluateUnaryPlus(object operand, ExecutionContext context, LythonSourceSpan span)
    {
        if (operand is PyCounter positiveCounter)
        {
            return BuildCounterUnaryResult(positiveCounter, count => count, keepPositiveOnly: true, span);
        }

        if (operand is PyTimedelta)
        {
            return operand;
        }

        if (operand is PyDecimal positiveDecimal)
        {
            // CPython plus yields positive zero for a signed-zero operand
            // (like minus), at any exponent; anything else is identity.
            return positiveDecimal.Value == 0m && positiveDecimal.IsSigned
                ? OwnDecimalValue(new PyDecimal(0m, positiveDecimal.Exponent), context, span)
                : operand;
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: false, out var positiveNormalDist))
        {
            return StatisticsModule.OwnNormalDist(positiveNormalDist, context, span);
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
            return BuildCounterUnaryResult(negativeCounter, count => NegateCounterCount(count, span, negativeCounter.OwnerMemoryGovernor), keepPositiveOnly: true, span);
        }

        if (operand is PyTimedelta)
        {
            return PyDateTimeOps.Negate(operand, context, span);
        }

        if (operand is PyDecimal decimalValue)
        {
            // CPython minus yields positive zero for zero operands (unlike
            // the pure sign flip of copy_negate), at any exponent.
            return OwnDecimalValue(
                new PyDecimal(decimalValue.Value == 0m ? 0m : -decimalValue.Value, decimalValue.Exponent),
                context,
                span);
        }

        if (StatisticsModule.TryUnaryNormalDist(operand, negative: true, out var negativeNormalDist))
        {
            return StatisticsModule.OwnNormalDist(negativeNormalDist, context, span);
        }

        if (!PyNumberOps.TryAsNumber(operand, out var numeric))
        {
            throw RuntimeErrors.BadUnaryOperand("-", operand, span);
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
            return OwnDecimalValue(PyDecimalOps.Add(left, right, span, "+"), governor, span);
        }

        PyNumberOps.TryAsNumber(left, out var lhs);
        PyNumberOps.TryAsNumber(right, out var rhs);
        return PyNumberOps.Add(lhs, rhs);
    }

    private static object SubtractCounterCounts(object left, object right, LythonSourceSpan span, MemoryGovernor? governor)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return OwnDecimalValue(PyDecimalOps.Subtract(left, right, span, "-"), governor, span);
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

    private static int CompareCounterCounts(object left, object right, LythonSourceSpan span, string? operation = null)
        => PyComparison.Compare(left, right, span, operation);

    // most_common gates on n == 1, then n >= size through full operator
    // dispatch like CPython (heapq.nlargest does the same); the general path
    // negates and slices, so each failure propagates with its own text.
    private static int CoerceMostCommonLimit(object value, int size, ExecutionContext context, LythonSourceSpan span)
    {
        if (value is null || ReferenceEquals(value, PyNone.Instance))
        {
            return size;
        }

        if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.Equal, value, BigInteger.One, context, span), context, span))
        {
            return 1;
        }

        if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.GreaterEqual, value, new BigInteger(size), context, span), context, span))
        {
            // CPython slices the sorted result with the raw bound here.
            return PyIndexing.NormalizeSliceBounds(size, null, PyIndexing.CoerceSliceBound(value, context, span), null, span).Count;
        }

        var stop = InterpretByteInteger(EvaluateUnaryOperator(UnaryOperatorSyntax.Minus, value, context, span), context, span);
        return stop >= 0 ? 0 : (int)BigInteger.Min(-stop, new BigInteger(size));
    }

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
        var resultLength = RuntimeMemoryEstimates.SaturatingAdd(left.Length, right.Length);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        var result = left.Concat(right, context.MemoryGovernor, span);
        // Dropped concatenations release through the reclamation pool once
        // collected; without tracking, every temporary owned its construction
        // charge forever and bounded call-free loops could never complete.
        context.Services.State.CallTemporaries.TrackFreshString(result);
        return result;
    }

    // Bytes concatenation mirrors the list path: results owned by the
    // inputs stay charged to their governor, while combinations of shared
    // constants stay free like their inputs.
    private static PyBytes ConcatBytes(PyBytes left, PyBytes right, LythonSourceSpan span)
    {
        var leftSpan = left.Bytes;
        var rightSpan = right.Bytes;
        var total = (long)leftSpan.Length + rightSpan.Length;
        if (total > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "Bytes concatenation is too large.", span);
        }

        var combined = new byte[(int)total];
        leftSpan.CopyTo(combined);
        rightSpan.CopyTo(combined.AsSpan(leftSpan.Length));
        var governor = left.OwnerMemoryGovernor ?? right.OwnerMemoryGovernor;
        return governor is null ? new PyBytes(combined) : new PyBytes(combined, governor, span);
    }

    // String methods build results from receiver storage, so a result derived
    // from an unowned (shared-constant) receiver would escape accounting. Adopt
    // fresh results here; aliases and the shared empty string stay free.
    // Adopted results register with refund-on-deny semantics: when the pool entry
    // cannot be funded, the in-flight denial orphans this fresh value, so its
    // construction charge releases instead of stranding. Results returned as-is
    // above stay on the alias-safe plain path (the later funnel no-ops on the
    // already-tracked adoption through reference-identity dedup). A null pool is only
    // valid beside a null governor (both stay together on ungoverned paths).
    internal static PyString OwnMethodResult(PyString result, PyString receiver, MemoryGovernor? governor, LythonSourceSpan? span, ChargeReclamationPool? pool)
    {
        if (governor is null || result.OwnerMemoryGovernor is not null ||
            ReferenceEquals(result, receiver) || ReferenceEquals(result, PyString.Empty))
        {
            return result;
        }

        var owned = PyString.FromString(result.AsString(), governor, span);
        pool?.TrackFreshString(owned, span);
        return owned;
    }

    // Path values wrap a governed string payload; the wrapper itself retains a
    // small object header beside that payload, so own both together.
    internal static PyPath OwnPathResult(PyString raw, PyString receiver, MemoryGovernor? governor, LythonSourceSpan? span, ChargeReclamationPool? pool)
    {
        if (governor is null)
        {
            return new PyPath(raw);
        }

        var owned = OwnMethodResult(raw, receiver, governor, span, pool);
        governor.Reserve(64L, span);
        governor.Commit(64L);
        return new PyPath(owned);
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

        var repeated = text.Repeat((int)count, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshString(repeated);
        return repeated;
    }

    // Bytes repetition mirrors RepeatString without a string-length cap:
    // the byte budget flows through the memory governor instead.
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
            context.Services.State.CallTemporaries.TrackFreshMutable(emptyList, emptyList.CommittedStorageBytes);
            return emptyList;
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

        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
        return result;
    }

    private static PyTuple RepeatTuple(IReadOnlyList<object> tuple, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero || tuple.Count == 0)
        {
            var emptyTuple = new PyTuple([], context.MemoryGovernor, span);
            context.Services.State.CallTemporaries.TrackFreshMutable(emptyTuple, emptyTuple.CommittedStorageBytes);
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

        var items = new object[(int)totalLength];
        for (var i = 0; i < repeatCount; i++)
        {
            for (var j = 0; j < tuple.Count; j++)
            {
                items[i * tuple.Count + j] = tuple[j];
            }

            context.ObserveCollectionCount((i + 1) * tuple.Count, span);
        }

        var repeated = new PyTuple(items, context.MemoryGovernor, span);
        context.Services.State.CallTemporaries.TrackFreshMutable(repeated, repeated.CommittedStorageBytes);
        return repeated;
    }

    private static PyDeque RepeatDeque(PyDeque source, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        var repeatCount = ToDequeRepeatCount(count, span);
        var governor = source.OwnerMemoryGovernor ?? context.MemoryGovernor;
        if (repeatCount == 0 || source.Count == 0)
        {
            return new PyDeque([], source.MaxLength, governor, span);
        }

        var totalLength = (long)source.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Deque repetition is too large.", span);
        }

        var result = new PyDeque([], source.MaxLength, governor, span);
        for (var i = 0; i < repeatCount; i++)
        {
            result.Extend(source.Iterate());
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static int ToDequeRepeatCount(BigInteger count, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return 0;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Deque repetition is too large.", span);
        }

        return (int)count;
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
                    result.SetItem(ValidateDictionaryKey(pair.Key, unpacking.Span), RuntimeValue(pair.Value));
                    context.ObserveCollectionCount(result.Count, dict.Span);
                }

                continue;
            }

            var keyValue = (DictionaryKeyValueItemSyntax)item;
            result.SetItem(
                ValidateDictionaryKey(EvaluateExpression(keyValue.Key, context), keyValue.Key.Span),
                RuntimeValue(EvaluateExpression(keyValue.Value, context)));
            context.ObserveCollectionCount(result.Count, dict.Span);
        }

        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
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

        context.Services.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes);
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
        context.ObserveCollectionCount(result.Count, setSpan);
    }

    internal static IEnumerable<KeyValuePair<object, object>> EnumerateMappingItems(
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
                // The merged-list build below rides Iterate's own transient
                // estimate; the governed destination is charged as it fills.
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

        throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.OperandTypeName(mapping) + "' object is not a mapping", span);
    }

}
