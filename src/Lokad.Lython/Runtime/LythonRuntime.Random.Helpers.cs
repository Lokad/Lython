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
            var integer = ExpectInteger(value, message, span);
            if (integer < 0 || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return (int)integer;
        }

        private static BigInteger ExpectInteger(object value, string message, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            return integer;
        }

        private static double ExpectReal(object value, string owner, LythonSourceSpan span)
        {
            if (!PyRealNumber.TryAsDouble(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            return real;
        }

        private static double ExpectPositiveReal(object value, string owner, LythonSourceSpan span)
        {
            var real = ExpectReal(value, owner, span);
            if (real <= 0.0 || double.IsNaN(real) || double.IsInfinity(real))
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects a positive finite number.", span);
            }

            return real;
        }

        private static double[] ReadWeights(object value, int expectedCount, string owner, LythonSourceSpan span)
        {
            var values = MaterializeSequence(value, span);
            if (values.Count != expectedCount)
            {
                throw new LythonRuntimeException("ValueError", $"{owner} expects one weight per population item.", span);
            }

            var result = new double[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                var weight = ExpectReal(values[i], owner, span);
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects finite non-negative weights.", span);
                }

                result[i] = weight;
            }

            return result;
        }

        private static double[] ReadCumulativeWeights(object value, int expectedCount, string owner, LythonSourceSpan span)
        {
            var values = ReadWeights(value, expectedCount, owner, span);
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] < values[i - 1])
                {
                    throw new LythonRuntimeException("ValueError", $"{owner} expects monotonically increasing cumulative weights.", span);
                }
            }

            return values;
        }

        private static List<object> ExpandPopulationCounts(IReadOnlyList<object> population, object countsValue, LythonSourceSpan span, ExecutionContext context)
        {
            var counts = MaterializeSequence(countsValue, span);
            if (counts.Count != population.Count)
            {
                throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects one count per population item.", span);
            }

            var total = BigInteger.Zero;
            var parsed = new int[counts.Count];
            for (var i = 0; i < counts.Count; i++)
            {
                var count = ExpectInteger(counts[i], "random.sample(..., counts=...) expects integer counts.", span);
                if (count < BigInteger.Zero || count > int.MaxValue)
                {
                    throw new LythonRuntimeException("ValueError", "random.sample(..., counts=...) expects non-negative counts.", span);
                }

                parsed[i] = (int)count;
                total += count;
            }

            if (total > int.MaxValue)
            {
                throw new LythonRuntimeException("OverflowError", "random.sample(..., counts=...) population is too large.", span);
            }

            context.ObserveCollectionCount((int)total, span);
            var expanded = new List<object>((int)total);
            for (var i = 0; i < population.Count; i++)
            {
                for (var j = 0; j < parsed[i]; j++)
                {
                    expanded.Add(population[i]);
                }
            }

            return expanded;
        }

        private static List<object> MaterializeSequence(object value, LythonSourceSpan span)
        {
            var result = new List<object>();
            foreach (var item in ToSequence(value, span))
            {
                result.Add(RuntimeValue(item));
            }

            return result;
        }

        private static List<object> MaterializePopulation(object value, string owner, LythonSourceSpan span)
        {
            if (value is not IPyIndexableValue && value is not PyRange)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} population must be a sequence.", span);
            }

            return MaterializeSequence(value, span);
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
