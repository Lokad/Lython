namespace Lokad.Lython.Runtime;

internal static partial class PyHashProtocols
{
    internal static ValueTask<int> GetProtocolHashAsync(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var prepared = PrepareKeyHashAsync(key, context, span);
        return prepared.IsCompletedSuccessfully ? new(prepared.Result.Hash) : AwaitHash(prepared);

        static async ValueTask<int> AwaitHash(ValueTask<(int Hash, bool Protocol)> pending)
            => (await pending.ConfigureAwait(false)).Hash;
    }

    // Native leaves complete directly. Tuple/slice recursion discovers pending
    // work in Python order, without a separate protocol-classification walk.
    internal static ValueTask<(int Hash, bool Protocol)> PrepareKeyHashAsync(object key,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (key is PyInstance instance) return GetInstanceHashAsync(instance, context, span);
        if (key is PySlice slice) return PrepareSliceHashAsync(slice, context, span);
        if (PyTupleLike.TryGetItems(key, out var items)) return PrepareTupleHashAsync(items, context, span);
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        return new((LythonRuntime.ComputeStructuralHashCode(key, span, context), false));
    }

    private static async ValueTask<(int Hash, bool Protocol)> GetInstanceHashAsync(PyInstance instance,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (HasDisabledHash(instance)) throw RuntimeErrors.UnhashableType(instance, span);
        if (!HasCustomSlot(instance, "__hash__"))
            return (LythonRuntime.ComputeStructuralHashCode(instance, span, context), true);

        instance.Type.TryLookupInMro("__hash__", 0, out var rawSlot, out _);
        var member = await PyAttributeLookup.BindForInstanceAsync(instance, rawSlot!, context, span).ConfigureAwait(false);
        context.CheckExecution(span);
        if (member is not LythonRuntime.ICallable callable)
            throw RuntimeErrors.Type("'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(member, context) + "' object is not callable", span);
        var value = await callable.InvokeAsync([], span, context).ConfigureAwait(false);
        context.CheckExecution(span);
        return (NormalizeProtocolHash(value, span), true);
    }

    private static async ValueTask<(int Hash, bool Protocol)> PrepareSliceHashAsync(PySlice slice,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        using var guard = PyStructuralGuard.EnterSingle(context.State.StructuralTraversal, slice, span, context);
        PyStructuralGuard.NoteWork(context, span);
        var start = await PrepareKeyHashAsync(slice.Start, context, span).ConfigureAwait(false);
        var stop = await PrepareKeyHashAsync(slice.Stop, context, span).ConfigureAwait(false);
        var step = await PrepareKeyHashAsync(slice.Step, context, span).ConfigureAwait(false);
        context.CheckExecution(span);
        return (PyValueComparer.NormalizeHash(HashCode.Combine(start.Hash, stop.Hash, step.Hash)),
            start.Protocol || stop.Protocol || step.Protocol);
    }

    internal static async ValueTask<int> ComputeTupleHashAsync(IReadOnlyList<object> items,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => (await PrepareTupleHashAsync(items, context, span).ConfigureAwait(false)).Hash;

    private static async ValueTask<(int Hash, bool Protocol)> PrepareTupleHashAsync(IReadOnlyList<object> items,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        using var guard = PyStructuralGuard.EnterSingle(context.State.StructuralTraversal, items, span, context);
        var hash = new HashCode();
        var protocol = false;
        foreach (var item in items)
        {
            PyStructuralGuard.NoteWork(context, span);
            var prepared = await PrepareKeyHashAsync(item, context, span).ConfigureAwait(false);
            hash.Add(prepared.Hash);
            protocol |= prepared.Protocol;
        }
        context.CheckExecution(span);
        return (PyValueComparer.NormalizeHash(hash.ToHashCode()), protocol);
    }
}
