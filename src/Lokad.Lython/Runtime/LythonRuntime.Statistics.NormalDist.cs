using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class StatisticsModule : PyModule
    {
        private const long NormalDistValueBytes = 64;

        private static PyNormalDist NewNormalDist(double mean, double stdev, ExecutionContext context, LythonSourceSpan span)
        {
            using var storage = context.MemoryGovernor.ReserveTemporary(NormalDistValueBytes, span);
            context.CheckExecution(span);
            var value = new PyNormalDist(mean, stdev);
            storage.Commit(NormalDistValueBytes);
            return value;
        }

        private static object CreateNormalDist(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CreateNormalDistCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> CreateNormalDistAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CreateNormalDistCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> CreateNormalDistCoreAsync(CallArgumentValue[] arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span,
                LythonKnownCallableSignatures.StatisticsNormalDist, PythonCallableKind.Builtin);
            var sigma = bound.Assigned[1] ? bound.Values[1] : 1.0;
            var comparison = asynchronous
                ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Less, sigma, 0.0, context, span).ConfigureAwait(false)
                : EvaluateBinaryOperator(BinaryOperatorSyntax.Less, sigma, 0.0, context, span);
            if (asynchronous ? await IsTruthyAsync(comparison, context, span).ConfigureAwait(false) : IsTruthy(comparison, context, span))
                throw StatisticsError("sigma must be non-negative", span);
            var mean = bound.Assigned[0] ? (double)(asynchronous
                ? await FloatAsync([bound.Values[0]], span, context).ConfigureAwait(false)
                : Float([bound.Values[0]], span, context)) : 0.0;
            var stdev = bound.Assigned[1] ? (double)(asynchronous
                ? await FloatAsync([bound.Values[1]], span, context).ConfigureAwait(false)
                : Float([bound.Values[1]], span, context)) : 1.0;
            return NewNormalDist(mean, stdev, context, span);
        }

        private static object NormalDistFromSamples(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => NormalDistFromSamplesCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> NormalDistFromSamplesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => NormalDistFromSamplesCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> NormalDistFromSamplesCoreAsync(object[] arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            var values = asynchronous
                ? await GetNumericObjectsAsync(arguments, "statistics.NormalDist.from_samples", span, context, scratch).ConfigureAwait(false)
                : GetNumericObjects(arguments, "statistics.NormalDist.from_samples", span, context, scratch);
            if (values.Count < 2)
                throw StatisticsError("statistics.NormalDist.from_samples(data) requires at least two data points.", span);
            var stdev = (double)ComputeDispersion(values, PyNone.Instance, true, true, true, context, span, out var mean);
            return NewNormalDist(mean, stdev, context, span);
        }

        public static bool TryAddNormalDist(object left, object right, LythonSourceSpan span, ExecutionContext context, [MaybeNullWhen(false)] out object value)
        {
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => NewNormalDist(lhs.Mean + rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance), context, span),
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var amount) => NewNormalDist(lhs.Mean + amount, lhs.Stdev, context, span),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var amount) => NewNormalDist(amount + rhs.Mean, rhs.Stdev, context, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TrySubtractNormalDist(object left, object right, LythonSourceSpan span, ExecutionContext context, [MaybeNullWhen(false)] out object value)
        {
            value = (left, right) switch
            {
                (PyNormalDist lhs, PyNormalDist rhs) => NewNormalDist(lhs.Mean - rhs.Mean, Math.Sqrt(lhs.Variance + rhs.Variance), context, span),
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var amount) => NewNormalDist(lhs.Mean - amount, lhs.Stdev, context, span),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var amount) => NewNormalDist(amount - rhs.Mean, rhs.Stdev, context, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryMultiplyNormalDist(object left, object right, LythonSourceSpan span, ExecutionContext context, [MaybeNullWhen(false)] out object value)
        {
            value = (left, right) switch
            {
                (PyNormalDist lhs, _) when PyRealNumber.TryAsDouble(right, out var factor) => NewNormalDist(lhs.Mean * factor, lhs.Stdev * Math.Abs(factor), context, span),
                (_, PyNormalDist rhs) when PyRealNumber.TryAsDouble(left, out var factor) => NewNormalDist(factor * rhs.Mean, Math.Abs(factor) * rhs.Stdev, context, span),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public static bool TryDivideNormalDist(object left, object right, LythonSourceSpan span, ExecutionContext context, [MaybeNullWhen(false)] out object value)
        {
            value = null;
            if (left is not PyNormalDist lhs || !PyRealNumber.TryAsDouble(right, out var divisor))
            {
                return false;
            }

            if (divisor == 0.0)
            {
                throw new LythonRuntimeException("ZeroDivisionError", "float division by zero", span);
            }

            value = NewNormalDist(lhs.Mean / divisor, lhs.Stdev / Math.Abs(divisor), context, span);
            return true;
        }

        public static bool TryUnaryNormalDist(object operand, bool negative, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (operand is not PyNormalDist dist)
            {
                value = null;
                return false;
            }

            value = NewNormalDist(negative ? -dist.Mean : dist.Mean, dist.Stdev, context, span);
            return true;
        }

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
