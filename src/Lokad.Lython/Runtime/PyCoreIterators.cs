using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyRange : IPyIterableValue, IPySliceableValue, IPySubscriptableValue, IPyRenderableValue, IPyTruthyValue, IPyHashableValue
{
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly ChargeReclamationPool? _pool;
    private readonly LythonSourceSpan? _allocationSpan;

    public PyRange(BigInteger start, BigInteger stop, BigInteger step)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    // Governed ranges own their heap-scale yields through these reservations:
    // each yielded magnitude below commits its payload and pool-tracks the box
    // that flows onward, so dropped iterations reclaim on sweep. Ungoverned
    // ranges (annotation defaults) never yield heap magnitudes.
    public PyRange(BigInteger start, BigInteger stop, BigInteger step, MemoryGovernor governor, ChargeReclamationPool pool, LythonSourceSpan? allocationSpan)
    {
        Start = start;
        Stop = stop;
        Step = step;
        _memoryGovernor = governor;
        _pool = pool;
        _allocationSpan = allocationSpan;
    }

    public BigInteger Start { get; }
    public BigInteger Stop { get; }
    public BigInteger Step { get; }

    // Shell coupon matching the range() factory below: the object slot plus
    // heap payloads of the retained bound limbs.
    internal long CommittedStorageBytes => 64L + BoundMagnitudeBytes(Start) + BoundMagnitudeBytes(Stop) + BoundMagnitudeBytes(Step);

    private static long BoundMagnitudeBytes(BigInteger bound)
        => RuntimeMemoryEstimates.GetMagnitudeBitLength(bound) > 64
            ? RuntimeMemoryEstimates.EstimateBigIntegerBytes(bound)
            : 0;

    // Boxes one produced magnitude: the single box that flows onward owns its
    // payload through the shared fresh-magnitude rule, so sweeps release it
    // once dropped. Small magnitudes and ungoverned ranges stay free.
    private object OwnYield(BigInteger value)
    {
        object box = value;
        if (_memoryGovernor is not null && _pool is not null)
        {
            LythonRuntime.OwnFreshInteger(box, _memoryGovernor, _pool, _allocationSpan);
        }

        return box;
    }
    public BigInteger Length => Step > 0
        ? Stop <= Start ? 0 : (Stop - Start - 1) / Step + 1
        : Stop >= Start ? 0 : (Start - Stop - 1) / -Step + 1;

    public bool IsTruthy() => Length != 0;

    // Equal ranges share length, start and (past singletons) step, so
    // hash exactly those components and nothing else.
    public int GetPyHashCode()
    {
        var hash = new HashCode();
        hash.Add(Length);
        if (!Length.IsZero)
        {
            hash.Add(Start);
            if (Length != BigInteger.One)
            {
                hash.Add(Step);
            }
        }

        return hash.ToHashCode();
    }

    public IEnumerable<object> Iterate()
    {
        for (var value = Start; Step > 0 ? value < Stop : value > Stop; value += Step)
        {
            yield return OwnYield(value);
        }
    }

    public object GetSubscript(object index, LythonSourceSpan span)
    {
        if (!Numbers.PyNumberOps.TryAsInteger(index, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "range indices must be integers", span);
        }

        if (integer < 0)
        {
            integer += Length;
        }

        if (integer < 0 || integer >= Length)
        {
            throw new LythonRuntimeException("IndexError", "range object index out of range", span);
        }

        return OwnYield(Start + integer * Step);
    }

    public object GetSlice(object? start, object? end, object? step, LythonSourceSpan span)
    {
        var sliceStep = Bound(step, BigInteger.One, span);
        if (sliceStep == 0)
        {
            throw new LythonRuntimeException("ValueError", "slice step cannot be zero", span);
        }

        if (sliceStep < 0)
        {
            var sliceFrom = AdjustNegativeStart(start, span);
            var sliceTo = AdjustNegativeStop(end, span);
            var sliceLength = sliceFrom > sliceTo ? (sliceFrom - sliceTo - 1) / (-sliceStep) + 1 : BigInteger.Zero;
            var sliceNewStart = Start + sliceFrom * Step;
            return SliceRange(sliceNewStart, sliceNewStart + sliceLength * Step * sliceStep, Step * sliceStep);
        }

        if (sliceStep < 0)
        {
            throw new LythonRuntimeException("NotImplementedError", "negative range slice steps are not supported", span);
        }

        var from = Bound(start, BigInteger.Zero, span);
        var to = Bound(end, Length, span);
        if (from < 0) from = BigInteger.Max(BigInteger.Zero, from + Length);
        if (to < 0) to = BigInteger.Max(BigInteger.Zero, to + Length);
        from = BigInteger.Min(from, Length);
        to = BigInteger.Min(to, Length);
        var newStart = Start + from * Step;
        var newStop = Start + BigInteger.Max(from, to) * Step;
        return SliceRange(newStart, newStop, Step * sliceStep);
    }

    // Slice results own a fresh shell coupon over their recomputed bounds and
    // pool-track it with refund, so a dropped slice reclaims instead of
    // stranding. Ungoverned ranges keep the plain construction.
    private PyRange SliceRange(BigInteger newStart, BigInteger newStop, BigInteger newStep)
    {
        if (_memoryGovernor is null || _pool is null)
        {
            return new PyRange(newStart, newStop, newStep);
        }

        var bytes = 64L + BoundMagnitudeBytes(newStart) + BoundMagnitudeBytes(newStop) + BoundMagnitudeBytes(newStep);
        _memoryGovernor.Reserve(bytes, _allocationSpan);
        _memoryGovernor.Commit(bytes);
        var child = new PyRange(newStart, newStop, newStep, _memoryGovernor, _pool, _allocationSpan);
        _pool.TrackFreshMutable(child, bytes, _allocationSpan);
        return child;
    }

    private BigInteger AdjustNegativeStart(object? value, LythonSourceSpan span)
    {
        // Mirror CPython slice.indices for negative steps: omitted starts
        // begin past the end, out-of-range values clamp inside.
        var bound = value is null || value is PyNone ? Length - 1 : Bound(value, BigInteger.Zero, span);
        if (bound < 0)
        {
            bound += Length;
        }

        if (bound < 0)
        {
            return BigInteger.MinusOne;
        }

        if (bound >= Length)
        {
            return Length - 1;
        }

        return bound;
    }

    private BigInteger AdjustNegativeStop(object? value, LythonSourceSpan span)
    {
        // Omitted stops end before the beginning for negative steps.
        if (value is null || value is PyNone)
        {
            return BigInteger.MinusOne;
        }

        var bound = Bound(value, BigInteger.Zero, span);
        if (bound < 0)
        {
            bound += Length;
        }

        if (bound < 0)
        {
            return BigInteger.MinusOne;
        }

        if (bound >= Length)
        {
            return Length - 1;
        }

        return bound;
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        var text = Step == 1
            ? "range(" + Start + ", " + Stop + ")"
            : "range(" + Start + ", " + Stop + ", " + Step + ")";
        return PyString.FromString(text);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private static BigInteger Bound(object? value, BigInteger fallback, LythonSourceSpan span)
        => value is null || value is PyNone
            ? fallback
            : Numbers.PyNumberOps.TryAsInteger(value, out var integer)
                ? integer
                : throw new LythonRuntimeException("TypeError", "slice indices must be integers", span);
}

internal sealed class PyEnumerateIterator : PyIteratorBase
{
    private readonly MemoryGovernor _governor;
    private readonly ChargeReclamationPool _pool;
    private readonly LythonSourceSpan _span;
    private readonly PyIteration.Cursor _cursor;
    private BigInteger _index;

    public PyEnumerateIterator(object iterable, BigInteger start, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _cursor = PyIteration.Cursor.Create(iterable, span, context);
        _governor = context.MemoryGovernor;
        _pool = context.Services.State.CallTemporaries;
        _span = span;
        _index = start;
        ChargeIteratorValue(context.MemoryGovernor, span);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (!_cursor.TryMoveNext(out var item))
        {
            value = PyNone.Instance;
            return false;
        }

        // Each yielded pair is a fresh governed tuple: track it at this factory
        // so dropped items reclaim through the pool; later registrations dedup.
        // The boxed index is one box from here on: heap-scale counters own their
        // magnitude the same way instead of riding free beside the tuple.
        object indexBox = _index;
        var produced = new PyTuple([indexBox, item], _governor, _span);
        LythonRuntime.OwnFreshInteger(indexBox, _governor, _pool, _span);
        // Consume the index with the item: a denied track below orphans the tuple
        // after the cursor advanced, so the index must advance too to stay aligned.
        _index++;
        _pool.TrackFreshMutable(produced, produced.CommittedStorageBytes, _span);
        value = produced;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var (hasValue, item) = await _cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        // Same fresh-tuple ownership as the sync path above.
        object indexBox = _index;
        var produced = new PyTuple([indexBox, item], _governor, _span);
        LythonRuntime.OwnFreshInteger(indexBox, _governor, _pool, _span);
        _index++;
        _pool.TrackFreshMutable(produced, produced.CommittedStorageBytes, _span);
        return PyIterationResult.Yield(produced);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<enumerate object>");
}

internal sealed class PyZipIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor[] _cursors;
    private readonly MemoryGovernor _governor;
    private readonly ChargeReclamationPool _pool;
    private readonly bool _strict;
    private readonly LythonSourceSpan _span;
    private bool _finished;

    public PyZipIterator(object[] iterables, bool strict, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _cursors = iterables.Select(value => PyIteration.Cursor.Create(value, span, context)).ToArray();
        _governor = context.MemoryGovernor;
        _pool = context.Services.State.CallTemporaries;
        _strict = strict;
        _span = span;
        ChargeIteratorValue(context.MemoryGovernor, span);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_finished || _cursors.Length == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        var items = new object[_cursors.Length];
        for (var i = 0; i < _cursors.Length; i++)
        {
            if (_cursors[i].TryMoveNext(out var item))
            {
                items[i] = item;
                continue;
            }

            _finished = true;
            if (_strict)
            {
                // Like CPython, an exhausted cursor behind earlier yielders
                // is shorter; only the first cursor peeks ahead for a longer
                // tail, naming the first diverging argument.
                if (i > 0)
                {
                    throw new LythonRuntimeException("ValueError", "zip() argument " + (i + 1) + " is shorter than " + StrictOthers(i), _span);
                }

                for (var j = i + 1; j < _cursors.Length; j++)
                {
                    if (_cursors[j].TryMoveNext(out _))
                    {
                        throw new LythonRuntimeException("ValueError", "zip() argument " + (j + 1) + " is longer than " + StrictOthers(j), _span);
                    }
                }
            }

            value = PyNone.Instance;
            return false;
        }

        // Each yielded item is a fresh governed tuple: track it at this factory
        // so dropped items reclaim through the pool; later registrations dedup.
        var produced = new PyTuple(items, _governor, _span);
        _pool.TrackFreshMutable(produced, produced.CommittedStorageBytes, _span);
        value = produced;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_finished || _cursors.Length == 0)
        {
            return PyIterationResult.End;
        }

        var items = new object[_cursors.Length];
        for (var i = 0; i < _cursors.Length; i++)
        {
            var (hasValue, item) = await _cursors[i].TryMoveNextAsync().ConfigureAwait(false);
            if (hasValue)
            {
                items[i] = item;
                continue;
            }

            _finished = true;
            if (_strict)
            {
                // Same strict tail-peeking as the sync path above.
                if (i > 0)
                {
                    throw new LythonRuntimeException("ValueError", "zip() argument " + (i + 1) + " is shorter than " + StrictOthers(i), _span);
                }

                for (var j = i + 1; j < _cursors.Length; j++)
                {
                    var (laterHasValue, _) = await _cursors[j].TryMoveNextAsync().ConfigureAwait(false);
                    if (laterHasValue)
                    {
                        throw new LythonRuntimeException("ValueError", "zip() argument " + (j + 1) + " is longer than " + StrictOthers(j), _span);
                    }
                }
            }

            return PyIterationResult.End;
        }

        // Same fresh-tuple ownership as the sync path above.
        var produced = new PyTuple(items, _governor, _span);
        _pool.TrackFreshMutable(produced, produced.CommittedStorageBytes, _span);
        return PyIterationResult.Yield(produced);
    }

    private static string StrictOthers(int index)
        => index == 1 ? "argument 1" : "arguments 1-" + index;

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<zip object>");
}
