using System.Collections;
using System.Linq;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDefaultDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyMutableDynamicAttributes, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private readonly PyDict _items;

    public PyDefaultDict(object defaultFactory)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict();
    }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor) : this(defaultFactory, governor, null) { }

    public PyDefaultDict(object defaultFactory, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Own the shell beside the governed inner dict.
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(governor, allocationSpan);
    }

    public PyDefaultDict(object defaultFactory, PyDict items)
    {
        DefaultFactory = ValidateDefaultFactory(defaultFactory);
        _items = new PyDict(items);
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

        var innerBefore = _items.CommittedStorageBytes;
        _items.SetItem(key, created);
        context.ObserveCollectionCount(Count, span);
        NoteGrowth(innerBefore);
        return created;
    }

    // Current committed shell-plus-inner charges, for pooled owners that release
    // them if this wrapper is dropped. Inner growth refreshes the snapshot through
    // NoteGrowth below; the inner dict's own notifications key on the inner value
    // and stay misses while only wrappers are tracked.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : 64L + _items.CommittedStorageBytes;

    // Refreshes the wrapper coupon after inner growth; change-detected so steady
    // use costs two field reads. Denied growth throws before mutating.
    private void NoteGrowth(long innerBefore)
    {
        if (_items.CommittedStorageBytes != innerBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }

    public void SetItem(object key, object value)
    {
        var innerBefore = _items.CommittedStorageBytes;
        _items.SetItem(key, value);
        NoteGrowth(innerBefore);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (OwnerMemoryGovernor is null)
        {
            governor.Reserve(64L, allocationSpan);
            governor.Commit(64L);
        }

        _items.AttachMemoryGovernor(governor, allocationSpan);
    }

    public bool Remove(object key) => _items.Remove(key);

    public bool TryRemoveLast([MaybeNullWhen(false)] out object key, [MaybeNullWhen(false)] out object value)
        => _items.TryRemoveLast(out key, out value);

    public object UpdateFrom(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var innerBefore = _items.CommittedStorageBytes;
        var result = LythonRuntime.UpdateDictionary(_items, arguments, span, context);
        NoteGrowth(innerBefore);
        return result;
    }

    public void Clear()
    {
        var innerBefore = _items.CommittedStorageBytes;
        _items.Clear();
        NoteGrowth(innerBefore);
    }

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

    public PyCounter()
    {
        _items = new PyDict();
    }

    public PyCounter(MemoryGovernor governor) : this(governor, null) { }

    public PyCounter(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Own the shell beside the governed inner dict.
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        _items = new PyDict(governor, allocationSpan);
    }

    public PyCounter(PyCounter other)
    {
        _items = new PyDict(other._items);
    }

    public PyCounter(PyCounter other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyCounter(PyCounter other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        governor.Reserve(64L, allocationSpan);
        governor.Commit(64L);
        _items = new PyDict(other._items, governor, allocationSpan);
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

    // Current committed shell-plus-inner charges, for pooled owners that release
    // them if this wrapper is dropped. Inner growth refreshes the snapshot through
    // NoteGrowth below; the inner dict's own notifications key on the inner value
    // and stay misses while only wrappers are tracked.
    internal long CommittedStorageBytes => OwnerMemoryGovernor is null ? 0 : 64L + _items.CommittedStorageBytes;

    // Refreshes the wrapper coupon after inner growth; change-detected so steady
    // use costs two field reads. Denied growth throws before mutating.
    private void NoteGrowth(long innerBefore)
    {
        if (_items.CommittedStorageBytes != innerBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }

    public void SetItem(object key, object value)
    {
        var innerBefore = _items.CommittedStorageBytes;
        _items.SetItem(key, value);
        NoteGrowth(innerBefore);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (OwnerMemoryGovernor is null)
        {
            governor.Reserve(64L, allocationSpan);
            governor.Commit(64L);
        }

        _items.AttachMemoryGovernor(governor, allocationSpan);
    }

    public bool Remove(object key) => _items.Remove(key);

    public void Clear()
    {
        var innerBefore = _items.CommittedStorageBytes;
        _items.Clear();
        NoteGrowth(innerBefore);
    }

    public void Increment(object key, object delta, LythonSourceSpan span, ChargeReclamationPool? pool)
    {
        var innerBefore = _items.CommittedStorageBytes;
        if (_items.TryGetValue(key, out var value))
        {
            _items.SetItem(key, LythonRuntime.AddCounterCounts(value, delta, span, OwnerMemoryGovernor, pool));
            NoteGrowth(innerBefore);
            return;
        }

        _items.SetItem(key, delta);
        NoteGrowth(innerBefore);
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

    // LinkedList nodes are separate heap objects (prev/next/value plus header);
    // charge each live node so retained deques accumulate like other containers.
    // The wrapper plus its empty list object (~96 B measured) owns one shell charge
    // per live instance at the constructed-function shell rate, matching sets.
    // Clear releases nodes while the shell persists, and regrowth re-charges.
    private const long DequeNodeBytes = 64;
    private const long DequeShellBytes = 128;
    private bool _shellCharged;
    private AdoptedScalarCoupons? _scalarCoupons;
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

    // Current committed shell-plus-node charges, for pooled owners that
    // release them if this deque is dropped. Growth after the snapshot only
    // ever leaves a safe residual behind; every release path re-snapshots below.
    // Adopted scalar coupons fold in, so snapshots and drop sweeps carry them.
    internal long CommittedStorageBytes => (_shellCharged ? DequeShellBytes : 0) + _committedNodeBytes + (_scalarCoupons?.CommittedBytes ?? 0);

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
        // A denied coupon strands nothing: Append rolls back its own node, and
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

        _memoryGovernor = governor;
        _allocationSpan ??= allocationSpan;
        ChargeShell();
        if (_items.Count > 0)
        {
            // Nodes predating the attachment were never charged: own them now
            // that the deque is governed, or later drops would strand them.
            var bytes = checked((long)_items.Count * DequeNodeBytes);
            _memoryGovernor.Reserve(bytes, _allocationSpan);
            _memoryGovernor.Commit(bytes);
            _committedNodeBytes += bytes;
        }
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

        var committedBefore = CommittedStorageBytes;
        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            // Bounded eviction reuses one node charge: the list stays at maxlen.
            // Coupons still turn over (the retained identity changes), so a
            // razor-exhausted budget can deny rotation that costs no node charge.
            AdoptIncoming(value);
            var evicted = _items.First.RequireNotNull().Value;
            _items.RemoveFirst();
            ReleaseOutgoing(evicted);
            _items.AddLast(value);
            NoteGrowth(committedBefore);
            return;
        }

        AdoptIncoming(value);
        try
        {
            ReserveNode();
        }
        catch (Exception)
        {
            UnadoptIncoming(value);
            throw;
        }

        LinkedListNode<object> node;
        try
        {
            node = _items.AddLast(value);
        }
        catch (Exception)
        {
            ReleaseNode();
            UnadoptIncoming(value);
            throw;
        }

        NoteGrowth(committedBefore);
    }


    public void AppendLeft(object value)
    {
        if (MaxLength == 0)
        {
            return;
        }

        var committedBefore = CommittedStorageBytes;
        if (MaxLength is int maxLength && _items.Count == maxLength)
        {
            // Same charge reuse as Append: eviction keeps the node count flat.
            AdoptIncoming(value);
            var evicted = _items.Last.RequireNotNull().Value;
            _items.RemoveLast();
            ReleaseOutgoing(evicted);
            _items.AddFirst(value);
            NoteGrowth(committedBefore);
            return;
        }

        AdoptIncoming(value);
        try
        {
            ReserveNode();
        }
        catch (Exception)
        {
            UnadoptIncoming(value);
            throw;
        }

        LinkedListNode<object> node;
        try
        {
            node = _items.AddFirst(value);
        }
        catch (Exception)
        {
            ReleaseNode();
            UnadoptIncoming(value);
            throw;
        }

        NoteGrowth(committedBefore);
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
        ReleaseOutgoing(value);
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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
        ReleaseOutgoing(value);
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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
                var removed = current.Value;
                _items.Remove(current);
                ReleaseNode();
                ReleaseOutgoing(removed);
                ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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

        var committedBefore = CommittedStorageBytes;
        AdoptIncoming(value);
        LinkedListNode<object> node;
        try
        {
            ReserveNode();
        }
        catch (Exception)
        {
            UnadoptIncoming(value);
            throw;
        }

        try
        {
            if (index <= 0)
            {
                node = _items.AddFirst(value);
            }
            else if (index >= _items.Count)
            {
                node = _items.AddLast(value);
            }
            else
            {
                node = _items.AddBefore(GetNodeAt(index), value);
            }
        }
        catch (Exception)
        {
            ReleaseNode();
            UnadoptIncoming(value);
            throw;
        }

        NoteGrowth(committedBefore);
    }

    public bool RemoveValue(object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var current = _items.First;
        while (current is not null)
        {
            if (LythonRuntime.MembershipEquals(current.Value, candidate, context, span))
            {
                var removed = current.Value;
                _items.Remove(current);
                ReleaseNode();
                ReleaseOutgoing(removed);
                ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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
                var moved = _items.Last.RequireNotNull().Value;
                _items.RemoveLast();
                _items.AddFirst(moved);
            }
        }
        else if (steps < 0)
        {
            for (var i = 0; i > steps; i--)
            {
                var moved = _items.First.RequireNotNull().Value;
                _items.RemoveFirst();
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

        ReleaseAllCoupons();
        _items.Clear();
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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

        AdoptIncoming(value);
        var outgoing = node.Value;
        node.Value = value;
        ReleaseOutgoing(outgoing);
    }

    public void RemoveAt(int index)
    {
        var node = GetNodeAt(index);
        var outgoing = node.Value;
        _items.Remove(node);
        ReleaseNode();
        ReleaseOutgoing(outgoing);
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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

    // Refreshes the tracked snapshot when the owned total moved (nodes or
    // coupons): a stale snapshot would over-release on a later drop sweep.
    private void NoteGrowth(long committedBefore)
    {
        if (CommittedStorageBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }

    // Releases every adopted coupon plus shell and node charges when construction
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
        _scalarCoupons = null;
    }

    // Adopts one incoming value when governed; denies before the caller mutates.
    private void AdoptIncoming(object value)
    {
        if (_memoryGovernor is null || !AdoptedScalarCoupons.IsAdoptableScalar(value))
        {
            return;
        }

        _scalarCoupons ??= new AdoptedScalarCoupons();
        _scalarCoupons.Adopt(value, _memoryGovernor, _allocationSpan);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void UnadoptIncoming(object value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    // Releases one outgoing reference and refreshes the snapshot when the owned
    // total moved, so drops never sweep a stale charge.
    private void ReleaseOutgoing(object? value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        var committedBefore = CommittedStorageBytes;
        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }

        NoteGrowth(committedBefore);
    }

    private void ReleaseAllCoupons()
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            _scalarCoupons = null;
            return;
        }

        var committedBefore = CommittedStorageBytes;
        _scalarCoupons.ReleaseAll(_memoryGovernor);
        _scalarCoupons = null;
        NoteGrowth(committedBefore);
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
