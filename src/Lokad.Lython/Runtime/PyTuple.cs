using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTuple : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private readonly object[] _items;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private readonly long _committedBytes;


    public static readonly PyTuple Empty = new([], takeOwnership: true);

    public PyTuple(IEnumerable<object> items)
    {
        _items = items.ToArray();
    }

    public PyTuple(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    public PyTuple(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = MaterializeGovernedItems(items, governor, allocationSpan, out _committedBytes);
    }

    public PyTuple(object[] items)
    {
        _items = [.. items];
    }

    public PyTuple(object[] items, MemoryGovernor governor) : this(items, governor, null) { }

    public PyTuple(object[] items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = CopyGovernedItems(items, governor, allocationSpan, out _committedBytes);
    }

    private PyTuple(object[] items, bool takeOwnership)
    {
        _items = takeOwnership ? items : [.. items];
    }

    private PyTuple(object[] items, MemoryGovernor governor, LythonSourceSpan? allocationSpan, long committedBytes)
    {
        _items = items;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _committedBytes = committedBytes;
    }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int count) => 32L + (16L * count);

    internal long CommittedStorageBytes => _committedBytes;

    internal static PyTuple FromOwnedArray(object[] items) => new(items, takeOwnership: true);

    internal static PyTuple FromOwnedArray(object[] items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Ownership transfer lets collection-expression callers avoid a second
        // backing-array copy. The caller must not retain or mutate the array.
        // Box identities are only read, so the transfer proof is unaffected.
        var bytes = EstimateStorageBytes(items);
        governor.Reserve(bytes, allocationSpan);
        governor.Commit(bytes);
        return new PyTuple(items, governor, allocationSpan, bytes);
    }

    // The caller already admitted the private backing before allocation.
    // Transfer that funding without refunding a still-live array. Scalar estimates
    // supplement the backing estimate before publishing the tuple shell.
    internal static PyTuple FromReservedOwnedArray(object[] items, MemoryGovernor governor,
        LythonSourceSpan? span, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        var bytes = EstimateStorageBytes(items);
        storage.Grow(bytes - EstimateApproximateBytes(items.Length), span);
        var tuple = new PyTuple(items, governor, span, bytes);
        storage.Commit(bytes);
        return tuple;
    }

    internal static long EstimateStorageBytes(ReadOnlySpan<object> items)
    {
        var bytes = EstimateApproximateBytes(items.Length);
        foreach (var item in items) bytes += RuntimeMemoryEstimates.EstimateScalarStorageBytes(item);
        return bytes;
    }

    // Known arrays need one admission before copying and no funding helper
    // allocation. Named tuples share this same storage construction rule.
    internal static object[] CopyGovernedItems(object[] items, MemoryGovernor governor,
        LythonSourceSpan? span, out long bytes)
    {
        bytes = EstimateStorageBytes(items);
        governor.Reserve(bytes, span);
        object[] result;
        try
        {
            result = [.. items];
        }
        catch
        {
            governor.ReleaseReserved(bytes);
            throw;
        }
        governor.Commit(bytes);
        return result;
    }

    private static object[] MaterializeGovernedItems(IEnumerable<object> items, MemoryGovernor governor,
        LythonSourceSpan? allocationSpan, out long bytes)
    {
        if (items is object[] array)
        {
            return CopyGovernedItems(array, governor, allocationSpan, out bytes);
        }

        var capacity = items switch
        {
            ICollection<object> collection => collection.Count,
            IReadOnlyCollection<object> collection => collection.Count,
            _ => 0,
        };
        using var funding = governor.ReserveTemporary(EstimateApproximateBytes(capacity), allocationSpan);
        var buffer = capacity == 0 ? Array.Empty<object>() : new object[capacity];
        var count = 0;
        long scalarBytes = 0;
        foreach (var item in items)
        {
            if (count == buffer.Length)
            {
                var nextCapacity = buffer.Length == 0 ? 4 : checked(buffer.Length * 2);
                funding.Grow(EstimateApproximateBytes(nextCapacity) - EstimateApproximateBytes(buffer.Length), allocationSpan);
                Array.Resize(ref buffer, nextCapacity);
            }

            var scalar = RuntimeMemoryEstimates.EstimateScalarStorageBytes(item);
            funding.Grow(scalar, allocationSpan);
            scalarBytes += scalar;
            buffer[count++] = item;
        }

        var backingBytes = EstimateApproximateBytes(count);
        var materialized = buffer;
        if (count != buffer.Length)
        {
            // The exact final array coexists with the temporary growth buffer.
            funding.Grow(backingBytes, allocationSpan);
            materialized = new object[count];
            Array.Copy(buffer, materialized, count);
        }
        bytes = backingBytes + scalarBytes;
        funding.Commit(bytes);
        return materialized;
    }

    public int Count => _items.Length;

    public int Length => _items.Length;

    public object this[int index] => _items[index];

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_items).GetEnumerator();

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null ? new PyTuple(items) : new PyTuple(items, _memoryGovernor, _allocationSpan);

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices)
    {
        var items = PySequenceMaterialization.MaterializeSlice(_items, indices, _memoryGovernor, _allocationSpan);
        return _memoryGovernor is null
            ? PyTuple.FromOwnedArray(items)
            : PyTuple.FromOwnedArray(items, _memoryGovernor, _allocationSpan);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public object[] ToArray()
    {
        var bytes = EstimateApproximateBytes(_items.Length);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var result = _items.ToArray();
        storage?.Commit(bytes);
        return result;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public int GetPyHashCode() => PyTupleLike.ComputeHashCode(this);

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

}
