using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// The dictionary iterator captures the key version now, but reads each count
// on its first pull. Repetition retains the original key without copying it.
internal sealed class PyCounterElementsIterator : PyIteratorBase
{
    private readonly IEnumerator<KeyValuePair<object, object>> _entries;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _item = PyNone.Instance;
    private long _remaining;
    private bool _exhausted;

    internal PyCounterElementsIterator(PyCounter source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Fund the iterator and its retained dictionary-enumerator state.
        using var storage = context.MemoryGovernor.ReserveTemporary(2 * IteratorValueBytes, span);
        _entries = source.GetEnumerator();
        _context = context;
        _span = span;
        storage.Commit(2 * IteratorValueBytes);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        var result = AdvanceAsync(false).GetAwaiter().GetResult();
        value = result.HasValue ? result.Value : null;
        return result.HasValue;
    }

    public override ValueTask<PyIterationResult> TryMoveNextAsync() => AdvanceAsync(true);

    private async ValueTask<PyIterationResult> AdvanceAsync(bool asynchronous)
    {
        while (!_exhausted)
        {
            _context.CheckExecution(_span);
            if (_remaining > 0)
            {
                _remaining--;
                return PyIterationResult.Yield(_item);
            }
            if (!_entries.MoveNext())
            {
                _exhausted = true;
                _entries.Dispose();
                _item = PyNone.Instance;
                break;
            }
            var pair = _entries.Current;
            var converted = await LythonRuntime.CoerceTypeIndexAsync(pair.Value, _context, _span, asynchronous).ConfigureAwait(false);
            if (converted is null || !PyNumberOps.TryAsInteger(converted, out var count))
                throw new LythonRuntimeException("TypeError", "Counter.elements() counts must be integers.", _span);
            if (count > long.MaxValue || count < long.MinValue)
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", _span);
            _item = pair.Key;
            _remaining = Math.Max(0, (long)count);
        }
        return PyIterationResult.End;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<iterator object>");
}
