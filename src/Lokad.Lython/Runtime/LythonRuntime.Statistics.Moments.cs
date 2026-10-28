using System.Numerics;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule
    {
        private static async ValueTask<object> DispersionAsync(object[] arguments, string owner,
            LythonSourceSpan span, ExecutionContext context, bool sample, bool squareRoot, bool asynchronous)
        {
            using var input = context.MemoryGovernor.ReserveTemporary(0, span);
            if (arguments.Length > 0 && arguments[0] is IReadOnlyCollection<object> sized && sized.Count == 0)
                throw StatisticsError(sample ? $"{owner}(data) requires at least two data points."
                    : $"{owner} requires at least one data point.", span);
            var source = arguments.Length <= 1 ? arguments : [arguments[0]];
            var values = asynchronous
                ? await GetNumericObjectsAsync(source, owner, span, context, input).ConfigureAwait(false)
                : GetNumericObjects(source, owner, span, context, input);
            if (sample && values.Count < 2)
                throw StatisticsError($"{owner}(data) requires at least two data points.", span);
            return ComputeDispersion(values, arguments.Length > 1 ? arguments[1] : PyNone.Instance,
                sample, squareRoot, false, context, span, out _);
        }

        // Retain exact binary/integer units until the final division or square root.
        // Input acquisition differs between APIs; this calculation does not.
        private static object ComputeDispersion(List<object> values, object center, bool sample,
            bool squareRoot, bool meanRequired, ExecutionContext context, LythonSourceSpan span, out double mean)
        {
            var suppliedCenter = center is not PyNone;
            var integral = !suppliedCenter || PyNumberOps.TryAsInteger(center, out _);
            long maximumBits = 1;
            foreach (var value in values)
            {
                context.CheckExecution(span);
                if (PyNumberOps.TryAsInteger(value, out var integer))
                    maximumBits = Math.Max(maximumBits, RuntimeMemoryEstimates.GetMagnitudeBitLength(integer));
                else integral = false;
            }
            if (suppliedCenter && PyNumberOps.TryAsInteger(center, out var integerCenter))
                maximumBits = Math.Max(maximumBits, RuntimeMemoryEstimates.GetMagnitudeBitLength(integerCenter));
            var scale = 0;
            var centerDouble = suppliedCenter && !integral ? ExpectRealForStatistics(center, "statistics variance", span) : 0.0;
            if (!integral)
            {
                foreach (var value in values)
                {
                    context.CheckExecution(span);
                    if (!suppliedCenter && PyNumberOps.TryAsInteger(value, out _)) continue;
                    var floating = ExpectRealForStatistics(value, "statistics variance", span);
                    if (suppliedCenter) { var difference = floating - centerDouble; floating = difference * difference; }
                    if (!double.IsFinite(floating) || floating == 0.0) continue;
                    var raw = (ulong)BitConverter.DoubleToInt64Bits(floating);
                    var exponent = (int)((raw >> 52) & 0x7FF);
                    var mantissa = raw & 0x000F_FFFF_FFFF_FFFFUL;
                    if (exponent != 0) mantissa |= 1UL << 52;
                    scale = Math.Max(scale, Math.Max(0,
                        (exponent == 0 ? 1074 : 1075 - exponent) - BitOperations.TrailingZeroCount(mantissa)));
                    maximumBits = Math.Max(maximumBits, exponent - 1022);
                }
            }
            // Sums, squares, replacement overlap, ratio alignment and division workspace.
            // Admit before creating scaled values or multiplying wide integers.
            var bits = RuntimeMemoryEstimates.SaturatingAdd(
                RuntimeMemoryEstimates.SaturatingMultiply(2, maximumBits + scale), 128);
            using var working = context.MemoryGovernor.ReserveTemporary(
                RuntimeMemoryEstimates.SaturatingMultiply(16, RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(bits)), span);
            BigInteger sum = 0, squares = 0;
            var special = 0.0;
            var hasSpecial = false;
            _ = PyNumberOps.TryAsInteger(center, out var centerInteger);
            foreach (var value in values)
            {
                context.CheckExecution(span);
                BigInteger units;
                if (suppliedCenter && !integral)
                {
                    // Python rounds subtraction and multiplication before summing
                    // when the caller supplies a floating center.
                    var difference = ExpectRealForStatistics(value, "statistics variance", span) - centerDouble;
                    var term = difference * difference;
                    if (!double.IsFinite(term)) { special += term; hasSpecial = true; continue; }
                    squares += ScaleDouble(term, scale);
                    continue;
                }
                if (PyNumberOps.TryAsInteger(value, out var integer)) units = integer << scale;
                else
                {
                    var floating = ExpectRealForStatistics(value, "statistics variance", span);
                    if (!double.IsFinite(floating)) { special += floating; hasSpecial = true; continue; }
                    units = ScaleDouble(floating, scale);
                }
                if (suppliedCenter) units -= centerInteger;
                else sum += units;
                squares += units * units;
            }
            var divisor = sample ? values.Count - 1 : values.Count;
            mean = 0;
            if (hasSpecial)
            {
                mean = special / values.Count;
                // Keep the established nonfinite stdev extension; from_samples
                // follows Python's graceful mean/stdev handling of special sums.
                return meanRequired ? mean / special : squareRoot ? Math.Sqrt(special / divisor) : special / divisor;
            }
            if (meanRequired)
                mean = StatisticsRatio(sum, (BigInteger)values.Count << scale, context, span);
            var numerator = suppliedCenter ? squares : values.Count * squares - sum * sum;
            var denominator = suppliedCenter ? new BigInteger(divisor) : (BigInteger)values.Count * divisor;
            denominator <<= suppliedCenter ? scale : 2 * scale;
            if (squareRoot) return StatisticsSquareRoot(numerator, denominator, context, span);
            if (integral)
            {
                var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
                if (remainder.IsZero) return OwnHeapInteger(quotient, context.MemoryGovernor, span);
            }
            return StatisticsRatio(numerator, denominator, context, span);
        }

        private static double StatisticsRatio(BigInteger numerator, BigInteger denominator,
            ExecutionContext context, LythonSourceSpan span)
        {
            try { return (double)PyNumberOps.TrueDivide(PyNumber.FromInteger(numerator), PyNumber.FromInteger(denominator), context, span); }
            catch (OverflowException error) { throw new LythonRuntimeException("OverflowError", error.Message, span); }
        }

        // CPython statistics' round-to-odd fraction square root. Keep 109 bits
        // before one binary64 rounding, including subnormal/overflow boundaries.
        private static double StatisticsSquareRoot(BigInteger numerator, BigInteger denominator,
            ExecutionContext context, LythonSourceSpan span)
        {
            if (numerator.IsZero) return 0.0;
            var shift = checked((int)((numerator.GetBitLength() - denominator.GetBitLength() - 109) >> 1));
            var n = shift < 0 ? numerator << checked(-2 * shift) : numerator;
            var d = shift >= 0 ? denominator << checked(2 * shift) : denominator;
            var root = IntegerSquareRoot(n / d, context, span);
            context.CheckExecution(span);
            if (root * root * d != n) root |= BigInteger.One;
            return shift >= 0 ? StatisticsRatio(root << shift, BigInteger.One, context, span)
                : StatisticsRatio(root, BigInteger.One << -shift, context, span);
        }
    }
}
