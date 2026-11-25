using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyEnumerableIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly string _displayName;
    private readonly object _hintSource;
    private int _position;
    private bool _exhausted;

    // Position is solely the Python length-hint state. Saturating at the maximum
    // possible container length neither limits nor changes iteration.
    internal int? RemainingHint => _hintSource switch
    {
        IPySizedValue sized => _exhausted ? 0 : Math.Max(0, sized.Length - _position),
        IReadOnlyCollection<object> sized => _exhausted ? 0 : Math.Max(0, sized.Count - _position),
        _ => null,
    };

    public PyEnumerableIterator(object source, LythonSourceSpan span, LythonRuntime.ExecutionContext context) : this(source, span, context, "iterator") { }

    public PyEnumerableIterator(object source, LythonSourceSpan span, LythonRuntime.ExecutionContext context, string displayName)
    {
        _source = PyIteration.Cursor.Create(source, span, context);
        _displayName = displayName;
        _hintSource = source;
        // Construction sites own the shell charge; this wrapper does not charge it twice.
    }

    public override PyIterationResult TryMoveNext()
    {
        var next = _source.TryMoveNext();
        if (!next.HasValue) { _exhausted = true; return next; }
        if (_position < int.MaxValue) _position++;
        return next;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var next = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!next.HasValue) { _exhausted = true; return next; }
        if (_position < int.MaxValue) _position++;
        return next;
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<" + _displayName + " object>");
    }
}

internal sealed class PyCallableSentinelIterator : PyIteratorBase
{
    private LythonRuntime.ICallable? _callable;
    private object? _sentinel;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyCallableSentinelIterator(LythonRuntime.ICallable callable, object sentinel,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _callable = callable; _sentinel = sentinel;
        _context = LythonRuntime.GetGlobalContext(context); _span = span;
        ChargeIteratorValue(context.MemoryGovernor, span);
    }

    public override PyIterationResult TryMoveNext()
    {
        _context.CheckExecution(_span);

        var callable = _callable;
        if (callable is null) return PyIterationResult.End;
        object result;
        try { result = LythonRuntime.RuntimeValue(callable.Invoke([], _span, _context)); }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration") { _callable = null; _sentinel = null; return PyIterationResult.End; }
        if (_sentinel is null || LythonRuntime.ElementEquals(_sentinel, result, _context, _span)) { _callable = null; _sentinel = null; return PyIterationResult.End; }
        return PyIterationResult.Yield(result);
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        var callable = _callable;
        if (callable is null) return PyIterationResult.End;
        object result;
        try { result = LythonRuntime.RuntimeValue(await callable.InvokeAsync([], _span, _context).ConfigureAwait(false)); }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration") { _callable = null; _sentinel = null; return PyIterationResult.End; }
        if (_sentinel is null || await LythonRuntime.ElementEqualsAsync(_sentinel, result, _context, _span).ConfigureAwait(false))
        { _callable = null; _sentinel = null; return PyIterationResult.End; }
        return PyIterationResult.Yield(result);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<callable_iterator object>");
}

internal sealed class PyReversedIterator : PyIteratorBase
{
    private readonly Func<int, object> _getIndex;
    private int _nextIndex;

    internal int RemainingHint => Math.Max(0, _nextIndex + 1);

    public PyReversedIterator(int length, Func<int, object> getIndex)
    {
        _nextIndex = length - 1;
        _getIndex = getIndex;
    }

    public override PyIterationResult TryMoveNext()
    {
        object? value;
        if (_nextIndex < 0)
        {
            return PyIterationResult.End;
        }

        value = LythonRuntime.RuntimeValue(_getIndex(_nextIndex));
        _nextIndex--;
        return PyIterationResult.Yield(value!);
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<reversed object>");
    }
}

internal sealed class PyMapIterator : PyIteratorBase
{
    [System.Runtime.CompilerServices.InlineArray(3)]
    private struct Operands
    {
        private LythonRuntime.ExecutableSlot _first;
    }

    private readonly object _function;
    private readonly PyIteration.Cursor[] _iterators;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    private PyMapIterator(object function, PyIteration.Cursor[] iterators,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _function = function; _iterators = iterators; _context = context; _span = span;
    }

    internal static async ValueTask<PyMapIterator> CreateAsync(object[] arguments,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context = LythonRuntime.GetGlobalContext(context);
        var count = arguments.Length - 1;
        var bytes = checked(IteratorValueBytes + 32L + 256L * count);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        var cursors = new PyIteration.Cursor[count];
        try
        {
            for (var i = 0; i < count; i++)
            {
                context.CheckExecution(span);
                cursors[i] = asynchronous
                    ? await PyIteration.Cursor.CreateAsync(arguments[i + 1], span, context).ConfigureAwait(false)
                    : PyIteration.Cursor.Create(arguments[i + 1], span, context);
            }
            var result = new PyMapIterator(arguments[0], cursors, context, span);
            storage.Commit(bytes);
            return result;
        }
        catch
        {
            foreach (var cursor in cursors)
                if (cursor is not null)
                {
                    if (asynchronous) await cursor.DisposeAsync().ConfigureAwait(false);
                    else cursor.Dispose();
                }
            throw;
        }
    }

    public override PyIterationResult TryMoveNext()
    {
        using var scratch = _iterators.Length > 3
            ? _context.MemoryGovernor.ReserveTemporary(32L + 24L * _iterators.Length, _span) : null;
        Operands inline = default;
        var arguments = _iterators.Length > 3 ? new CallArgumentValue[_iterators.Length] : null;
        for (var i = 0; i < _iterators.Length; i++)
        {
            var next = _iterators[i].TryMoveNext();
            if (!next.HasValue) return PyIterationResult.End;
            if (arguments is null) inline[i] = next.TakeSlotValue();
            else arguments[i] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(next.Value));
        }

        if (arguments is null)
        {
            var result = LythonRuntime.StartIterationCallback(_function, inline[.._iterators.Length], _span, _context,
                asynchronous: false, out _);
            System.Diagnostics.Debug.Assert(result.HasValue);
            return result;
        }
        var callable = _function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
        return PyIterationResult.Yield(callable.Invoke(arguments, _span, _context));
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        using var scratch = _iterators.Length > 3
            ? _context.MemoryGovernor.ReserveTemporary(32L + 24L * _iterators.Length, _span) : null;
        Operands inline = default;
        var arguments = _iterators.Length > 3 ? new CallArgumentValue[_iterators.Length] : null;
        for (var i = 0; i < _iterators.Length; i++)
        {
            var next = await _iterators[i].TryMoveNextAsync().ConfigureAwait(false);
            if (!next.HasValue) return PyIterationResult.End;
            if (arguments is null) inline[i] = next.TakeSlotValue();
            else arguments[i] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(next.Value));
        }

        if (arguments is null)
        {
            var result = LythonRuntime.StartIterationCallback(_function, inline[.._iterators.Length], _span, _context,
                asynchronous: true, out var pending);
            if (result.HasValue) return result;
            return PyIterationResult.Yield(await pending.ConfigureAwait(false));
        }
        var callable = _function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
        return PyIterationResult.Yield(await callable.InvokeAsync(arguments, _span, _context).ConfigureAwait(false));
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<map object>");
    }
}

internal sealed class PyFilterIterator : PyIteratorBase
{
    private readonly object? _function;
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyFilterIterator(
        object? function,
        PyIteration.Cursor source,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _function = function;
        _source = source;
        _context = LythonRuntime.GetGlobalContext(context);
        _span = span;
    }

    public override PyIterationResult TryMoveNext()
    {
        while ((_source.TryMoveNext() is { HasValue: true, Value: var current }))
        {
            var candidate = LythonRuntime.RuntimeValue(current);
            var keep = _function is null
                ? LythonRuntime.IsTruthy(candidate, _context, _span)
                : LythonRuntime.IsTruthy(CallableInvocation.InvokeUnary(_function, candidate, _span, _context), _context, _span);

            if (keep)
            {
                return PyIterationResult.Yield(candidate);
            }
        }

        return PyIterationResult.End;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        while (true)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                return PyIterationResult.End;
            }

            var candidate = LythonRuntime.RuntimeValue(current);
            var keep = _function is null
                ? await LythonRuntime.IsTruthyAsync(candidate, _context, _span).ConfigureAwait(false)
                : await LythonRuntime.IsTruthyAsync(await CallableInvocation.InvokeUnaryAsync(_function, candidate, _span, _context).ConfigureAwait(false), _context, _span).ConfigureAwait(false);

            if (keep)
            {
                return PyIterationResult.Yield(candidate);
            }
        }
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<filter object>");
    }
}
