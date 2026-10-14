namespace Lokad.Lython.Runtime;

internal sealed class PyUserIterator : IPyIteratorValue, IMemoryStorageEdges
{
    private readonly object _iterator;
    private readonly LythonRuntime.ExecutionContext _context;
    private readonly LythonSourceSpan _span;

    public void VisitStorageChildren(MemoryStorageWalk visitor)
    {
        if (visitor.Add(_iterator)) visitor.Add(_context);
    }

    public static PyUserIterator Create(object iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => new(ResolveIterator(InvokeIter(iterable, context, span), context, span), context, span);

    public static async ValueTask<PyUserIterator> CreateAsync(object iterable, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var resolved = await InvokeIterAsync(iterable, context, span).ConfigureAwait(false);
        return new PyUserIterator(ResolveIterator(resolved, context, span), context, span);
    }

    private PyUserIterator(object iterator, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        _iterator = iterator;
        _context = context;
        _span = span;
    }

    /// <summary>
    /// The object produced by <c>__iter__</c>; often the source instance itself,
    /// which is why <c>iter(it) is it</c> holds for well-behaved iterators.
    /// </summary>
    public object Iterator => _iterator;

    private static object InvokeIter(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (value is IPyProtocolIterator)
            return LythonRuntime.InvokeCallableTarget(NativeMember(value, "__iter__", context, span), span, span, context, static () => []);
        var iterable = (PyInstance)value;
        if (!iterable.Type.TryLookupInMro("__iter__", 0, out var raw, out _))
        {
            if (iterable.Type.TryLookupInMro("__getitem__", 0, out _, out _))
                return new PySequenceIterator(iterable, context, span);
            throw new PyNotIterableException($"'{iterable.Type.Name}' object is not iterable", span);
        }
        var member = PyAttributeLookup.BindForInstance(iterable, raw, context, span);
        return LythonRuntime.InvokeCallableTarget(member, span, span, context, static () => []);
    }

    private static async ValueTask<object> InvokeIterAsync(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (value is IPyProtocolIterator)
            return await LythonRuntime.InvokeCallableTargetAsync(NativeMember(value, "__iter__", context, span), span, span, context,
                static () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false);
        var iterable = (PyInstance)value;
        if (!iterable.Type.TryLookupInMro("__iter__", 0, out var raw, out _))
        {
            if (iterable.Type.TryLookupInMro("__getitem__", 0, out _, out _))
                return new PySequenceIterator(iterable, context, span);
            throw new PyNotIterableException($"'{iterable.Type.Name}' object is not iterable", span);
        }
        var member = await PyAttributeLookup.BindForInstanceAsync(iterable, raw, context, span).ConfigureAwait(false);
        return await LythonRuntime.InvokeCallableTargetAsync(member, span, span, context,
            static () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false);
    }

    private static object ResolveIterator(object resolved, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Like CPython, iter() validates eagerly: the result must be an iterator
        // or provide __next__, so a bad __iter__ fails here instead of later.
        if (resolved is IPyIteratorValue)
        {
            return resolved;
        }

        if (HasNext(resolved, context, span))
        {
            return resolved;
        }

        throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
    }

    internal static bool HasNext(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => value is PyInstance instance
            ? instance.Type.TryLookupInMro("__next__", 0, out _, out _)
            : value is IPyProtocolIterator && PyMemberAccess.TryResolve(value, "__next__", context, span, out var member)
                && member is LythonRuntime.ICallable;

    private static object NativeMember(object value, string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => PyMemberAccess.TryResolve(value, name, context, span, out var member) && member is LythonRuntime.ICallable
            ? member : throw new PyNotIterableException("native iterator does not provide " + name, span);

    public bool TryMoveNext([MaybeNullWhen(false)] out object value)
    {
        if (_iterator is IPyIteratorValue iterator)
        {
            return iterator.TryMoveNext(out value);
        }

        return TryAdvanceInstance(_iterator, _context, _span, out value);
    }

    public async ValueTask<PyIterationResult> TryMoveNextAsync()
    {
        if (_iterator is IPyAsyncIteratorValue asynchronous)
            return await asynchronous.TryMoveNextAsync().ConfigureAwait(false);

        if (_iterator is IPyIteratorValue iterator)
        {
            return iterator.TryMoveNext(out var value)
                ? PyIterationResult.Yield(value)
                : PyIterationResult.End;
        }

        return await TryAdvanceInstanceAsync(_iterator, _context, _span).ConfigureAwait(false);
    }

    internal static bool TryAdvanceInstance(object instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        try
        {
            value = InvokeNext(instance, context, span);
            return true;
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
        {
            value = PyNone.Instance;
            return false;
        }
    }

    // next() surfaces whatever __next__ raises (including a custom
    // StopIteration) like CPython instead of replacing it with the
    // end-of-iteration signal consumed by consuming loops.
    internal static object InvokeNext(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (value is IPyProtocolIterator)
            return LythonRuntime.InvokeCallableTarget(NativeMember(value, "__next__", context, span), span, span, context, static () => []);
        var instance = (PyInstance)value;
        if (!instance.Type.TryLookupInMro("__next__", 0, out var raw, out _))
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
        }

        var member = PyAttributeLookup.BindForInstance(instance, raw, context, span);
        return LythonRuntime.InvokeCallableTarget(member, span, span, context, static () => []);
    }

    internal static async ValueTask<object> InvokeNextAsync(object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (value is IPyProtocolIterator)
            return await LythonRuntime.InvokeCallableTargetAsync(NativeMember(value, "__next__", context, span), span, span, context,
                static () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false);
        var instance = (PyInstance)value;
        if (!instance.Type.TryLookupInMro("__next__", 0, out var raw, out _))
        {
            throw new LythonRuntimeException("TypeError", "iter() returned non-iterator", span);
        }

        var member = await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false);
        return await LythonRuntime.InvokeCallableTargetAsync(member, span, span, context,
            static () => ValueTask.FromResult<CallArgumentValue[]>([])).ConfigureAwait(false);
    }

    internal static async ValueTask<PyIterationResult> TryAdvanceInstanceAsync(object instance, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        try
        {
            var value = await InvokeNextAsync(instance, context, span).ConfigureAwait(false);
            return PyIterationResult.Yield(value);
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "StopIteration")
        {
            return PyIterationResult.End;
        }
    }

    public IEnumerable<object> Iterate() => PyIteration.EnumerateIterator(this);
}
