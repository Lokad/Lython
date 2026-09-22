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
        {
            var coerced = CoerceIndexProtocol(value, context, span);
            if (coerced is bool flag)
            {
                return flag ? BigInteger.One : BigInteger.Zero;
            }

            if (coerced is int small)
            {
                return new BigInteger(small);
            }

            if (coerced is BigInteger integer)
            {
                return integer;
            }

            throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
        }

        private static BigInteger ComputeRangeCount(BigInteger start, BigInteger stop, BigInteger step, string emptyMessage, LythonSourceSpan span)
        {
            if (step == BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", "zero step for randrange()", span);
            }

            if (step > BigInteger.Zero)
            {
                if (stop <= start)
                {
                    throw new LythonRuntimeException("ValueError", emptyMessage, span);
                }

                return ((stop - start - BigInteger.One) / step) + BigInteger.One;
            }

            if (stop >= start)
            {
                throw new LythonRuntimeException("ValueError", emptyMessage, span);
            }

            var magnitude = BigInteger.Abs(step);
            return ((start - stop - BigInteger.One) / magnitude) + BigInteger.One;
        }

        private static ulong ToBound(BigInteger count, string owner, LythonSourceSpan span)
        {
            if (count <= 0 || count > ulong.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", $"{owner}(...) range is too large.", span);
            }

            return (ulong)count;
        }

        private static int ExpectNonNegativeInt(object value, string message, LythonSourceSpan span)
        {
            var integer = RuntimeArgumentValidation.ExpectInteger(value, message, span);
            if (integer < 0 || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return (int)integer;
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

        private static double[] ReadWeights(object value, int expectedCount, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
        {
            var values = MaterializeSequence(value, span, context);
            if (values.Count != expectedCount)
            {
                throw new LythonRuntimeException("ValueError", "The number of weights does not match the population", span);
            }

            // The converted copy coexists with the drained values, so it
            // reserves its exact backing as caller-scoped scratch: it lives
            // for the selection and releases on every exit path.
            var result = new double[values.Count];
            if (values.Count > 0)
            {
                scratch.Grow(32L + (8L * values.Count), span);
            }
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

        private static double[] ReadCumulativeWeights(object value, int expectedCount, string owner, LythonSourceSpan span, ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation scratch)
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
            Func<int, object> getAt,
            int poolCount,
            object countsValue,
            object rawCount,
            PyRandomState state,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            // Counted sampling without expanding: drain the counts once, map
            // them to cumulative bounds, draw k distinct expanded positions
            // with Floyd's algorithm (exactly k draws, O(k) state), and map
            // each position back to its pool by index. Sized populations
            // serve picks with no drain; only lazy ones arrive materialized.
            // Uniform over the expanded
            // multiset like shuffling the expansion, but the live structures
            // scale with pools plus picks instead of the expanded total.
            var governor = context.MemoryGovernor;
            var sizedCount = countsValue switch
            {
                object[] array => array.Length,
                PyList list => list.Count,
                PyTuple tuple => tuple.Count,
                ICollection<object> collection => collection.Count,
                _ => (int?)null,
            };
            if (sizedCount.HasValue && sizedCount.Value != poolCount)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            // N07: the drained counts are call-scoped scratch: growth
            // reserves exactly like the durable path did, then releases on
            // every exit path instead of stranding.
            using var countsScratch = governor.ReserveTemporary(0, span);
            var counts = sizedCount.HasValue ? new List<object>(sizedCount.Value) : new List<object>();
            var chargedCapacity = counts.Capacity;
            if (chargedCapacity > 0)
            {
                countsScratch.Grow(8L * chargedCapacity, span);
            }

            foreach (var item in ToSequence(countsValue, span, context))
            {
                if (counts.Count == counts.Capacity)
                {
                    var predicted = counts.Capacity == 0 ? 4L : (long)counts.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    countsScratch.Grow(delta, span);
                    chargedCapacity = (int)predicted;
                }

                counts.Add(RuntimeValue(item));
                if (counts.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (counts.Capacity - chargedCapacity));
                    countsScratch.Grow(delta, span);
                    chargedCapacity = counts.Capacity;
                }

                context.ObserveCollectionCount(counts.Count, span);
                if ((counts.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (counts.Count != poolCount)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            using var cumulativeScratch = counts.Count == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(24L + (8L * counts.Count), span);
            var cumulative = counts.Count == 0 ? [] : new long[counts.Count];

            var total = 0L;
            for (var i = 0; i < counts.Count; i++)
            {
                var single = RuntimeArgumentValidation.ExpectInteger(counts[i], "random.sample(..., counts=...) expects integer counts.", span);
                if (single < BigInteger.Zero || single > int.MaxValue)
                {
                    throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects non-negative counts.", span);
                }

                total += (long)(int)single;
                cumulative[i] = total;
            }

            if (total > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "random.sample(..., counts=...) population is too large.", span);
            }

            context.ObserveCollectionCount((int)total, span);
            // Raw CLR ints sit outside the numeric tower; normalize like the chokes.
            rawCount = rawCount is int smallCount ? new BigInteger(smallCount) : rawCount;
            // Like CPython, k validates against the expanded total through
            // operator dispatch before the multiply gate sizes the draw.
            if (IsTruthy(EvaluateBinaryOperator(BinaryOperatorSyntax.Greater, rawCount, new BigInteger(total), context, span), context, span))
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative", span);
            }

            var count = CoerceSampleCount(rawCount, span);
            using var selectedScratch = count == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(80L + (24L * count), span);
            var selected = count == 0 ? new HashSet<int>() : new HashSet<int>(count);

            var result = new object[count];
            for (var drawn = 0; drawn < count; drawn++)
            {
                var resume = total - count + drawn;
                var draw = (int)state.NextBelow((ulong)(resume + 1));
                var picked = draw;
                if (!selected.Add(draw))
                {
                    // Floyd collision: the resume slot is the fresh position,
                    // so it joins the set and maps to the output. Mapping the
                    // original draw instead could repeat an expanded position.
                    picked = (int)resume;
                    selected.Add(picked);
                }

                result[drawn] = RuntimeValue(getAt(MapCountedPosition(cumulative, picked)));
                if (((drawn + 1) & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
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

        private static List<object> MaterializeSequence(object value, LythonSourceSpan span, ExecutionContext context)
        {
            // Populations and weights are caller-lifetime scratch with no
            // governed adopter, so backing commits durably (statistics-drain
            // shape): sized inputs pay exactly once up front, lazy ones pay
            // each doubling. Payloads stay owned elsewhere.
            var governor = context.MemoryGovernor;
            var sizedCount = value switch
            {
                object[] array => array.Length,
                PyList list => list.Count,
                PyTuple tuple => tuple.Count,
                ICollection<object> collection => collection.Count,
                _ => (int?)null,
            };
            var result = sizedCount.HasValue ? new List<object>(sizedCount.Value) : new List<object>();
            var chargedCapacity = result.Capacity;
            if (chargedCapacity > 0)
            {
                governor.Reserve(8L * chargedCapacity, span);
                governor.Commit(8L * chargedCapacity);
            }

            foreach (var item in ToSequence(value, span, context))
            {
                if (result.Count == result.Capacity)
                {
                    var predicted = result.Capacity == 0 ? 4L : (long)result.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    governor.Reserve(delta, span);
                    governor.Commit(delta);
                    chargedCapacity = (int)predicted;
                }

                result.Add(RuntimeValue(item));
                if (result.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (result.Capacity - chargedCapacity));
                    governor.Reserve(delta, span);
                    governor.Commit(delta);
                    chargedCapacity = result.Capacity;
                }

                context.ObserveCollectionCount(result.Count, span);
                if ((result.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return result;
        }

        private static List<object> MaterializePopulation(object value, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is not IPyIndexableValue && value is not PyRange)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} population must be a sequence.", span);
            }

            return MaterializeSequence(value, span, context);
        }

        private static int ChooseWeightedIndex(PyRandomState state, int populationLength, double[]? weights, double[]? cumulative, LythonSourceSpan span)
        {
            if (weights is null && cumulative is null)
            {
                return (int)state.NextBelow((ulong)populationLength);
            }

            if (weights is not null)
            {
                var total = weights.Sum();
                if (total <= 0)
                {
                    throw new LythonRuntimeException("ValueError", "random.choices(..., weights=...) total of weights must be greater than zero.", span);
                }

                var threshold = state.NextDouble() * total;
                double running = 0;
                for (var i = 0; i < weights.Length; i++)
                {
                    running += weights[i];
                    if (threshold < running)
                    {
                        return i;
                    }
                }

                return weights.Length - 1;
            }

            var cumulativeWeights = cumulative.RequireNotNull();
            var maximum = cumulativeWeights[^1];
            if (maximum <= 0)
            {
                throw new LythonRuntimeException("ValueError", "random.choices(..., cum_weights=...) total of weights must be greater than zero.", span);
            }

            var thresholdCum = state.NextDouble() * maximum;
            for (var i = 0; i < cumulativeWeights.Length; i++)
            {
                if (thresholdCum < cumulativeWeights[i])
                {
                    return i;
                }
            }

            return cumulativeWeights.Length - 1;
        }

        private static void ShuffleMaterialized(PyRandomState state, IList<object> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = (int)state.NextBelow((ulong)(i + 1));
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        // N07: sized populations serve indexed picks without draining.
        // Lengths beyond int range, and non-sequences, return null so callers
        // fall back to the materializing path, preserving its validation and
        // denial behavior. The limit observation keeps oversized-denial
        // identical to the drain it replaces.
        private static (int Length, Func<int, object> GetAt)? TryGetDirectPopulation(object value, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is IPyIndexableValue indexable)
            {
                context.ObserveCollectionCount(indexable.Length, span);
                return (indexable.Length, indexable.GetIndex);
            }

            if (value is PyRange range && range.Length <= int.MaxValue)
            {
                var length = (int)range.Length;
                context.ObserveCollectionCount(length, span);
                // Raw CLR ints are outside the numeric tower; the subscript
                // funnel only accepts normalized magnitudes.
                return (length, index => range.GetSubscript(new BigInteger(index), span));
            }

            return null;
        }

        // Floyd positions over an indexable population: k draws and O(k)
        // state instead of a full drain and shuffle. Uniform over k-subsets;
        // larger takes keep the legacy shuffle path and its exact draws.
        private static object SampleDirectPositions(PyRandomState state, Func<int, object> getAt, int length, int count, MemoryGovernor governor, LythonSourceSpan span, ExecutionContext context)
        {
            using var selectedScratch = count == 0
                ? governor.ReserveTemporary(0, span)
                : governor.ReserveTemporary(80L + (24L * count), span);
            var selected = count == 0 ? new HashSet<int>() : new HashSet<int>(count);
            var result = new object[count];
            for (var drawn = 0; drawn < count; drawn++)
            {
                var resume = length - count + drawn;
                var draw = (int)state.NextBelow((ulong)(resume + 1));
                var picked = draw;
                if (!selected.Add(draw))
                {
                    picked = (int)resume;
                    selected.Add(picked);
                }

                result[drawn] = RuntimeValue(getAt(picked));
                if (((drawn + 1) & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            return new PyList(result, governor, span);
        }

        private static double SampleGamma(PyRandomState state, double alpha, double beta)
        {
            if (alpha < 1.0)
            {
                return SampleGamma(state, alpha + 1.0, beta) * Math.Pow(NonZeroRandom(state), 1.0 / alpha);
            }

            var d = alpha - 1.0 / 3.0;
            var c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                var x = StandardNormal(state);
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

        private static double StandardNormal(PyRandomState state)
        {
            var u1 = NonZeroRandom(state);
            var u2 = state.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(TwoPi * u2);
        }

        private static double NonZeroRandom(PyRandomState state)
        {
            double value;
            do
            {
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
