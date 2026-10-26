using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyAccumulateIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly object? _function;
    private readonly object _initial;
    private readonly bool _hasInitial;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _total = PyNone.Instance;
    private bool _started;

    public PyAccumulateIterator(
        PyIteration.Cursor source,
        object? function,
        object initial,
        bool hasInitial,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _source = source;
        _function = function;
        _initial = initial;
        _hasInitial = hasInitial;
        _context = LythonRuntime.GetGlobalContext(context);
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (!_started)
        {
            _started = true;
            if (_hasInitial)
            {
                _total = LythonRuntime.RuntimeValue(_initial);
                value = _total;
                return true;
            }

            if (!_source.TryMoveNext(out var first))
            {
                value = PyNone.Instance;
                return false;
            }

            _total = LythonRuntime.RuntimeValue(first);
            value = _total;
            return true;
        }

        if (!_source.TryMoveNext(out var current))
        {
            value = PyNone.Instance;
            return false;
        }

        var next = LythonRuntime.RuntimeValue(current);
        _total = _function is null
            ? LythonRuntime.RuntimeValue(LythonRuntime.EvaluateBinaryOperator(BinaryOperatorSyntax.Add, _total, next, _context, _span))
            : LythonRuntime.RuntimeValue(CallableInvocation.InvokeBinary(_function, _total, next, _span, _context));
        value = _total;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        if (!_started)
        {
            _started = true;
            if (_hasInitial)
            {
                _total = LythonRuntime.RuntimeValue(_initial);
                return PyIterationResult.Yield(_total);
            }

            var (hasFirst, first) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasFirst)
            {
                return PyIterationResult.End;
            }

            _total = LythonRuntime.RuntimeValue(first);
            return PyIterationResult.Yield(_total);
        }

        var (hasCurrent, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasCurrent)
        {
            return PyIterationResult.End;
        }

        var next = LythonRuntime.RuntimeValue(current);
        _total = _function is null
            ? LythonRuntime.RuntimeValue(await LythonRuntime.EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, _total, next, _context, _span).ConfigureAwait(false))
            : LythonRuntime.RuntimeValue(await CallableInvocation.InvokeBinaryAsync(_function, _total, next, _span, _context).ConfigureAwait(false));
        return PyIterationResult.Yield(_total);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.accumulate object>");
}

internal sealed class PyCompressIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _data;
    private readonly PyIteration.Cursor _selectors;

    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyCompressIterator(PyIteration.Cursor data, PyIteration.Cursor selectors, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        _data = data; _selectors = selectors;
        _context = LythonRuntime.GetGlobalContext(context); _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        while (_data.TryMoveNext(out var data) && _selectors.TryMoveNext(out var selector))
        {
            if (LythonRuntime.IsTruthy(selector, _context, _span))
            {
                value = LythonRuntime.RuntimeValue(data);
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        while (true)
        {
            var (hasData, data) = await _data.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasData)
            {
                return PyIterationResult.End;
            }

            var (hasSelector, selector) = await _selectors.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasSelector)
            {
                return PyIterationResult.End;
            }

            if (await LythonRuntime.IsTruthyAsync(selector, _context, _span).ConfigureAwait(false))
            {
                return PyIterationResult.Yield(LythonRuntime.RuntimeValue(data));
            }
        }
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.compress object>");
}

internal enum PyPredicateIteratorMode
{
    FilterFalse,
    DropWhile,
    TakeWhile,
}

internal sealed class PyPredicateIterator : PyIteratorBase
{
    private readonly object? _predicate;
    private readonly PyIteration.Cursor _source;
    private readonly PyPredicateIteratorMode _mode;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _dropping = true;
    private bool _done;

    public PyPredicateIterator(
        object? predicate,
        PyIteration.Cursor source,
        PyPredicateIteratorMode mode,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        _predicate = predicate;
        _source = source;
        _mode = mode;
        _context = LythonRuntime.GetGlobalContext(context);
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        while (_source.TryMoveNext(out var current))
        {
            var item = LythonRuntime.RuntimeValue(current);
            if (_mode == PyPredicateIteratorMode.DropWhile && !_dropping) { value = item; return true; }
            var matched = PredicateMatches(item);
            switch (_mode)
            {
                case PyPredicateIteratorMode.FilterFalse:
                    if (!matched)
                    {
                        value = item;
                        return true;
                    }

                    break;

                case PyPredicateIteratorMode.DropWhile:
                    if (!matched)
                    {
                        _dropping = false;
                        value = item;
                        return true;
                    }

                    break;

                case PyPredicateIteratorMode.TakeWhile:
                    if (!matched)
                    {
                        _done = true;
                        value = PyNone.Instance;
                        return false;
                    }

                    value = item;
                    return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        if (_done)
        {
            return PyIterationResult.End;
        }

        while (true)
        {
            var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasValue)
            {
                return PyIterationResult.End;
            }

            var item = LythonRuntime.RuntimeValue(current);
            if (_mode == PyPredicateIteratorMode.DropWhile && !_dropping) return PyIterationResult.Yield(item);
            var matched = await PredicateMatchesAsync(item).ConfigureAwait(false);
            switch (_mode)
            {
                case PyPredicateIteratorMode.FilterFalse:
                    if (!matched)
                    {
                        return PyIterationResult.Yield(item);
                    }

                    break;

                case PyPredicateIteratorMode.DropWhile:
                    if (!matched)
                    {
                        _dropping = false;
                        return PyIterationResult.Yield(item);
                    }

                    break;

                case PyPredicateIteratorMode.TakeWhile:
                    if (!matched)
                    {
                        _done = true;
                        return PyIterationResult.End;
                    }

                    return PyIterationResult.Yield(item);
            }
        }
    }

    public override PyString RenderPython(PyRenderingContext context)
        => PyString.FromString(_mode switch
        {
            PyPredicateIteratorMode.FilterFalse => "<itertools.filterfalse object>",
            PyPredicateIteratorMode.DropWhile => "<itertools.dropwhile object>",
            _ => "<itertools.takewhile object>",
        });

    private bool PredicateMatches(object item)
        => _predicate is null
            ? LythonRuntime.IsTruthy(item, _context, _span)
            : LythonRuntime.IsTruthy(CallableInvocation.InvokeUnary(_predicate, item, _span, _context), _context, _span);

    private async ValueTask<bool> PredicateMatchesAsync(object item)
        => _predicate is null
            ? await LythonRuntime.IsTruthyAsync(item, _context, _span).ConfigureAwait(false)
            : await LythonRuntime.IsTruthyAsync(await CallableInvocation.InvokeUnaryAsync(_predicate, item, _span, _context).ConfigureAwait(false), _context, _span).ConfigureAwait(false);
}

internal sealed class PyStarmapIterator : PyIteratorBase
{
    private readonly object _function;
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyStarmapIterator(object function, PyIteration.Cursor source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _function = function;
        _source = source;
        _context = LythonRuntime.GetGlobalContext(context);
        _span = span;
    }

    // Per-element argument arrays scale with the element, not the output: one
    // 24B CallArgumentValue slot (placement enum plus two references) per item,
    // live beside the drained element while the call runs. The sync shape drains
    // lazily, so it grows the reservation incrementally like the shared drains;
    // the async shape reuses the leased drain and covers the exact final array.
    private const long ArgumentSlotBytes = 24;

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecution(_span);
        if (!_source.TryMoveNext(out var current))
        {
            value = PyNone.Instance;
            return false;
        }

        // The argument array duplicates the streamed element beside it; grow a
        // transient reservation with the backing list (mirroring the shared
        // drains) and cover the final exact array, released after the call.
        using var scratch = _context.MemoryGovernor.ReserveTemporary(0, _span);
        var collected = new List<CallArgumentValue>();
        var chargedCapacity = 0;
        foreach (var item in LythonRuntime.ToSequence(current, _span, _context))
        {
            if (collected.Count == collected.Capacity)
            {
                var predicted = collected.Capacity == 0 ? 4L : (long)collected.Capacity * 2L;
                scratch.Grow(checked(ArgumentSlotBytes * (predicted - chargedCapacity)), _span);
                chargedCapacity = (int)predicted;
            }

            collected.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(item)));
            _context.CheckExecution(_span);
            if (collected.Capacity > chargedCapacity)
            {
                scratch.Grow(ArgumentSlotBytes * (collected.Capacity - chargedCapacity), _span);
                chargedCapacity = collected.Capacity;
            }
        }

        scratch.Grow(ArgumentSlotBytes * collected.Count, _span);
        var args = collected.ToArray();
        value = LythonRuntime.RuntimeValue(LythonRuntime.InvokeCallableTarget(_function, _span, _span, _context, () => args));
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        // N09: leased drain keeps the element list charged beside the argument array
        // and callback (reservation ends after the call, not before).
        using var lease = await PyIteration.MaterializeLeasedAsync(current, _span, _context).ConfigureAwait(false);
        var values = lease.Items;
        // The drained list stays leased beside this exact-size copy, released
        // after the call completes.
        using var scratch = _context.MemoryGovernor.ReserveTemporary(checked(ArgumentSlotBytes * (long)values.Count), _span);
        var args = values.Count == 0
            ? Array.Empty<CallArgumentValue>()
            : new CallArgumentValue[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            args[index] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(values[index]));
        }

        var value = LythonRuntime.RuntimeValue(await LythonRuntime.InvokeCallableTargetAsync(_function, _span, _span, _context, args).ConfigureAwait(false));
        return PyIterationResult.Yield(value);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.starmap object>");
}

internal sealed class PyPairwiseIterator : PyIteratorBase
{
    private PyIteration.Cursor? _source;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private object _previous = PyNone.Instance;
    private bool _hasPrevious;

    public PyPairwiseIterator(PyIteration.Cursor source, MemoryGovernor governor, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    { _source = source; _memoryGovernor = governor; _span = span; }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        var source = _source;
        if (source is null) return false;
        if (!_hasPrevious)
        {
            if (!source.TryMoveNext(out var first)) { _source = null; source.Dispose(); return false; }
            _previous = LythonRuntime.RuntimeValue(first); _hasPrevious = true;
        }
        if (!source.TryMoveNext(out var next)) { _source = null; _previous = PyNone.Instance; source.Dispose(); return false; }
        var current = LythonRuntime.RuntimeValue(next);
        value = PyTuple.FromOwnedArray([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        var source = _source;
        if (source is null) return PyIterationResult.End;
        if (!_hasPrevious)
        {
            var first = await source.TryMoveNextAsync().ConfigureAwait(false);
            if (!first.HasValue) { _source = null; await source.DisposeAsync().ConfigureAwait(false); return PyIterationResult.End; }
            _previous = LythonRuntime.RuntimeValue(first.Value); _hasPrevious = true;
        }
        var next = await source.TryMoveNextAsync().ConfigureAwait(false);
        if (!next.HasValue) { _source = null; _previous = PyNone.Instance; await source.DisposeAsync().ConfigureAwait(false); return PyIterationResult.End; }
        var current = LythonRuntime.RuntimeValue(next.Value);
        var value = PyTuple.FromOwnedArray([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return PyIterationResult.Yield(value);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.pairwise object>");
}
