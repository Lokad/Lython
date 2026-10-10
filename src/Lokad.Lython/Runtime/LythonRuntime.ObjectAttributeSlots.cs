namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Only the sealed engine slots qualify. Live guest overrides and slot
    // descriptors continue through their ordinary binding/invocation paths.
    internal static bool IsDefaultObjectGetAttribute(object slot) => slot is ObjectGetAttrMethod;
    internal static bool IsDefaultObjectSetAttribute(object slot) => slot is ObjectSetAttrMethod;

    internal static object GetObjectInstanceAttribute(PyInstance instance, string name, ExecutionContext context, LythonSourceSpan span)
    {
        if (PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallback(instance, name, context, span, out var value))
            return value;
        throw PyMemberAccess.CreateMissingMemberError(instance, name, span, context);
    }

    internal static async ValueTask<object> GetObjectInstanceAttributeAsync(PyInstance instance, string name, ExecutionContext context, LythonSourceSpan span)
    {
        var resolved = await PyAttributeLookup.TryResolveInstanceMemberWithoutGetAttrFallbackAsync(instance, name, context, span).ConfigureAwait(false);
        if (resolved.Found) return resolved.Value;
        throw PyMemberAccess.CreateMissingMemberError(instance, name, span, context);
    }

    internal static void SetObjectInstanceAttribute(PyInstance instance, string name, object value, ExecutionContext context, LythonSourceSpan span)
    {
        if (instance.Type.TryLookupInMro(name, 0, out var descriptor, out _) &&
            PyAttributeLookup.TrySetDescriptorValue(descriptor, instance, value, context, span))
            return;
        StoreObjectInstanceAttribute(instance, name, value, context, span);
    }

    internal static async ValueTask SetObjectInstanceAttributeAsync(PyInstance instance, string name, object value, ExecutionContext context, LythonSourceSpan span)
    {
        if (instance.Type.TryLookupInMro(name, 0, out var descriptor, out _) &&
            await PyAttributeLookup.TrySetDescriptorValueAsync(descriptor, instance, value, context, span).ConfigureAwait(false))
            return;
        StoreObjectInstanceAttribute(instance, name, value, context, span);
    }

    private static void StoreObjectInstanceAttribute(PyInstance instance, string name, object value, ExecutionContext context, LythonSourceSpan span)
    {
        instance.AttachMemoryGovernor(context.MemoryGovernor, span);
        var attributesBefore = instance.CommittedAttributeBytes;
        instance.SetAttribute(name, value);
        if (instance.CommittedAttributeBytes != attributesBefore)
            context.Services.State.CallTemporaries.TrackGrowth(instance, instance.CommittedAttributeBytes, span);
    }
}
