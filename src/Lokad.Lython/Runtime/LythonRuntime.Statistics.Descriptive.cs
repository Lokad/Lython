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

        // Error-free partials serve checked floating sums and exact mean/product
        // accumulation. Mean periodically folds into an exact binary total and
        // switches to exact additions for huge magnitudes; fsum checks overflow
        // at each partial addition, before any later input can cancel it.
        private struct ExactDoubleSum
        {
            // Shewchuk non-overlapping partials for streaming inputs (CPython msum order).
            private List<double>? _partials;
            // Exact folded total of streaming inputs, scaled by 2**1074.
            private BigInteger _grandTotal;
            private int _sinceFold;
            private bool _useBigInteger;
            private bool _hasPositiveInfinity;
            private bool _hasNegativeInfinity;
            private bool _hasNaN;

            public void AddChecked(double value, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch)
            {
                if (!double.IsFinite(value))
                {
                    if (double.IsNaN(value)) _hasNaN = true;
                    else if (value > 0) _hasPositiveInfinity = true;
                    else _hasNegativeInfinity = true;
                    _partials?.Clear();
                    return;
                }
                _partials ??= CreateNumericBuffer<double>(0, scratch, span!);
                MsumAdd(value, span, scratch, checkedOverflow: true);
            }

            // Streaming add for mean: Shewchuk partials exactly like CPython's msum inner
            // loop (double ops only, folded exactly below), migrating to direct exact adds
            // past 2**1000 magnitudes where a partial could round to infinity.
            public void AddMean(double value, LythonSourceSpan? span = null, MemoryGovernor.TemporaryMemoryReservation? scratch = null)
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

                _partials ??= scratch is null ? new List<double>() : CreateNumericBuffer<double>(0, scratch, span!);
                if (Math.Abs(value) >= HugeMagnitudeThreshold || HasHugePartial())
                {
                    FoldMean();
                    _useBigInteger = true;
                    _grandTotal += ScaleDouble(value);
                    return;
                }

                MsumAdd(value, span, scratch);
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
            private void MsumAdd(double x, LythonSourceSpan? span = null, MemoryGovernor.TemporaryMemoryReservation? scratch = null, bool checkedOverflow = false)
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
                    if (checkedOverflow && double.IsInfinity(hi))
                        throw new LythonRuntimeException("OverflowError", "intermediate overflow in fsum", span);
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
                    if (scratch is not null) GrowNumericBuffer(partials, scratch, span!);
                    partials.Add(x);
                }
            }

            public double TotalFSum(LythonSourceSpan span)
            {
                if (_hasPositiveInfinity && _hasNegativeInfinity)
                    throw new LythonRuntimeException("ValueError", "-inf + inf in fsum", span);
                if (_hasNaN) return double.NaN;
                if (_hasPositiveInfinity) return double.PositiveInfinity;
                if (_hasNegativeInfinity) return double.NegativeInfinity;
                var total = BigInteger.Zero;
                if (_partials is not null)
                    foreach (var partial in _partials) total += ScaleDouble(partial);
                var result = DivideExact(total, 1);
                if (double.IsInfinity(result))
                    throw new LythonRuntimeException("OverflowError", "intermediate overflow in fsum", span);
                return result;
            }

            public void AddProduct(double left, double right, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch)
            {
                var product = left * right;
                AddMean(product, span, scratch);
                if (double.IsFinite(product))
                    AddMean(Math.FusedMultiplyAdd(left, right, -product), span, scratch);
            }

            public double TotalProduct()
            {
                if (_hasNaN || (_hasPositiveInfinity && _hasNegativeInfinity)) return double.NaN;
                if (_hasPositiveInfinity) return double.PositiveInfinity;
                if (_hasNegativeInfinity) return double.NegativeInfinity;
                FoldMean();
                return DivideExact(_grandTotal, 1);
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
                context.CheckExecution(span);
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
                        return new PyDecimal(PyDecimalOps.StripTrailingZeros((_decimalTotal + (decimal)_intTotal) / _count));
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
            => FMeanCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> FMeanAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => FMeanCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> FMeanCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            if (arguments.Length < 2 || arguments[1] is PyNone)
            {
                BigInteger? knownCount = null;
                try
                {
                    knownCount = (BigInteger)(asynchronous
                        ? await LenAsync([arguments[0]], span, context).ConfigureAwait(false)
                        : GetLength(arguments[0], span, context));
                }
                catch (LythonRuntimeException error) when (error.ExceptionType == "TypeError") { context.CheckExecution(span); }
                long count = 0;
                var sum = new ExactDoubleSum();
                if (asynchronous)
                {
                    await foreach (var value in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false)) AddValue(value);
                }
                else
                {
                    foreach (var value in ToSequence(arguments[0], span, context)) AddValue(value);
                }
                var total = sum.TotalFSum(span);
                var denominator = knownCount ?? count;
                if (denominator.IsZero) throw StatisticsError("fmean requires at least one data point", span);
                return total / (double)denominator;

                void AddValue(object value)
                {
                    context.CheckExecution(span);
                    sum.AddChecked(ExpectRealForStatistics(value, "statistics.fmean", span), span, scratch);
                    count++;
                }
            }

            // Python prepares weights before consuming data. Retain their original
            // values so conversion/denominator errors occur after the paired pass.
            using var weights = asynchronous
                ? await PyIteration.DrainLeasedAsync(ToSequenceAsync(arguments[1], span, context), span, context).ConfigureAwait(false)
                : PyIteration.DrainLeased(ToSequence(arguments[1], span, context), span, context);
            var products = new ExactDoubleSum();
            var index = 0;
            try
            {
                if (asynchronous)
                {
                    await foreach (var value in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false)) AddPair(value);
                }
                else
                {
                    foreach (var value in ToSequence(arguments[0], span, context)) AddPair(value);
                }
                if (index != weights.Items.Count) throw StatisticsError("data and weights must be the same length", span);
            }
            catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("ValueError"), error.Identity))
            {
                context.CheckExecution(span);
                throw StatisticsError("data and weights must be the same length", span);
            }
            var numerator = products.TotalProduct();
            var weightSum = new ExactDoubleSum();
            foreach (var weight in weights.Items)
            {
                context.CheckExecution(span);
                weightSum.AddChecked(ExpectRealForStatistics(weight, "statistics.fmean", span), span, scratch);
            }
            var denominatorSum = weightSum.TotalFSum(span);
            if (denominatorSum == 0.0) throw StatisticsError("sum of weights must be non-zero", span);
            return numerator / denominatorSum;

            void AddPair(object value)
            {
                context.CheckExecution(span);
                if (index == weights.Items.Count) throw StatisticsError("data and weights must be the same length", span);
                products.AddProduct(ExpectRealForStatistics(value, "statistics.fmean", span),
                    ExpectRealForStatistics(weights.Items[index++], "statistics.fmean", span), span, scratch);
            }
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
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
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
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
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
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
            return values[(values.Count - 1) / 2];
        }

        private static async ValueTask<object> MedianLowAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericObjectsAsync(arguments, "statistics.median_low", span, context, scratch).ConfigureAwait(false);
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
            return values[(values.Count - 1) / 2];
        }

        private static object MedianHigh(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // N07: same caller-scoped lifetime as MedianLow.
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericObjects(arguments, "statistics.median_high", span, context, scratch);
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
            return values[values.Count / 2];
        }

        private static async ValueTask<object> MedianHighAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = await GetNumericObjectsAsync(arguments, "statistics.median_high", span, context, scratch).ConfigureAwait(false);
            ExecutionSort.Sort(values, context, span, (left, right) => Compare(left, right, span));
            return values[values.Count / 2];
        }

        private static object Mode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = GetModeValues(arguments, "statistics.mode", allowEmpty: false, span, context);
            var values = modeLease.Items;
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            return GetModeCounts(values, scratch, span, context)[0].Key;
        }

        private static async ValueTask<object> ModeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = await GetModeValuesAsync(arguments, "statistics.mode", allowEmpty: false, span, context).ConfigureAwait(false);
            var values = modeLease.Items;
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            return GetModeCounts(values, scratch, span, context)[0].Key;
        }

        private static object MultiMode(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = GetModeValues(arguments, "statistics.multimode", allowEmpty: true, span, context);
            var values = modeLease.Items;
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var counts = GetModeCounts(values, scratch, span, context);
            var modes = new PyList(counts.Count, context.MemoryGovernor, span);
            for (var i = 0; i < counts.Count; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                modes.Add(counts[i].Key);
            }

            return modes;
        }

        private static async ValueTask<object> MultiModeAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var modeLease = await GetModeValuesAsync(arguments, "statistics.multimode", allowEmpty: true, span, context).ConfigureAwait(false);
            var values = modeLease.Items;
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var counts = GetModeCounts(values, scratch, span, context);
            var modes = new PyList(counts.Count, context.MemoryGovernor, span);
            for (var i = 0; i < counts.Count; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                modes.Add(counts[i].Key);
            }

            return modes;
        }

        private static object MedianGrouped(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = GetNumericValuesFromData(arguments, "statistics.median_grouped", span, context, scratch);
            ExecutionSort.Sort(values, context, span);
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
            ExecutionSort.Sort(values, context, span);
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

        private static object PopulationStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.pstdev", span, context, false, true, false).GetAwaiter().GetResult();

        private static ValueTask<object> PopulationStdevAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.pstdev", span, context, false, true, true);

        private static object SampleStdev(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.stdev", span, context, true, true, false).GetAwaiter().GetResult();

        private static ValueTask<object> SampleStdevAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.stdev", span, context, true, true, true);

        private static object PopulationVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.pvariance", span, context, false, false, false).GetAwaiter().GetResult();

        private static ValueTask<object> PopulationVarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.pvariance", span, context, false, false, true);

        private static object SampleVariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.variance", span, context, true, false, false).GetAwaiter().GetResult();

        private static ValueTask<object> SampleVarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => DispersionAsync(arguments, "statistics.variance", span, context, true, false, true);

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
                context.CheckExecution(span);
                var single = new PyList(n - 1, context.MemoryGovernor, span);
                for (var i = 1; i < n; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    single.Add(originals[0]);
                }

                return single;
            }
            // Cut points interpolate over doubles (true-division floats like
            // CPython); the originals above only serve the single-point case.
            var values = CreateNumericBuffer<double>(originals.Count, scratch, span);
            foreach (var original in originals)
            {
                values.Add(ExpectRealForStatistics(original, "statistics.quantiles", span));
            }

            ExecutionSort.Sort(values, context, span);
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

            // The final container admits its capacity before allocation.
            var outputCount = n - 1;
            context.CheckExecution(span);
            var cutPoints = new PyList(outputCount, context.MemoryGovernor, span);
            var count = values.Count;
            for (var i = 1; i < n; i++)
            {
                var value = method == "inclusive"
                    ? InterpolateInclusiveQuantile(values, i, n)
                    : InterpolateExclusiveQuantile(values, i, n);
                cutPoints.Add(value);
                if ((i & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }

            return cutPoints;
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
                context.CheckExecution(span);
                var single = new PyList(n - 1, context.MemoryGovernor, span);
                for (var i = 1; i < n; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    single.Add(originals[0]);
                }

                return single;
            }
            // Cut points interpolate over doubles (true-division floats like
            // CPython); the originals above only serve the single-point case.
            var values = CreateNumericBuffer<double>(originals.Count, scratch, span);
            foreach (var original in originals)
            {
                values.Add(ExpectRealForStatistics(original, "statistics.quantiles", span));
            }

            ExecutionSort.Sort(values, context, span);
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
            context.CheckExecution(span);
            var cutPoints = new PyList(outputCount, context.MemoryGovernor, span);
            var count = values.Count;
            for (var i = 1; i < n; i++)
            {
                var value = method == "inclusive"
                    ? InterpolateInclusiveQuantile(values, i, n)
                    : InterpolateExclusiveQuantile(values, i, n);
                cutPoints.Add(value);
                if ((i & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }

            return cutPoints;
        }

        private static object Covariance(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => PairedStatisticAsync(arguments, span, context, false, correlation: false).GetAwaiter().GetResult();

        private static ValueTask<object> CovarianceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => PairedStatisticAsync(arguments, span, context, true, correlation: false);

        private static object Correlation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => PairedStatisticAsync(arguments, span, context, false, correlation: true).GetAwaiter().GetResult();

        private static ValueTask<object> CorrelationAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => PairedStatisticAsync(arguments, span, context, true, correlation: true);

        private static async ValueTask<object> PairedStatisticAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous, bool correlation)
        {
            var owner = correlation ? "statistics.correlation" : "statistics.covariance";
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var (x, y) = asynchronous
                ? await GetPairedNumericValuesAsync(arguments, owner, span, context, scratch).ConfigureAwait(false)
                : GetPairedNumericValues(arguments, owner, span, context, scratch);
            if (x.Count < 2) throw StatisticsError(owner + " requires at least two data points", span);
            var sums = ComputeCenteredSums(x, y, context, span, scratch, includeNorms: correlation);
            if (!correlation) return sums.SumXY / (x.Count - 1);
            if (sums.SumXX == 0.0 || sums.SumYY == 0.0)
                throw StatisticsError("at least one of the inputs is constant", span);
            return sums.SumXY / SqrtProduct(sums.SumXX, sums.SumYY);
        }

        private static object LinearRegression(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => LinearRegressionCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> LinearRegressionAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => LinearRegressionCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> LinearRegressionCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            BigInteger? originalCount = null;
            try
            {
                var nx = (BigInteger)(asynchronous ? await LenAsync([arguments[0]], span, context).ConfigureAwait(false) : GetLength(arguments[0], span, context));
                var ny = (BigInteger)(asynchronous ? await LenAsync([arguments[1]], span, context).ConfigureAwait(false) : GetLength(arguments[1], span, context));
                if (nx != ny) throw StatisticsError("linear_regression requires that both inputs have same number of data points", span);
                if (nx < 2) throw StatisticsError("linear_regression requires at least two data points", span);
                originalCount = nx;
            }
            catch (LythonRuntimeException error) when (error.ExceptionType == "TypeError")
            {
                // Retain Lython's existing general-iterable extension.
                context.CheckExecution(span);
            }
            var proportional = arguments.Length >= 3 ? arguments[2] : false;
            var first = asynchronous
                ? await IsTruthyAsync(proportional, context, span).ConfigureAwait(false)
                : IsTruthy(proportional, context, span);
            var x = asynchronous
                ? await GetNumericValuesFromIterableAsync(arguments[0], "statistics.linear_regression", span, context, scratch).ConfigureAwait(false)
                : GetNumericValuesFromIterable(arguments[0], "statistics.linear_regression", span, context, scratch);
            var y = asynchronous
                ? await GetNumericValuesFromIterableAsync(arguments[1], "statistics.linear_regression", span, context, scratch).ConfigureAwait(false)
                : GetNumericValuesFromIterable(arguments[1], "statistics.linear_regression", span, context, scratch);
            if (x.Count != y.Count) throw new LythonRuntimeException("ValueError", "Inputs are not the same length", span);
            if (originalCount is null && x.Count < 2) throw StatisticsError("linear_regression requires at least two data points", span);
            var xMean = 0.0;
            var yMean = 0.0;
            if (!first)
            {
                xMean = CheckedAverage(x, context, span, scratch, originalCount);
                yMean = CheckedAverage(y, context, span, scratch, originalCount);
                for (var i = 0; i < x.Count; i++)
                {
                    context.CheckExecution(span);
                    x[i] -= xMean;
                    y[i] -= yMean;
                }
            }
            var xy = new ExactDoubleSum();
            var xx = new ExactDoubleSum();
            for (var i = 0; i < x.Count; i++)
            {
                context.CheckExecution(span);
                xy.AddProduct(x[i], y[i], span, scratch);
                xx.AddProduct(x[i], x[i], span, scratch);
            }
            var divisor = xx.TotalProduct();
            if (divisor == 0.0) throw StatisticsError("x is constant", span);
            var slope = xy.TotalProduct() / divisor;
            var second = asynchronous
                ? await IsTruthyAsync(proportional, context, span).ConfigureAwait(false)
                : IsTruthy(proportional, context, span);
            if (!second && first)
                throw new LythonRuntimeException("UnboundLocalError", "cannot access local variable 'ybar' where it is not associated with a value", span);
            return OwnRegressionResult(slope, second ? 0.0 : yMean - slope * xMean, context, span);
        }

        private static PyNamedTupleObject OwnRegressionResult(object slope, object intercept, ExecutionContext context, LythonSourceSpan span)
            => context.State.StatisticsRegressionType.RequireNotNull().CreateFromValues([slope, intercept], context.MemoryGovernor, span);

        private static List<KeyValuePair<object, int>> GetModeCounts(IReadOnlyList<object> values, MemoryGovernor.TemporaryMemoryReservation scratch, LythonSourceSpan span, ExecutionContext context)
        {
            if (values.Count == 0)
            {
                return [];
            }

            // N12: contextual frequency table (guest __hash__/__eq__, first-seen order
            // decides ties). Same caller-lifetime scratch bound as before, plus work checks.
            // Includes the table, buckets, order/result lists and replacement headroom.
            scratch.Grow(144L + (192L * values.Count), span);
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
                    context.CheckExecution(span);
                }
            }

            var maxCount = 0;
            foreach (var entry in counts.EntriesInOrder)
            {
                context.CheckExecution(span);
                maxCount = Math.Max(maxCount, entry.Value);
            }

            var result = new List<KeyValuePair<object, int>>(counts.Count);
            foreach (var entry in counts.EntriesInOrder)
            {
                context.CheckExecution(span);
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

        private static PairedNumericValues GetPairedNumericValues(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two iterable arguments.", span);
            }

            var x = GetNumericValuesFromIterable(arguments[0], owner + "(x, y)", span, context, scratch);
            var y = GetNumericValuesFromIterable(arguments[1], owner + "(x, y)", span, context, scratch);
            if (x.Count != y.Count)
            {
                throw StatisticsError($"{owner.Split('.').Last()} requires that both inputs have same number of data points", span);
            }

            return new PairedNumericValues(x, y);
        }

        private static async ValueTask<PairedNumericValues> GetPairedNumericValuesAsync(object[] arguments, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(x, y) expects two iterable arguments.", span);
            }

            var x = await GetNumericValuesFromIterableAsync(arguments[0], owner + "(x, y)", span, context, scratch).ConfigureAwait(false);
            var y = await GetNumericValuesFromIterableAsync(arguments[1], owner + "(x, y)", span, context, scratch).ConfigureAwait(false);
            if (x.Count != y.Count)
            {
                throw StatisticsError($"{owner.Split('.').Last()} requires that both inputs have same number of data points", span);
            }

            return new PairedNumericValues(x, y);
        }

        private static double CheckedAverage(IReadOnlyList<double> values, ExecutionContext context,
            LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch, BigInteger? originalCount = null)
        {
            var sum = new ExactDoubleSum();
            foreach (var value in values)
            {
                context.CheckExecution(span);
                sum.AddChecked(value, span, scratch);
            }
            return sum.TotalFSum(span) / (double)(originalCount ?? values.Count);
        }

        private static CenteredSums ComputeCenteredSums(IReadOnlyList<double> x, IReadOnlyList<double> y,
            ExecutionContext context, LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation scratch, bool includeNorms)
        {
            var xMean = CheckedAverage(x, context, span, scratch);
            var yMean = CheckedAverage(y, context, span, scratch);
            var xx = new ExactDoubleSum();
            var yy = new ExactDoubleSum();
            var xy = new ExactDoubleSum();
            for (var i = 0; i < x.Count; i++)
            {
                context.CheckExecution(span);
                var dx = x[i] - xMean;
                var dy = y[i] - yMean;
                if (includeNorms)
                {
                    xx.AddProduct(dx, dx, span, scratch);
                    yy.AddProduct(dy, dy, span, scratch);
                }
                xy.AddProduct(dx, dy, span, scratch);
            }
            return new CenteredSums(xx.TotalProduct(), yy.TotalProduct(), xy.TotalProduct());
        }

        private static double SqrtProduct(double x, double y)
        {
            var result = Math.Sqrt(x * y);
            if (double.IsInfinity(result) && double.IsFinite(x) && double.IsFinite(y))
            {
                var scale = Math.ScaleB(1.0, -512);
                return SqrtProduct(x * scale, y * scale) / scale;
            }
            if (result == 0.0 && x != 0.0 && y != 0.0)
            {
                var scale = Math.ScaleB(1.0, 537);
                return SqrtProduct(x * scale, y * scale) / scale;
            }
            if (!double.IsFinite(result) || result == 0.0) return result;
            var squared = result * result;
            var error = Math.FusedMultiplyAdd(x, y, -squared) - Math.FusedMultiplyAdd(result, result, -squared);
            return result + error / (2 * result);
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

        // These buffers contain only eight-byte object slots or doubles. Their
        // caller retains the reservation until every consumer has finished.
        private static List<T> CreateNumericBuffer<T>(int capacity,
            MemoryGovernor.TemporaryMemoryReservation scratch, LythonSourceSpan span)
        {
            scratch.Grow(32L + (capacity > 0 ? 32L + 8L * capacity : 0), span);
            return new List<T>(capacity);
        }

        private static void GrowNumericBuffer<T>(List<T> values,
            MemoryGovernor.TemporaryMemoryReservation scratch, LythonSourceSpan span)
        {
            if (values.Count != values.Capacity) return;
            var previous = values.Capacity;
            var capacity = (int)Math.Min(Array.MaxLength, previous == 0 ? 4L : 2L * previous);
            if (capacity <= previous)
                throw new LythonRuntimeException("OverflowError", "statistics input exceeds the supported buffer size", span);
            scratch.Grow(32L + 8L * capacity, span);
            values.Capacity = capacity;
            if (previous > 0) scratch.Shrink(32L + 8L * previous);
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
            var values = CreateNumericBuffer<object>(arguments[0] is IReadOnlyCollection<object> sized ? sized.Count : 0, scratch, span);

            foreach (var value in ToSequence(arguments[0], span, context))
            {
                GrowNumericBuffer(values, scratch, span);
                values.Add(value);

                context.CheckExecution(span);
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

            var values = CreateNumericBuffer<object>(arguments[0] is IReadOnlyCollection<object> sized ? sized.Count : 0, scratch, span);

            await foreach (var value in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
            {
                GrowNumericBuffer(values, scratch, span);
                values.Add(value);

                context.CheckExecution(span);
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
            // sources whose count disagrees; replacement capacity is admitted
            // alongside the old backing before resizing.
            var values = CreateNumericBuffer<double>(data is IReadOnlyCollection<object> sized ? sized.Count : 0, scratch, span);

            foreach (var value in ToSequence(data, span, context))
            {
                var real = ExpectRealForStatistics(value, owner, span);
                GrowNumericBuffer(values, scratch, span);
                values.Add(real);

                context.CheckExecution(span);
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner} requires at least one data point.", span);
            }

            return values;
        }

        private static async ValueTask<List<double>> GetNumericValuesFromIterableAsync(object data, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var values = CreateNumericBuffer<double>(data is IReadOnlyCollection<object> sized ? sized.Count : 0, scratch, span);

            await foreach (var value in ToSequenceAsync(data, span, context).ConfigureAwait(false))
            {
                var real = ExpectRealForStatistics(value, owner, span);
                GrowNumericBuffer(values, scratch, span);
                values.Add(real);

                context.CheckExecution(span);
            }

            if (values.Count == 0)
            {
                throw StatisticsError($"{owner} requires at least one data point.", span);
            }

            return values;
        }

    }
}
