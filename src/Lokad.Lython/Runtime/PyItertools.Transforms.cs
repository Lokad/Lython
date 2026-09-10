using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyAccumulateIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ICallable? _function;
    private readonly object _initial;
    private readonly bool _hasInitial;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private object _total = PyNone.Instance;
    private bool _started;

    public PyAccumulateIterator(
        object source,
        LythonRuntime.ICallable? function,
        object initial,
        bool hasInitial,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _function = function;
        _initial = initial;
        _hasInitial = hasInitial;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecutionBudget(_span);
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
            ? LythonRuntime.RuntimeValue(LythonRuntime.AddRuntimeValues(_total, next, _context, _span))
            : LythonRuntime.RuntimeValue(CallableInvocation.InvokeBinary(_function, _total, next, _span, _context));
        value = _total;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecutionBudget(_span);
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
            ? LythonRuntime.RuntimeValue(LythonRuntime.AddRuntimeValues(_total, next, _context, _span))
            : LythonRuntime.RuntimeValue(await CallableInvocation.InvokeBinaryAsync(_function, _total, next, _span, _context).ConfigureAwait(false));
        return PyIterationResult.Yield(_total);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.accumulate object>");
}

internal sealed class PyCompressIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _data;
    private readonly PyIteration.Cursor _selectors;

    public PyCompressIterator(object data, object selectors, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        _data = PyIteration.Cursor.Create(data, span, context);
        _selectors = PyIteration.Cursor.Create(selectors, span, context);
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        while (_data.TryMoveNext(out var data) && _selectors.TryMoveNext(out var selector))
        {
            if (PyTruthiness.IsTruthy(selector))
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

            if (PyTruthiness.IsTruthy(selector))
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
    private readonly LythonRuntime.ICallable? _predicate;
    private readonly PyIteration.Cursor _source;
    private readonly PyPredicateIteratorMode _mode;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private bool _dropping = true;
    private bool _done;

    public PyPredicateIterator(
        LythonRuntime.ICallable? predicate,
        object source,
        PyPredicateIteratorMode mode,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        _predicate = predicate;
        _source = PyIteration.Cursor.Create(source, span, context);
        _mode = mode;
        _context = context;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecutionBudget(_span);
        if (_done)
        {
            value = PyNone.Instance;
            return false;
        }

        while (_source.TryMoveNext(out var current))
        {
            var item = LythonRuntime.RuntimeValue(current);
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
                    if (!_dropping || !matched)
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
        _context.CheckExecutionBudget(_span);
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
                    if (!_dropping || !matched)
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
            ? PyTruthiness.IsTruthy(item)
            : PyTruthiness.IsTruthy(CallableInvocation.InvokeUnary(_predicate, item, _span, _context));

    private async ValueTask<bool> PredicateMatchesAsync(object item)
        => _predicate is null
            ? PyTruthiness.IsTruthy(item)
            : PyTruthiness.IsTruthy(await CallableInvocation.InvokeUnaryAsync(_predicate, item, _span, _context).ConfigureAwait(false));
}

internal sealed class PyStarmapIterator : PyIteratorBase
{
    private readonly LythonRuntime.ICallable _function;
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public PyStarmapIterator(LythonRuntime.ICallable function, object source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        PyIteratorBase.ChargeIteratorValue(context.MemoryGovernor, span);
        _function = function;
        _source = PyIteration.Cursor.Create(source, span, context);
        _context = context;
        _span = span;
    }

    // Per-element argument arrays scale with the element, not the output: one
    // 24B CallArgumentValue slot (placement enum plus two references) per item,
    // live beside the drained element while the call runs. The sync shape drains
    // lazily, so it grows the reservation incrementally like the shared drains;
    // the async shape reuses MaterializeAsync and covers the exact final array.
    private const long ArgumentSlotBytes = 24;

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        _context.CheckExecutionBudget(_span);
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
            }

            collected.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(item)));
            if (collected.Capacity > chargedCapacity)
            {
                scratch.Grow(ArgumentSlotBytes * (collected.Capacity - chargedCapacity), _span);
                chargedCapacity = collected.Capacity;
            }
        }

        scratch.Grow(ArgumentSlotBytes * collected.Count, _span);
        var args = collected.ToArray();
        value = LythonRuntime.RuntimeValue(_function.Invoke(args, _span, _context));
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecutionBudget(_span);
        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        var values = await PyIteration.MaterializeAsync(current, _span, _context).ConfigureAwait(false);
        // The drained list stays governed beside this exact-size copy, released
        // after the call completes.
        using var scratch = _context.MemoryGovernor.ReserveTemporary(checked(ArgumentSlotBytes * (long)values.Count), _span);
        var args = values.Count == 0
            ? Array.Empty<CallArgumentValue>()
            : new CallArgumentValue[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            args[index] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(values[index]));
        }

        var value = LythonRuntime.RuntimeValue(await _function.InvokeAsync(args, _span, _context).ConfigureAwait(false));
        return PyIterationResult.Yield(value);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.starmap object>");
}

internal sealed class PyPairwiseIterator : PyIteratorBase
{
    private readonly PyIteration.Cursor _source;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private object _previous = PyNone.Instance;
    private bool _hasPrevious;

    public PyPairwiseIterator(object source, MemoryGovernor memoryGovernor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        PyIteratorBase.ChargeIteratorValue(memoryGovernor, span);
        _source = PyIteration.Cursor.Create(source, span, context);
        _memoryGovernor = memoryGovernor;
        _span = span;
    }

    public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (!_hasPrevious)
        {
            if (!_source.TryMoveNext(out var first))
            {
                value = PyNone.Instance;
                return false;
            }

            _previous = LythonRuntime.RuntimeValue(first);
            _hasPrevious = true;
        }

        if (!_source.TryMoveNext(out var next))
        {
            value = PyNone.Instance;
            return false;
        }

        var current = LythonRuntime.RuntimeValue(next);
        value = PyTuple.FromOwnedArray([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return true;
    }

    public override async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (!_hasPrevious)
        {
            var (hasFirst, first) = await _source.TryMoveNextAsync().ConfigureAwait(false);
            if (!hasFirst)
            {
                return PyIterationResult.End;
            }

            _previous = LythonRuntime.RuntimeValue(first);
            _hasPrevious = true;
        }

        var (hasNext, next) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasNext)
        {
            return PyIterationResult.End;
        }

        var current = LythonRuntime.RuntimeValue(next);
        var value = PyTuple.FromOwnedArray([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return PyIterationResult.Yield(value);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.pairwise object>");
}
