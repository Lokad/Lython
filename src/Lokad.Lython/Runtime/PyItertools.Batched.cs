using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyBatchedIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly int _size;
    private readonly bool _strict;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;

    public PyBatchedIterator(object source, int size, bool strict, MemoryGovernor memoryGovernor, LythonSourceSpan span)
    {
        _source = PyIteration.Cursor.Create(source, span);
        _size = size;
        _strict = strict;
        _memoryGovernor = memoryGovernor;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        var items = new object[_size];
        var count = 0;
        while (count < _size && _source.TryMoveNext(out var current))
        {
            items[count++] = LythonRuntime.RuntimeValue(current);
        }

        if (count == 0)
        {
            value = PyNone.Instance;
            return false;
        }

        if (_strict && count < _size)
        {
            throw new LythonRuntimeException("ValueError", "batched(): incomplete batch", _span);
        }

        if (count != _size)
        {
            Array.Resize(ref items, count);
        }

        value = PyTuple.FromOwnedArray(items, _memoryGovernor, _span);
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var items = new object[_size];
        var count = 0;
        while (count < _size)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                break;
            }

            items[count++] = LythonRuntime.RuntimeValue(current);
        }

        if (count == 0)
        {
            return PyIterationResult.End;
        }

        if (_strict && count < _size)
        {
            throw new LythonRuntimeException("ValueError", "batched(): incomplete batch", _span);
        }

        if (count != _size)
        {
            Array.Resize(ref items, count);
        }

        return PyIterationResult.Yield(PyTuple.FromOwnedArray(items, _memoryGovernor, _span));
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.batched object>");
}
