using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTuple : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyGovernedValue
{
    private readonly object[] _items;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;

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
        _items = MaterializeGovernedItems(items, governor, allocationSpan);
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
        var approximateBytes = EstimateApproximateBytes(items.Length);
        governor.EnsureCanReserve(approximateBytes, allocationSpan);
        _items = [.. items];
        governor.Reserve(approximateBytes, allocationSpan);
        governor.Commit(approximateBytes);
    }

    private PyTuple(object[] items, bool takeOwnership)
    {
        _items = takeOwnership ? items : [.. items];
    }

    private PyTuple(object[] items, MemoryGovernor governor, LythonSourceSpan? allocationSpan, bool takeOwnership)
    {
        _items = takeOwnership ? items : [.. items];
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    internal static long EstimateApproximateBytes(int count) => 32L + (16L * count);

    internal static PyTuple FromOwnedArray(object[] items) => new(items, takeOwnership: true);

    internal static PyTuple FromOwnedArray(object[] items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        // Ownership transfer lets collection-expression callers avoid a second
        // backing-array copy. The caller must not retain or mutate the array.
        var approximateBytes = EstimateApproximateBytes(items.Length);
        governor.Reserve(approximateBytes, allocationSpan);
        governor.Commit(approximateBytes);
        return new PyTuple(items, governor, allocationSpan, takeOwnership: true);
    }

    private static object[] MaterializeGovernedItems(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (items is object[] array)
        {
            var approximateBytes = EstimateApproximateBytes(array.Length);
            governor.EnsureCanReserve(approximateBytes, allocationSpan);
            var result = new object[array.Length];
            Array.Copy(array, result, array.Length);
            governor.Reserve(approximateBytes, allocationSpan);
            governor.Commit(approximateBytes);
            return result;
        }

        if (items is ICollection<object> collection)
        {
            var approximateBytes = EstimateApproximateBytes(collection.Count);
            governor.EnsureCanReserve(approximateBytes, allocationSpan);
            var result = new object[collection.Count];
            collection.CopyTo(result, 0);
            governor.Reserve(approximateBytes, allocationSpan);
            governor.Commit(approximateBytes);
            return result;
        }

        if (items is IReadOnlyCollection<object> readOnlyCollection)
        {
            var approximateBytes = EstimateApproximateBytes(readOnlyCollection.Count);
            governor.EnsureCanReserve(approximateBytes, allocationSpan);
            var result = new object[readOnlyCollection.Count];
            governor.Reserve(approximateBytes, allocationSpan);
            governor.Commit(approximateBytes);
            var index = 0;
            foreach (var item in readOnlyCollection)
            {
                result[index++] = item;
            }

            return result;
        }

        object[] materialized;
        long bytes;
        using (var temporary = governor.ReserveTemporary(0, allocationSpan))
        {
            var count = 0;
            var buffer = Array.Empty<object>();
            foreach (var item in items)
            {
                if (count == buffer.Length)
                {
                    var nextCapacity = buffer.Length == 0 ? 4 : checked(buffer.Length * 2);
                    temporary.Grow(
                        EstimateApproximateBytes(nextCapacity) - EstimateApproximateBytes(buffer.Length),
                        allocationSpan);
                    Array.Resize(ref buffer, nextCapacity);
                }

                buffer[count++] = item;
            }

            bytes = EstimateApproximateBytes(count);
            if (count == buffer.Length)
            {
                materialized = buffer;
            }
            else
            {
                // The exact array coexists briefly with the growth buffer, so
                // reserve both before making the final allocation.
                temporary.Grow(bytes, allocationSpan);
                materialized = new object[count];
                Array.Copy(buffer, materialized, count);
            }
        }

        governor.Reserve(bytes, allocationSpan);
        governor.Commit(bytes);
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
        var items = MaterializeSlice(indices);
        return _memoryGovernor is null
            ? PyTuple.FromOwnedArray(items)
            : PyTuple.FromOwnedArray(items, _memoryGovernor, _allocationSpan);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();

    public object[] ToArray()
    {
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(EstimateApproximateBytes(_items.Length), _allocationSpan);
        }

        return [.. _items];
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public int GetPyHashCode() => PyTupleLike.ComputeHashCode(this);

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    private object[] MaterializeSlice(IEnumerable<int> indices)
    {
        if (indices is ICollection<int> collection)
        {
            if (_memoryGovernor is not null)
            {
                _memoryGovernor.EnsureCanReserve(EstimateApproximateBytes(collection.Count), _allocationSpan);
            }

            var result = new object[collection.Count];
            var index = 0;
            foreach (var item in indices)
            {
                result[index++] = _items[item];
            }

            return result;
        }

        var values = new List<object>();
        foreach (var item in indices)
        {
            values.Add(_items[item]);
        }

        return [.. values];
    }

}
