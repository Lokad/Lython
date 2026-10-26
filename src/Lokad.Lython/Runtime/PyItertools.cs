using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal abstract class PyIteratorBase : IPyAsyncIteratorValue, IPyRenderableValue, IPyTruthyValue, IPyDynamicAttributes
{
    // Iterator objects retain callbacks, cursors and spans per live iterator;
    // charge one constructed-value unit when a governor is present.
    internal const long IteratorValueBytes = 128;

    internal static void ChargeIteratorValue(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is null)
        {
            return;
        }

        governor.Reserve(IteratorValueBytes, span);
        governor.Commit(IteratorValueBytes);
    }

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);

    public IAsyncEnumerable<object> IterateAsync() => PyIteration.EnumerateAsyncIterator(this);

    /// <summary>Advances once and supplies the current Python value when successful.</summary>
    public abstract bool TryMoveNext([MaybeNullWhen(false)] out object value);

    /// <summary>Provides async iteration with semantics identical to <see cref="TryMoveNext"/>.</summary>
    public virtual ValueTask<PyIterationResult> TryMoveNextAsync()
        => ValueTask.FromResult<PyIterationResult>(
            TryMoveNext(out var value)
                ? PyIterationResult.Yield(value)
                : PyIterationResult.End);

    public bool IsTruthy() => true;

    /// <summary>Renders the iterator using its Python-facing representation.</summary>
    public abstract PyString RenderPython(PyRenderingContext context);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public virtual bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        => LythonRuntime.IteratorMembers.TryGetMember(this, name, out value);
}

internal sealed class PyChainIterator : PyIteratorBase
{
    private PyIteration.Cursor? _outer;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private PyIteration.Cursor? _current;

    private PyChainIterator(PyIteration.Cursor outer, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _outer = outer; _span = span; _context = context;
    }

    internal static PyChainIterator FromSources(CallArgumentValue[] arguments, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        context = LythonRuntime.GetGlobalContext(context);
        var bytes = checked(512L + 16L * arguments.Length);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        var sources = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++) sources[i] = arguments[i].Value;
        var result = new PyChainIterator(PyIteration.Cursor.Create(sources, span, context), span, context);
        storage.Commit(bytes);
        return result;
    }

    internal static async ValueTask<PyChainIterator> FromIterableAsync(object source, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        context = LythonRuntime.GetGlobalContext(context);
        using var storage = context.MemoryGovernor.ReserveTemporary(512, span);
        var cursor = asynchronous ? await PyIteration.Cursor.CreateAsync(source, span, context).ConfigureAwait(false)
            : PyIteration.Cursor.Create(source, span, context);
        var result = new PyChainIterator(cursor, span, context);
        storage.Commit(512);
        return result;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        while (_outer is not null)
        {
            _context.CheckExecution(_span);
            if (_current is not null)
            {
                if (_current.TryMoveNext(out value)) { value = LythonRuntime.RuntimeValue(value); return true; }
                _current.Dispose(); _current = null;
            }
            if (!_outer.TryMoveNext(out var source)) { var ended = _outer; _outer = null; ended.Dispose(); break; }
            try { _current = PyIteration.Cursor.Create(source, _span, _context); }
            catch { var ended = _outer; _outer = null; ended.Dispose(); throw; }
        }
        value = PyNone.Instance;
        return false;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        while (_outer is not null)
        {
            _context.CheckExecution(_span);
            if (_current is not null)
            {
                var item = await _current.TryMoveNextAsync().ConfigureAwait(false);
                if (item.HasValue) return PyIterationResult.Yield(LythonRuntime.RuntimeValue(item.Value));
                await _current.DisposeAsync().ConfigureAwait(false); _current = null;
            }
            var next = await _outer.TryMoveNextAsync().ConfigureAwait(false);
            if (!next.HasValue) { var ended = _outer; _outer = null; await ended.DisposeAsync().ConfigureAwait(false); break; }
            try { _current = await PyIteration.Cursor.CreateAsync(next.Value, _span, _context).ConfigureAwait(false); }
            catch { var ended = _outer; _outer = null; await ended.DisposeAsync().ConfigureAwait(false); throw; }
        }
        return PyIterationResult.End;
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.chain object>");
}

internal sealed class PyIsliceIterator : PyIteratorBase
{
    private PyIteration.Cursor? _source;
    private readonly long? _stop;
    private readonly long _step;
    private long _skip, _position;
    private bool _initial = true;

    public PyIsliceIterator(PyIteration.Cursor source, long start, long? stop, long step)
    { _source = source; _skip = start; _stop = stop; _step = step; }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        var source = _source;
        if (source is null) return false;
        while (_skip > 0 && (_initial || _stop is null || _position < _stop))
        {
            if (!source.TryMoveNext(out _)) { _source = null; source.Dispose(); return false; }
            if (_position < long.MaxValue) _position++;
            _skip--;
        }
        _initial = false;
        if (_stop is { } stop && _position >= stop || !source.TryMoveNext(out var item))
        { _source = null; source.Dispose(); return false; }
        if (_position < long.MaxValue) _position++;
        _skip = _step - 1;
        value = LythonRuntime.RuntimeValue(item);
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var source = _source;
        if (source is null) return PyIterationResult.End;
        while (_skip > 0 && (_initial || _stop is null || _position < _stop))
        {
            var skipped = await source.TryMoveNextAsync().ConfigureAwait(false);
            if (!skipped.HasValue) { _source = null; await source.DisposeAsync().ConfigureAwait(false); return PyIterationResult.End; }
            if (_position < long.MaxValue) _position++;
            _skip--;
        }
        _initial = false;
        if (_stop is { } stop && _position >= stop)
        { _source = null; await source.DisposeAsync().ConfigureAwait(false); return PyIterationResult.End; }
        var item = await source.TryMoveNextAsync().ConfigureAwait(false);
        if (!item.HasValue) { _source = null; await source.DisposeAsync().ConfigureAwait(false); return PyIterationResult.End; }
        if (_position < long.MaxValue) _position++;
        _skip = _step - 1;
        return PyIterationResult.Yield(LythonRuntime.RuntimeValue(item.Value));
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.islice object>");
}
