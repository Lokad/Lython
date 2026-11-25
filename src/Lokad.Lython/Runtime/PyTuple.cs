using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyTuple : IPySequenceValue, IPyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyHashableValue, IPyOwnershipSnapshot
{
    private readonly Array _items;
    private readonly MemoryGovernor? _memoryGovernor;
    private readonly LythonSourceSpan? _allocationSpan;
    private long _committedBytes;


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

    private PyTuple(Array items, MemoryGovernor governor, LythonSourceSpan? allocationSpan, long committedBytes)
    {
        _items = items;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _committedBytes = committedBytes;
    }

    internal static long EstimateObjectSlotStorage(ReadOnlySpan<LythonRuntime.ExecutableSlot> values,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => checked(EstimateApproximateBytes(values.Length) + EstimateScalarSlots(values, context, span));

    internal static long EstimateScalarSlots(ReadOnlySpan<LythonRuntime.ExecutableSlot> values,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        long bytes = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            bytes = checked(bytes + values[i].ScalarStorageBytes);
        }
        return bytes;
    }

    internal static PyTuple FromSlots(Span<LythonRuntime.ExecutableSlot> values,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Fresh integers still have no aliases. Retain their machine slots and
        // fund eventual boxes. Other slots publish their existing identities.
        var integers = !values.IsEmpty;
        for (var i = 0; i < values.Length && integers; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            integers = values[i].IsUnpublishedInteger;
        }
        var bytes = integers
            ? checked(64L + (24L + RuntimeMemoryEstimates.ScalarStorageBytes) * values.Length)
            : EstimateObjectSlotStorage(values, context, span);
        var governor = context.MemoryGovernor;
        // The tuple has one fixed reservation and remains private until
        // its storage is complete. Only this construction can refund it.
        governor.Reserve(bytes, span);
        try
        {
            context.CheckExecution(span);
            Array items;
            if (integers)
            {
                var slots = new LythonRuntime.ExecutableSlot[values.Length];
                for (var offset = 0; offset < values.Length;)
                {
                    context.CheckExecution(span);
                    var count = Math.Min(1024, values.Length - offset);
                    values.Slice(offset, count).CopyTo(slots.AsSpan(offset));
                    offset += count;
                }
                items = slots;
            }
            else
            {
                var objects = new object[values.Length];
                for (var i = 0; i < objects.Length; i++)
                {
                    if ((i & 1023) == 0) context.CheckExecution(span);
                    objects[i] = values[i].Object.RequireNotNull();
                }
                items = objects;
            }
            var tuple = new PyTuple(items, governor, span, bytes);
            governor.Commit(bytes);
            return tuple;
        }
        catch { governor.ReleaseReserved(bytes); throw; }
    }

    // Definition defaults are written once into their final storage. Admit
    // capacity and eventual small scalar boxes before evaluating any defaults.
    internal static long EstimateDefaultStorage(int count)
        => checked(64L + (24L + RuntimeMemoryEstimates.ScalarStorageBytes) * count);

    internal static PyTuple CreateAdmittedDefaultStorage(int count, MemoryGovernor governor,
        LythonSourceSpan span, long bytes)
        => new(new LythonRuntime.ExecutableSlot[count], governor, span, bytes);

    internal static PyTuple CreateAdmittedSingletonDefault(object value, MemoryGovernor governor,
        LythonSourceSpan? span, long bytes)
        => new(new object[] { value }, governor, span, bytes);

    internal void SetPrivateDefault(int index, LythonRuntime.ExecutableSlot value, ExecutionState state, LythonSourceSpan span)
    {
        WritableValueSlot(index) = FunctionDefaults.PreparePositionalValue(value, state, span, out var extra);
        _committedBytes += extra;
    }

    internal bool TryReadInteger(int index, out long value)
    {
        if (_items is not object[]) return WritableValueSlot(index).TryGetInteger(out value);
        value = 0;
        return false;
    }

    internal bool TryReadStoredSlot(long index, out LythonRuntime.ExecutableSlot value)
    {
        value = default;
        if (_items is object[]) return false;
        var count = Count;
        if (index < 0) index += count;
        if ((ulong)index >= (uint)count) return false;
        value = ReadValueSlot((int)index);
        return true;
    }

    // Positional defaults align with the end of the callable's parameter list.
    internal bool TryReadTrailingSlot(int distance, out LythonRuntime.ExecutableSlot value)
    {
        var count = Count;
        var index = count - distance;
        if ((uint)index >= (uint)count)
        {
            value = default;
            return false;
        }
        value = _items is object[] objects ? LythonRuntime.ExecutableSlot.FromObject(objects[index])
            : ReadValueSlot(index);
        return true;
    }

    private ref LythonRuntime.ExecutableSlot WritableValueSlot(int index)
        => ref ((LythonRuntime.ExecutableSlot[])_items)[index];

    private LythonRuntime.ExecutableSlot ReadValueSlot(int index)
    {
        ref var item = ref WritableValueSlot(index);
        if (!item.IsUnpublishedInteger)
            return LythonRuntime.ExecutableSlot.FromObject(item.Reference);
        LythonRuntime.ExecutableSlot value = default;
        value.BorrowTupleInteger(this, index);
        return value;
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

    // The caller has admitted backing and any new scalars before allocating
    // the private array. It commits their reservation before publication.
    internal static PyTuple FromAdmittedOwnedArray(object[] items, MemoryGovernor governor, LythonSourceSpan span, long bytes)
        => new(items, governor, span, bytes);

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
        LythonSourceSpan? allocationSpan, out long bytes, int? expectedCount = null,
        LythonRuntime.ExecutionContext? context = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        if (items is object[] array)
        {
            ValidateCount(array.Length);
            return CopyGovernedItems(array, governor, allocationSpan, out bytes, context);
        }

        var capacity = items switch
        {
            PyIteration.Cursor cursor => cursor.KnownCount.GetValueOrDefault(),
            ICollection<object> collection => collection.Count,
            IReadOnlyCollection<object> collection => collection.Count,
            _ => 0,
        };
        // Fixed records know their necessary result capacity even when their
        // source is a generator. This is admission, not an iteration cutoff.
        capacity = Math.Max(capacity, expectedCount.GetValueOrDefault());
        using var metadata = governor.ReserveTemporary(PyList.EstimateCapacityBytes(0), allocationSpan);
        var builder = new PyList(capacity, governor, allocationSpan);
        try
        {
            builder.AppendItems(items, context, allocationSpan);
            ValidateCount(builder.Count);
            return builder.TakeConstructorStorage(out bytes, context, allocationSpan);
        }
        finally { builder.DiscardUnpublishedStorage(); }

        void ValidateCount(int count)
        {
            if (expectedCount is { } expected && count != expected)
                throw new LythonRuntimeException("TypeError", $"Expected {expected} arguments, got {count}", allocationSpan);
        }
    }

    internal static PyTuple CreateFromIterable(IEnumerable<object> source,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var items = MaterializeGovernedItems(source, context.MemoryGovernor, span, out var bytes, context: context);
        return new PyTuple(items, context.MemoryGovernor, span, bytes);
    }

    internal static async ValueTask<PyTuple> CreateFromIterableAsync(object source,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var governor = context.MemoryGovernor;
        using var metadata = governor.ReserveTemporary(PyList.EstimateCapacityBytes(0), span);
        var builder = new PyList(source is IReadOnlyCollection<object> known ? known.Count : 0, governor, span);
        try
        {
            await builder.AppendItemsAsync(PyIteration.Cursor.ForAsyncEnumeration(source, span, context), context, span).ConfigureAwait(false);
            var items = builder.TakeConstructorStorage(out var bytes, context, span);
            return new PyTuple(items, governor, span, bytes);
        }
        finally { builder.DiscardUnpublishedStorage(); }
    }

    public int Count => _items.Length;

    public int Length => Count;

    public object this[int index] => _items is object[] objects ? objects[index]
        : WritableValueSlot(index).PublishSharedInteger();

    public IEnumerator<object> GetEnumerator() => _items is object[] objects
        ? ((IEnumerable<object>)objects).GetEnumerator() : EnumerateSlots().GetEnumerator();

    private IEnumerable<object> EnumerateSlots()
    {
        for (var i = 0; i < Count; i++) yield return this[i];
    }

    public object GetItem(int index) => this[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null ? new PyTuple(items) : new PyTuple(items, _memoryGovernor, _allocationSpan);

    public object GetIndex(int index) => this[index];

    internal PyTuple Slice(PyIndexing.SliceBounds bounds, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => LythonRuntime.CreateTuple(bounds.Count,
            index => this[(int)(bounds.Start + (long)index * bounds.Step)], context, span);

    public object GetSlice(IEnumerable<int> indices)
    {
        var items = PySequenceMaterialization.MaterializeSlice(this, indices, _memoryGovernor, _allocationSpan);
        return _memoryGovernor is null
            ? PyTuple.FromOwnedArray(items)
            : PyTuple.FromOwnedArray(items, _memoryGovernor, _allocationSpan);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public object[] ToArray()
    {
        var bytes = EstimateApproximateBytes(Count);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var result = this.ToArrayCore();
        storage?.Commit(bytes);
        return result;
    }

    private object[] ToArrayCore()
    {
        if (_items is object[] objects) return objects.ToArray();
        var result = new object[Count];
        for (var i = 0; i < result.Length; i++)
        {
            if ((i & 1023) == 0) PyStructuralGuard.AmbientContext?.CheckExecution(_allocationSpan);
            result[i] = this[i];
        }
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
