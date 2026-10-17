namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static ValueTask<object> IntAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length == 1 && arguments[0] is PyInstance instance)
            return ConvertInstanceToOwnedIntegerAsync(instance, context, span);
        if (arguments.Length == 2 && arguments[1] is PyInstance baseInstance)
            return ConvertIntegerWithBaseAsync(arguments[0], baseInstance, context, span);
        return ValueTask.FromResult(Int(arguments, span, context));
    }

    private static async ValueTask<object> ConvertInstanceToOwnedIntegerAsync(
        PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        object converted;
        var protocol = await InvokeIntegerConversionProtocolAsync(instance, "__int__", context, span).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
        {
            converted = ValidateIntegerConversionResult(protocol.Value, "__int__", context, span);
        }
        else
        {
            protocol = await InvokeIntegerConversionProtocolAsync(instance, "__index__", context, span).ConfigureAwait(false);
            if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            {
                converted = ValidateIntegerConversionResult(protocol.Value, "__index__", context, span);
            }
            else
            {
                protocol = await InvokeIntegerConversionProtocolAsync(instance, "__trunc__", context, span).ConfigureAwait(false);
                if (protocol.Kind != SpecialMethodInvocationKind.Invoked) throw IntegerConversionError(instance, context, span);
                var indexed = await ConvertIntegerIndexAsync(protocol.Value, context, span).ConfigureAwait(false);
                converted = ValidateTruncatedInteger(protocol.Value, indexed, context, span);
            }
        }

        return OwnFreshInteger(converted, context.MemoryGovernor, span);
    }

    private static async ValueTask<object> ConvertIntegerWithBaseAsync(
        object value, PyInstance originalBase, ExecutionContext context, LythonSourceSpan span)
    {
        var indexed = await ConvertIntegerIndexAsync(originalBase, context, span).ConfigureAwait(false);
        var numberBase = ValidateIntegerBase(indexed, originalBase, value, context, span);
        return ConvertIntegerValue(value, numberBase, context, span);
    }

    private static async ValueTask<object> ConvertIntegerIndexAsync(object value, ExecutionContext context, LythonSourceSpan span)
    {
        if (value is not PyInstance instance) return value;
        var protocol = await InvokeIntegerConversionProtocolAsync(instance, "__index__", context, span).ConfigureAwait(false);
        return protocol.Kind == SpecialMethodInvocationKind.Invoked
            ? ValidateIntegerConversionResult(protocol.Value, "__index__", context, span)
            : value;
    }

    private static async ValueTask<SpecialMethodInvocation> InvokeIntegerConversionProtocolAsync(
        PyInstance instance, string name, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.Type.TryLookupInMro(name, 0, out var raw, out _)) return SpecialMethodInvocation.Missing;
        var member = await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false);
        if (member is not ICallable callable) throw NotCallableError(member, context, span);
        return SpecialMethodInvocation.Invoked(await callable.InvokeAsync([], span, context).ConfigureAwait(false));
    }
}
