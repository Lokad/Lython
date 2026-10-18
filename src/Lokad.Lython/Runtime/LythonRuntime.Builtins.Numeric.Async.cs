using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static ValueTask<object> AbsAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => arguments.Length == 1
            ? AbsValueAsync(arguments[0], context, span)
            : ValueTask.FromResult(Abs(arguments, span, context));

    private static ValueTask<object> AbsValueAsync(object value, ExecutionContext context, LythonSourceSpan span)
        => value is PyInstance instance ? AbsInstanceAsync(instance, context, span)
            : ValueTask.FromResult(AbsValueWithoutProtocols(value, context, span));

    private static async ValueTask<object> AbsInstanceAsync(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        var protocol = await InvokeUnarySpecialMethodAsync(instance, "__abs__", context, span).ConfigureAwait(false);
        return protocol.Kind == SpecialMethodInvocationKind.Invoked
            ? protocol.Value
            : AbsValueWithoutProtocols(instance, context, span);
    }

    private static ValueTask<object> FloatAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => arguments.Length == 1 && arguments[0] is PyInstance instance
            ? ConvertInstanceToFloatAsync(instance, context, span)
            : ValueTask.FromResult(Float(arguments, span, context));

    private static async ValueTask<object> ConvertInstanceToFloatAsync(
        PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        var protocol = await InvokeNumericConversionProtocolAsync(instance, "__float__", context, span).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            return ValidateFloatConversionResult(protocol.Value, instance, context, span);
        protocol = await InvokeNumericConversionProtocolAsync(instance, "__index__", context, span).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            return FloatFromInteger((BigInteger)ValidateIntegerConversionResult(protocol.Value, "__index__", context, span), span);
        throw FloatArgumentError(instance, context, span);
    }

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
        var protocol = await InvokeNumericConversionProtocolAsync(instance, "__int__", context, span).ConfigureAwait(false);
        if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
        {
            converted = ValidateIntegerConversionResult(protocol.Value, "__int__", context, span);
        }
        else
        {
            protocol = await InvokeNumericConversionProtocolAsync(instance, "__index__", context, span).ConfigureAwait(false);
            if (protocol.Kind == SpecialMethodInvocationKind.Invoked)
            {
                converted = ValidateIntegerConversionResult(protocol.Value, "__index__", context, span);
            }
            else
            {
                protocol = await InvokeNumericConversionProtocolAsync(instance, "__trunc__", context, span).ConfigureAwait(false);
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
        var protocol = await InvokeNumericConversionProtocolAsync(instance, "__index__", context, span).ConfigureAwait(false);
        return protocol.Kind == SpecialMethodInvocationKind.Invoked
            ? ValidateIntegerConversionResult(protocol.Value, "__index__", context, span)
            : value;
    }

    private static async ValueTask<SpecialMethodInvocation> InvokeNumericConversionProtocolAsync(
        PyInstance instance, string name, ExecutionContext context, LythonSourceSpan span)
    {
        if (!instance.Type.TryLookupInMro(name, 0, out var raw, out _)) return SpecialMethodInvocation.Missing;
        var member = await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false);
        if (member is not ICallable callable) throw NotCallableError(member, context, span);
        return SpecialMethodInvocation.Invoked(await callable.InvokeAsync([], span, context).ConfigureAwait(false));
    }
}
