using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    private static async ValueTask<object> ConvertValue(object value, object? factory, bool tuple, bool asynchronous,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.EnterInterpreterFrame(span);
        try
        {
            context.CheckExecution(span);
            if (value is PyInstance instance && HasFieldMap(instance.Type))
            {
                using var fields = PyIteration.DrainLeased(VisibleFields(instance.Type, span), span, context);
                if (tuple && factory is null)
                {
                    using var backing = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(fields.Items.Count), span);
                    var items = new object[fields.Items.Count];
                    for (var i = 0; i < items.Length; i++)
                    {
                        var field = (PyDataclassFieldObject)fields.Items[i];
                        var item = asynchronous ? await ReadFieldAsync(instance, field.Name.RequireNotNull(), context, span).ConfigureAwait(false) : ReadField(instance, field.Name.RequireNotNull(), context, span);
                        items[i] = await ConvertValue(item, factory, tuple, asynchronous, context, span).ConfigureAwait(false);
                    }
                    return PyTuple.FromReservedOwnedArray(items, context.MemoryGovernor, span, backing);
                }
                PyDict? result = !tuple && factory is null
                    ? new PyDict(PyDictStorage.Create(fields.Items.Count, context.MemoryGovernor, span), context.MemoryGovernor, span) : null;
                var arguments = result is null ? new PyList(fields.Items.Count, context.MemoryGovernor, span) : null;
                var exposed = false;
                try
                {
                    foreach (PyDataclassFieldObject field in fields.Items)
                    {
                        var raw = asynchronous ? await ReadFieldAsync(instance, field.Name.RequireNotNull(), context, span).ConfigureAwait(false) : ReadField(instance, field.Name.RequireNotNull(), context, span);
                        var item = await ConvertValue(raw, factory, tuple, asynchronous, context, span).ConfigureAwait(false);
                        var name = PyString.FromString(field.Name.RequireNotNull(), context.MemoryGovernor, span);
                        if (result is not null) result.SetItem(name, item);
                        else arguments!.Add(tuple ? item : PyTuple.FromOwnedArray([name, item], context.MemoryGovernor, span));
                    }
                    if (result is not null) return result;
                    if (factory is not LythonRuntime.ICallable callable) throw RuntimeErrors.Type("dataclass result factory must be callable", span);
                    exposed = true;
                    return asynchronous ? await CallableInvocation.InvokeUnaryAsync(callable, arguments!, span, context).ConfigureAwait(false)
                        : CallableInvocation.InvokeUnary(callable, arguments!, span, context);
                }
                catch
                {
                    if (result is not null) context.MemoryGovernor.RefundUnpublishedValue(result);
                    if (arguments is not null && !exposed) arguments.DiscardUnpublishedStorage();
                    throw;
                }
            }
            if (value is PyList list)
            {
                var result = new PyList(list.Count, context.MemoryGovernor, span);
                try
                {
                    for (var i = 0; i < list.Count; i++) result.Add(await ConvertValue(list[i], factory, tuple, asynchronous, context, span).ConfigureAwait(false));
                    return result;
                }
                catch { result.DiscardUnpublishedStorage(); throw; }
            }
            if (value is PyTuple source)
            {
                using var backing = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(source.Count), span);
                var items = new object[source.Count];
                for (var i = 0; i < items.Length; i++) items[i] = await ConvertValue(source[i], factory, tuple, asynchronous, context, span).ConfigureAwait(false);
                return PyTuple.FromReservedOwnedArray(items, context.MemoryGovernor, span, backing);
            }
            if (value is PyDict dict)
            {
                var result = new PyDict(PyDictStorage.Create(dict.Count, context.MemoryGovernor, span), context.MemoryGovernor, span);
                try
                {
                    foreach (var pair in dict)
                    {
                        var key = await ConvertValue(pair.Key, factory, tuple, asynchronous, context, span).ConfigureAwait(false);
                        var item = await ConvertValue(pair.Value, factory, tuple, asynchronous, context, span).ConfigureAwait(false);
                        result.SetItem(key, item);
                    }
                    return result;
                }
                catch { context.MemoryGovernor.RefundUnpublishedValue(result); throw; }
            }
            return await LythonRuntime.CopyDataclassLeafAsync(value, context, span, asynchronous).ConfigureAwait(false);
        }
        finally { context.LeaveInterpreterFrame(); }
    }
}
