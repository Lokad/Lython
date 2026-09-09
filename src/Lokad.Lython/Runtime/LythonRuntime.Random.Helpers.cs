using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class RandomModule : PyModule
    {
        private static BigInteger ComputeRangeCount(BigInteger start, BigInteger stop, BigInteger step, string owner, LythonSourceSpan span)
        {
            if (step == BigInteger.Zero)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) arg 3 must not be zero.", span);
            }

            if (step > BigInteger.Zero)
            {
                if (stop <= start)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner}(...) empty range for randrange().", span);
                }

                return ((stop - start - BigInteger.One) / step) + BigInteger.One;
            }

            if (stop >= start)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) empty range for randrange().", span);
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

        private static double[] ReadWeights(object value, int expectedCount, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            var values = MaterializeSequence(value, span, context);
            if (values.Count != expectedCount)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects one weight per population item.", span);
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

        private static double[] ReadCumulativeWeights(object value, int expectedCount, string owner, LythonSourceSpan span, ExecutionContext context)
        {
            var values = ReadWeights(value, expectedCount, owner, span, context);
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
            List<object> population,
            object countsValue,
            int count,
            PyRandomState state,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            // Counted sampling without expanding: drain the counts once, map
            // them to cumulative bounds, draw k distinct expanded positions
            // with Floyd's algorithm (exactly k draws, O(k) state), and map
            // each position back to its pool. Uniform over the expanded
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
            if (sizedCount.HasValue && sizedCount.Value != population.Count)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            var counts = sizedCount.HasValue ? new List<object>(sizedCount.Value) : new List<object>();
            var chargedCapacity = counts.Capacity;
            if (chargedCapacity > 0)
            {
                governor.Reserve(8L * chargedCapacity, span);
                governor.Commit(8L * chargedCapacity);
            }

            foreach (var item in ToSequence(countsValue, span, context))
            {
                if (counts.Count == counts.Capacity)
                {
                    var predicted = counts.Capacity == 0 ? 4L : (long)counts.Capacity * 2L;
                    var delta = checked(8L * (predicted - chargedCapacity));
                    governor.Reserve(delta, span);
                    governor.Commit(delta);
                    chargedCapacity = (int)predicted;
                }

                counts.Add(RuntimeValue(item));
                if (counts.Capacity > chargedCapacity)
                {
                    var delta = checked(8L * (counts.Capacity - chargedCapacity));
                    governor.Reserve(delta, span);
                    governor.Commit(delta);
                    chargedCapacity = counts.Capacity;
                }

                context.ObserveCollectionCount(counts.Count, span);
                if ((counts.Count & 63) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
            }

            if (counts.Count != population.Count)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            var cumulative = counts.Count == 0 ? [] : new long[counts.Count];
            if (counts.Count > 0)
            {
                governor.Reserve(24L + (8L * counts.Count), span);
                governor.Commit(24L + (8L * counts.Count));
            }

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
            if (count > total)
            {
                throw new LythonRuntimeException("ValueError", "Sample larger than population or is negative.", span);
            }

            var selected = count == 0 ? new HashSet<int>() : new HashSet<int>(count);
            if (count > 0)
            {
                governor.Reserve(80L + (24L * count), span);
                governor.Commit(80L + (24L * count));
            }

            var result = new object[count];
            for (var drawn = 0; drawn < count; drawn++)
            {
                var resume = total - count + drawn;
                var draw = (int)state.NextBelow((ulong)(resume + 1));
                if (!selected.Add(draw))
                {
                    selected.Add((int)resume);
                }

                result[drawn] = population[MapCountedPosition(cumulative, draw)];
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
            var result = new List<object>();
            foreach (var item in ToSequence(value, span, context))
            {
                result.Add(RuntimeValue(item));
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
