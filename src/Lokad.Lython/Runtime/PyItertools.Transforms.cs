using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
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

    private LythonRuntime.PyGenerator? ListYieldSource(out PyExecutableFunction? function, out object? receiver, out int arity)
    {
        function = LythonRuntime.IterationFunction(_function, out receiver) as PyExecutableFunction;
        arity = (function?.PositionalCount ?? 0) - (receiver is null ? 0 : 1);
        return arity > 0 && function is { CanStartInline: true } && function.HasCompletePositionalSupply(arity, receiver)
            ? _source.ListYieldGeneratorForPull(_context) : null;
    }

    private bool TryStartSequenceCallback(object current, bool asynchronous, out PyIterationResult result,
        out ValueTask<object> pending)
    {
        if (current is not (PyList or PyTuple))
        {
            result = default;
            pending = default;
            return false;
        }

        var row = (IPySequenceValue)current;
        var count = row.Count;
        if (count > LythonRuntime.InlineIterationOperandCount)
        {
            if (count <= LythonRuntime.NativeValueCapacity &&
                LythonRuntime.TryStartIterationRow(_function, row, count, _span, _context, asynchronous, out result, out pending))
                return true;
            return TryStartWideSequenceCallback(row, count, asynchronous, out result, out pending);
        }

        // The exact row has no guest iteration protocol. Tuple loans preserve
        // deferred scalar identity through binding or immediate return.
        LythonRuntime.IterationOperands operands = default;
        for (var i = 0; i < count; i++)
        {
            _context.CheckExecution(_span);
            if (row is PyTuple tuple) _ = tuple.TryReadTrailingSlot(count - i, out operands[i]);
            else operands[i] = LythonRuntime.ExecutableSlot.FromObject(row[i]);
        }

        result = LythonRuntime.StartIterationCallback(_function, operands, count, _borrowStorage, _span, _context,
            asynchronous, out pending);
        return true;
    }

    private bool TryStartWideSequenceCallback(IPySequenceValue row, int count, bool asynchronous,
        out PyIterationResult result, out ValueTask<object> pending)
    {
        result = default;
        pending = default;
        var storage = LythonRuntime.ReserveCallArgumentStorage(count, _context, _span);
        CallArgumentValue[]? arguments = null;
        var transferred = false;
        try
        {
            _context.CheckExecution(_span);
            // Admission can resize a live list. Until any item is read, the
            // ordinary iterator can consume its new length without replay.
            if (row.Count != count) return false;
            arguments = new CallArgumentValue[count];
            for (var i = 0; i < arguments.Length; i++)
            {
                if ((i & 255) == 0) _context.CheckExecution(_span);
                arguments[i] = CallArgumentValue.Positional(row[i]);
            }

            _context.CheckExecution(_span);
            var callable = _function as LythonRuntime.ICallable
                ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
            if (!asynchronous)
            {
                result = PyIterationResult.Yield(callable.Invoke(arguments, _span, _context));
                return true;
            }

            var invocation = callable.InvokeAsync(arguments, _span, _context);
            if (invocation.IsCompleted)
                result = PyIterationResult.Yield(invocation.GetAwaiter().GetResult());
            else
            {
                pending = LythonRuntime.CompleteBorrowedArguments(invocation, arguments, storage);
                transferred = true;
            }
            return true;
        }
        finally
        {
            if (!transferred)
            {
                if (arguments is not null) Array.Clear(arguments);
                storage?.Dispose();
            }
        }
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
        var generator = ListYieldSource(out var function, out var receiver, out var arity);
        var next = generator is null ? _source.TryMoveNext() : generator.AdvanceListYield(arity);
        if (generator?.HasStagedListYield == true)
            return LythonRuntime.StartIterationListYield(function!, receiver, generator, _span, _context, asynchronous: false, out _);
        if (!next.HasValue) return PyIterationResult.End;
        var current = next.Value;

        if (TryStartSequenceCallback(current, asynchronous: false, out var rowResult, out _))
            return rowResult;

        return InvokeIterableRowAsync(current, asynchronous: false).GetAwaiter().GetResult();
    }

    public ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        _context.CheckExecution(_span);
        if (_source.ShortZipForPull() is { } zip)
            return StartZipRowAsync(zip);
        var generator = ListYieldSource(out var function, out var receiver, out var arity);
        var next = generator is null ? _source.TryMoveNextAsync() : generator.AdvanceListYieldAsync(arity);
        if (!next.IsCompleted) return FinishPullAsync(next, generator, function, receiver);

        if (TryStartPulledCallback(next.GetAwaiter().GetResult(), generator, function, receiver,
            out var iterable, out var result, out var pending))
            return result.HasValue ? new(result) : CompleteCallbackAsync(pending);
        return iterable is null ? new(PyIterationResult.End) : FinishPullAsync(default, null, null, null, iterable);
    }

    private bool TryStartPulledCallback(PyIterationResult row, LythonRuntime.PyGenerator? generator,
        PyExecutableFunction? function, object? receiver, out object? iterable,
        out PyIterationResult result, out ValueTask<object> pending)
    {
        iterable = null;
        if (generator?.HasStagedListYield == true)
        {
            result = LythonRuntime.StartIterationListYield(function!, receiver, generator, _span, _context,
                asynchronous: true, out pending);
            return true;
        }
        if (row.HasValue)
        {
            iterable = row.Value;
            return TryStartSequenceCallback(iterable, asynchronous: true, out result, out pending);
        }

        result = default;
        pending = default;
        return false;
    }

    private async ValueTask<PyIterationResult> FinishPullAsync(ValueTask<PyIterationResult> source,
        LythonRuntime.PyGenerator? generator, PyExecutableFunction? function, object? receiver, object? iterable = null)
    {
        if (iterable is null)
        {
            var row = await source.ConfigureAwait(false);
            if (TryStartPulledCallback(row, generator, function, receiver, out iterable, out var result, out var pending))
                return result.HasValue ? result : PyIterationResult.Yield(await pending.ConfigureAwait(false));
            if (iterable is null) return PyIterationResult.End;
        }

        return await InvokeIterableRowAsync(iterable, asynchronous: true).ConfigureAwait(false);
    }

    private async ValueTask<PyIterationResult> InvokeIterableRowAsync(object iterable, bool asynchronous)
    {
        // Consume one owned argument buffer directly. Suspend with it funded
        // through the source pull, binding and any callback cleanup.
        using var arguments = new CallExpansion.CallArgumentAccumulator(0, _context, _function, span: _span);
        using (var cursor = asynchronous
            ? await PyIteration.Cursor.CreateAsync(iterable, _span, _context).ConfigureAwait(false)
            : PyIteration.Cursor.Create(iterable, _span, _context))
        {
            var generator = cursor.GetBulkGenerator(_context);
            var next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false) : cursor.TryMoveNext();
            while (next.HasValue)
            {
                arguments.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(next.Value)), _span);
                var limit = arguments.BufferedCapacity;
                if (generator is not null && limit != 0)
                    next = asynchronous
                        ? await generator.AdvanceBufferedAsync(arguments, limit).ConfigureAwait(false)
                        : generator.AdvanceBuffered(arguments, limit);
                else next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false) : cursor.TryMoveNext();
            }
        }

        var callable = _function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
        object value;
        if (callable is LythonRuntime.ISpanCallable)
            value = asynchronous
                ? await callable.InvokeBorrowedAsync(arguments.Arguments, _span, _context).ConfigureAwait(false)
                : callable.InvokeBorrowed(arguments.Arguments, _span, _context);
        else
        {
            var packet = arguments.ToArray();
            value = asynchronous ? await callable.InvokeAsync(packet, _span, _context).ConfigureAwait(false)
                : callable.Invoke(packet, _span, _context);
        }
        return PyIterationResult.Yield(LythonRuntime.RuntimeValue(value));
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
