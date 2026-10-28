using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        // Integer arguments coerce through __index__ like CPython; hooks run
        // with full dispatch while plain non-integers name the type.
        private static BigInteger CoerceRandomIndex(object value, ExecutionContext context, LythonSourceSpan span)
            => RequireRandomIndex(CoerceIndexProtocol(value, context, span), value, context, span);

        private static async ValueTask<BigInteger> CoerceRandomIndexAsync(object value, ExecutionContext context, LythonSourceSpan span)
            => RequireRandomIndex(await ConvertIntegerIndexAsync(value, context, span).ConfigureAwait(false), value, context, span);

        private static BigInteger RequireRandomIndex(object coerced, object value, ExecutionContext context, LythonSourceSpan span)
        {
            if (PyNumberOps.TryAsInteger(coerced, out var integer))
            {
                return integer;
            }

            throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        private static double ExpectPositiveReal(object value, string owner, LythonSourceSpan span)
        {
            var real = RuntimeArgumentValidation.ExpectReal(value, owner, span);
            if (real <= 0.0 || double.IsNaN(real) || double.IsInfinity(real))
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects a positive finite number.", span);
            }

            return real;
        }

        private static double[] ReadWeights(object value, long expectedCount, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var values = MaterializeSequence(value, span, context, scratch);
            if (values.Count != expectedCount)
            {
                throw new LythonRuntimeException("ValueError", "The number of weights does not match the population", span);
            }

            // The converted copy coexists with the drained values, so it
            // reserves its exact backing as caller-scoped scratch: it lives
            // for the selection and releases on every exit path.
            if (values.Count > 0)
            {
                scratch.Grow(32L + (8L * values.Count), span);
            }
            var result = new double[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                var weight = RuntimeArgumentValidation.ExpectReal(values[i], owner, span);
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects finite non-negative weights.", span);
                }

                result[i] = weight;
            }

            return result;
        }

        private static double[] ReadCumulativeWeights(object value, long expectedCount, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var values = ReadWeights(value, expectedCount, owner, span, context, scratch);
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects monotonically increasing cumulative weights.", span);
                }
            }

            return values;
        }

        private static object SampleCountedPositions(
            Func<long, object> getAt,
            long poolCount,
            object countsValue,
            object rawCount,
            PyRandomState state,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            // Borrow native indexed counts; only general iterables need a
            // preflighted scratch copy. Neither path expands the multiset.
            var governor = context.MemoryGovernor;
            using var countsScratch = governor.ReserveTemporary(0, span);
            var borrowed = countsValue as IPyIndexableValue;
            var materialized = borrowed is null ? MaterializeSequence(countsValue, span, context, countsScratch) : null;
            var countsLength = borrowed?.Length ?? materialized!.Count;
            if (countsLength != poolCount)
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);

            using var cumulativeScratch = countsLength == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(24L + (8L * countsLength), span);
            var cumulative = countsLength == 0 ? [] : new long[countsLength];

            var total = 0L;
            for (var i = 0; i < countsLength; i++)
            {
                var single = RuntimeArgumentValidation.ExpectInteger(borrowed is not null ? borrowed.GetIndex(i) : materialized![i], "random.sample(..., counts=...) expects integer counts.", span);
                if (single < BigInteger.Zero)
                {
                    throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects non-negative counts.", span);
                }

                if (single > long.MaxValue - total)
                    throw new LythonRuntimeException("OverflowError", "random.sample(..., counts=...) population is too large.", span);
                total += (long)single;
                context.CheckExecution(span);
                cumulative[i] = total;
            }

            if (countsLength == 0)
                throw new LythonRuntimeException("IndexError", "pop from empty list", span);
            if (total <= 0)
                throw new LythonRuntimeException("ValueError", "Total of counts must be greater than zero", span);
            // Like CPython, k validates against the expanded total through
            // operator dispatch before the multiply gate sizes the draw.
            if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.Greater, rawCount, new BigInteger(total), context, span), context, span))
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative", span);
            }

            var count = CoerceSampleCount(rawCount, span);
            using var selectedScratch = count == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(80L + (40L * count), span);
            var selected = count == 0 ? new HashSet<long>() : new HashSet<long>(count);

            var result = new object[count];
            for (var drawn = 0; drawn < count; drawn++)
            {
                var resume = total - count + drawn;
                var draw = (long)state.NextBelow((ulong)(resume + 1), context, span);
                var picked = draw;
                if (!selected.Add(draw))
                {
                    // Floyd collision: the resume slot is the fresh position,
                    // so it joins the set and maps to the output. Mapping the
                    // original draw instead could repeat an expanded position.
                    picked = resume;
                    selected.Add(picked);
                }

                result[drawn] = RuntimeValue(getAt(MapCountedPosition(cumulative, picked)));
                if (((drawn + 1) & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }

            return new PyList(result, governor, span);
        }

        // Maps an expanded position to its pool: the first cumulative
        // bound above it. Plateaus from zero counts resolve to the pool
        // after them, matching the expanded layout.
        private static int MapCountedPosition(long[] cumulative, long position)
        {
            var lo = 0;
            var hi = cumulative.Length - 1;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (cumulative[mid] > position)
                {
                    hi = mid;
                }
                else
                {
                    lo = mid + 1;
                }
            }

            return lo;
        }

        private static List<object> MaterializeSequence(object value, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            // Drains stay funded for their caller's lifetime, including validation
            // failure. Payloads remain owned elsewhere; scratch never commits.
            var sizedCount = value switch
            {
                object[] array => array.Length,
                PyList list => list.Count,
                PyTuple tuple => tuple.Count,
                ICollection<object> collection => collection.Count,
                _ => (int?)null,
            };
            scratch.Grow(32L + (sizedCount > 0 ? 32L + 8L * sizedCount.Value : 0), span);
            var result = sizedCount.HasValue ? new List<object>(sizedCount.Value) : new List<object>();

            foreach (var item in ToSequence(value, span, context))
            {
                if (result.Count == result.Capacity)
                {
                    var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                    var previous = result.Capacity;
                    scratch.Grow(checked(32L + 8L * predicted), span);
                    result.Capacity = checked((int)predicted);
                    if (previous > 0) scratch.Shrink(32L + 8L * previous);
                }

                result.Add(RuntimeValue(item));
                context.CheckExecution(span);
            }

            return result;
        }

        private static double ValidateWeightTotal(double[]? weights, double[]? cumulative, ExecutionContext context, LythonSourceSpan span)
        {
            if (weights is null && cumulative is null) return 0;
            if ((weights?.Length ?? cumulative!.Length) == 0)
                throw new LythonRuntimeException("IndexError", "list index out of range", span);
            double total = 0;
            if (weights is not null)
            {
                for (var i = 0; i < weights.Length; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    total += weights[i];
                }
            }
            else total = cumulative![^1];
            if (total <= 0 || !double.IsFinite(total))
                throw new LythonRuntimeException("ValueError", "Total of weights must be greater than zero and finite", span);
            return total;
        }

        private static long ChooseWeightedIndex(PyRandomState state, long populationLength, double[]? weights,
            double[]? cumulative, double total, ExecutionContext context, LythonSourceSpan span)
        {
            if (weights is null && cumulative is null)
                return (long)state.NextBelow((ulong)populationLength, context, span);
            var threshold = state.NextDouble() * total;
            var length = weights?.Length ?? cumulative!.Length;
            double running = 0;
            for (var i = 0; i < length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                running = weights is not null ? running + weights[i] : cumulative![i];
                if (threshold < running) return i;
            }
            return length - 1;
        }

        private static void ShuffleMaterialized(PyRandomState state, IList<object> items, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            for (var i = items.Count - 1; i > 0; i--)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var j = (int)state.NextBelow((ulong)(i + 1), context, span);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        // Virtual ranges stay indexed. Only Python's actual length
        // representation constrains the population; output storage is separate.
        private static (long Length, Func<long, object> GetAt) GetPopulation(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (value is IPyIndexableValue indexable)
                return (indexable.Length, index => indexable.GetIndex(checked((int)index)));
            if (value is PyRange range)
            {
                var length = range.Length;
                if (length > long.MaxValue)
                    throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
                return ((long)length, index => range.GetSubscript(new BigInteger(index), span));
            }
            throw new LythonRuntimeException("TypeError", $"{owner} population must be a sequence.", span);
        }

        // Floyd positions over an indexable population: k draws and O(k)
        // state instead of a full drain and shuffle. Uniform over k-subsets;
        // larger takes keep the legacy shuffle path and its exact draws.
        private static object SampleDirectPositions(PyRandomState state, Func<long, object> getAt, long length, int count, MemoryGovernor governor, LythonSourceSpan span, ExecutionContext context)
        {
            using var selectedScratch = count == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(80L + (40L * count), span);
            var selected = count == 0 ? new HashSet<long>() : new HashSet<long>(count);
            var result = new object[count];
            for (var drawn = 0; drawn < count; drawn++)
            {
                var resume = length - count + drawn;
                var draw = (long)state.NextBelow((ulong)(resume + 1), context, span);
                var picked = draw;
                if (!selected.Add(draw))
                {
                    picked = resume;
                    selected.Add(picked);
                }

                result[drawn] = RuntimeValue(getAt(picked));
                if (((drawn + 1) & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }

            return new PyList(result, governor, span);
        }

        private static double SampleGamma(PyRandomState state, double alpha, double beta, ExecutionContext context, LythonSourceSpan span)
        {
            if (alpha < 1.0)
            {
                return SampleGamma(state, alpha + 1.0, beta, context, span) * Math.Pow(NonZeroRandom(state, context, span), 1.0 / alpha);
            }

            var d = alpha - 1.0 / 3.0;
            var c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                context.CheckExecution(span);
                var x = StandardNormal(state, context, span);
                var v = 1.0 + c * x;
                if (v <= 0.0)
                {
                    continue;
                }

                v *= v * v;
                var u = state.NextDouble();
                if (u < 1.0 - 0.0331 * x * x * x * x ||
                    Math.Log(u) < 0.5 * x * x + d * (1.0 - v + Math.Log(v)))
                {
                    return beta * d * v;
                }
            }
        }

        private static double StandardNormal(PyRandomState state, ExecutionContext context, LythonSourceSpan span)
        {
            var u1 = NonZeroRandom(state, context, span);
            var u2 = state.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(TwoPi * u2);
        }

        private static double NonZeroRandom(PyRandomState state, ExecutionContext context, LythonSourceSpan span)
        {
            double value;
            do
            {
                context.CheckExecution(span);
                value = state.NextDouble();
            }
            while (value <= 0.0);

            return value;
        }

        private static double ModTwoPi(double value)
        {
            var result = value % TwoPi;
            return result < 0.0 ? result + TwoPi : result;
        }
    }
}
