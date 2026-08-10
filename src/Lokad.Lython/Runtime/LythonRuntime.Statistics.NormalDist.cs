using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        private static object CreateNormalDist(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.StatisticsNormalDist,
                PythonCallableKind.Builtin);
            var mean = bound.Length >= 1 && bound[0] is not PyNone
                ? ExpectReal(bound[0], "statistics.NormalDist(..., mu=...)", span)
                : 0.0;
            var stdev = bound.Length >= 2 && bound[1] is not PyNone
                ? ExpectReal(bound[1], "statistics.NormalDist(..., sigma=...)", span)
                : 1.0;
            if (stdev < 0.0)
            {
                throw new LythonRuntimeException("StatisticsError", "sigma must be non-negative", span);
            }

            _ = context;
            return new PyNormalDist(mean, stdev);
        }

        private static object NormalDistFromSamples(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var values = GetNumericValuesFromData(arguments, "statistics.NormalDist.from_samples", span, context);
            if (values.Count < 2)
            {
                throw new LythonRuntimeException("StatisticsError", "statistics.NormalDist.from_samples(data) requires at least two data points.", span);
            }

            var mean = values.Average();
            var sum = values.Sum(value => Math.Pow(value - mean, 2));
            return new PyNormalDist(mean, Math.Sqrt(sum / (values.Count - 1)));
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

        private static double ExpectReal(object value, string owner, LythonSourceSpan span)
        {
            if (!PyRealNumber.TryAsDouble(value, out var real))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a real number.", span);
            }

            return real;
        }

        private static string ExpectText(object value, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a string.", span);
            }

            return text.AsString();
        }

        private static object BoxStatisticalFloat(double value)
            => IsWholeInteger(value) ? new BigInteger(value) : value;

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

        private sealed class TypeMemberCallable : ICallable
        {
            private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
            private readonly LythonCallableSignature _signature;

            public TypeMemberCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string[] parameterNames)
            {
                _signature = new LythonCallableSignature(name, parameterNames);
                _implementation = implementation;
            }

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin);
                return _implementation(positional, span, context);
            }
        }
    }
}
