using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

// N02: bounded batching without sized preallocation.
// Storage grows with consumed input (never new object[_size] upfront), so an
// empty or short source with a huge requested size never allocates it. Each
// pull observes the batch count (collection limit + step budget) and ensures
// the next growth before it can allocate, so denial precedes large allocation
// in both modes. An exhaustion latch stops allocating or pulling after the
// source ends. Strict shortfall preserves ValueError semantics.
internal sealed class PyBatchedIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly int _size;
    private readonly bool _strict;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private readonly ChargeReclamationPool? _reclamationPool;
    private readonly LythonRuntime.ExecutionContext _context;
    private bool _exhausted;

    public PyBatchedIterator(object source, int size, bool strict, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _size = size;
        _strict = strict;
        _memoryGovernor = memoryGovernor;
        _span = span;
        _reclamationPool = context.Services.State.CallTemporaries;
        _context = context;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_exhausted)
        {
            value = PyNone.Instance;
            return false;
        }

        _context.CheckExecutionBudget(_span);
        var buffer = Array.Empty<object>();
        var count = 0;
        while (count < _size && _source.TryMoveNext(out var current))
        {
            if (count == buffer.Length)
            {
                var nextCapacity = buffer.Length == 0 ? Math.Min(_size, 4) : Math.Min(_size, checked(buffer.Length * 2));
                _memoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(nextCapacity), _span);
                Array.Resize(ref buffer, nextCapacity);
            }

            buffer[count++] = LythonRuntime.RuntimeValue(current);
            _context.ObserveCollectionCount(count, _span);
            if ((count & 63) == 0)
            {
                _context.CheckExecutionBudget(_span);
            }
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

        value = CreateBatch(buffer, count);
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_exhausted)
        {
            return PyIterationResult.End;
        }

        _context.CheckExecutionBudget(_span);
        var buffer = Array.Empty<object>();
        var count = 0;
        while (count < _size)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                break;
            }

            if (count == buffer.Length)
            {
                var nextCapacity = buffer.Length == 0 ? Math.Min(_size, 4) : Math.Min(_size, checked(buffer.Length * 2));
                _memoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(nextCapacity), _span);
                Array.Resize(ref buffer, nextCapacity);
            }

            buffer[count++] = LythonRuntime.RuntimeValue(current);
            _context.ObserveCollectionCount(count, _span);
            if ((count & 63) == 0)
            {
                _context.CheckExecutionBudget(_span);
            }
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

        return PyIterationResult.Yield(CreateBatch(buffer, count));
    }

    private PyTuple CreateBatch(object[] buffer, int count)
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
            _memoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), _span);
            owned = new object[count];
            Array.Copy(buffer, owned, count);
        }

        var produced = PyTuple.FromOwnedArray(owned, _memoryGovernor, _span);
        _reclamationPool?.TrackFreshMutable(produced, produced.CommittedStorageBytes, _span);
        return produced;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.batched object>");
}
