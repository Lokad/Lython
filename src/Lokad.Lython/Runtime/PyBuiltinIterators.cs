using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal abstract class PyEnumerableIterator : PyIteratorBase
{
    private readonly string _displayName;
    protected readonly object Source;
    protected int Position;
    protected bool Exhausted;

    internal int? RemainingHint => Source switch
    {
        IPySizedValue sized => Exhausted ? 0 : Math.Max(0, sized.Length - Position),
        IReadOnlyCollection<object> sized => Exhausted ? 0 : Math.Max(0, sized.Count - Position),
        _ => null,
    };

    internal static PyEnumerableIterator Create(object source, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, string displayName = "iterator")
        => source is PyList or PyTuple
            ? new SequenceIterator(source, span, context, displayName)
            : new CursorIterator(source, span, context, displayName);

    private PyEnumerableIterator(object source, string displayName)
    {
        _displayName = displayName;
        Source = source;
        // Construction sites fund the complete iterator before the factory runs.
    }

    public override PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString("<" + _displayName + " object>");
    }

    private sealed class SequenceIterator(object source, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, string displayName) : PyEnumerableIterator(source, displayName)
    {
        public override PyIterationResult TryMoveNext()
        {
            // The live position also supplies the Python length hint. Repeated
            // pulls after exhaustion must still observe cancellation.
            if ((Position & 63) == 0 || Exhausted) context.CheckExecution(span);
            if (Exhausted) return PyIterationResult.End;
            var next = PyIteration.AdvanceBuiltinSequence(Source, ref Position);
            Exhausted = !next.HasValue;
            return next;
        }
    }

    private sealed class CursorIterator(object source, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, string displayName) : PyEnumerableIterator(source, displayName), IPyAsyncIteratorValue
    {
        private readonly PyIteration.Cursor _cursor = PyIteration.Cursor.Create(source, span, context);

        public override PyIterationResult TryMoveNext() => RecordAdvance(_cursor.TryMoveNext());

        public async ValueTask<PyIterationResult> TryMoveNextAsync()
            => RecordAdvance(await _cursor.TryMoveNextAsync().ConfigureAwait(false));

        private PyIterationResult RecordAdvance(PyIterationResult next)
        {
            if (!next.HasValue) Exhausted = true;
            // Position only supplies the hint here. The cursor can keep pulling.
            else if (Position < int.MaxValue) Position++;
            return next;
        }
    }
}

internal sealed class PyCallableSentinelIterator : PyIteratorBase, IPyAsyncIteratorValue
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

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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

internal sealed class PyMapIterator : PyIteratorBase, IPyAsyncIteratorValue
{
    private readonly object _function;
    private readonly PyIteration.Cursor[] _iterators;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;
    private readonly bool _borrowStorage;

    private PyMapIterator(object function, PyIteration.Cursor[] iterators,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool borrowStorage)
    {
        _function = function; _iterators = iterators; _context = context; _span = span; _borrowStorage = borrowStorage;
    }

    internal static async ValueTask<PyMapIterator> CreateAsync(object[] arguments,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context = LythonRuntime.GetGlobalContext(context);
        var count = arguments.Length - 1;
        var bytes = checked(IteratorValueBytes + 32L + 256L * count);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        var borrowStorage = count <= LythonRuntime.InlineIterationOperandCount &&
            arguments[0] is PyExecutableFunction function && function.HasCompletePositionalSupply(count, receiver: null) &&
            function.CanBorrowIterationStorage(context, span);
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
            var result = new PyMapIterator(arguments[0], cursors, context, span, borrowStorage);
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
        var wide = _iterators.Length > LythonRuntime.InlineIterationOperandCount;
        using var scratch = wide
            ? _context.MemoryGovernor.ReserveTemporary(32L + 24L * _iterators.Length, _span) : null;
        LythonRuntime.IterationOperands inline = default;
        var arguments = wide ? new CallArgumentValue[_iterators.Length] : null;
        for (var i = 0; i < _iterators.Length; i++)
        {
            var next = _iterators[i].TryMoveNext();
            if (!next.HasValue) return PyIterationResult.End;
            if (arguments is null) inline[i] = next.TakeSlotValue();
            else arguments[i] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(next.Value));
        }

        if (arguments is null)
        {
            var result = LythonRuntime.StartIterationCallback(_function, inline, _iterators.Length, _borrowStorage, _span, _context,
                asynchronous: false).GetAwaiter().GetResult();
            System.Diagnostics.Debug.Assert(result.HasValue);
            return result;
        }
        var callable = _function as LythonRuntime.ICallable ?? throw LythonRuntime.NotCallableError(_function, _context, _span);
        return PyIterationResult.Yield(callable.Invoke(arguments, _span, _context));
    }

    public ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_iterators.Length > LythonRuntime.InlineIterationOperandCount) return MoveWideRowAsync();
        try
        {
            LythonRuntime.IterationOperands operands = default;
            var count = 0;
            if (!ReadAvailableOperands(ref count, ref operands, out var pending))
                return new(PyIterationResult.End);
            return count == _iterators.Length
                ? InvokeShortRowAsync(operands, count)
                : ResumeShortRowAsync(pending, count, operands);
        }
        catch (Exception error) { return ValueTask.FromException<PyIterationResult>(error); }
    }

    // A pending source takes ownership of the evaluated prefix. The same
    // loop resumes from that source without reading earlier iterators again.
    private bool ReadAvailableOperands(ref int count, ref LythonRuntime.IterationOperands operands,
        out ValueTask<PyIterationResult> pending)
    {
        pending = default;
        while (count < _iterators.Length)
        {
            var next = _iterators[count].TryMoveNextAsync();
            if (!next.IsCompleted)
            {
                pending = next;
                return true;
            }
            var ready = next.GetAwaiter().GetResult();
            if (!ready.HasValue) return false;
            operands[count++] = ready.TakeSlotValue();
        }
        return true;
    }

    private async ValueTask<PyIterationResult> ResumeShortRowAsync(ValueTask<PyIterationResult> pending, int count,
        LythonRuntime.IterationOperands operands)
    {
        while (true)
        {
            var next = await pending.ConfigureAwait(false);
            if (!next.HasValue) return PyIterationResult.End;
            operands[count++] = next.TakeSlotValue();
            if (!ReadAvailableOperands(ref count, ref operands, out pending)) return PyIterationResult.End;
            if (count == _iterators.Length) return await InvokeShortRowAsync(operands, count).ConfigureAwait(false);
        }
    }

    private ValueTask<PyIterationResult> InvokeShortRowAsync(Span<LythonRuntime.ExecutableSlot> operands, int count)
        => LythonRuntime.StartIterationCallback(_function, operands, count, _borrowStorage, _span, _context,
            asynchronous: true);

    private async ValueTask<PyIterationResult> MoveWideRowAsync()
    {
        using var scratch = _context.MemoryGovernor.ReserveTemporary(32L + 24L * _iterators.Length, _span);
        var arguments = new CallArgumentValue[_iterators.Length];
        for (var i = 0; i < _iterators.Length; i++)
        {
            var next = await _iterators[i].TryMoveNextAsync().ConfigureAwait(false);
            if (!next.HasValue) return PyIterationResult.End;
            arguments[i] = CallArgumentValue.Positional(LythonRuntime.RuntimeValue(next.Value));
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

internal sealed class PyFilterIterator : PyIteratorBase, IPyAsyncIteratorValue
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

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
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
