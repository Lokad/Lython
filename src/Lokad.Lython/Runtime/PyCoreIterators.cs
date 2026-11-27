using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyRange : IPyIterableValue, IPySliceableValue, IPySubscriptableValue, IPyRenderableValue, IPyTruthyValue, IPyHashableValue, IEnumerable<object>
{
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;

    public PyRange(BigInteger start, BigInteger stop, BigInteger step)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    public PyRange(BigInteger start,BigInteger stop,BigInteger step,MemoryGovernor governor,LythonSourceSpan? allocationSpan)
    {
        Start = start;
        Stop = stop;
        Step = step;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public BigInteger Start { get; }
    public BigInteger Stop { get; }
    public BigInteger Step { get; }

    // Shell storage charge matching the range() factory below: the object slot plus
    // heap payloads of the retained bound limbs.
    internal long CommittedStorageBytes => 64L + BoundMagnitudeBytes(Start) + BoundMagnitudeBytes(Stop) + BoundMagnitudeBytes(Step);

    private static long BoundMagnitudeBytes(BigInteger bound)
        => RuntimeMemoryEstimates.GetMagnitudeBitLength(bound) > 64
            ? RuntimeMemoryEstimates.EstimateBigIntegerBytes(bound)
            : 0;

    private object OwnYield(BigInteger value)
    {
        object box = value;
        if (_memoryGovernor is not null)
        {
            LythonRuntime.OwnFreshInteger(box, _memoryGovernor, _allocationSpan);
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

    public IEnumerable<object> Iterate() => this;

    internal Enumerator GetEnumerator() => new(this);

    IEnumerator<object> IEnumerable<object>.GetEnumerator() => GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // One position serves ordinary object pulls and native machine pulls. Only
    // observing Current boxes a machine value; wider yields retain their eager
    // admission and publication. Each enumeration owns independent state.
    internal sealed class Enumerator : ExecutableTemporaryValue, IEnumerator<object>
    {
        private readonly PyRange _range;
        private readonly long _machineStep;
        private long _machineValue;
        private ulong _remaining;
        private BigInteger _value;
        private object? _current;
        private bool _started;
        private bool _finished;

        internal Enumerator(PyRange range)
        {
            _range = range;
            HasMachineBounds = !range.Step.IsZero && range.Start.GetByteCount() <= sizeof(long)
                && range.Stop.GetByteCount() <= sizeof(long) && range.Step.GetByteCount() <= sizeof(long);
            if (HasMachineBounds)
            {
                _machineValue = (long)range.Start;
                _machineStep = (long)range.Step;
                // The distance between signed machine bounds fits UInt64.
                _remaining = (ulong)range.Length;
            }
        }

        private bool HasMachineBounds { get; }

        public object Current => !_started ? null! : _current ??= _range.OwnYield((BigInteger)_machineValue);
        object System.Collections.IEnumerator.Current => Current;
        public void Reset() => throw new NotSupportedException();
        public override void Dispose() => _finished = true;

        internal PyIterationResult TryMoveNext()
        {
            if (!MoveNext()) return PyIterationResult.End;
            return HasMachineBounds
                ? PyIterationResult.Yield(LythonRuntime.ExecutableSlot.FromInteger(_machineValue))
                : PyIterationResult.Yield(Current);
        }

        public bool MoveNext()
        {
            if (_finished) return false;
            // Like the generated iterator's executing state, this also makes
            // reentrant pulls during major-yield admission terminal and prevents
            // retrying a failed pull. Publish only after admission succeeds.
            _finished = true;
            if (HasMachineBounds)
            {
                if (_remaining == 0) return false;
                if (_started) _machineValue = unchecked(_machineValue + _machineStep);
                // A successor exists only while it remains within the original
                // signed bounds. Do not compute a final out-of-range successor.
                _remaining--;
                _current = null;
            }
            else
            {
                var next = _started ? _value + _range.Step : _range.Start;
                if (_range.Step > 0 ? next >= _range.Stop : next <= _range.Stop) return false;
                var current = _range.OwnYield(next);
                _value = next;
                _current = current;
            }
            _started = true;
            _finished = false;
            return true;
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

    private PyRange SliceRange(BigInteger newStart, BigInteger newStop, BigInteger newStep)
    {
        if (_memoryGovernor is null)
        {
            return new PyRange(newStart, newStop, newStep);
        }

        var bytes = 64L + BoundMagnitudeBytes(newStart) + BoundMagnitudeBytes(newStop) + BoundMagnitudeBytes(newStep);
        _memoryGovernor.Reserve(bytes, _allocationSpan);
        _memoryGovernor.Commit(bytes);
        var child = new PyRange(newStart,newStop,newStep,_memoryGovernor,_allocationSpan);
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

internal sealed class PyEnumerateIterator : PyIteratorBase, IPyAsyncIteratorValue
{
    private readonly MemoryGovernor _governor;
    private readonly LythonSourceSpan _span;
    private readonly PyIteration.Cursor _cursor;
    private BigInteger _index;

    public PyEnumerateIterator(object iterable, BigInteger start, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _cursor = PyIteration.Cursor.Create(iterable, span, context);
        _governor = context.MemoryGovernor;
        _span = span;
        _index = start;
        ChargeIteratorValue(context.MemoryGovernor, span);
    }

    public override PyIterationResult TryMoveNext()
    {
        if (!(_cursor.TryMoveNext() is { HasValue: true, Value: var item }))
        {
            return PyIterationResult.End;
        }

        object indexBox = _index;
        // The item has been consumed. Advance before tuple admission so a
        // refused result cannot reuse its index for the following item.
        AdvanceIndex();
        var produced = new PyTuple([indexBox, item], _governor, _span);
        return PyIterationResult.Yield(produced);
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var (hasValue, item) = await _cursor.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        // Same fresh-tuple ownership as the sync path above.
        object indexBox = _index;
        AdvanceIndex();
        var produced = new PyTuple([indexBox, item], _governor, _span);
        return PyIterationResult.Yield(produced);
    }

    private void AdvanceIndex()
    {
        // The tuple owns its exposed old index. Fund the next retained magnitude
        // before addition allocates; ordinary counters need no extra result box.
        if (_index.GetByteCount() < 8) _index++;
        else _index = (BigInteger)LythonRuntime.AddExactIntegers(_index, BigInteger.One, _governor, _span);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<enumerate object>");
}

internal sealed class PyZipIterator : PyIteratorBase, IPyAsyncIteratorValue
{
    private PyIteration.Cursor[] _cursors;
    private readonly MemoryGovernor _governor;
    private readonly bool _strict;
    private readonly LythonSourceSpan _span;
    private bool _finished;
    private long _storageBytes;

    internal long CommittedStorageBytes => _storageBytes;

    public PyZipIterator(object[] iterables, bool strict, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        : this(iterables.Length, strict, span, context)
    {
        try { InitializeCursors(iterables, context); }
        catch { DiscardUnpublishedStorage(); throw; }
    }

    // Allocate the actual owner and preflight its cursor storage before any
    // source __iter__ can run.
    internal PyZipIterator(int count, bool strict, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _governor = context.MemoryGovernor;
        _strict = strict;
        _span = span;
        var bytes = checked(IteratorValueBytes + 32L + 256L * count);
        _governor.Reserve(bytes, span);
        try { _cursors = new PyIteration.Cursor[count]; }
        catch { _governor.ReleaseReserved(bytes); throw; }
        _governor.Commit(bytes);
        _storageBytes = bytes;
    }

    internal void InitializeCursors(object[] iterables, LythonRuntime.ExecutionContext context)
    {
        for (var i = 0; i < iterables.Length; i++)
            _cursors[i] = PyIteration.Cursor.Create(iterables[i], _span, context);
    }

    internal void InitializeCursors(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context)
    {
        var index = 0;
        foreach (var argument in arguments)
            if (argument.IsPositional) _cursors[index++] = PyIteration.Cursor.Create(argument.Value, _span, context);
    }

    internal void DiscardUnpublishedStorage()
    {
        _cursors = [];
        var bytes = _storageBytes;
        _storageBytes = 0;
        _governor.Release(bytes);
    }

    public override PyIterationResult TryMoveNext()
    {
        if (!CanPull) return PyIterationResult.End;
        var funding = checked(32L + 80L * Arity);
        _governor.Reserve(funding, _span);
        try
        {
            var items = new object[Arity];
            return Pull(new(items, default, reverse: false))
                ? PyIterationResult.Yield(PyTuple.FromOwnedArray(items, _governor, _span)) : PyIterationResult.End;
        }
        finally { _governor.ReleaseReserved(funding); }
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (!CanPull) return PyIterationResult.End;
        var funding = checked(32L + 80L * Arity);
        _governor.Reserve(funding, _span);
        try
        {
            var items = new object[Arity];
            return await PullBufferedAsync(items, default).ConfigureAwait(false) >= 0
                ? PyIterationResult.Yield(PyTuple.FromOwnedArray(items, _governor, _span)) : PyIterationResult.End;
        }
        finally { _governor.ReleaseReserved(funding); }
    }

    internal int Arity => _cursors.Length;

    private bool CanPull => !_finished && Arity != 0;

    internal int UnpackNext(Memory<LythonRuntime.ExecutableSlot> destination)
        => CanPull && Pull(new(null, destination.Span, reverse: true)) ? Arity : -1;

    internal ValueTask<int> UnpackNextAsync(Memory<LythonRuntime.ExecutableSlot> destination)
        => CanPull ? PullBufferedAsync(null, destination) : new(-1);

    internal bool TryPullOperands(ref LythonRuntime.IterationOperands operands, bool asynchronous,
        out bool hasRow, out ValueTask<(bool HasRow, LythonRuntime.IterationOperands Operands)> pending)
    {
        pending = default;
        hasRow = false;
        if (!CanPull) return true;
        PullPosition position = default;
        if (TryCompletePull(new(null, operands, reverse: false), asynchronous, ref position, out hasRow, out var next))
            return true;
        // A suspended pull owns the prefix from this particular advance. A
        // reentrant next() starts with its own storage and the live cursors.
        pending = ResumePullAsync<InlineRow, (bool, LythonRuntime.IterationOperands)>(new(operands), position, next);
        return false;
    }

    // Fixed unpack consumes its first target from the top of the stack. A
    // callback takes the same values in parameter order without making a tuple.
    private readonly ref struct Row(object[]? items, Span<LythonRuntime.ExecutableSlot> slots, bool reverse)
    {
        private readonly Span<LythonRuntime.ExecutableSlot> _slots = slots;

        internal void Set(int index, PyIterationResult item)
        {
            if (items is not null) items[index] = item.Value;
            else _slots[reverse ? _slots.Length - 1 - index : index] = item.TakeSlotValue();
        }
    }

    private struct PullPosition
    {
        internal int Index;
        internal bool CheckingTail;
    }

    // Keep the existing storage in the suspended operation. A row view exists
    // only between awaits and never retains the caller's stack span.
    private interface IPendingRow<TStorage, TResult> where TStorage : struct, IPendingRow<TStorage, TResult>
    {
        static abstract Row Open(ref TStorage storage);
        TResult Complete(int count);
    }

    private readonly struct BufferedRow(object[]? items, Memory<LythonRuntime.ExecutableSlot> destination)
        : IPendingRow<BufferedRow, int>
    {
        private readonly object[]? _items = items;
        private readonly Memory<LythonRuntime.ExecutableSlot> _destination = destination;

        public static Row Open(ref BufferedRow storage) => new(storage._items, storage._destination.Span, reverse: true);
        public int Complete(int count) => count;
    }

    private struct InlineRow(LythonRuntime.IterationOperands operands)
        : IPendingRow<InlineRow, (bool, LythonRuntime.IterationOperands)>
    {
        private LythonRuntime.IterationOperands _operands = operands;

        public static Row Open(ref InlineRow storage) => new(null, storage._operands, reverse: false);
        public readonly (bool, LythonRuntime.IterationOperands) Complete(int count)
            => count < 0 ? default : (true, _operands);
    }

    private bool Pull(Row row)
    {
        PullPosition position = default;
        TryCompletePull(row, false, ref position, out var hasRow, out _);
        return hasRow;
    }

    private ValueTask<int> PullBufferedAsync(object[]? items, Memory<LythonRuntime.ExecutableSlot> destination)
    {
        PullPosition position = default;
        return TryCompletePull(new(items, destination.Span, reverse: true), true, ref position, out var hasRow, out var next)
            ? new(hasRow ? Arity : -1) : ResumePullAsync<BufferedRow, int>(new(items, destination), position, next);
    }

    private bool TryCompletePull(Row row, bool asynchronous, ref PullPosition position, out bool hasRow,
        out ValueTask<PyIterationResult> pending)
    {
        pending = default;
        hasRow = false;
        while (position.Index < Arity)
        {
            var cursor = _cursors[position.Index];
            var next = asynchronous ? cursor.TryMoveNextAsync() : new(cursor.TryMoveNext());
            if (!next.IsCompleted)
            {
                pending = next;
                return false;
            }
            if (!AcceptPull(row, ref position, next.GetAwaiter().GetResult())) return true;
        }
        hasRow = !position.CheckingTail;
        return true;
    }

    private bool AcceptPull(Row row, ref PullPosition position, PyIterationResult item)
    {
        var index = position.Index++;
        if (position.CheckingTail)
        {
            if (item.HasValue) throw UnevenLength(index, "longer");
        }
        else if (item.HasValue) row.Set(index, item);
        else
        {
            _finished = true;
            if (!_strict) return false;
            if (index != 0) throw UnevenLength(index, "shorter");
            position.CheckingTail = true;
        }
        return true;
    }

    private async ValueTask<TResult> ResumePullAsync<TStorage, TResult>(TStorage storage,
        PullPosition position, ValueTask<PyIterationResult> pending)
        where TStorage : struct, IPendingRow<TStorage, TResult>
    {
        while (true)
        {
            var item = await pending.ConfigureAwait(false);
            var row = TStorage.Open(ref storage);
            if (!AcceptPull(row, ref position, item)) return storage.Complete(-1);
            if (TryCompletePull(row, true, ref position, out var hasRow, out pending))
                return storage.Complete(hasRow ? Arity : -1);
        }
    }

    private LythonRuntimeException UnevenLength(int index, string comparison)
        => new("ValueError", "zip() argument " + (index + 1) + " is " + comparison + " than " +
            (index == 1 ? "argument 1" : "arguments 1-" + index), _span);

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<zip object>");
}
