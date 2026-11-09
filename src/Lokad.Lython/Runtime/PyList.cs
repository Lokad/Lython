using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyList : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyOwnershipSnapshot
{
    internal const int InitialCapacity = 8;
    // Deliberately distinct from Array.Empty: a sort callback can grow and
    // clear its view, which still counts as modification when sorting ends.
    private static readonly object[] SortingEmpty = new object[0];
    private object[] _items;
    private int _count;
    private long _committedBytes;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;

    // Major capacity includes metadata and scalar-box headroom. Contents and
    // removals never change this charge; large numeric payloads keep their owners.
    internal static long EstimateCapacityBytes(int capacity)
        => checked(64L + ((16L + RuntimeMemoryEstimates.ScalarStorageBytes) * capacity));

    public PyList() : this(0, null, null) { }
    public PyList(IEnumerable<object> items) : this(items, null, null) { }
    public PyList(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    internal PyList(int capacity, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        var bytes = EstimateCapacityBytes(capacity);
        using var backing = governor?.ReserveTemporary(bytes, allocationSpan);
        _items = capacity == 0 ? [] : new object[capacity];
        backing?.Commit(bytes);
        if (backing is not null) _committedBytes = bytes;
    }

    public PyList(IEnumerable<object> items, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
        : this(items is IReadOnlyCollection<object> known ? known.Count : 0, governor, allocationSpan)
    {
        try
        {
            if (items is object[] array)
            {
                array.CopyTo(_items, 0);
                _count = array.Length;
            }
            else foreach (var item in items) Add(item);
        }
        catch { DiscardUnpublishedStorage(); throw; }
    }

    public PyList(PyList other) : this(other, other._memoryGovernor, other._allocationSpan) { }

    // The caller funds this exact array and commits its capacity before
    // publishing the result. Adoption copies no storage and refunds nothing.
    internal static PyList FromTransferredArray(object[] items, MemoryGovernor? governor, LythonSourceSpan? span)
        => new()
        {
            _items = items, _count = items.Length, _memoryGovernor = governor,
            _allocationSpan = span, _committedBytes = governor is null ? 0 : EstimateCapacityBytes(items.Length),
        };

    // Only unpublished constructor storage can be refunded; contained aliases
    // retain their independent owners. Drop the backing before returning funding.
    internal void DiscardUnpublishedStorage()
    {
        _items = [];
        _count = 0;
        var bytes = _committedBytes;
        _committedBytes = 0;
        _memoryGovernor?.Release(bytes);
    }

    public int Count => _count;
    internal long CommittedStorageBytes => _committedBytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public int Length => Count;

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(object value)
    {
        EnsureCapacity(checked(_count + 1));
        _items[_count++] = value;
    }

    public void AddRange(IEnumerable<object> values) => AddRange(values, null, null);

    internal void AddRange(IEnumerable<object> values, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.CheckExecution(span);
        if (values is PyIteration.CheckedSequence { Source: PyList or IReadOnlyCollection<object> } checkedSequence)
        {
            values = checkedSequence.Source;
        }

        if (values is PyList sourceList)
        {
            // Snapshot the source (which may be this list) so extending appends
            // the original elements; the copy itself is budget-checked.
            AddRange(sourceList.ToArray(), context, span);
            return;
        }

        // List-backed inputs are snapshotted above (PyList implements this interface
        // through its sequence interfaces, so it must never reach this live count).
        if (values is IReadOnlyCollection<object> known)
        {
            // Admit the reported count before mutation. A changing collection
            // can yield more, so each store also ensures its actual capacity.
            EnsureCapacity(checked(_count + known.Count));

            foreach (var value in known)
            {
                context?.CheckExecution(span);
                Add(value);
            }

            context?.CheckExecution(span);
            return;
        }

        try
        {
            // Unknown-length inputs stream incrementally, admitting actual
            // capacity before each store. Failing iterables retain their
            // successful prefix and its funding.
            foreach (var value in values)
            {
                context?.CheckExecution(span);
                Add(value);
            }
            context?.CheckExecution(span);
        }
        catch (InvalidOperationException)
        {
            // Iteration observed our own concurrent growth (for example extending
            // with a live iterator over this list). That cannot terminate like
            // CPython does, so fail explicitly instead of leaking the CLR error.
            throw RuntimeErrors.Runtime("list modified during extension.", span ?? _allocationSpan);
        }
    }

    internal async ValueTask AddRangeAsync(object source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // In-memory fast paths ride the sync bulk path unchanged (no suspension
        // possible, self-extension stays snapshotted); only lazy sources await.
        if (source is PyList || source is IReadOnlyCollection<object>)
        {
            AddRange(LythonRuntime.ToSequence(source, span, context), context, span);
            return;
        }

        try
        {
            context.CheckExecution(span);
            await foreach (var value in LythonRuntime.ToSequenceAsync(source, span, context).ConfigureAwait(false))
            {
                context.CheckExecution(span);
                Add(value);
            }
            context.CheckExecution(span);
        }
        catch (InvalidOperationException)
        {
            throw RuntimeErrors.Runtime("list modified during extension.", span);
        }
    }

    public void Insert(int index, object value)
    {
        var normalized = index < 0 ? index + Count : index;
        normalized = Math.Clamp(normalized, 0, Count);
        EnsureCapacity(checked(_count + 1));
        Array.Copy(_items, normalized, _items, normalized + 1, _count - normalized);
        _items[normalized] = value;
        _count++;
    }

    public void Reverse()
    {
        // Swap in place: the count never changes, so no charging is needed.
        var count = Count;
        for (var i = 0; i < count / 2; i++)
        {
            var opposite = count - 1 - i;
            var saved = _items[i];
            _items[i] = _items[opposite];
            _items[opposite] = saved;
        }
    }

    // A sort owns the detached backing through every callback and awaited user.
    // This value snapshot does not allocate a second collection/backend.
    internal readonly record struct SortBacking(object[] Items, int Count, long Bytes)
    {
        internal ArraySegment<object> Values => new(Items, 0, Count);
    }

    internal SortBacking DetachForSort()
    {
        var bytes = EstimateCapacityBytes(0);
        _memoryGovernor?.Reserve(bytes, _allocationSpan);
        var original = new SortBacking(_items, _count, _committedBytes);
        _items = SortingEmpty;
        _count = 0;
        _committedBytes = _memoryGovernor is null ? 0 : bytes;
        _memoryGovernor?.Commit(bytes);
        return original;
    }

    internal bool RestoreAfterSort(SortBacking original, PyStableSort.Buffer? sorted)
    {
        var modified = !ReferenceEquals(_items, SortingEmpty);
        if (sorted is not null)
            for (var i = 0; i < original.Count; i++) original.Items[i] = sorted[i].Value;
        var discardedBytes = _committedBytes;
        _items = original.Items;
        _count = original.Count;
        _committedBytes = original.Bytes;
        _memoryGovernor?.Release(discardedBytes);
        return modified;
    }

    public void RepeatInPlace(int count, LythonSourceSpan span, LythonRuntime.ExecutionContext? context = null)
    {
        if (_items.Length == 0) return; // Empty callback view while sorting.
        if (count <= 0 || _count == 0)
        {
            Clear();
            return;
        }
        var totalLength = (long)_count * count;
        if (totalLength > int.MaxValue)
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        var total = (int)totalLength;
        EnsureCapacity(total);
        while (_count < total)
        {
            // The populated prefix already is a whole number of repetitions.
            // Reuse it in bounded copies, rather than call Array.Copy per item.
            var chunk = Math.Min(_count, total - _count);
            for (var copied = 0; copied < chunk;)
            {
                context?.CheckExecution(span);
                var length = Math.Min(4096, chunk - copied);
                Array.Copy(_items, copied, _items, _count + copied, length);
                copied += length;
            }
            _count += chunk;
        }
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is null)
        {
            // Prepaid producer storage retains its original funding. Admission
            // here conservatively funds this backing before governed mutation.
            var bytes = EstimateCapacityBytes(_items.Length);
            governor.Reserve(bytes, allocationSpan);
            governor.Commit(bytes);
            _committedBytes = bytes;
            _memoryGovernor = governor;
        }
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index) => RemoveRange(index, 1);

    public void RemoveRange(int index, int count)
    {
        if (count <= 0) return;
        Array.Copy(_items, index + count, _items, index, _count - index - count);
        Array.Clear(_items, _count - count, count);
        _count -= count;
    }

    public void DeleteSlice(PyIndexing.SliceBounds bounds)
    {
        if (bounds.Step == 1)
        {
            RemoveRange(bounds.Start, Math.Max(bounds.End - bounds.Start, 0));
            return;
        }
        if (bounds.Count == 0) return;
        var count = Count;
        var destination = 0;
        for (var source = 0; source < count; source++)
        {
            if (!bounds.Contains(source))
            {
                if (destination != source) _items[destination] = _items[source];
                destination++;
            }
        }
        if (destination < count) RemoveRange(destination, count - destination);
    }

    public void SetSlice(PyIndexing.SliceBounds bounds, IReadOnlyList<object> values, LythonSourceSpan span)
    {
        if (bounds.Step == 1)
        {
            var removeCount = Math.Max(bounds.End - bounds.Start, 0);
            IReadOnlyList<object> staged = values;
            if (values is PyList sourceList) staged = sourceList.ToArray();
            var total = checked(_count - removeCount + staged.Count);
            EnsureCapacity(total);
            Array.Copy(_items, bounds.Start + removeCount, _items, bounds.Start + staged.Count,
                _count - bounds.Start - removeCount);
            for (var index = 0; index < staged.Count; index++) _items[bounds.Start + index] = staged[index];
            if (total < _count) Array.Clear(_items, total, _count - total);
            _count = total;
            return;
        }
        if (bounds.Count != values.Count)
            throw new LythonRuntimeException("ValueError", "attempt to assign sequence of size " + values.Count + " to extended slice of size " + bounds.Count, span);
        var position = 0;
        foreach (var index in bounds.Indices()) _items[index] = values[position++];
    }

    public void Clear()
    {
        if (_items.Length == 0) return;
        _items = [];
        _count = 0;
        // Relinquish backing before returning its allowance. Metadata and
        // independent payload/alias owners remain funded; no replacement allocates.
        var retained = Math.Min(_committedBytes, EstimateCapacityBytes(0));
        var released = _committedBytes - retained;
        _committedBytes = retained;
        _memoryGovernor?.Release(released);
    }

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null
        ? new PyList(items)
        : new PyList(items, _memoryGovernor, _allocationSpan);

    public void SetItem(int index, object value) => _items[index] = value;

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices) => _memoryGovernor is null
        ? new PyList(PySequenceMaterialization.MaterializeSlice(this, indices, null, _allocationSpan))
        : new PyList(PySequenceMaterialization.MaterializeSlice(this, indices, _memoryGovernor, _allocationSpan), _memoryGovernor, _allocationSpan);

    public void SetIndex(int index, object value) => _items[index] = value;

    public object[] ToArray()
    {
        var bytes = EstimateArrayBytes(Count);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var result = new object[_count];
        Array.Copy(_items, result, _count);
        storage?.Commit(bytes);
        return result;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    // Live indexed iteration sees replacements, appends and removals, including
    // backing replacement. No CLR version-checked storage enumerator is exposed.
    public IEnumerator<object> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    internal void EnsureCapacity(int targetCount)
    {
        if (targetCount <= _items.Length) return;
        var capacity = Math.Max(targetCount, (int)Math.Min(
            _items.Length == 0 ? InitialCapacity : (long)_items.Length * 2, Array.MaxLength));
        var bytes = EstimateCapacityBytes(capacity);
        using var backing = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var replacement = new object[capacity];
        Array.Copy(_items, replacement, _count);
        _items = replacement;
        if (backing is not null)
        {
            backing.Commit(bytes);
            _memoryGovernor!.Release(_committedBytes);
            _committedBytes = bytes;
        }
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

}
