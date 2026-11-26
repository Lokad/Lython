using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed class PyAccumulateIterator : PyIteratorBase, IPyAsyncIteratorValue
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

    public override PyIterationResult TryMoveNext()
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

            if (!(_source.TryMoveNext() is { HasValue: true, Value: var first }))
            {
                return PyIterationResult.End;
            }

            _total = LythonRuntime.RuntimeValue(first);
            return PyIterationResult.Yield(_total);
        }

        if (!(_source.TryMoveNext() is { HasValue: true, Value: var current }))
        {
            return PyIterationResult.End;
        }

        var next = LythonRuntime.RuntimeValue(current);
        _total = _function is null
            ? LythonRuntime.RuntimeValue(LythonRuntime.EvaluateBinaryOperator(BinaryOperatorSyntax.Add, _total, next, _context, _span))
            : LythonRuntime.RuntimeValue(CallableInvocation.InvokeBinary(_function, _total, next, _span, _context));
        return PyIterationResult.Yield(_total);
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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

internal sealed class PyCompressIterator : PyIteratorBase, IPyAsyncIteratorValue
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

    public override PyIterationResult TryMoveNext()
    {
        while ((_data.TryMoveNext() is { HasValue: true, Value: var data }) && (_selectors.TryMoveNext() is { HasValue: true, Value: var selector }))
        {
            if (LythonRuntime.IsTruthy(selector, _context, _span))
            {
                return PyIterationResult.Yield(LythonRuntime.RuntimeValue(data));
            }
        }

        return PyIterationResult.End;
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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

internal sealed class PyPredicateIterator : PyIteratorBase, IPyAsyncIteratorValue
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

    public override PyIterationResult TryMoveNext()
    {
        object? value;
        _context.CheckExecution(_span);
        if (_done)
        {
            return PyIterationResult.End;
        }

        while ((_source.TryMoveNext() is { HasValue: true, Value: var current }))
        {
            var item = LythonRuntime.RuntimeValue(current);
            if (_mode == PyPredicateIteratorMode.DropWhile && !_dropping) { return PyIterationResult.Yield(item); }
            var matched = PredicateMatches(item);
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

                    value = item;
                    return PyIterationResult.Yield(value!);
            }
        }

        return PyIterationResult.End;
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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

internal sealed class PyStarmapIterator : PyIteratorBase, IPyAsyncIteratorValue
{
    private readonly object _function;
    private readonly PyIteration.Cursor _source;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private readonly bool _borrowStorage;

    public PyStarmapIterator(object function, PyIteration.Cursor source, LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool borrowStorage)
    {
        _function = function;
        _source = source;
        _context = LythonRuntime.GetGlobalContext(context);
        _span = span;
        _borrowStorage = borrowStorage;
    }

    // Per-element argument arrays scale with the element, not the output: one
    // 24B CallArgumentValue slot (placement enum plus two references) per item,
    // live beside the drained element while the call runs. The sync shape drains
    // lazily, so it grows the reservation incrementally like the shared drains;
    // the async shape reuses the leased drain and covers the exact final array.
    private const long ArgumentSlotBytes = 24;

    private bool TryStartTupleCallback(object current, bool asynchronous, out PyIterationResult result,
        out ValueTask<object> pending)
    {
        result = default;
        pending = default;
        if (current is not PyTuple tuple || tuple.Count > LythonRuntime.InlineIterationOperandCount) return false;

        // Exact tuples own any deferred scalar identities. Keep those loans
        // through binding or immediate return, before a callback can suspend.
        LythonRuntime.IterationOperands operands = default;
        for (var i = 0; i < tuple.Count; i++)
        {
            _context.CheckExecution(_span);
            _ = tuple.TryReadTrailingSlot(tuple.Count - i, out operands[i]);
        }
        result = LythonRuntime.StartIterationCallback(_function, operands, tuple.Count, _borrowStorage, _span, _context,
            asynchronous, out pending);
        return true;
    }

    public override PyIterationResult TryMoveNext()
    {
        _context.CheckExecution(_span);
        if (_source.ShortZipForPull() is { } zip)
        {
            LythonRuntime.IterationOperands operands = default;
            zip.TryPullOperands(ref operands, asynchronous: false, out var hasRow, out _);
            return hasRow ? LythonRuntime.StartIterationCallback(_function, operands, zip.Arity, _borrowStorage,
                _span, _context, asynchronous: false, out _) : PyIterationResult.End;
        }
        if (!(_source.TryMoveNext() is { HasValue: true, Value: var current }))
        {
            return PyIterationResult.End;
        }

        if (TryStartTupleCallback(current, asynchronous: false, out var tupleResult, out _))
            return tupleResult;

        // The argument array duplicates the streamed element beside it; grow a
        // transient reservation with the backing list (mirroring the shared
        // drains) and cover the final exact array, released after the call.
        using var scratch = _context.MemoryGovernor.ReserveTemporary(0, _span);
        var collected = new List<CallArgumentValue>();
        var chargedCapacity = 0;
        foreach (var item in PyIteration.Cursor.Create(current, _span, _context))
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
        return PyIterationResult.Yield(LythonRuntime.RuntimeValue((_function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span)).Invoke(args, _span, _context)));
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        if (_source.ShortZipForPull() is { } zip)
            return await StartZipRowAsync(zip).ConfigureAwait(false);
        var (hasValue, current) = await _source.TryMoveNextAsync().ConfigureAwait(false);
        if (!hasValue)
        {
            return PyIterationResult.End;
        }

        if (TryStartTupleCallback(current, asynchronous: true, out var tupleResult, out var pending))
            return tupleResult.HasValue ? tupleResult : PyIterationResult.Yield(await pending.ConfigureAwait(false));

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

        var value = LythonRuntime.RuntimeValue(await (_function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span)).InvokeAsync(args, _span, _context).ConfigureAwait(false));
        return PyIterationResult.Yield(value);
    }

    private ValueTask<PyIterationResult> StartZipRowAsync(PyZipIterator zip)
    {
        LythonRuntime.IterationOperands operands = default;
        if (!zip.TryPullOperands(ref operands, asynchronous: true, out var hasRow, out var pending))
            return ResumeZipRowAsync(pending, zip.Arity);
        return hasRow ? InvokeZipRowAsync(operands, zip.Arity) : new(PyIterationResult.End);
    }

    private async ValueTask<PyIterationResult> ResumeZipRowAsync(
        ValueTask<(bool HasRow, LythonRuntime.IterationOperands Operands)> pending, int count)
    {
        var (hasRow, operands) = await pending.ConfigureAwait(false);
        return hasRow ? await InvokeZipRowAsync(operands, count).ConfigureAwait(false) : PyIterationResult.End;
    }

    private ValueTask<PyIterationResult> InvokeZipRowAsync(Span<LythonRuntime.ExecutableSlot> operands, int count)
    {
        var result = LythonRuntime.StartIterationCallback(_function, operands, count, _borrowStorage, _span, _context,
            asynchronous: true, out var pending);
        return result.HasValue ? new(result) : CompleteCallbackAsync(pending);
    }

    private static async ValueTask<PyIterationResult> CompleteCallbackAsync(ValueTask<object> pending)
        => PyIterationResult.Yield(await pending.ConfigureAwait(false));

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.starmap object>");
}

internal sealed class PyPairwiseIterator : PyIteratorBase, IPyAsyncIteratorValue
{
    private PyIteration.Cursor? _source;
    private readonly MemoryGovernor _memoryGovernor;
    private readonly LythonSourceSpan _span;
    private object _previous = PyNone.Instance;
    private bool _hasPrevious;

    public PyPairwiseIterator(PyIteration.Cursor source, MemoryGovernor governor, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    { _source = source; _memoryGovernor = governor; _span = span; }

    public override PyIterationResult TryMoveNext()
    {
        object? value;

        var source = _source;
        if (source is null) return PyIterationResult.End;
        if (!_hasPrevious)
        {
            if (!(source.TryMoveNext() is { HasValue: true, Value: var first })) { _source = null; source.Dispose(); return PyIterationResult.End; }
            _previous = LythonRuntime.RuntimeValue(first); _hasPrevious = true;
        }
        if (!(source.TryMoveNext() is { HasValue: true, Value: var next })) { _source = null; _previous = PyNone.Instance; source.Dispose(); return PyIterationResult.End; }
        var current = LythonRuntime.RuntimeValue(next);
        value = new PyTuple([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return PyIterationResult.Yield(value!);
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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
        var value = new PyTuple([_previous, current], _memoryGovernor, _span);
        _previous = current;
        return PyIterationResult.Yield(value);
    }

    public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<itertools.pairwise object>");
}
