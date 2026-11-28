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

    private bool TryStartSequenceCallback(object current, bool asynchronous, out ValueTask<PyIterationResult> result)
    {
        if (current is not (PyList or PyTuple))
        {
            result = default;
            return false;
        }

        var row = (IPySequenceValue)current;
        var count = row.Count;
        if (count > LythonRuntime.InlineIterationOperandCount)
        {
            if (count <= LythonRuntime.NativeValueCapacity &&
                LythonRuntime.TryStartIterationRow(_function, row, count, _span, _context, asynchronous, out result))
                return true;
            return TryStartWideSequenceCallback(row, count, asynchronous, out result);
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
            asynchronous);
        return true;
    }

    private bool TryStartWideSequenceCallback(IPySequenceValue row, int count, bool asynchronous,
        out ValueTask<PyIterationResult> result)
    {
        result = default;
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
                result = new(PyIterationResult.Yield(callable.Invoke(arguments, _span, _context)));
                return true;
            }

            var invocation = callable.InvokeAsync(arguments, _span, _context);
            if (invocation.IsCompleted)
                result = new(PyIterationResult.Yield(invocation.GetAwaiter().GetResult()));
            else
            {
                result = LythonRuntime.CompleteObjectIterationCallback(
                    LythonRuntime.CompleteBorrowedArguments(invocation, arguments, storage));
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
                _span, _context, asynchronous: false).GetAwaiter().GetResult() : PyIterationResult.End;
        }
        var generator = ListYieldSource(out var function, out var receiver, out var arity);
        var next = generator is null ? _source.TryMoveNext() : generator.AdvanceListYield(arity);
        if (TryStartStagedGenerator(generator, function, receiver, asynchronous: false, out var staged))
            return staged.GetAwaiter().GetResult();
        if (generator?.HasStagedListYield == true)
            return LythonRuntime.StartIterationListYield(function!, receiver, generator, _span, _context, asynchronous: false)
                .GetAwaiter().GetResult();
        if (!next.HasValue) return PyIterationResult.End;
        var current = next.Value;

        if (TryStartSequenceCallback(current, asynchronous: false, out var rowResult))
            return rowResult.GetAwaiter().GetResult();

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
            out var iterable, out var result))
            return result;
        return iterable is null ? new(PyIterationResult.End) : FinishPullAsync(default, null, null, null, iterable);
    }

    private bool TryStartPulledCallback(PyIterationResult row, LythonRuntime.PyGenerator? generator,
        PyExecutableFunction? function, object? receiver, out object? iterable,
        out ValueTask<PyIterationResult> result)
    {
        iterable = null;
        if (TryStartStagedGenerator(generator, function, receiver, asynchronous: true, out result))
            return true;
        if (generator?.HasStagedListYield == true)
        {
            result = LythonRuntime.StartIterationListYield(function!, receiver, generator, _span, _context,
                asynchronous: true);
            return true;
        }
        if (row.HasValue)
        {
            iterable = row.Value;
            return TryStartSequenceCallback(iterable, asynchronous: true, out result);
        }

        result = default;
        return false;
    }

    private bool TryStartStagedGenerator(LythonRuntime.PyGenerator? source, PyExecutableFunction? function,
        object? receiver, bool asynchronous, out ValueTask<PyIterationResult> result)
    {
        var staged = source?.TakeStagedGeneratorYield() ?? default;
        if (staged.Frame is null)
        {
            result = default;
            return false;
        }

        var first = receiver is null ? 0 : 1;
        if (!function!.CodeObject.HasDirectOperandReturn && function.SupportsPositionalBinding
            && function.PositionalCount > first && ReferenceEquals(function.DefinitionContext.State, _context.State))
        {
            result = StartGeneratorRow(null, function, receiver, first, asynchronous, staged);
            return true;
        }
        LythonRuntime.PyGenerator generator;
        try { generator = staged.Materialize(); }
        catch { staged.Release(); throw; }
        result = InvokeGeneralIterableRowAsync(generator, asynchronous);
        return true;
    }

    private async ValueTask<PyIterationResult> FinishPullAsync(ValueTask<PyIterationResult> source,
        LythonRuntime.PyGenerator? generator, PyExecutableFunction? function, object? receiver, object? iterable = null)
    {
        if (iterable is null)
        {
            var row = await source.ConfigureAwait(false);
            if (TryStartPulledCallback(row, generator, function, receiver, out iterable, out var result))
                return await result.ConfigureAwait(false);
            if (iterable is null) return PyIterationResult.End;
        }

        return await InvokeIterableRowAsync(iterable, asynchronous: true).ConfigureAwait(false);
    }

    private ValueTask<PyIterationResult> InvokeIterableRowAsync(object iterable, bool asynchronous)
    {
        if (iterable is LythonRuntime.PyGenerator generator && generator.CanBufferFor(_context) &&
            LythonRuntime.IterationFunction(_function, out var receiver) is PyExecutableFunction { CanStartInline: true } function &&
            !function.CodeObject.HasDirectOperandReturn && ReferenceEquals(function.DefinitionContext.State, _context.State))
        {
            var firstArgument = receiver is null ? 0 : 1;
            if (function.PositionalCount > firstArgument && function.SupportsPositionalBinding)
                return StartGeneratorRow(generator, function, receiver, firstArgument, asynchronous);
        }
        return InvokeGeneralIterableRowAsync(iterable, asynchronous);
    }

    private ValueTask<PyIterationResult> StartGeneratorRow(LythonRuntime.PyGenerator? generator,
        PyExecutableFunction function, object? receiver, int firstArgument, bool asynchronous,
        LythonRuntime.StagedGeneratorYield staged = default)
    {
        BoundFunctionArguments workspace = default;
        try
        {
            workspace = LythonRuntime.RentFunctionArgumentStorage(function, _span, _context, function.CodeObject.ValueStorageCapacity);
            var values = workspace.Values;
            if (receiver is not null) values[0].Object = receiver;
            _context.CheckExecution(_span);
            _context.State.CheckInterpreterStack(_span);
            var end = function.PositionalCount;
            var pull = staged.Frame is { } frame
                ? asynchronous
                    ? LythonRuntime.PyGenerator.AdvanceFrameAsync(frame, staged.Span!, null, PyNone.Instance, null, values, end, firstArgument)
                    : new(LythonRuntime.PyGenerator.AdvanceFrame(frame, staged.Span!, null, PyNone.Instance, null, values, end, firstArgument))
                : asynchronous
                    ? generator!.AdvanceBufferedAsync(values, end, firstArgument)
                    : new ValueTask<PyIterationResult>(generator!.AdvanceBuffered(values, end, firstArgument));
            if (pull.IsCompleted)
            {
                var excess = pull.GetAwaiter().GetResult();
                if (TryStartExactGeneratorRow(function, excess, ref workspace, asynchronous, out var result))
                    return result;
                // The original operation has been consumed. The ordinary
                // binder needs only the outcome and the prefetched slots.
                pull = new(excess);
            }
            var transferred = staged;
            staged = default;
            return FinishGeneratorRowAsync(generator, function, firstArgument, pull,
                TakeGeneratorRowWorkspace(ref workspace), asynchronous, transferred);
        }
        finally
        {
            try { staged.Release(); }
            finally { ReturnGeneratorRowWorkspace(ref workspace, function); }
        }
    }

    private bool TryStartExactGeneratorRow(PyExecutableFunction function, PyIterationResult excess, ref BoundFunctionArguments workspace,
        bool asynchronous, out ValueTask<PyIterationResult> result)
    {
        // The drain fills consecutive slots in a cleared loan. The last slot
        // suffices to prove exact arity when there was no excess yield.
        if (excess.HasValue || workspace.Values[function.PositionalCount - 1].IsUnassigned)
        {
            result = default;
            return false;
        }
        _context.CheckExecution(_span);
        result = LythonRuntime.StartBoundIterationFunction(function, TakeGeneratorRowWorkspace(ref workspace),
            _span, asynchronous);
        return true;
    }

    private async ValueTask<PyIterationResult> FinishGeneratorRowAsync(LythonRuntime.PyGenerator? generator,
        PyExecutableFunction function, int firstArgument, ValueTask<PyIterationResult> pull, BoundFunctionArguments workspace,
        bool asynchronous, LythonRuntime.StagedGeneratorYield staged)
    {
        try
        {
            var excess = await pull.ConfigureAwait(false);
            if (TryStartExactGeneratorRow(function, excess, ref workspace, asynchronous, out var result))
                return await result.ConfigureAwait(false);

            // Preserve the first excess yield and every prefetched value.
            // The dynamic binder still handles defaults and arity errors.
            using var arguments = new CallExpansion.CallArgumentAccumulator(0, _context, _function, span: _span);
            var values = workspace.Values;
            for (var i = firstArgument; i < function.PositionalCount && !values[i].IsUnassigned; i++)
                arguments.Add(CallArgumentValue.Positional(values[i].Object.RequireNotNull()), _span);
            ReturnGeneratorRowWorkspace(ref workspace, function);
            if (excess.HasValue)
            {
                arguments.Add(CallArgumentValue.Positional(excess.Value), _span);
                await arguments.AppendPositionalIterableAsync(generator ?? staged.Materialize(started: true),
                    _span, asynchronous).ConfigureAwait(false);
            }
            var value = await InvokeCollectedRow(arguments, asynchronous).ConfigureAwait(false);
            return PyIterationResult.Yield(LythonRuntime.RuntimeValue(value));
        }
        finally
        {
            try { staged.Release(); }
            finally { ReturnGeneratorRowWorkspace(ref workspace, function); }
        }
    }

    private static BoundFunctionArguments TakeGeneratorRowWorkspace(ref BoundFunctionArguments workspace)
    {
        var owned = workspace;
        workspace = default;
        return owned;
    }

    private static void ReturnGeneratorRowWorkspace(ref BoundFunctionArguments workspace, PyExecutableFunction function)
    {
        if (workspace.Values is not null)
            LythonRuntime.ReturnBoundArguments(TakeGeneratorRowWorkspace(ref workspace), function.CodeObject.ValueStorageCapacity);
    }

    private async ValueTask<PyIterationResult> InvokeGeneralIterableRowAsync(object iterable, bool asynchronous)
    {
        // Consume one owned argument buffer directly. Suspend with it funded
        // through the source pull, binding and any callback cleanup.
        using var arguments = new CallExpansion.CallArgumentAccumulator(0, _context, _function, span: _span);
        await arguments.AppendPositionalIterableAsync(iterable, _span, asynchronous).ConfigureAwait(false);
        var value = await InvokeCollectedRow(arguments, asynchronous).ConfigureAwait(false);
        return PyIterationResult.Yield(LythonRuntime.RuntimeValue(value));
    }

    private ValueTask<object> InvokeCollectedRow(CallExpansion.CallArgumentAccumulator arguments, bool asynchronous)
    {
        var callable = _function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
        if (callable is LythonRuntime.ISpanCallable)
            return asynchronous ? callable.InvokeBorrowedAsync(arguments.Arguments, _span, _context)
                : new(callable.InvokeBorrowed(arguments.Arguments, _span, _context));

        var packet = arguments.ToArray();
        return asynchronous ? callable.InvokeAsync(packet, _span, _context)
            : new(callable.Invoke(packet, _span, _context));
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
        => LythonRuntime.StartIterationCallback(_function, operands, count, _borrowStorage, _span, _context,
            asynchronous: true);

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
