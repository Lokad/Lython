using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTuple : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyOwnershipSnapshot
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

    public PyTuple(ReadOnlySpan<object> items)
    {
        _items = items.ToArray();
    }

    public PyTuple(ReadOnlySpan<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    public PyTuple(ReadOnlySpan<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan,
        LythonRuntime.ExecutionContext? context = null)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = CopyGovernedItems(items, governor, allocationSpan, out _committedBytes, context);
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

    internal static PyTuple Repeat(IReadOnlyList<object> source, int count, int length,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        long scalarBytes = 0;
        for (var i = 0; i < source.Count; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            scalarBytes = checked(scalarBytes + RuntimeMemoryEstimates.EstimateScalarStorageBytes(source[i]));
        }
        var bytes = checked(EstimateApproximateBytes(length) + scalarBytes * count);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var items = new object[length];
        for (var i = 0; i < source.Count; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            items[i] = source[i];
        }
        PySequenceMaterialization.FillRepeated<object>(items.AsSpan(0, source.Count), items, context, span);
        var result = new PyTuple(items, context.MemoryGovernor, span, bytes);
        storage.Commit(bytes);
        return result;
    }

    internal static PyTuple FromPositionalArguments(ReadOnlySpan<CallArgumentValue> arguments,
        MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var count = 0;
        long scalars = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            if (!arguments[i].IsPositional) continue;
            count++;
            scalars = checked(scalars + RuntimeMemoryEstimates.EstimateScalarStorageBytes(arguments[i].Value));
        }
        var bytes = checked(EstimateApproximateBytes(count) + scalars);
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            var items = count == 0 ? Array.Empty<object>() : new object[count];
            var written = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (arguments[i].IsPositional) items[written++] = arguments[i].Value;
            }
            var result = new PyTuple(items, governor, span, bytes);
            context.CheckExecution(span);
            governor.Commit(bytes);
            return result;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

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
    // Callers may already have admitted scalar headroom as part of this output's
    // storage; it is separate from any live construction scratch in the lease.
    internal static PyTuple FromReservedOwnedArray(object[] items, MemoryGovernor governor,
        LythonSourceSpan? span, MemoryGovernor.TemporaryMemoryReservation storage, long reservedScalarBytes = 0)
    {
        var bytes = EstimateStorageBytes(items);
        storage.Grow(bytes - EstimateApproximateBytes(items.Length) - reservedScalarBytes, span);
        var tuple = new PyTuple(items, governor, span, bytes);
        storage.Commit(bytes);
        return tuple;
    }

    internal static long EstimateStorageBytes(ReadOnlySpan<object> items,
        LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        var bytes = EstimateApproximateBytes(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            if (i != 0 && (i & 1023) == 0) context?.CheckExecution(span);
            bytes = checked(bytes + RuntimeMemoryEstimates.EstimateScalarStorageBytes(items[i]));
        }
        return bytes;
    }

    // Completed references need one admission before copying and no funding helper
    // allocation. Named tuples share this same storage construction rule.
    internal static object[] CopyGovernedItems(ReadOnlySpan<object> items, MemoryGovernor governor,
        LythonSourceSpan? span, out long bytes, LythonRuntime.ExecutionContext? context = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        bytes = EstimateStorageBytes(items, context, span);
        governor.Reserve(bytes, span);
        object[] result;
        try
        {
            context?.CheckExecution(span);
            result = new object[items.Length];
            for (var offset = 0; offset < items.Length;)
            {
                if (offset != 0) context?.CheckExecution(span);
                var count = Math.Min(4096, items.Length - offset);
                items.Slice(offset, count).CopyTo(result.AsSpan(offset));
                offset += count;
            }
        }
        catch
        {
            governor.ReleaseReserved(bytes);
            throw;
        }
        governor.Commit(bytes);
        return result;
    }

    internal static object[] MaterializeGovernedItems(IEnumerable<object> items, MemoryGovernor governor,
        LythonSourceSpan? allocationSpan, out long bytes, int? expectedCount = null)
    {
        if (items is object[] array)
        {
            ValidateCount(array.Length);
            return CopyGovernedItems(array, governor, allocationSpan, out bytes);
        }

        var capacity = items switch
        {
            ICollection<object> collection => collection.Count,
            IReadOnlyCollection<object> collection => collection.Count,
            _ => 0,
        };
        // Fixed records know their necessary result capacity even when their
        // source is a generator. This is admission, not an iteration cutoff.
        capacity = Math.Max(capacity, expectedCount.GetValueOrDefault());
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

        ValidateCount(count);
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

        void ValidateCount(int count)
        {
            if (expectedCount is { } expected && count != expected)
                throw new LythonRuntimeException("TypeError", $"Expected {expected} arguments, got {count}", allocationSpan);
        }
    }

    public int Count => _items.Length;

    public int Length => _items.Length;

    public object this[int index] => _items[index];

    public IEnumerator<object> GetEnumerator() => ((IEnumerable<object>)_items).GetEnumerator();

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null ? new PyTuple(items) : new PyTuple(items, _memoryGovernor, _allocationSpan);

    public object GetIndex(int index) => _items[index];

    internal PyTuple Slice(PyIndexing.SliceBounds bounds, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => LythonRuntime.CreateTuple(bounds.Count,
            index => _items[(int)(bounds.Start + (long)index * bounds.Step)], context, span);

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
