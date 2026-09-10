using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        // Constructed NormalDist values retain two doubles (free 64-bit payloads
        // by design); charge one table slot per fresh value once built.
        private const long NormalDistValueBytes = 64;

        internal static object OwnNormalDist(object value, ExecutionContext context, LythonSourceSpan span)
        {
            context.MemoryGovernor.Reserve(NormalDistValueBytes, span);
            context.MemoryGovernor.Commit(NormalDistValueBytes);
            return value;
        }

        private static object CreateNormalDist(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.StatisticsNormalDist,
                PythonCallableKind.Builtin);
            var mean = bound.Length >= 1 && bound[0] is not PyNone
                ? RuntimeArgumentValidation.ExpectReal(bound[0], "statistics.NormalDist(..., mu=...)", span)
                : 0.0;
            var stdev = bound.Length >= 2 && bound[1] is not PyNone
                ? RuntimeArgumentValidation.ExpectReal(bound[1], "statistics.NormalDist(..., sigma=...)", span)
                : 1.0;
            if (stdev < 0.0)
            {
                throw StatisticsError("sigma must be non-negative", span);
            }

            return OwnNormalDist(new PyNormalDist(mean, stdev), context, span);
        }

        private static object NormalDistFromSamples(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.NormalDist.from_samples", span, context);
            if (values.Count < 2)
            {
                throw StatisticsError("statistics.NormalDist.from_samples(data) requires at least two data points.", span);
            }

            var mean = values.Average();
            var sum = values.Sum(value => Math.Pow(value - mean, 2));
            return OwnNormalDist(new PyNormalDist(mean, Math.Sqrt(sum / (values.Count - 1))), context, span);
        }

        public static bool TryAddNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => new PyNormalDist(lhs.Mean + rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance)),
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var amount) => new PyNormalDist(lhs.Mean + amount, lhs.Stdev),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var amount) => new PyNormalDist(amount + rhs.Mean, rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TrySubtractNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = span;
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => new PyNormalDist(lhs.Mean - rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance)),
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var amount) => new PyNormalDist(lhs.Mean - amount, lhs.Stdev),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var amount) => new PyNormalDist(amount - rhs.Mean, rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryMultiplyNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            _ = span;
            value = (left, right) switch
            {
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var factor) => new PyNormalDist(lhs.Mean * factor, lhs.Stdev * Math.Abs(factor)),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var factor) => new PyNormalDist(factor * rhs.Mean, Math.Abs(factor) * rhs.Stdev),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryDivideNormalDist(object left, object right, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            value = null;
            if (left is not PyNormalDist lhs || !PyRealNumber.TryAsDouble(right, out var divisor))
            {
                return false;
            }

            if (divisor == 0.0)
            {
                throw new LythonRuntimeException("ValueError", "division by zero", span);
            }

            value = new PyNormalDist(lhs.Mean / divisor, lhs.Stdev / Math.Abs(divisor));
            return true;
        }

        public static bool TryUnaryNormalDist(object operand, bool negative, [MaybeNullWhen(false)] out object value)
        {
            if (operand is not PyNormalDist dist)
            {
                value = null;
                return false;
            }

            value = negative ? new PyNormalDist(-dist.Mean, dist.Stdev) : new PyNormalDist(dist.Mean, dist.Stdev);
            return true;
        }


        private static bool IsWholeInteger(double value)
            => double.IsFinite(value) && Math.Abs(value % 1.0) < 1e-12;

        private static double ExpectRealForStatistics(object value, string owner, LythonSourceSpan span)
        {
            if (!PyRealNumber.TryAsDouble(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(data) expects real numbers.", span);
            }

            return real;
        }

    }
}
