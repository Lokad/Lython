using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        private readonly record struct PairedNumericValues(List<double> X, List<double> Y);

        private readonly record struct CenteredSums(double SumXX, double SumYY, double SumXY);

        // Exact binary summation (N13): every finite double is an exact integer multiple
        // of 2**-1074, so totals stay exact with a single round-half-even at the end,
        // giving correctly-rounded results identical to math.fsum in all finite cases.
        // Two accumulation halves share the infinity rules below. The fsum-checked half
        // (fmean) adds every element exactly into a BigInteger: simple and allocation
        // heavy, which no test constrains. The streaming half (mean) feeds Shewchuk
        // partials exactly like CPython's msum inner loop (double ops only, no per-element
        // allocation) and folds them exactly into a BigInteger grand total every 1024
        // inputs, so traffic stays O(1) however large the input. Past 2**1000 magnitudes
        // the streaming half migrates to direct exact adds (a partial could otherwise
        // round to infinity and destroy information the exact total still needs). Both
        // halves stay bounded by the floating exponent range (hundreds of bytes worst
        // case), never by element count, with no governor interaction. fsum's observable
        // edge rules are mirrored: intermediate overflow raises OverflowError on checked
        // adds, opposing infinities raise ValueError at total time, NaN dominates, and a
        // zero total is +0.0.
        private struct ExactDoubleSum
        {
            // Exact sum of all finite fsum-checked inputs, scaled by 2**1074.
            private BigInteger _scaledTotal;
            // Shewchuk non-overlapping partials for streaming inputs (CPython msum order).
            private List<double>? _partials;
            // Exact folded total of streaming inputs, scaled by 2**1074.
            private BigInteger _grandTotal;
            private int _sinceFold;
            private bool _useBigInteger;
            private bool _hasPositiveInfinity;
            private bool _hasNegativeInfinity;
            private bool _hasNaN;

            public void Add(double value)
            {
                if (double.IsNaN(value))
                {
                    _hasNaN = true;
                }
                else if (double.IsPositiveInfinity(value))
                {
                    _hasPositiveInfinity = true;
                }
                else if (double.IsNegativeInfinity(value))
                {
                    _hasNegativeInfinity = true;
                }
                else
                {
                    _scaledTotal += ScaleDouble(value);
                }
            }

            // fsum-style add: raises OverflowError when the exact running total exceeds
            // the double range, even if later terms would cancel (matches CPython, whose
            // partials overflow the same way). The largest finite scaled total is
            // (2**53-1) * 2**2045 at 2098 bits.
            public void AddChecked(double value, LythonSourceSpan span)
            {
                Add(value);
                if (!_hasPositiveInfinity && !_hasNegativeInfinity && !_hasNaN
                    && BigInteger.Abs(_scaledTotal).GetBitLength() > 2098)
                {
                    throw new LythonRuntimeException("OverflowError", "intermediate overflow in fsum", span);
                }
            }

            // Streaming add for mean: Shewchuk partials exactly like CPython's msum inner
            // loop (double ops only, folded exactly below), migrating to direct exact adds
            // past 2**1000 magnitudes where a partial could round to infinity.
            public void AddMean(double value)
            {
                if (double.IsNaN(value))
                {
                    _hasNaN = true;
                    return;
                }

                if (double.IsPositiveInfinity(value))
                {
                    _hasPositiveInfinity = true;
                    return;
                }

                if (double.IsNegativeInfinity(value))
                {
                    _hasNegativeInfinity = true;
                    return;
                }

                if (_useBigInteger)
                {
                    _grandTotal += ScaleDouble(value);
                    return;
                }

                _partials ??= new List<double>();
                if (Math.Abs(value) >= HugeMagnitudeThreshold || HasHugePartial())
                {
                    FoldMean();
                    _useBigInteger = true;
                    _grandTotal += ScaleDouble(value);
                    return;
                }

                MsumAdd(value);
                if (++_sinceFold >= 1024)
                {
                    FoldMean();
                }
            }

            public void AddMeanInteger(BigInteger value)
            {
                _grandTotal += value << 1074;
            }

            private bool HasHugePartial()
            {
                var partials = _partials;
                if (partials is null)
                {
                    return false;
                }

                foreach (var partial in partials)
                {
                    if (Math.Abs(partial) >= HugeMagnitudeThreshold)
                    {
                        return true;
                    }
                }

                return false;
            }

            // Exact fold of the current partials into the grand total (Shewchuk partials
            // sum exactly to the chunk total). Keeps capacity: no reallocation.
            private void FoldMean()
            {
                var partials = _partials;
                if (partials is not null)
                {
                    foreach (var partial in partials)
                    {
                        _grandTotal += ScaleDouble(partial);
                    }

                    partials.Clear();
                }

                _sinceFold = 0;
            }

            // One msum step in CPython order: merge x through the partials with
            // error-free transformations, keeping non-zero residuals and appending the
            // remainder. Only list growth allocates, and partial counts stay tiny.
            private void MsumAdd(double x)
            {
                var partials = _partials!;
                var count = partials.Count;
                var slot = 0;
                for (var index = 0; index < count; index++)
                {
                    var y = partials[index];
                    if (Math.Abs(x) < Math.Abs(y))
                    {
                        var saved = x;
                        x = y;
                        y = saved;
                    }

                    var hi = x + y;
                    var lo = y - (hi - x);
                    if (lo != 0.0)
                    {
                        partials[slot++] = lo;
                    }

                    x = hi;
                }

                if (slot < count)
                {
                    partials[slot] = x;
                    partials.RemoveRange(slot + 1, count - (slot + 1));
                }
                else
                {
                    partials.Add(x);
                }
            }

            // fsum total: correctly-rounded sum with fsum's infinity rules.
            public double TotalFSum(LythonSourceSpan span)
            {
                if (_hasNaN)
                {
                    return double.NaN;
                }

                if (_hasPositiveInfinity && _hasNegativeInfinity)
                {
                    throw new LythonRuntimeException("ValueError", "-inf + inf in fsum", span);
                }

                if (_hasPositiveInfinity)
                {
                    return double.PositiveInfinity;
                }

                if (_hasNegativeInfinity)
                {
                    return double.NegativeInfinity;
                }

                return DivideExact(_scaledTotal, 1);
            }

            // Mean total: exact streaming sum divided by a positive count with a single
            // rounding. Infinities combine quietly (like CPython's exact _sum, not fsum).
            // A quotient that cannot fit a float raises OverflowError like int division
            // instead of saturating to infinity.
            public double TotalMean(long count, LythonSourceSpan span)
            {
                if (_hasNaN)
                {
                    return double.NaN;
                }

                if (_hasPositiveInfinity && _hasNegativeInfinity)
                {
                    return double.NaN;
                }

                if (_hasPositiveInfinity)
                {
                    return double.PositiveInfinity;
                }

                if (_hasNegativeInfinity)
                {
                    return double.NegativeInfinity;
                }

                FoldMean();
                if (BigInteger.Abs(_grandTotal) > (MaxFiniteDoubleInteger * count) << 1074)
                {
                    throw new LythonRuntimeException("OverflowError", "integer division result too large for a float", span);
                }

                return DivideExact(_grandTotal, count);
            }
        }

        private static BigInteger ScaleDouble(double value)
        {
            // Callers filter non-finite inputs; zero scales to zero (its sign is covered
            // by the +0.0 total rule, matching fsum).
            var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
            var negative = (bits & 0x8000_0000_0000_0000UL) != 0;
            var exponent = (int)((bits >> 52) & 0x7FFUL);
            var mantissa = bits & 0x000F_FFFF_FFFF_FFFFUL;
            BigInteger scaled = exponent == 0
                ? new BigInteger(mantissa)
                : new BigInteger(mantissa | (1UL << 52)) << (exponent - 1);
            return negative ? -scaled : scaled;
        }

        // Correctly-rounded total/count for an exact scaled (x2**1074) BigInteger total
        // and a positive count: a single round-half-even like CPython's exact _sum divided
        // by n. Overflow saturates to infinity (mean/median semantics, unlike fsum's
        // intermediate OverflowError which callers raise during accumulation).
        private static double DivideExact(BigInteger total, long count)
        {
            if (total.IsZero)
            {
                return 0.0;
            }

            var negative = total.Sign < 0;
            var magnitude = BigInteger.Abs(total);
            var divisor = new BigInteger(count) << 1074;
            // Unbiased exponent E of magnitude/divisor.
            var exponent = (int)(magnitude.GetBitLength() - divisor.GetBitLength());
            if (exponent >= 0)
            {
                if ((magnitude >> exponent) < divisor)
                {
                    exponent -= 1;
                }
            }
            else if ((magnitude << -exponent) < divisor)
            {
                exponent -= 1;
            }

            if (exponent > 1023)
            {
                return negative ? double.NegativeInfinity : double.PositiveInfinity;
            }

            double result;
            var keep = exponent >= -1022 ? 53 : exponent + 1075;
            if (keep <= 0)
            {
                // Below half the least subnormal: the exact tie rounds to even (zero).
                result = (magnitude << 1075).CompareTo(divisor) > 0
                    ? BitConverter.Int64BitsToDouble(1L)
                    : 0.0;
            }
            else
            {
                // Round magnitude/divisor to keep bits with guard, round and sticky bits.
                var scale = keep + 1 - exponent;
                BigInteger quotient;
                BigInteger remainder;
                if (scale >= 0)
                {
                    quotient = BigInteger.DivRem(magnitude << scale, divisor, out remainder);
                }
                else
                {
                    quotient = BigInteger.DivRem(magnitude, divisor << -scale, out remainder);
                }

                var dropped = (int)(quotient & 3);
                var mantissa = quotient >> 2;
                if (dropped > 2 || (dropped == 2 && (!remainder.IsZero || !mantissa.IsEven)))
                {
                    mantissa += BigInteger.One;
                }

                if (mantissa == (BigInteger.One << keep))
                {
                    mantissa >>= 1;
                    exponent += 1;
                }

                result = exponent > 1023
                    ? double.PositiveInfinity
                    : Math.ScaleB((double)(ulong)mantissa, exponent - keep + 1);
            }

            return negative ? -result : result;
        }

        // Largest integer whose float conversion is finite; beyond it CPython raises
        // OverflowError instead of saturating (Lython's int->float contract).
        private static readonly BigInteger MaxFiniteDoubleInteger = (BigInteger)double.MaxValue;

        // Streaming partials migrate to direct exact adds at this magnitude so no
        // partial can round to infinity (2**1000 is exactly representable).
        private const double HugeMagnitudeThreshold = 1.0715086071862673e301;

        // Shared mean accumulation (N15): the exact N13 bookkeeping (int/bool exact
        // sum, exact binary float summer, exact decimal sum, CPython _convert type
        // rules, Decimal/float mixing rejection, arity/emptiness/validation precedence)
        // used identically by the sync and async loops, so only iteration differs.
        // Streaming stays O(1) scratch however large the input.
        private struct MeanAccumulator
        {
            private BigInteger _intTotal;
            private decimal _decimalTotal;
            private bool _seenFloat;
            private bool _seenDecimal;
            private ExactDoubleSum _summer;
            private int _count;

            public void Add(object value, LythonSourceSpan span, ExecutionContext context)
            {
                if (value is bool boolean)
                {
                    _intTotal += boolean ? BigInteger.One : BigInteger.Zero;
                }
                else if (value is BigInteger integer)
                {
                    _intTotal += integer;
                }
                else if (value is double floating)
                {
                    if (_seenDecimal)
                    {
                        throw new LythonRuntimeException("TypeError", "statistics.mean(data) doesn't support mixing Decimal and float.", span);
                    }

                    _seenFloat = true;
                    _summer.AddMean(floating);
                }
                else if (value is PyDecimal decimalValue)
                {
                    if (_seenFloat)
                    {
                        throw new LythonRuntimeException("TypeError", "statistics.mean(data) doesn't support mixing Decimal and float.", span);
                    }

                    _seenDecimal = true;
                    try
                    {
                        _decimalTotal += decimalValue.Value;
                    }
                    catch (OverflowException)
                    {
                        throw PyDecimalOps.DecimalOverflow(span);
                    }
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", "statistics.mean(data) expects real numbers.", span);
                }

                _count++;
                context.ObserveCollectionCount(_count, span);
                if ((_count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            public object Complete(LythonSourceSpan span)
            {
                if (_count == 0)
                {
                    throw StatisticsError("statistics.mean(data) requires at least one data point.", span);
                }

                if (_seenDecimal)
                {
                    try
                    {
                        return new PyDecimal((_decimalTotal + (decimal)_intTotal) / _count);
                    }
                    catch (OverflowException)
                    {
                        throw PyDecimalOps.DecimalOverflow(span);
                    }
                }

                if (!_seenFloat && _intTotal % _count == BigInteger.Zero)
                {
                    return _intTotal / _count;
                }

                _summer.AddMeanInteger(_intTotal);
                return _summer.TotalMean(_count, span);
            }
        }

        private static object Mean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // Exact streamed accumulation (N13): ints and bools sum exactly in a
            // BigInteger, floats join an exact binary summer, Decimals sum exactly in
            // decimal. Streaming stays O(1) scratch however large the input (the exact
            // summer is bounded by the floating exponent range, not element count).
            // CPython _convert rules decide the result type: all-int means int when
            // divisible, float otherwise; any float means float; any Decimal (without
            // float) means Decimal. Decimal+float raises TypeError like CPython.
            // Error precedence mirrors GetNumericObjects: arity, then emptiness,
            // then per-element validation.
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "statistics.mean(data) expects one iterable argument.", span);
            }

            var accumulator = new MeanAccumulator();
            foreach (var value in ToSequence(arguments[0], span, context))
            {
                accumulator.Add(value, span, context);
            }

            return accumulator.Complete(span);
        }

        private static async ValueTask<object> MeanAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "statistics.mean(data) expects one iterable argument.", span);
            }

            var accumulator = new MeanAccumulator();
            await foreach (var value in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
            {
                accumulator.Add(value, span, context);
            }

            return accumulator.Complete(span);
        }

        private static object FMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // N07: drained inputs are caller-scoped scratch: values and weights share
            // one reservation and release on every exit path instead of stranding.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericValuesFromData(arguments, "statistics.fmean", span, context, scratch);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                // N13: correctly-rounded fsum instead of the naive LINQ average.
                var unweighted = new ExactDoubleSum();
                foreach (var value in values)
                {
                    unweighted.AddChecked(value, span);
                }

                return unweighted.TotalFSum(span) / values.Count;
            }

            var weights = GetNumericValuesFromIterable(arguments[1], "statistics.fmean(..., weights=...)", span, context, scratch);
            if (weights.Count != values.Count)
            {
                throw StatisticsError("data and weights must be the same length", span);
            }

            var weightedSum = new ExactDoubleSum();
            var weightTotal = new ExactDoubleSum();
            for (var index = 0; index < values.Count; index++)
            {
                weightedSum.AddChecked(values[index] * weights[index], span);
                weightTotal.AddChecked(weights[index], span);
            }

            var weightSum = weightTotal.TotalFSum(span);
            if (weightSum == 0.0)
            {
                throw StatisticsError("sum of weights must be non-zero", span);
            }

            return weightedSum.TotalFSum(span) / weightSum;
        }

        private static async ValueTask<object> FMeanAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericValuesFromDataAsync(arguments, "statistics.fmean", span, context, scratch).ConfigureAwait(false);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var unweighted = new ExactDoubleSum();
                foreach (var value in values)
                {
                    unweighted.AddChecked(value, span);
                }

                return unweighted.TotalFSum(span) / values.Count;
            }

            var weights = await GetNumericValuesFromIterableAsync(arguments[1], "statistics.fmean(..., weights=...)", span, context, scratch).ConfigureAwait(false);
            if (weights.Count != values.Count)
            {
                throw StatisticsError("data and weights must be the same length", span);
            }

            var weightedSum = new ExactDoubleSum();
            var weightTotal = new ExactDoubleSum();
            for (var index = 0; index < values.Count; index++)
            {
                weightedSum.AddChecked(values[index] * weights[index], span);
                weightTotal.AddChecked(weights[index], span);
            }

            var weightSum = weightTotal.TotalFSum(span);
            if (weightSum == 0.0)
            {
                throw StatisticsError("sum of weights must be non-zero", span);
            }

            return weightedSum.TotalFSum(span) / weightSum;
        }

        private static object Median(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // N07: the objects drain stays caller-scoped scratch; unlike before, no
            // converted doubles copy exists. N13: the sort runs over the original
            // values and odd selections return the element itself, preserving
            // BigInteger/Decimal/bool identity like CPython. Even selections average
            // the two middles with (a+b)/2 value semantics per type.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericObjects(arguments, "statistics.median", span, context, scratch);
            values.Sort((left, right) => Compare(left, right, span));
            var middle = values.Count / 2;
            if (values.Count % 2 == 1)
            {
                return values[middle];
            }

            return AverageMedianPair(values[middle - 1], values[middle], span);
        }

        private static async ValueTask<object> MedianAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericObjectsAsync(arguments, "statistics.median", span, context, scratch).ConfigureAwait(false);
            values.Sort((left, right) => Compare(left, right, span));
            var middle = values.Count / 2;
            if (values.Count % 2 == 1)
            {
                return values[middle];
            }

            return AverageMedianPair(values[middle - 1], values[middle], span);
        }

        // CPython (a+b)/2 value semantics for an even median pair (already validated
        // as real numbers): int-like pairs divide exactly with one rounding (a float
        // result, saturating to infinity like CPython); Decimal pairs stay Decimal
        // unless a float is present, which is a TypeError; anything else averages
        // as doubles.
        private static object AverageMedianPair(object left, object right, LythonSourceSpan span)
        {
            if (left is PyDecimal || right is PyDecimal)
            {
                if (left is double || right is double)
                {
                    throw new LythonRuntimeException("TypeError", "statistics.median(data) doesn't support mixing Decimal and float.", span);
                }

                if (!PyDecimalOps.TryAsDecimal(left, out var lhs) || !PyDecimalOps.TryAsDecimal(right, out var rhs))
                {
                    throw PyDecimalOps.DecimalOverflow(span);
                }

                try
                {
                    return new PyDecimal((lhs + rhs) / 2m);
                }
                catch (OverflowException)
                {
                    throw PyDecimalOps.DecimalOverflow(span);
                }
            }

            if (TryMedianInteger(left, out var leftInteger) && TryMedianInteger(right, out var rightInteger))
            {
                // Like CPython's (a+b)/2 on ints: the quotient saturates nothing - when it
                // cannot fit a float this raises OverflowError instead.
                var sum = leftInteger + rightInteger;
                if (BigInteger.Abs(sum) > 2 * MaxFiniteDoubleInteger)
                {
                    throw new LythonRuntimeException("OverflowError", "integer division result too large for a float", span);
                }

                return DivideExact(sum << 1074, 2);
            }

            // Validated non-Decimal reals that are not both int-like contain a float.
            _ = PyRealNumber.TryAsDouble(left, out var leftReal);
            _ = PyRealNumber.TryAsDouble(right, out var rightReal);
            return (leftReal + rightReal) / 2.0;
        }

        private static bool TryMedianInteger(object value, out BigInteger integer)
        {
            switch (value)
            {
                case bool boolean:
                    integer = boolean ? BigInteger.One : BigInteger.Zero;
                    return true;
                case BigInteger bigInteger:
                    integer = bigInteger;
                    return true;
                default:
                    integer = default;
                    return false;
            }
        }

        private static object MedianLow(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // N07: the drain lives for the selection (including guest sort
            // callbacks) and releases on every exit path.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericObjects(arguments, "statistics.median_low", span, context, scratch);
            values.Sort((left, right) => Compare(left, right, span));
            return values[(values.Count - 1) / 2];
        }

        private static async ValueTask<object> MedianLowAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericObjectsAsync(arguments, "statistics.median_low", span, context, scratch).ConfigureAwait(false);
            values.Sort((left, right) => Compare(left, right, span));
            return values[(values.Count - 1) / 2];
        }

        private static object MedianHigh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // N07: same caller-scoped lifetime as MedianLow.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericObjects(arguments, "statistics.median_high", span, context, scratch);
            values.Sort((left, right) => Compare(left, right, span));
            return values[values.Count / 2];
        }

        private static async ValueTask<object> MedianHighAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericObjectsAsync(arguments, "statistics.median_high", span, context, scratch).ConfigureAwait(false);
            values.Sort((left, right) => Compare(left, right, span));
            return values[values.Count / 2];
        }

        private static object Mode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = GetModeValues(arguments, "statistics.mode", allowEmpty: false, span, context);
            var values = modeLease.Items;
            return GetModeCounts(values, context.MemoryGovernor, span, context).First().Key;
        }

        private static async ValueTask<object> ModeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = await GetModeValuesAsync(arguments, "statistics.mode", allowEmpty: false, span, context).ConfigureAwait(false);
            var values = modeLease.Items;
            return GetModeCounts(values, context.MemoryGovernor, span, context).First().Key;
        }

        private static object MultiMode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = GetModeValues(arguments, "statistics.multimode", allowEmpty: true, span, context);
            var values = modeLease.Items;
            var counts = GetModeCounts(values, context.MemoryGovernor, span, context);
            var modes = new object[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                modes[i] = counts[i].Key;
            }

            return new PyList(modes, context.MemoryGovernor, span);
        }

        private static async ValueTask<object> MultiModeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = await GetModeValuesAsync(arguments, "statistics.multimode", allowEmpty: true, span, context).ConfigureAwait(false);
            var values = modeLease.Items;
            var counts = GetModeCounts(values, context.MemoryGovernor, span, context);
            var modes = new object[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                modes[i] = counts[i].Key;
            }

            return new PyList(modes, context.MemoryGovernor, span);
        }

        private static object MedianGrouped(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericValuesFromData(arguments, "statistics.median_grouped", span, context, scratch);
            values.Sort();
            var interval = arguments.Length >= 2 && arguments[1] is not PyNone
                ? RuntimeArgumentValidation.ExpectReal(arguments[1], "statistics.median_grouped(..., interval=...)", span)
                : 1.0;
            if (interval <= 0)
            {
                throw new LythonRuntimeException("ValueError", "statistics.median_grouped(..., interval=...) expects a positive interval.", span);
            }

            var target = values[values.Count / 2];
            var below = 0;
            var equal = 0;
            foreach (var value in values)
            {
                if (value < target)
                {
                    below++;
                }
                else if (value == target)
                {
                    equal++;
                }
            }

            var lowerLimit = target - interval / 2.0;
            return lowerLimit + interval * (values.Count / 2.0 - below) / equal;
        }

        private static async ValueTask<object> MedianGroupedAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericValuesFromDataAsync(arguments, "statistics.median_grouped", span, context, scratch).ConfigureAwait(false);
            values.Sort();
            var interval = arguments.Length >= 2 && arguments[1] is not PyNone
                ? RuntimeArgumentValidation.ExpectReal(arguments[1], "statistics.median_grouped(..., interval=...)", span)
                : 1.0;
            if (interval <= 0)
            {
                throw new LythonRuntimeException("ValueError", "statistics.median_grouped(..., interval=...) expects a positive interval.", span);
            }

            var target = values[values.Count / 2];
            var below = 0;
            var equal = 0;
            foreach (var value in values)
            {
                if (value < target)
                {
                    below++;
                }
                else if (value == target)
                {
                    equal++;
                }
            }

            var lowerLimit = target - interval / 2.0;
            return lowerLimit + interval * (values.Count / 2.0 - below) / equal;
        }

        private static object HarmonicMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var data = GetNumericValuesFromData(arguments, "statistics.harmonic_mean", span, context, scratch);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var reciprocalTotal = 0.0;
                foreach (var value in data)
                {
                    if (value < 0)
                    {
                        throw StatisticsError("harmonic mean does not support negative values", span);
                    }

                    if (value == 0.0)
                    {
                        return 0.0;
                    }

                    reciprocalTotal += 1.0 / value;
                }

                return data.Count / reciprocalTotal;
            }

            var weights = GetNumericValuesFromIterable(arguments[1], "statistics.harmonic_mean(..., weights=...)", span, context, scratch);
            if (weights.Count != data.Count)
            {
                throw StatisticsError("Number of weights does not match data size", span);
            }

            var weightTotal = 0.0;
            var reciprocalTotalWeighted = 0.0;
            for (var i = 0; i < data.Count; i++)
            {
                var value = data[i];
                var weight = weights[i];
                if (value < 0 || weight < 0)
                {
                    throw StatisticsError("harmonic mean does not support negative values", span);
                }

                if (value == 0.0 && weight > 0.0)
                {
                    return 0.0;
                }

                weightTotal += weight;
                if (weight != 0.0)
                {
                    reciprocalTotalWeighted += weight / value;
                }
            }

            if (weightTotal <= 0.0)
            {
                throw StatisticsError("Weighted sum must be positive", span);
            }

            return weightTotal / reciprocalTotalWeighted;
        }

        private static async ValueTask<object> HarmonicMeanAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var data = await GetNumericValuesFromDataAsync(arguments, "statistics.harmonic_mean", span, context, scratch).ConfigureAwait(false);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var reciprocalTotal = 0.0;
                foreach (var value in data)
                {
                    if (value < 0)
                    {
                        throw StatisticsError("harmonic mean does not support negative values", span);
                    }

                    if (value == 0.0)
                    {
                        return 0.0;
                    }

                    reciprocalTotal += 1.0 / value;
                }

                return data.Count / reciprocalTotal;
            }

            var weights = await GetNumericValuesFromIterableAsync(arguments[1], "statistics.harmonic_mean(..., weights=...)", span, context, scratch).ConfigureAwait(false);
            if (weights.Count != data.Count)
            {
                throw StatisticsError("Number of weights does not match data size", span);
            }

            var weightTotal = 0.0;
            var reciprocalTotalWeighted = 0.0;
            for (var i = 0; i < data.Count; i++)
            {
                var value = data[i];
                var weight = weights[i];
                if (value < 0 || weight < 0)
                {
                    throw StatisticsError("harmonic mean does not support negative values", span);
                }

                if (value == 0.0 && weight > 0.0)
                {
                    return 0.0;
                }

                weightTotal += weight;
                if (weight != 0.0)
                {
                    reciprocalTotalWeighted += weight / value;
                }
            }

            if (weightTotal <= 0.0)
            {
                throw StatisticsError("Weighted sum must be positive", span);
            }

            return weightTotal / reciprocalTotalWeighted;
        }

        private static object GeometricMean(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericValuesFromData(arguments, "statistics.geometric_mean", span, context, scratch);
            var logTotal = 0.0;
            foreach (var value in values)
            {
                if (value < 0)
                {
                    throw StatisticsError("geometric mean does not support negative values", span);
                }

                if (value == 0.0)
                {
                    return 0.0;
                }

                logTotal += Math.Log(value);
            }

            return Math.Exp(logTotal / values.Count);
        }

        private static async ValueTask<object> GeometricMeanAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericValuesFromDataAsync(arguments, "statistics.geometric_mean", span, context, scratch).ConfigureAwait(false);
            var logTotal = 0.0;
            foreach (var value in values)
            {
                if (value < 0)
                {
                    throw StatisticsError("geometric mean does not support negative values", span);
                }

                if (value == 0.0)
                {
                    return 0.0;
                }

                logTotal += Math.Log(value);
            }

            return Math.Exp(logTotal / values.Count);
        }

        private static double VarianceToDouble(object variance)
            => variance is BigInteger integral ? (double)integral : (double)variance;

        private static bool TryAsIntegralStatistic(object value, out BigInteger integer)
        {
            switch (value)
            {
                case BigInteger integral:
                    integer = integral;
                    return true;
                case int small:
                    integer = new BigInteger(small);
                    return true;
                case long large:
                    integer = new BigInteger(large);
                    return true;
                case bool boolean:
                    integer = boolean ? BigInteger.One : BigInteger.Zero;
                    return true;
                default:
                    integer = default;
                    return false;
            }
        }

        // Exact-eligibility: every drained value is int-like. Anything else
        // (floats, Decimals, and anything the drain already rejected) takes
        // the historical doubles below.
        private static List<BigInteger>? TryGetIntegralList(List<object> values)
        {
            var integers = new List<BigInteger>(values.Count);
            foreach (var value in values)
            {
                if (!TryAsIntegralStatistic(value, out var integer))
                {
                    return null;
                }

                integers.Add(integer);
            }

            return integers;
        }

        // Exact sum-of-squares (CPython _ss over ints): integral results keep
        // int type, fractional ones convert once at the end. Budget checks ride
        // the accumulation at the drain cadence.
        private static object ComputeIntegralVariance(
            List<BigInteger> integers,
            BigInteger? mu,
            bool sample,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var count = integers.Count;
            var divisor = sample ? count - 1 : count;
            BigInteger numerator;
            BigInteger denominator;
            if (mu is { } center)
            {
                numerator = BigInteger.Zero;
                denominator = divisor;
                var i = 0;
                foreach (var value in integers)
                {
                    var diff = value - center;
                    numerator += diff * diff;
                    if ((++i & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }
                }
            }
            else
            {
                BigInteger sum = BigInteger.Zero;
                BigInteger sumSquares = BigInteger.Zero;
                var i = 0;
                foreach (var value in integers)
                {
                    sum += value;
                    sumSquares += value * value;
                    if ((++i & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }
                }

                numerator = (BigInteger)count * sumSquares - sum * sum;
                denominator = (BigInteger)count * divisor;
            }

            var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
            return remainder.IsZero ? (object)quotient : (double)numerator / (double)denominator;
        }

        private static object PopulationStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(VarianceToDouble(ComputeVariance(arguments, "statistics.pstdev", span, context, sample: false)));

        private static async ValueTask<object> PopulationStdevAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(VarianceToDouble(await ComputeVarianceAsync(arguments, "statistics.pstdev", span, context, sample: false).ConfigureAwait(false)));

        private static object SampleStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(VarianceToDouble(ComputeVariance(arguments, "statistics.stdev", span, context, sample: true)));

        private static async ValueTask<object> SampleStdevAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => Math.Sqrt(VarianceToDouble(await ComputeVarianceAsync(arguments, "statistics.stdev", span, context, sample: true).ConfigureAwait(false)));

        private static object PopulationVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ComputeVariance(arguments, "statistics.pvariance", span, context, sample: false);

        private static async ValueTask<object> PopulationVarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => await ComputeVarianceAsync(arguments, "statistics.pvariance", span, context, sample: false).ConfigureAwait(false);

        private static object SampleVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ComputeVariance(arguments, "statistics.variance", span, context, sample: true);

        private static async ValueTask<object> SampleVarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => await ComputeVarianceAsync(arguments, "statistics.variance", span, context, sample: true).ConfigureAwait(false);

        private static object ComputeVariance(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, bool sample)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            // CPython reports two points for an empty sample and one for an empty
            // population before draining; the shared drain reports one for both.
            if (arguments.Length > 0 && arguments[0] is IReadOnlyCollection<object> sized && sized.Count == 0)
            {
                throw StatisticsError(sample
                    ? $"{owner}(data) requires at least two data points."
                    : $"{owner} requires at least one data point.", span);
            }

            var source = arguments.Length <= 1 ? arguments : new[] { arguments[0] };
            var values = GetNumericObjects(source, owner, span, context, scratch);
            if (sample && values.Count < 2)
            {
                throw StatisticsError($"{owner}(data) requires at least two data points.", span);
            }

            // All-integral inputs (with an int-like mu) take the exact path below;
            // the rest convert to historical doubles.
            if (TryGetIntegralList(values) is { } integers)
            {
                BigInteger? mu = null;
                var exactMu = true;
                if (arguments.Length >= 2 && arguments[1] is not PyNone)
                {
                    exactMu = TryAsIntegralStatistic(arguments[1], out var center);
                    mu = center;
                }

                if (exactMu)
                {
                    return ComputeIntegralVariance(integers, mu, sample, span, context);
                }
            }

            var doubles = new List<double>(values.Count);
            foreach (var value in values)
            {
                doubles.Add(ExpectRealForStatistics(value, owner, span));
            }

            var mean = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectRealForStatistics(arguments[1], owner, span)
                : doubles.Average();
            // CPython _ss ignores finite values once any input is non-finite:
            // the result is the special-only sum (in encounter order) over the divisor.
            // With an explicit mu both engines already use naive doubles, so this
            // applies to the computed-mean path only.
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var special = 0.0;
                var hasSpecial = false;
                foreach (var value in doubles)
                {
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        special += value;
                        hasSpecial = true;
                    }
                }

                if (hasSpecial)
                {
                    return special / (sample ? doubles.Count - 1 : doubles.Count);
                }
            }

            var sum = doubles.Sum(value => Math.Pow(value - mean, 2));
            return sum / (sample ? doubles.Count - 1 : doubles.Count);
        }

        private static async ValueTask<object> ComputeVarianceAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, bool sample)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            // Same empty checks as the synchronous twin above.
            if (arguments.Length > 0 && arguments[0] is IReadOnlyCollection<object> sized && sized.Count == 0)
            {
                throw StatisticsError(sample
                    ? $"{owner}(data) requires at least two data points."
                    : $"{owner} requires at least one data point.", span);
            }

            var source = arguments.Length <= 1 ? arguments : new[] { arguments[0] };
            var values = await GetNumericObjectsAsync(source, owner, span, context, scratch).ConfigureAwait(false);
            if (sample && values.Count < 2)
            {
                throw StatisticsError($"{owner}(data) requires at least two data points.", span);
            }

            // All-integral inputs (with an int-like mu) take the exact path below;
            // the rest convert to historical doubles.
            if (TryGetIntegralList(values) is { } integers)
            {
                BigInteger? mu = null;
                var exactMu = true;
                if (arguments.Length >= 2 && arguments[1] is not PyNone)
                {
                    exactMu = TryAsIntegralStatistic(arguments[1], out var center);
                    mu = center;
                }

                if (exactMu)
                {
                    return ComputeIntegralVariance(integers, mu, sample, span, context);
                }
            }

            var doubles = new List<double>(values.Count);
            foreach (var value in values)
            {
                doubles.Add(ExpectRealForStatistics(value, owner, span));
            }

            var mean = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectRealForStatistics(arguments[1], owner, span)
                : doubles.Average();
            // CPython _ss ignores finite values once any input is non-finite:
            // the result is the special-only sum (in encounter order) over the divisor.
            // With an explicit mu both engines already use naive doubles, so this
            // applies to the computed-mean path only.
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                var special = 0.0;
                var hasSpecial = false;
                foreach (var value in doubles)
                {
                    if (double.IsNaN(value) || double.IsInfinity(value))
                    {
                        special += value;
                        hasSpecial = true;
                    }
                }

                if (hasSpecial)
                {
                    return special / (sample ? doubles.Count - 1 : doubles.Count);
                }
            }

            var sum = doubles.Sum(value => Math.Pow(value - mean, 2));
            return sum / (sample ? doubles.Count - 1 : doubles.Count);
        }

        private static object Quantiles(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            // Sized-empty keeps the historical text (the shared objects drain
            // below reports it with a different qualifier).
            if (arguments.Length > 0 && arguments[0] is IReadOnlyCollection<object> sized && sized.Count == 0)
            {
                throw StatisticsError("statistics.quantiles requires at least one data point.", span);
            }

            var source = arguments.Length <= 1 ? arguments : new[] { arguments[0] };
            var originals = GetNumericObjects(source, "statistics.quantiles", span, context, scratch);
            var n = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectPositivePartitionCount(arguments[1], "statistics.quantiles(..., n=...)", span)
                : 4;
            // CPython returns the single data point itself (original type) n-1
            // times, before validating the method.
            if (originals.Count == 1)
            {
                context.ObserveCollectionCount(n - 1, span);
                var single = new List<object>(Math.Max(n - 1, 0));
                for (var i = 1; i < n; i++)
                {
                    single.Add(originals[0]);
                }

                return new PyList(single, context.MemoryGovernor, span);
            }
            // Cut points interpolate over doubles (true-division floats like
            // CPython); the originals above only serve the single-point case.
            var values = new List<double>(originals.Count);
            foreach (var original in originals)
            {
                values.Add(ExpectRealForStatistics(original, "statistics.quantiles", span));
            }

            values.Sort();
            var method = arguments.Length >= 3 && arguments[2] is not PyNone
                ? RuntimeArgumentValidation.ExpectString(arguments[2], "statistics.quantiles(..., method=...)", span)
                : "exclusive";
            if (method != "exclusive" && method != "inclusive")
            {
                throw new LythonRuntimeException("ValueError", $"Unknown method: '{method}'", span);
            }

            if (n == 1)
            {
                return new PyList([], context.MemoryGovernor, span);
            }

            // N04: preflight output count/capacity; reserve scratch before it can allocate,
            // keep it alive through governed construction; check work during the fill.
            var outputCount = n - 1;
            context.ObserveCollectionCount(outputCount, span);
            using var cutScratch = context.MemoryGovernor.ReserveTemporary(checked(8L * outputCount), span);
            var cutPoints = new List<object>(outputCount);
            var count = values.Count;
            for (var i = 1; i < n; i++)
            {
                var value = method == "inclusive"
                    ? InterpolateInclusiveQuantile(values, i, n)
                    : InterpolateExclusiveQuantile(values, i, n);
                cutPoints.Add(value);
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return new PyList(cutPoints, context.MemoryGovernor, span);
        }

        private static async ValueTask<object> QuantilesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            // Same empty checks as the synchronous twin above.
            if (arguments.Length > 0 && arguments[0] is IReadOnlyCollection<object> sized && sized.Count == 0)
            {
                throw StatisticsError("statistics.quantiles requires at least one data point.", span);
            }

            var source = arguments.Length <= 1 ? arguments : new[] { arguments[0] };
            var originals = await GetNumericObjectsAsync(source, "statistics.quantiles", span, context, scratch).ConfigureAwait(false);
            var n = arguments.Length >= 2 && arguments[1] is not PyNone
                ? ExpectPositivePartitionCount(arguments[1], "statistics.quantiles(..., n=...)", span)
                : 4;
            // CPython returns the single data point itself (original type) n-1
            // times, before validating the method.
            if (originals.Count == 1)
            {
                context.ObserveCollectionCount(n - 1, span);
                var single = new List<object>(Math.Max(n - 1, 0));
                for (var i = 1; i < n; i++)
                {
                    single.Add(originals[0]);
                }

                return new PyList(single, context.MemoryGovernor, span);
            }
            // Cut points interpolate over doubles (true-division floats like
            // CPython); the originals above only serve the single-point case.
            var values = new List<double>(originals.Count);
            foreach (var original in originals)
            {
                values.Add(ExpectRealForStatistics(original, "statistics.quantiles", span));
            }

            values.Sort();
            var method = arguments.Length >= 3 && arguments[2] is not PyNone
                ? RuntimeArgumentValidation.ExpectString(arguments[2], "statistics.quantiles(..., method=...)", span)
                : "exclusive";
            if (method != "exclusive" && method != "inclusive")
            {
                throw new LythonRuntimeException("ValueError", $"Unknown method: '{method}'", span);
            }

            if (n == 1)
            {
                return new PyList([], context.MemoryGovernor, span);
            }

            var outputCount = n - 1;
            context.ObserveCollectionCount(outputCount, span);
            using var cutScratch = context.MemoryGovernor.ReserveTemporary(checked(8L * outputCount), span);
            var cutPoints = new List<object>(outputCount);
            var count = values.Count;
            for (var i = 1; i < n; i++)
            {
                var value = method == "inclusive"
                    ? InterpolateInclusiveQuantile(values, i, n)
                    : InterpolateExclusiveQuantile(values, i, n);
                cutPoints.Add(value);
                if ((i & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return new PyList(cutPoints, context.MemoryGovernor, span);
        }

        private static object Covariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = GetPairedNumericValues(arguments, "statistics.covariance", span, context);
            if (x.Count < 2)
            {
                throw StatisticsError("covariance requires at least two data points", span);
            }

            var (xMean, yMean) = (x.Average(), y.Average());
            var sum = 0.0;
            for (var i = 0; i < x.Count; i++)
            {
                sum += (x[i] - xMean) * (y[i] - yMean);
            }

            return sum / (x.Count - 1);
        }

        private static async ValueTask<object> CovarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = await GetPairedNumericValuesAsync(arguments, "statistics.covariance", span, context).ConfigureAwait(false);
            if (x.Count < 2)
            {
                throw StatisticsError("covariance requires at least two data points", span);
            }

            var (xMean, yMean) = (x.Average(), y.Average());
            var sum = 0.0;
            for (var i = 0; i < x.Count; i++)
            {
                sum += (x[i] - xMean) * (y[i] - yMean);
            }

            return sum / (x.Count - 1);
        }

        private static object Correlation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = GetPairedNumericValues(arguments, "statistics.correlation", span, context);
            if (x.Count < 2)
            {
                throw StatisticsError("correlation requires at least two data points", span);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0 || sums.SumYY == 0.0)
            {
                throw StatisticsError("at least one of the inputs is constant", span);
            }

            return sums.SumXY / Math.Sqrt(sums.SumXX * sums.SumYY);
        }

        private static async ValueTask<object> CorrelationAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (x, y) = await GetPairedNumericValuesAsync(arguments, "statistics.correlation", span, context).ConfigureAwait(false);
            if (x.Count < 2)
            {
                throw StatisticsError("correlation requires at least two data points", span);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0 || sums.SumYY == 0.0)
            {
                throw StatisticsError("at least one of the inputs is constant", span);
            }

            return sums.SumXY / Math.Sqrt(sums.SumXX * sums.SumYY);
        }

        private static object LinearRegression(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "statistics.linear_regression(x, y) expects two iterable arguments.", span);
            }

            var (x, y) = GetPairedNumericValues([arguments[0], arguments[1]], "statistics.linear_regression", span, context);
            if (x.Count < 2)
            {
                throw StatisticsError("linear_regression requires at least two data points", span);
            }

            var proportional = arguments.Length >= 3 && arguments[2] is not PyNone && IsTruthy(arguments[2]);
            if (proportional)
            {
                var sumXX = 0.0;
                var sumXY = 0.0;
                for (var i = 0; i < x.Count; i++)
                {
                    sumXX += x[i] * x[i];
                    sumXY += x[i] * y[i];
                }

                if (sumXX == 0.0)
                {
                    throw StatisticsError("x is constant", span);
                }

                return new StatisticsLinearRegressionResult(sumXY / sumXX, 0.0);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0)
            {
                throw StatisticsError("x is constant", span);
            }

            var slope = sums.SumXY / sums.SumXX;
            var intercept = y.Average() - slope * x.Average();
            return new StatisticsLinearRegressionResult(slope, intercept);
        }

        private static async ValueTask<object> LinearRegressionAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 2 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "statistics.linear_regression(x, y) expects two iterable arguments.", span);
            }

            var (x, y) = await GetPairedNumericValuesAsync([arguments[0], arguments[1]], "statistics.linear_regression", span, context).ConfigureAwait(false);
            if (x.Count < 2)
            {
                throw StatisticsError("linear_regression requires at least two data points", span);
            }

            var proportional = arguments.Length >= 3 && arguments[2] is not PyNone && IsTruthy(arguments[2]);
            if (proportional)
            {
                var sumXX = 0.0;
                var sumXY = 0.0;
                for (var i = 0; i < x.Count; i++)
                {
                    sumXX += x[i] * x[i];
                    sumXY += x[i] * y[i];
                }

                if (sumXX == 0.0)
                {
                    throw StatisticsError("x is constant", span);
                }

                return new StatisticsLinearRegressionResult(sumXY / sumXX, 0.0);
            }

            var sums = ComputeCenteredSums(x, y);
            if (sums.SumXX == 0.0)
            {
                throw StatisticsError("x is constant", span);
            }

            var slope = sums.SumXY / sums.SumXX;
            var intercept = y.Average() - slope * x.Average();
            return new StatisticsLinearRegressionResult(slope, intercept);
        }

        private static object LinearRegressionResult(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "statistics.LinearRegression(slope, intercept) expects two arguments.", span);
            }

            return new StatisticsLinearRegressionResult(
                RuntimeArgumentValidation.ExpectReal(arguments[0], "statistics.LinearRegression(..., slope=...)", span),
                RuntimeArgumentValidation.ExpectReal(arguments[1], "statistics.LinearRegression(..., intercept=...)", span));
        }

        private static List<KeyValuePair<object, int>> GetModeCounts(IReadOnlyList<object> values, MemoryGovernor governor, LythonSourceSpan span, ExecutionContext context)
        {
            if (values.Count == 0)
            {
                return [];
            }

            // N12: contextual frequency table (guest __hash__/__eq__, first-seen order
            // decides ties). Same caller-lifetime scratch bound as before, plus work checks.
            using var scratch = governor.ReserveTemporary(144L + (56L * values.Count), span);
            var counts = new ContextualKeyTable<int>();
            var work = 0;
            foreach (var value in values)
            {
                try
                {
                    counts.GetOrAdd(value, 0, context, span).Value++;
                }
                catch (InvalidOperationException)
                {
                    throw RuntimeErrors.UnhashableType(value, span);
                }

                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            var maxCount = 0;
            foreach (var entry in counts.EntriesInOrder)
            {
                maxCount = Math.Max(maxCount, entry.Value);
            }

            var result = new List<KeyValuePair<object, int>>(counts.Count);
            foreach (var entry in counts.EntriesInOrder)
            {
                if (entry.Value == maxCount)
                {
                    result.Add(new KeyValuePair<object, int>(entry.Key, entry.Value));
                }
            }

            return result;
        }

        private static PyIteration.DrainLease GetModeValues(
            object[] arguments,
            string owner,
            bool allowEmpty,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            // N03: leased drain keeps input scratch charged beside the frequency table.
            var lease = PyIteration.DrainLeased(ToSequence(arguments[0], span, context), span, context);
            if (!allowEmpty && lease.Items.Count == 0)
            {
                lease.Dispose();
                throw StatisticsError($"{owner}(data) requires at least one data point.", span);
            }
            return lease;
        }

        private static async ValueTask<PyIteration.DrainLease> GetModeValuesAsync(
            object[] arguments,
            string owner,
            bool allowEmpty,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            var lease = await PyIteration.DrainLeasedAsync(ToSequenceAsync(arguments[0], span, context), span, context).ConfigureAwait(false);
            if (!allowEmpty && lease.Items.Count == 0)
            {
                lease.Dispose();
                throw StatisticsError($"{owner}(data) requires at least one data point.", span);
            }
            return lease;
        }

        private static PairedNumericValues GetPairedNumericValues(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two iterable arguments.", span);
            }

            // N07: both paired drains share one caller-scoped reservation.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var x = GetNumericValuesFromIterable(arguments[0], owner + "(x, y)", span, context, scratch);
            var y = GetNumericValuesFromIterable(arguments[1], owner + "(x, y)", span, context, scratch);
            if (x.Count != y.Count)
            {
                throw StatisticsError($"{owner.Split('.').Last()} requires that both inputs have same number of data points", span);
            }

            return new PairedNumericValues(x, y);
        }

        private static async ValueTask<PairedNumericValues> GetPairedNumericValuesAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two iterable arguments.", span);
            }

            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var x = await GetNumericValuesFromIterableAsync(arguments[0], owner + "(x, y)", span, context, scratch).ConfigureAwait(false);
            var y = await GetNumericValuesFromIterableAsync(arguments[1], owner + "(x, y)", span, context, scratch).ConfigureAwait(false);
            if (x.Count != y.Count)
            {
                throw StatisticsError($"{owner.Split('.').Last()} requires that both inputs have same number of data points", span);
            }

            return new PairedNumericValues(x, y);
        }

        private static CenteredSums ComputeCenteredSums(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            var xMean = x.Average();
            var yMean = y.Average();
            var sumXX = 0.0;
            var sumYY = 0.0;
            var sumXY = 0.0;
            for (var i = 0; i < x.Count; i++)
            {
                var dx = x[i] - xMean;
                var dy = y[i] - yMean;
                sumXX += dx * dx;
                sumYY += dy * dy;
                sumXY += dx * dy;
            }

            return new CenteredSums(sumXX, sumYY, sumXY);
        }

        private static double InterpolateInclusiveQuantile(IReadOnlyList<double> values, int cut, int partitions)
        {
            if (values.Count == 1)
            {
                return values[0];
            }

            var index = cut * (values.Count - 1.0) / partitions;
            var below = (int)Math.Floor(index);
            var above = Math.Min(below + 1, values.Count - 1);
            return values[below] + (values[above] - values[below]) * (index - below);
        }

        private static double InterpolateExclusiveQuantile(IReadOnlyList<double> values, int cut, int partitions)
        {
            if (values.Count == 1)
            {
                return values[0];
            }

            var position = cut * (values.Count + 1.0) / partitions;
            if (position <= 1.0)
            {
                return values[0] + (values[1] - values[0]) * (position - 1.0);
            }

            if (position >= values.Count)
            {
                var last = values.Count - 1;
                return values[last - 1] + (values[last] - values[last - 1]) * (position - (values.Count - 1));
            }

            var below = (int)Math.Floor(position);
            var fraction = position - below;
            return values[below - 1] + (values[below] - values[below - 1]) * fraction;
        }

        private static int ExpectPositivePartitionCount(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects an integer.", span);
            }

            if (integer < BigInteger.One || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects n >= 1.", span);
            }

            return (int)integer;
        }

        private static List<object> GetNumericObjects(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            // Same caller-scoped scratch shape as the doubles drain: the objects
            // list is required scratch for selection with no governed adopter.
            // Payloads stay owned elsewhere; only reference slots reserve here,
            // released on every exit path instead of stranding.
            var values = arguments[0] is IReadOnlyCollection<object> sized
                ? new List<object>(sized.Count)
                : new List<object>();
            var chargedCapacity = values.Capacity;
            if (chargedCapacity > 0)
            {
                scratch.Grow(8L * chargedCapacity, span);
            }

            foreach (var value in ToSequence(arguments[0], span, context))
            {
                if (values.Count == values.Capacity)
                {
                    var predicted = values.Capacity == 0 ? 4L : (long)values.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = (int)predicted;
                }

                values.Add(value);
                if (values.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (values.Capacity - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = values.Capacity;
                }

                context.ObserveCollectionCount(values.Count, span);
                if ((values.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner}(data) requires at least one data point.", span);
            }

            foreach (var value in values)
            {
                _ = ExpectRealForStatistics(value, owner, span);
            }

            return values;
        }

        private static async ValueTask<List<object>> GetNumericObjectsAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            var values = arguments[0] is IReadOnlyCollection<object> sized
                ? new List<object>(sized.Count)
                : new List<object>();
            var chargedCapacity = values.Capacity;
            if (chargedCapacity > 0)
            {
                scratch.Grow(8L * chargedCapacity, span);
            }

            await foreach (var value in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
            {
                if (values.Count == values.Capacity)
                {
                    var predicted = values.Capacity == 0 ? 4L : (long)values.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = (int)predicted;
                }

                values.Add(value);
                if (values.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (values.Capacity - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = values.Capacity;
                }

                context.ObserveCollectionCount(values.Count, span);
                if ((values.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner}(data) requires at least one data point.", span);
            }

            foreach (var value in values)
            {
                _ = ExpectRealForStatistics(value, owner, span);
            }

            return values;
        }

        private static List<double> GetNumericValuesFromData(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            if (arguments.Length > 1)
            {
            }

            return GetNumericValuesFromIterable(arguments[0], owner, span, context, scratch);
        }

        private static async ValueTask<List<double>> GetNumericValuesFromDataAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length < 1)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects one iterable argument.", span);
            }

            if (arguments.Length > 1)
            {
            }

            return await GetNumericValuesFromIterableAsync(arguments[0], owner, span, context, scratch).ConfigureAwait(false);
        }

        private static List<double> GetNumericValuesFromIterable(object data, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            // The doubles list is required scratch for sorting and multi-pass
            // statistics with no governed adopter, so growth reserves as
            // caller-scoped scratch: identical growth math, released on every
            // exit path instead of stranding. Sized inputs reserve the exact
            // backing once up front; the loop keeps a growth backstop for
            // sources whose count disagrees.
            var values = data is IReadOnlyCollection<object> sized
                ? new List<double>(sized.Count)
                : new List<double>();
            var chargedCapacity = values.Capacity;
            if (chargedCapacity > 0)
            {
                scratch.Grow(8L * chargedCapacity, span);
            }

            foreach (var value in ToSequence(data, span, context))
            {
                var real = ExpectRealForStatistics(value, owner, span);
                if (values.Count == values.Capacity)
                {
                    var predicted = values.Capacity == 0 ? 4L : (long)values.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = (int)predicted;
                }

                values.Add(real);
                if (values.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (values.Capacity - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = values.Capacity;
                }

                context.ObserveCollectionCount(values.Count, span);
                if ((values.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner} requires at least one data point.", span);
            }

            return values;
        }

        private static async ValueTask<List<double>> GetNumericValuesFromIterableAsync(object data, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var values = data is IReadOnlyCollection<object> sized
                ? new List<double>(sized.Count)
                : new List<double>();
            var chargedCapacity = values.Capacity;
            if (chargedCapacity > 0)
            {
                scratch.Grow(8L * chargedCapacity, span);
            }

            await foreach (var value in ToSequenceAsync(data, span, context).ConfigureAwait(false))
            {
                var real = ExpectRealForStatistics(value, owner, span);
                if (values.Count == values.Capacity)
                {
                    var predicted = values.Capacity == 0 ? 4L : (long)values.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = (int)predicted;
                }

                values.Add(real);
                if (values.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (values.Capacity - chargedCapacity));
                    scratch.Grow(delta, span);
                    chargedCapacity = values.Capacity;
                }

                context.ObserveCollectionCount(values.Count, span);
                if ((values.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner} requires at least one data point.", span);
            }

            return values;
        }

    }
}
