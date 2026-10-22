using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Bounded batching without sized preallocation.
// Storage grows with consumed input (never new object[_size] upfront), so an
// empty or short source with a huge requested size never allocates it. Each
// pull observes cancellation and admits the next capacity/overlap through the
// shared allowance before a source pull. Scratch stays owned across suspension,
// replacement and final copies.
// An exhaustion latch stops allocating or pulling after the source ends.
// Strict shortfall preserves ValueError semantics.
internal sealed class PyBatchedIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly long _size;
    private readonly bool _strict;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _exhausted;

    public PyBatchedIterator(object source, long size, bool strict, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _size = size;
        _strict = strict;
        _memoryGovernor = memoryGovernor;
        _span = span;
        _context = context;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_exhausted)
        {
            value = PyNone.Instance;
            return false;
        }

        _context.CheckExecution(_span);
        using var storage = _memoryGovernor.ReserveTemporary(0, _span);
        var buffer = Array.Empty<object>();
        var count = 0;
        while (count < _size)
        {
            GrowBufferIfNeeded(ref buffer, count, storage);
            if (!_source.TryMoveNext(out var current)) break;

            buffer[count++] = LythonRuntime.RuntimeValue(current);
            _context.CheckExecution(_span);
        }

        if (count == 0)
        {
            _exhausted = true;
            value = PyNone.Instance;
            return false;
        }

        if (count < _size)
        {
            _exhausted = true;
            if (_strict)
            {
                throw new LythonRuntimeException("ValueError", "batched(): incomplete batch", _span);
            }
        }

        value = CreateBatch(buffer, count, storage);
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_exhausted)
        {
            return PyIterationResult.End;
        }

        _context.CheckExecution(_span);
        using var storage = _memoryGovernor.ReserveTemporary(0, _span);
        var buffer = Array.Empty<object>();
        var count = 0;
        while (count < _size)
        {
            GrowBufferIfNeeded(ref buffer, count, storage);
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                break;
            }

            buffer[count++] = LythonRuntime.RuntimeValue(current);
            _context.CheckExecution(_span);
        }

        if (count == 0)
        {
            _exhausted = true;
            return PyIterationResult.End;
        }

        if (count < _size)
        {
            _exhausted = true;
            if (_strict)
            {
                throw new LythonRuntimeException("ValueError", "batched(): incomplete batch", _span);
            }
        }

        return PyIterationResult.Yield(CreateBatch(buffer, count, storage));
    }

    private void GrowBufferIfNeeded(ref object[] buffer, int count, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        if (count < buffer.Length) return;
        var previousBytes = buffer.Length == 0 ? 0 : PyTuple.EstimateApproximateBytes(buffer.Length);
        var next = Math.Min(_size, buffer.Length == 0 ? 4L : 2L * buffer.Length);
        if (next > Array.MaxLength) throw RuntimeErrors.Memory("batch exceeds managed array capacity", _span);
        var nextCapacity = (int)next;
        storage.Grow(PyTuple.EstimateApproximateBytes(nextCapacity), _span);
        Array.Resize(ref buffer, nextCapacity);
        storage.Shrink(previousBytes);
    }

    private PyTuple CreateBatch(object[] buffer, int count, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        object[] owned;
        if (count == buffer.Length)
        {
            // Fresh buffer sized exactly to the batch transfers without copying.
            // Escape proof: buffer was allocated in this pull and never escapes
            // except through the returned tuple.
            owned = buffer;
        }
        else
        {
            storage.Grow(PyTuple.EstimateApproximateBytes(count), _span);
            owned = new object[count];
            Array.Copy(buffer, owned, count);
        }

        return PyTuple.FromReservedOwnedArray(owned, _memoryGovernor, _span, storage);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.batched object>");
}
