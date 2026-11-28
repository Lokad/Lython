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

        if (arguments[0] is PyBytes bytes) return RuntimeValue(ReduceBytesTruth(bytes, false, span, context));

        foreach (var item in PyIteration.Cursor.Create(arguments[0], span, context))
        {
            if (IsTruthy(item, context, span))
            {
                return RuntimeValue(true);
            }
        }

        return RuntimeValue(false);
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
        await using (((IAsyncDisposable)cursor).ConfigureAwait(false))
        {
            while (true)
            {
                var item = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!item.HasValue) return RuntimeValue(false);
                if (await IsTruthyAsync(item.Value, context, span).ConfigureAwait(false)) return RuntimeValue(true);
            }
        }
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        if (arguments[0] is PyBytes bytes) return RuntimeValue(ReduceBytesTruth(bytes, true, span, context));

        foreach (var item in PyIteration.Cursor.Create(arguments[0], span, context))
        {
            if (!IsTruthy(item, context, span))
            {
                return RuntimeValue(false);
            }
        }

        return RuntimeValue(true);
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
        await using (((IAsyncDisposable)cursor).ConfigureAwait(false))
        {
            while (true)
            {
                var item = await cursor.TryMoveNextAsync().ConfigureAwait(false);
                if (!item.HasValue) return RuntimeValue(true);
                if (!await IsTruthyAsync(item.Value, context, span).ConfigureAwait(false)) return RuntimeValue(false);
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

    private static object Sum(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var total = ReadSumStart(arguments, span);
        return Sum(arguments[0], total, span, context);
    }

    private static object Sum(object iterable, object total, LythonSourceSpan span, ExecutionContext context)
    {
        if (iterable is ExecutionContext.BorrowedGeneratorSum reduction)
            return reduction.Reduce(context, span, false).GetAwaiter().GetResult();
        if (TrySumExactBytes(iterable, total, span, context, out var byteResult)) return byteResult;
        var privateFrame = iterable as ExecutionContext;
        using var cursor = privateFrame is null ? PyIteration.Cursor.Create(iterable, span, context) : null;
        var generatorSpan = privateFrame is null ? null : context.PrivateGeneratorSpan;
        ValidateSumStart(total, span);
        // The private accumulator is borrowed by this command's generator only
        // until AdvanceBuffered returns. Fund it through all pulls and cleanup.
        context.MemoryGovernor.Reserve(64, span);
        try
        {
            return ReduceSum(new SumState(total), privateFrame, cursor, generatorSpan, span, context);
        }
        finally { context.MemoryGovernor.ReleaseReserved(64); }
    }

    private static ValueTask<object> SumAsync(ReadOnlySpan<object> arguments, LythonSourceSpan span, ExecutionContext context)
    {
        var total = ReadSumStart(arguments, span);
        var iterable = arguments[0];
        if (iterable is ExecutionContext.BorrowedGeneratorSum reduction)
            return reduction.Reduce(context, span, true);
        // Exact range items and integer addition cannot invoke guest protocols.
        // Reuse checked synchronous iteration; other starts may suspend in __add__.
        if (iterable is PyRange && PyNumberOps.IsInteger(total)) return new(Sum(iterable, total, span, context));
        if (TrySumExactBytes(iterable, total, span, context, out var byteResult)) return new(byteResult);
        return SumAsync(iterable, total, span, context);
    }

    private static async ValueTask<object> SumAsync(object iterable, object total, LythonSourceSpan span, ExecutionContext context)
    {
        var privateFrame = iterable as ExecutionContext;
        var cursor = privateFrame is null
            ? await PyIteration.Cursor.CreateAsync(iterable, span, context).ConfigureAwait(false) : null;
        var generatorSpan = privateFrame is null ? null : context.PrivateGeneratorSpan;
        try
        {
            ValidateSumStart(total, span);
            context.MemoryGovernor.Reserve(64, span);
            try
            {
                return await ReduceSumAsync(new SumState(total), privateFrame, cursor, generatorSpan, span, context)
                    .ConfigureAwait(false);
            }
            finally { context.MemoryGovernor.ReleaseReserved(64); }
        }
        finally { if (cursor is not null) await cursor.DisposeAsync().ConfigureAwait(false); }
    }

    private static object ReduceSum(SumState sum, ExecutionContext? privateFrame, PyIteration.Cursor? cursor,
        LythonSourceSpan? generatorSpan, LythonSourceSpan span, ExecutionContext context)
    {
        // Keep the public generator object through callbacks, which can
        // advance it or finish it before the next pull.
        var generator = (object?)privateFrame ?? cursor?.GetBulkGenerator(context);
        while (true)
        {
            var next = generator is null || (privateFrame is null && !sum.CanBuffer)
                ? cursor!.TryMoveNext()
                : privateFrame is not null
                    ? PyGenerator.AdvanceFrame(privateFrame, generatorSpan!, null, PyNone.Instance, null,
                        sum.CanBuffer ? sum : null, 0, 0)
                    : ((PyGenerator)generator).AdvanceBuffered(sum, 0);
            if (!next.HasValue) return sum.Result;
            if (sum.TryAddResult(ref next)) continue;
            var item = next.Value;
            var result = sum.Total is PyInstance || item is PyInstance
                ? EvaluateBinaryOperator(BinaryOperatorSyntax.Add, sum.Total, item, context, span)
                : EvaluateAdd(sum.Total, item, context, span);
            sum.CompleteAddition(item, result);
        }
    }

    private static async ValueTask<object> ReduceSumAsync(SumState sum, ExecutionContext? privateFrame,
        PyIteration.Cursor? cursor, LythonSourceSpan? generatorSpan, LythonSourceSpan span, ExecutionContext context)
    {
        var generator = (object?)privateFrame ?? cursor?.GetBulkGenerator(context);
        while (true)
        {
            var pending = generator is null || (privateFrame is null && !sum.CanBuffer)
                ? cursor!.TryMoveNextAsync()
                : privateFrame is not null
                    ? PyGenerator.AdvanceFrameAsync(privateFrame, generatorSpan!, null, PyNone.Instance, null,
                        sum.CanBuffer ? sum : null, 0, 0)
                    : ((PyGenerator)generator).AdvanceBufferedAsync(sum, 0);
            var item = await pending.ConfigureAwait(false);
            if (!item.HasValue) return sum.Result;
            if (sum.TryAddResult(ref item)) continue;
            var value = item.Value;
            var result = sum.Total is PyInstance || value is PyInstance
                ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, sum.Total, value, context, span).ConfigureAwait(false)
                : EvaluateAdd(sum.Total, value, context, span);
            sum.CompleteAddition(value, result);
        }
    }

    // Numeric phases are shared by the existing sync/async loops. Fixed 64-bit
    // eligibility keeps rounding independent of the embedding platform's C long.
    private enum SumPhase { Integer, Float, General }

    private sealed class SumState
    {
        private SumPhase _phase;
        private double _correction;
        private long _integer;
        private double _floating;
        private bool _numericAddition;

        internal SumState(object start)
        {
            Total = start;
            _correction = 0;
            _integer = 0;
            _floating = 0;
            _numericAddition = false;
            if (start is double floating)
            {
                _floating = floating;
                _phase = SumPhase.Float;
            }
            else if (start is not bool && TryInt64(start, out _integer)) _phase = SumPhase.Integer;
            else _phase = SumPhase.General;
        }

        // Preserve the supplied start's identity until an addition happens.
        // During a native prefix, the running value lives only in scalar fields.
        internal object Total { get; private set; }

        internal bool CanBuffer => _phase != SumPhase.General;

        internal object Result
        {
            get
            {
                if (!_numericAddition) return Total;
                if (_phase == SumPhase.Integer) return new BigInteger(_integer);
                // Keep signed zero/nonfinite behavior when no finite correction
                // needs folding into the completed numeric prefix.
                return _correction != 0 && double.IsFinite(_correction)
                    ? _floating + _correction : _floating;
            }
        }

        internal bool TryAddResult(ref PyIterationResult item)
        {
            if (_phase == SumPhase.General) return false;
            if (item.TryGetInteger(out var integer)) return TryAddInteger(integer);
            if (_phase == SumPhase.Float && item.TryGetDouble(out var floating)) return TryAddDouble(floating);
            return TryAddNumber(item.Value);
        }

        private bool TryAddInteger(long integer)
        {
            if (_phase == SumPhase.Float)
            {
                // Python 3.13 adds small integers without compensating them.
                _floating += integer;
                _numericAddition = true;
                return true;
            }
            if (TryAccumulateSumInteger(ref _integer, integer))
            {
                _numericAddition = true;
                return true;
            }
            // Exact promotion uses the same guarded general addition.
            Total = Result;
            _numericAddition = false;
            return false;
        }

        internal bool TryAddNumber(object item)
        {
            if (_phase == SumPhase.Integer)
            {
                if (TryInt64(item, out var integer)) return TryAddInteger(integer);
                // At most 64 bits: materialization uses the existing minor-scalar
                // headroom. Substantial integer promotion keeps its preflight.
                Total = Result;
                _numericAddition = false;
                return false;
            }
            if (_phase != SumPhase.Float) return false;
            if (item is double value) return TryAddDouble(value);
            if (TryInt64(item, out var small)) return TryAddInteger(small);

            // A genuine general addition sees the completed corrected prefix.
            // A later double result never restarts compensation from this phase.
            Total = Result;
            _numericAddition = false;
            _correction = 0;
            _phase = SumPhase.General;
            return false;
        }

        private bool TryAddDouble(double value)
        {
            var previous = _floating;
            var next = previous + value;
            _correction += Math.Abs(previous) >= Math.Abs(value)
                ? (previous - next) + value : (value - next) + previous;
            _floating = next;
            _numericAddition = true;
            return true;
        }

        internal void CompleteAddition(object item, object result)
        {
            if (_phase == SumPhase.Integer)
            {
                if (result is double floating)
                {
                    _phase = SumPhase.Float;
                    _floating = floating;
                }
                else if (!(TryInt64(item, out _) && TryInt64(result, out _integer))) _phase = SumPhase.General;
            }
            Total = result;
            _numericAddition = false;
        }

        internal static bool TryInt64(object value, out long result)
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

    private static bool TryAccumulateSumInteger(ref long subtotal, long integer)
    {
        var next = unchecked(subtotal + integer);
        // Overflow changes the result's sign relative to both operands.
        if (((subtotal ^ next) & (integer ^ next)) < 0) return false;
        subtotal = next;
        return true;
    }

    private static readonly Func<object, LythonSourceSpan, ExecutionContext, ExecutableSlot> SumBytesOperation
        = static (bytes, span, context) => ExecutableSlot.FromObject(SumExactBytes((PyBytes)bytes, BigInteger.Zero, span, context));

    private static bool TrySumExactBytes(object value, object start, LythonSourceSpan span, ExecutionContext context, out object result)
    {
        result = start;
        if (value is not PyBytes bytes || !PyNumberOps.IsInteger(start)) return false;
        result = SumExactBytes(bytes, start, span, context);
        return true;
    }

    private static object SumExactBytes(PyBytes bytes, object start, LythonSourceSpan span, ExecutionContext context)
    {
        var result = start;
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
        return result;
    }

    private static object ReadSumStart(ReadOnlySpan<object> arguments, LythonSourceSpan span)
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
