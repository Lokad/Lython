using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyMutableDynamicAttributes, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private readonly PyDict _items;

    public PyDefaultDict(object defaultFactory) : this(defaultFactory, null, null, null) { }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor) : this(defaultFactory, governor, null) { }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(defaultFactory, null, governor, allocationSpan) { }

    public PyDefaultDict(object defaultFactory, PyDict items)
        : this(defaultFactory, items, items.OwnerMemoryGovernor, items.AllocationSpan) { }

    public PyDefaultDict(object defaultFactory, PyDict? source, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        governor?.Reserve(64L, allocationSpan);
        try
        {
            _items = source is null
                ? governor is null ? new PyDict() : new PyDict(governor, allocationSpan)
                : new PyDict(source, governor, allocationSpan);
        }
        catch
        {
            governor?.ReleaseReserved(64L);
            throw;
        }
        governor?.Commit(64L);
    }

    public object DefaultFactory { get; private set; }

    public int Count => _items.Count;

    object IChainMapSource.Underlying => this;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    internal PyDict InnerDict => _items;

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => _items.TryGetValue(key, out value, context, span);

    public object GetOrCreate(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        _items.AttachMemoryGovernor(context.MemoryGovernor, span);
        if (_items.TryGetValue(key, out var value))
        {
            return value;
        }

        var created = DefaultFactory switch
        {
            PyNone => throw RuntimeErrors.MissingKey(key, span),
            LythonRuntime.ICallable callable => LythonRuntime.RuntimeValue(callable.Invoke([], span, context)),
            _ => throw new LythonRuntimeException("TypeError", "defaultdict default_factory must be callable or None.", span)
        };

        _items.SetItem(key, created);
        context.CheckExecution(span);
        return created;
    }

    // The inner dictionary refreshes this single snapshot on every storage change
    // and keeps the wrapper alive for escaped views and iterators.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : 64L + _items.CommittedStorageBytes;

    public void SetItem(object key, object value) => _items.SetItem(key, value);

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (OwnerMemoryGovernor is null)
        {
            governor.Reserve(64L, allocationSpan);
            governor.Commit(64L);
            try { _items.AttachMemoryGovernor(governor, allocationSpan); }
            catch
            {
                governor.Release(64L);
                throw;
            }
            return;
        }

        _items.AttachMemoryGovernor(governor, allocationSpan);
    }

    public bool Remove(object key) => _items.Remove(key);

    public bool TryRemove(object key, [MaybeNullWhen(false)] out object value) => _items.TryRemove(key, out value);

    public bool TryRemoveLast([MaybeNullWhen(false)] out object key, [MaybeNullWhen(false)] out object value)
        => _items.TryRemoveLast(out key, out value);

    public object UpdateFrom(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => LythonRuntime.UpdateDictionary(_items, arguments, span, context);

    public async ValueTask<object> UpdateFromAsync(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => await LythonRuntime.UpdateDictionaryAsync(_items, arguments, span, context).ConfigureAwait(false);

    public void Clear() => _items.Clear();

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        if (name == "default_factory")
        {
            value = DefaultFactory;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public bool TrySetMember(string name, object value)
    {
        if (name != "default_factory")
        {
            return false;
        }

        DefaultFactory = ValidateDefaultFactory(value);
        return true;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context)
    {
        var factory = DefaultFactory switch
        {
            PyNone => PyStringOps.NoneLiteral,
            _ => PyRendering.ToPythonPyString(DefaultFactory, context)
        };
        return PyRendering.JoinRenderedSequence(
            "defaultdict(",
            [factory, PyRendering.JoinRenderedDictionary(_items, context, interpolated: false)],
            ")", context);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static object ValidateDefaultFactory(object value)
        => value switch
        {
            PyNone => value,
            LythonRuntime.ICallable => value,
            _ => throw new LythonRuntimeException("TypeError", "defaultdict default_factory must be callable or None.", null)
        };
}
internal sealed class PyCounter : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private readonly PyDict _items;

    public PyCounter() : this((PyDict?)null, null, null) { }

    public PyCounter(MemoryGovernor governor) : this(governor, null) { }

    public PyCounter(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this((PyDict?)null, governor, allocationSpan) { }

    public PyCounter(PyCounter other)
        : this(other._items, other.OwnerMemoryGovernor, other.AllocationSpan) { }

    public PyCounter(PyCounter other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyCounter(PyCounter other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(other._items, governor, allocationSpan) { }

    private PyCounter(PyDict? source, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        governor?.Reserve(64L, allocationSpan);
        try
        {
            _items = source is null
                ? governor is null ? new PyDict() : new PyDict(governor, allocationSpan)
                : new PyDict(source, governor, allocationSpan);
        }
        catch
        {
            governor?.ReleaseReserved(64L);
            throw;
        }
        governor?.Commit(64L);
    }

    public int Count => _items.Count;

    object IChainMapSource.Underlying => this;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _items.OwnerMemoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _items.AllocationSpan;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public IEnumerable<KeyValuePair<object, object>> Items => _items;

    internal PyDict InnerDict => _items;

    public object GetCount(object key) => _items.TryGetValue(key, out var value) ? value : BigInteger.Zero;

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => _items.TryGetValue(key, out value, context, span);

    // The inner dictionary refreshes this single snapshot on every storage change
    // and keeps the wrapper alive for escaped views and iterators.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : 64L + _items.CommittedStorageBytes;

    public void SetItem(object key, object value) => _items.SetItem(key, value);

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (OwnerMemoryGovernor is null)
        {
            governor.Reserve(64L, allocationSpan);
            governor.Commit(64L);
            try { _items.AttachMemoryGovernor(governor, allocationSpan); }
            catch
            {
                governor.Release(64L);
                throw;
            }
            return;
        }

        _items.AttachMemoryGovernor(governor, allocationSpan);
    }

    public bool Remove(object key) => _items.Remove(key);

    public bool TryRemove(object key, [MaybeNullWhen(false)] out object value) => _items.TryRemove(key, out value);

    public void Clear() => _items.Clear();

    public void Increment(object key,object delta,LythonSourceSpan span)
    {
        LythonRuntime.ValidateDictionaryKey(key, span);
        var incoming = _items.TryGetValue(key, out var value)
            ? LythonRuntime.AddCounterCounts(value,delta,span,OwnerMemoryGovernor) : delta;
        _items.SetItem(key, incoming);
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context)
    {
        if (Count == 0)
        {
            return PyString.FromString("Counter()", context.Context.MemoryGovernor);
        }

        // Like CPython repr, entries order by most-common count with ties
        // in insertion order; unorderable values fall back to insertion
        // order instead of failing rendering.
        List<KeyValuePair<object, object>> ordered;
        try
        {
            ordered = _items
                .OrderByDescending(pair => pair.Value, Comparer<object>.Create((left, right) => CompareCountsForRender(left, right)))
                .ToList();
        }
        // OrderBy wraps comparer failures, so the handler keys off the
        // documented inner exception instead of the surface type: ordering
        // failures fall back to insertion, while a decimal InvalidOperation
        // propagates like CPython repr instead of masking as a failed render.
        catch (InvalidOperationException ex) when (ex.InnerException is LythonRuntimeException lythonFailure &&
            (lythonFailure.ExceptionType is "TypeError"
            || lythonFailure.Identity == LythonRuntime.ModuleException("decimal", "InvalidOperation")))
        {
            if (lythonFailure.ExceptionType is "TypeError")
            {
                ordered = _items.ToList();
            }
            else
            {
                throw lythonFailure;
            }
        }

        return PyRendering.JoinRenderedSequence("Counter(", [PyRendering.JoinRenderedDictionary(ordered, context, interpolated: false)], ")", context);
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    // Counts order like most_common: decimal-involved pairs compare exactly
    // across domains first (mirroring PyComparison, including its failure),
    // then plain numerics, then strings alphabetically; anything else raises
    // the shared not-comparable TypeError so rendering falls back to
    // insertion order.
    private static int CompareCountsForRender(object left, object right)
    {
        if ((left is PyDecimal || right is PyDecimal) && PyDecimalOps.TryCompareMixed(left, right, out var mixedDecimal))
        {
            return mixedDecimal;
        }

        if (PyNumberOps.TryAsNumber(left, out var lhs) && PyNumberOps.TryAsNumber(right, out var rhs))
        {
            return PyNumberOps.Compare(lhs, rhs);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return PyString.CompareOrdinal(leftText, rightText);
        }

        // NaN against a decimal propagates decimal.InvalidOperation
        // instead of falling back, mirroring the comparison itself.
        PyDecimalOps.ThrowIfNanComparison(left, right, null);
        throw new LythonRuntimeException("TypeError", "Values are not comparable.", null);
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Typed deque state signals (N16): thrown where the deque itself reports empty/full
// (never for user-raised failures, even with identical text), so member bindings
// recognize them by type instead of comparing English message text. Deriving from
// InvalidOperationException keeps any unknown CLR-level catcher behaving as before.
// The public IndexError identity and message flow from these origins like any
// guest-visible failure.
internal sealed class PyDequeEmptyException : InvalidOperationException
{
    public PyDequeEmptyException(string message)
        : base(message)
    {
    }
}

internal sealed class PyDequeFullException : InvalidOperationException
{
    public PyDequeFullException(string message)
        : base(message)
    {
    }
}

internal sealed class PyDeque : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private readonly LinkedList<object> _items = [];

    // Each live node owns its links plus conservative small-value headroom.
    // Aliases and nonnumeric values pay the same charge; separately governed
    // payloads retain their owners. No scalar identity map is needed.
    // The wrapper plus its empty list object (~96 B measured) owns one shell charge
    // per live instance at the constructed-function shell rate, matching sets.
    // Clear releases nodes while the shell persists, and regrowth re-charges.
    private const long DequeNodeBytes = 128;
    private const long DequeShellBytes = 128;
    private bool _shellCharged;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedNodeBytes;

    public PyDeque() : this((int?)null) { }

    public PyDeque(int? maxLength)
    {
        MaxLength = maxLength;
    }

    // Owns the shell once: construction and first-attach are the only paths
    // that introduce a governed deque, so the flag makes each distinct object
    // pay exactly once while aliases and re-attaches ride free.
    private void ChargeShell()
    {
        if (_shellCharged || _memoryGovernor is null)
        {
            return;
        }

        _memoryGovernor.Reserve(DequeShellBytes, _allocationSpan);
        _memoryGovernor.Commit(DequeShellBytes);
        _shellCharged = true;
    }

    // Shell and current node funding for unpublished rollback. Explicit node
    // removal can refund its storage; the shell remains charged to the batch.
    internal long CommittedStorageBytes => (_shellCharged ? DequeShellBytes : 0) + _committedNodeBytes;

    public PyDeque(int? maxLength, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        MaxLength = maxLength;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        ChargeShell();
    }

    public PyDeque(IEnumerable<object> items) : this(items, null) { }

    public PyDeque(IEnumerable<object> items, int? maxLength)
    {
        MaxLength = maxLength;
        foreach (var item in items)
        {
            Append(item);
        }
    }

    public PyDeque(IEnumerable<object> items, int? maxLength, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(maxLength, governor, allocationSpan)
    {
        // A denied node strands nothing: Append rolls back its own node, and
        // the refund below releases earlier items plus the shell.
        try
        {
            foreach (var item in items)
            {
                Append(item);
            }
        }
        catch
        {
            RefundAbortedConstruction();
            throw;
        }
    }

    public int? MaxLength { get; }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is not null)
        {
            return;
        }

        // Refusal leaves the deque ungoverned and retryable. Fund every existing
        // node and its retained-value allowance before publishing the owner.
        var nodeBytes = checked((long)_items.Count * DequeNodeBytes);
        var span = _allocationSpan ?? allocationSpan;
        var bytes = checked(DequeShellBytes + nodeBytes);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
        _memoryGovernor = governor;
        _allocationSpan = span;
        _shellCharged = true;
        _committedNodeBytes = nodeBytes;
    }

    public int Count => _items.Count;

    public int Length => Count;

    public object this[int index] => GetItem(index);

    public void Append(object value)
    {
        if (MaxLength == 0)
        {
            return;
        }

        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            var evicted = _items.First.RequireNotNull();
            _items.Remove(evicted);
            evicted.Value = value;
            _items.AddLast(evicted);
            return;
        }

        ReserveNode();
        try
        {
            _items.AddLast(value);
        }
        catch (Exception)
        {
            ReleaseNode();
            throw;
        }
    }

    public void AppendLeft(object value)
    {
        if (MaxLength == 0)
        {
            return;
        }

        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            var evicted = _items.Last.RequireNotNull();
            _items.Remove(evicted);
            evicted.Value = value;
            _items.AddFirst(evicted);
            return;
        }

        ReserveNode();
        try
        {
            _items.AddFirst(value);
        }
        catch (Exception)
        {
            ReleaseNode();
            throw;
        }
    }

    public object Pop()
    {
        if (_items.Count == 0)
        {
            throw new PyDequeEmptyException("pop from an empty deque");
        }

        var value = _items.Last.RequireNotNull().Value;
        _items.RemoveLast();
        ReleaseNode();
        return value;
    }

    public object PopLeft()
    {
        if (_items.Count == 0)
        {
            throw new PyDequeEmptyException("pop from an empty deque");
        }

        var value = _items.First.RequireNotNull().Value;
        _items.RemoveFirst();
        ReleaseNode();
        return value;
    }

    public void Extend(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            Append(value);
        }
    }

    public void ExtendLeft(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            AppendLeft(value);
        }
    }

    public void RepeatInPlace(int count, LythonSourceSpan span)
    {
        if (count <= 0 || Count == 0)
        {
            Clear();
            return;
        }

        var totalLength = (long)Count * count;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "Deque repetition is too large.", span);
        }

        // Snapshot the source (which may be this deque) so repeating
        // appends the original elements; clearing first releases the live
        // charges that the re-appends below recommit through Append.
        var snapshot = _items.ToArray();
        Clear();
        for (var i = 0; i < count; i++)
        {
            foreach (var item in snapshot)
            {
                Append(item);
            }
        }
    }

    public int CountValue(object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var count = 0;
        foreach (var item in _items)
        {
            if (LythonRuntime.MembershipEquals(item, candidate, context, span))
            {
                count++;
            }
        }

        return count;
    }

    public int IndexOf(object candidate, int start, int stop, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var index = 0;
        foreach (var item in _items)
        {
            if (index >= start && index < stop && LythonRuntime.MembershipEquals(item, candidate, context, span))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public async ValueTask<int> CountValueAsync(object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var count = 0;
        foreach (var item in _items)
        {
            if (await LythonRuntime.MembershipEqualsAsync(item, candidate, context, span).ConfigureAwait(false))
            {
                count++;
            }
        }

        return count;
    }

    public async ValueTask<int> IndexOfAsync(object candidate, int start, int stop, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var index = 0;
        foreach (var item in _items)
        {
            if (index >= start && index < stop && await LythonRuntime.MembershipEqualsAsync(item, candidate, context, span).ConfigureAwait(false))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    public async ValueTask<bool> RemoveValueAsync(object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var current = _items.First;
        while (current is not null)
        {
            if (await LythonRuntime.MembershipEqualsAsync(current.Value, candidate, context, span).ConfigureAwait(false))
            {
                _items.Remove(current);
                ReleaseNode();
                return true;
            }

            current = current.Next;
        }

        return false;
    }

    public void Insert(int index, object value)
    {
        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            throw new PyDequeFullException("deque already at its maximum size");
        }

        ReserveNode();
        try
        {
            if (index <= 0)
            {
                _items.AddFirst(value);
            }
            else if (index >= _items.Count)
            {
                _items.AddLast(value);
            }
            else
            {
                _items.AddBefore(GetNodeAt(index), value);
            }
        }
        catch (Exception)
        {
            ReleaseNode();
            throw;
        }
    }

    public bool RemoveValue(object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var current = _items.First;
        while (current is not null)
        {
            if (LythonRuntime.MembershipEquals(current.Value, candidate, context, span))
            {
                _items.Remove(current);
                ReleaseNode();
                return true;
            }

            current = current.Next;
        }

        return false;
    }

    public void Reverse()
    {
        // Swap values in place: the node count never changes, so no charging
        // is needed and no snapshot array escapes ownership.
        if (_items.Count <= 1)
        {
            return;
        }

        var forward = _items.First.RequireNotNull();
        var backward = _items.Last.RequireNotNull();
        for (var i = 0; i < _items.Count / 2; i++)
        {
            (forward.Value, backward.Value) = (backward.Value, forward.Value);
            forward = forward.Next.RequireNotNull();
            backward = backward.Previous.RequireNotNull();
        }
    }

    public void Rotate(BigInteger offset)
    {
        if (_items.Count == 0 || offset == 0)
        {
            return;
        }

        var steps = (int)(offset % _items.Count);
        if (steps > _items.Count / 2)
        {
            steps -= _items.Count;
        }
        else if (steps < -_items.Count / 2)
        {
            steps += _items.Count;
        }

        if (steps > 0)
        {
            // Rotate reuses nodes: pop and re-add balance exactly, so keep the
            // committed charge flat instead of releasing and re-reserving.
            for (var i = 0; i < steps; i++)
            {
                var moved = _items.Last.RequireNotNull();
                _items.Remove(moved);
                _items.AddFirst(moved);
            }
        }
        else if (steps < 0)
        {
            for (var i = 0; i > steps; i--)
            {
                var moved = _items.First.RequireNotNull();
                _items.Remove(moved);
                _items.AddLast(moved);
            }
        }
    }

    public void Clear()
    {
        if (_memoryGovernor is not null && _committedNodeBytes > 0)
        {
            _memoryGovernor.Release(_committedNodeBytes);
            _committedNodeBytes = 0;
        }

        _items.Clear();
    }

    public object GetItem(int index) => GetNodeAt(index).Value;

    public object GetIndex(int index) => GetItem(index);

    public object GetSlice(IEnumerable<int> indices)
    {
        // Snapshot the source under a transient reservation when governed so
        // peak scratch is bounded; the retained slice charges durably below.
        var governor = _memoryGovernor;
        var span = _allocationSpan;
        object[] source;
        if (governor is null)
        {
            source = _items.ToArray();
        }
        else
        {
            using var scratch = governor.ReserveTemporary(checked(24L + (8L * _items.Count)), span);
            source = _items.ToArray();
        }

        IEnumerable<object> EnumerateSliceItems()
        {
            foreach (var index in indices)
            {
                yield return source[index];
            }
        }

        return governor is null
            ? new PyDeque(EnumerateSliceItems(), MaxLength)
            : new PyDeque(EnumerateSliceItems(), MaxLength, governor, span);
    }

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null
        ? new PyDeque(items, MaxLength)
        : new PyDeque(items, MaxLength, _memoryGovernor, _allocationSpan);

    public void SetItem(int index, object value) => SetIndex(index, value);

    public void SetIndex(int index, object value)
    {
        var node = GetNodeAt(index);
        if (ReferenceEquals(node.Value, value))
        {
            return;
        }

        node.Value = value;
    }

    public void RemoveAt(int index)
    {
        var node = GetNodeAt(index);
        _items.Remove(node);
        ReleaseNode();
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => _items;

    public PyString RenderPython(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedReprValues("deque([", _items, suffix, context);
    }

    public PyString RenderInterpolated(PyRenderingContext context)
    {
        var suffix = MaxLength is null ? "])" : $"], maxlen={MaxLength.Value})";
        return PyRendering.JoinRenderedReprValues("deque([", _items, suffix, context);
    }

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void ReserveNode()
    {
        if (_memoryGovernor is null)
        {
            return;
        }

        _memoryGovernor.Reserve(DequeNodeBytes, _allocationSpan);
        _memoryGovernor.Commit(DequeNodeBytes);
        _committedNodeBytes += DequeNodeBytes;
    }

    private void ReleaseNode()
    {
        // Only release when a matching reservation is actually held: ungoverned
        // nodes and attach-time ownership both converge here.
        if (_memoryGovernor is null || _committedNodeBytes < DequeNodeBytes)
        {
            return;
        }

        _memoryGovernor.Release(DequeNodeBytes);
        _committedNodeBytes -= DequeNodeBytes;
    }

    // Releases every shell and node charge when construction
    // aborts: the orphaned deque publishes nothing.
    private void RefundAbortedConstruction()
    {
        if (_memoryGovernor is not null)
        {
            var release = CommittedStorageBytes;
            if (release > 0)
            {
                _memoryGovernor.Release(release);
            }
        }

        _committedNodeBytes = 0;
        _shellCharged = false;
    }

    private LinkedListNode<object> GetNodeAt(int index)
    {
        if (index < _items.Count / 2)
        {
            var forward = _items.First.RequireNotNull();
            for (var i = 0; i < index; i++)
            {
                forward = forward.Next.RequireNotNull();
            }

            return forward;
        }

        var backward = _items.Last.RequireNotNull();
        for (var i = _items.Count - 1; i > index; i--)
        {
            backward = backward.Previous.RequireNotNull();
        }

        return backward;
    }

}
