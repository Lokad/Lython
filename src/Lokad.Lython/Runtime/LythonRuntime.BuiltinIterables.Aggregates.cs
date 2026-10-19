using System.Buffers;
using System.Globalization;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        if (arguments[0] is PyBytes bytes) return ReduceBytesTruth(bytes, false, span, context);

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (IsTruthy(item, context, span))
            {
                return true;
            }
        }

        return false;
    }

    private static ValueTask<object> AnyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => arguments.Length == 1 && arguments[0] is PyBytes or PyRange
            ? new(Any(arguments, span, context))
            : AnyGeneralAsync(arguments, span, context);

    private static async ValueTask<object> AnyGeneralAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        var cursor = await PyIteration.Cursor.CreateAsync(arguments[0], span, context).ConfigureAwait(false);
        await using (cursor.ConfigureAwait(false))
        {
            while (true)
            {
                var item = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!item.HasValue) return false;
                if (await IsTruthyAsync(item.Value, context, span).ConfigureAwait(false)) return true;
            }
        }
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        if (arguments[0] is PyBytes bytes) return ReduceBytesTruth(bytes, true, span, context);

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (!IsTruthy(item, context, span))
            {
                return false;
            }
        }

        return true;
    }

    private static ValueTask<object> AllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => arguments.Length == 1 && arguments[0] is PyBytes or PyRange
            ? new(All(arguments, span, context))
            : AllGeneralAsync(arguments, span, context);

    private static async ValueTask<object> AllGeneralAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        var cursor = await PyIteration.Cursor.CreateAsync(arguments[0], span, context).ConfigureAwait(false);
        await using (cursor.ConfigureAwait(false))
        {
            while (true)
            {
                var item = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!item.HasValue) return true;
                if (!await IsTruthyAsync(item.Value, context, span).ConfigureAwait(false)) return false;
            }
        }
    }

    private static bool ReduceBytesTruth(PyBytes bytes, bool all, LythonSourceSpan span, ExecutionContext context)
    {
        var source = bytes.Bytes;
        for (var i = 0; ; i++)
        {
            // Match checked iteration, including the final unsuccessful pull.
            if ((i & 63) == 0) context.CheckExecution(span);
            if (i == source.Length) return all;
            if ((source[i] != 0) != all) return !all;
        }
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var total = ReadSumStart(arguments, span);
        if (TrySumExactBytes(arguments[0], total, span, context, out var byteResult)) return byteResult;
        var sequence = ToSequence(arguments[0], span, context);
        ValidateSumStart(total, span);
        var sum = new SumState(total);
        foreach (var item in sequence)
        {
            if (sum.TryAddFloat(item)) continue;
            var result = sum.Total is PyInstance || item is PyInstance
                ? EvaluateBinaryOperator(BinaryOperatorSyntax.Add, sum.Total, item, context, span)
                : EvaluateAdd(sum.Total, item, context, span);
            sum.CompleteAddition(item, result);
        }
        return sum.Result;
    }

    private static async ValueTask<object> SumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var total = ReadSumStart(arguments, span);
        // Exact range items and integer addition cannot invoke guest protocols.
        // Reuse checked synchronous iteration; other starts may suspend in __add__.
        if (arguments[0] is PyRange && PyNumberOps.IsInteger(total)) return Sum(arguments, span, context);
        if (TrySumExactBytes(arguments[0], total, span, context, out var byteResult)) return byteResult;
        var cursor = await PyIteration.Cursor.CreateAsync(arguments[0], span, context).ConfigureAwait(false);
        var sum = new SumState(total);
        await using (cursor.ConfigureAwait(false))
        {
            ValidateSumStart(total, span);
            while (true)
            {
                var item = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!item.HasValue) break;
                if (sum.TryAddFloat(item.Value)) continue;
                var result = sum.Total is PyInstance || item.Value is PyInstance
                    ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, sum.Total, item.Value, context, span).ConfigureAwait(false)
                    : EvaluateAdd(sum.Total, item.Value, context, span);
                sum.CompleteAddition(item.Value, result);
            }
        }
        return sum.Result;
    }

    // Numeric phases are shared by the existing sync/async loops. Fixed 64-bit
    // eligibility keeps rounding independent of the embedding platform's C long.
    private enum SumPhase { Integer, Float, General }

    private struct SumState
    {
        private SumPhase _phase;
        private double _correction;

        internal SumState(object start)
        {
            Total = start;
            _correction = 0;
            _phase = start is double ? SumPhase.Float
                : start is not bool && TryInt64(start, out _) ? SumPhase.Integer : SumPhase.General;
        }

        internal object Total { get; private set; }

        // Skip a zero/nonfinite correction: retain signed zero, and do not turn
        // an infinite or overflowed running sum into NaN during finalization.
        internal readonly object Result => _phase == SumPhase.Float && _correction != 0 && double.IsFinite(_correction)
            ? (double)Total + _correction : Total;

        internal bool TryAddFloat(object item)
        {
            if (_phase != SumPhase.Float) return false;
            var previous = (double)Total;
            if (item is double value)
            {
                var next = previous + value;
                _correction += Math.Abs(previous) >= Math.Abs(value)
                    ? (previous - next) + value : (value - next) + previous;
                Total = next;
                return true;
            }
            if (TryInt64(item, out var integer))
            {
                // Python 3.13 adds small integers without compensating them.
                Total = previous + integer;
                return true;
            }

            // A generic operation must see the completed numeric prefix. Once
            // it takes over, a later double result does not restart compensation.
            Total = Result;
            _correction = 0;
            _phase = SumPhase.General;
            return false;
        }

        internal void CompleteAddition(object item, object result)
        {
            if (_phase == SumPhase.Integer)
                _phase = result is double ? SumPhase.Float
                    : TryInt64(item, out _) && TryInt64(result, out _) ? SumPhase.Integer : SumPhase.General;
            Total = result;
        }

        private static bool TryInt64(object value, out long result)
        {
            if (PyNumberOps.TryAsInteger(value, out var integer) && integer >= long.MinValue && integer <= long.MaxValue)
            {
                result = (long)integer;
                return true;
            }
            result = 0;
            return false;
        }
    }

    private static bool TrySumExactBytes(object value, object start, LythonSourceSpan span, ExecutionContext context, out object result)
    {
        result = start;
        if (value is not PyBytes bytes || !PyNumberOps.IsInteger(start)) return false;
        var source = bytes.Bytes;
        long subtotal = 0;
        for (var i = 0; ; i++)
        {
            // Match checked iteration, including the final unsuccessful pull.
            if ((i & 63) == 0) context.CheckExecution(span);
            if (i == source.Length) break;
            // Even a maximum-length CLR byte array totals less than 2^39.
            subtotal += source[i];
        }
        if (source.Length != 0)
        {
            _ = PyNumberOps.TryAsInteger(start, out var initial);
            result = AddExactIntegers(initial, new BigInteger(subtotal), context.MemoryGovernor, span);
        }
        return true;
    }

    private static object ReadSumStart(object[] arguments, LythonSourceSpan span)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        return arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
    }

    private static void ValidateSumStart(object total, LythonSourceSpan span)
    {
        // CPython acquires the iterator before checking the start type, without
        // pulling an item. Acquisition effects/errors precede this rejection.
        if (PyStringOps.TryAsString(total, out _))
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum strings [use ''.join(seq) instead]", span);
        }

        if (total is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum bytes [use b''.join(seq) instead]", span);
        }
    }

}
