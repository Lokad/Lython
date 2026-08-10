using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal abstract class PyIteratorBase : IPyAsyncIteratorValue, IPyRenderableValue, IPyTruthyValue
{
    public IEnumerable<object> Iterate()
    {
        while (TryMoveNext(out var value))
        {
            yield return value;
        }
    }

    public async IAsyncEnumerable<object> IterateAsync()
    {
        while (true)
        {
            var (hasValue, value) = await TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                yield break;
            }

            yield return value;
        }
    }

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
}

internal sealed class PyChainIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor[]? _sources;
    private readonly PyIteration.Cursor? _outer;
    private readonly LythonSourceSpan _span;
    private PyIteration.Cursor? _current;
    private int _sourceIndex;

    public PyChainIterator(IReadOnlyList<object> sources, LythonSourceSpan span)
    {
        _sources = new PyIteration.Cursor[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            _sources[i] = PyIteration.Cursor.Create(sources[i], span);
        }

        _span = span;
    }

    public PyChainIterator(object outer, LythonSourceSpan span)
    {
        _outer = PyIteration.Cursor.Create(outer, span);
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        while (true)
        {
            if (_current is not null && _current.TryMoveNext(out value))
            {
                value = LythonRuntime.RuntimeValue(value);
                return true;
            }

            if (!TryOpenNextCursor(out var next))
            {
                value = PyNone.Instance;
                return false;
            }

            _current = next.RequireNotNull();
        }
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        while (true)
        {
            if (_current is not null)
            {
                var (hasCurrent, current) = await _current.TryMoveNextAsync().ConfigureAwait(false);
                if (hasCurrent)
                {
                    return PyIterationResult.Yield(LythonRuntime.RuntimeValue(current));
                }
            }

            var (hasNext, next) = await TryOpenNextCursorAsync().ConfigureAwait(false);
            if (!hasNext)
            {
                return PyIterationResult.End;
            }

            _current = next;
        }
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.chain object>");

    private bool TryOpenNextCursor([MaybeNullWhen(false)] out PyIteration.Cursor cursor)
    {
        if (_sources is not null)
        {
            if (_sourceIndex >= _sources.Length)
            {
                cursor = null;
                return false;
            }

            cursor = _sources[_sourceIndex++];
            return true;
        }

        if (_outer is not null && _outer.TryMoveNext(out var nested))
        {
            cursor = PyIteration.Cursor.Create(nested, _span);
            return true;
        }

        cursor = null;
        return false;
    }

    private async ValueTask<(bool HasValue, PyIteration.Cursor? Cursor)> TryOpenNextCursorAsync()
    {
        if (_sources is not null)
        {
            if (_sourceIndex >= _sources.Length)
            {
                return (false, null);
            }

            return (true, _sources[_sourceIndex++]);
        }

        if (_outer is not null)
        {
            var (hasNested, nested) = await _outer.TryMoveNextAsync().ConfigureAwait(false);
            if (hasNested)
            {
                return (true, PyIteration.Cursor.Create(nested, _span));
            }
        }

        return (false, null);
    }
}

internal sealed class PyIsliceIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly long _start;
    private readonly long? _stop;
    private readonly long _step;
    private long _position;
    private bool _skippedStart;

    public PyIsliceIterator(object source, long start, long? stop, long step, LythonSourceSpan span)
    {
        _source = PyIteration.Cursor.Create(source, span);
        _start = start;
        _stop = stop;
        _step = step;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (!SkipStart())
        {
            value = PyNone.Instance;
            return false;
        }

        if (_stop is { } stop && _position >= stop)
        {
            value = PyNone.Instance;
            return false;
        }

        if (!_source.TryMoveNext(out var current))
        {
            value = PyNone.Instance;
            return false;
        }

        value = LythonRuntime.RuntimeValue(current);
        _position++;

        var skip = _step - 1;
        for (var i = 0L; i < skip && (_stop is not { } stopLimit || _position < stopLimit) && _source.TryMoveNext(out _); i++)
        {
            _position++;
        }

        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (!await SkipStartAsync().ConfigureAwait(false) || _stop is { } stop && _position >= stop)
        {
            return PyIterationResult.End;
        }

        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        _position++;

        var skip = _step - 1;
        for (var i = 0L; i < skip && (_stop is not { } stopLimit || _position < stopLimit); i++)
        {
            var (skipped, _) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!skipped)
            {
                break;
            }

            _position++;
        }

        return PyIterationResult.Yield(LythonRuntime.RuntimeValue(current));
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.islice object>");

    private bool SkipStart()
    {
        if (_skippedStart)
        {
            return true;
        }

        while (_position < _start)
        {
            if (!_source.TryMoveNext(out _))
            {
                return false;
            }

            _position++;
        }

        _skippedStart = true;
        return true;
    }

    private async ValueTask<bool> SkipStartAsync()
    {
        if (_skippedStart)
        {
            return true;
        }

        while (_position < _start)
        {
            var (hasValue, _) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                return false;
            }

            _position++;
        }

        _skippedStart = true;
        return true;
    }
}
