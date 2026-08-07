using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyFunctionBinding
{
    public static bool TryBuildImplicitSuperContext(
        PyType? ownerType,
        IReadOnlyList<LoweredFunctionParameter> parameters,
        IReadOnlyDictionary<string, object> boundArguments,
        [MaybeNullWhen(false)] out PyType anchorType,
        [MaybeNullWhen(false)] out object receiver)
    {
        if (ownerType is null || parameters.Count == 0 || !boundArguments.TryGetValue(parameters[0].Name, out receiver))
        {
            anchorType = null;
            receiver = null;
            return false;
        }

        if (receiver is PyInstance instance && instance.Type.IsSubtypeOf(ownerType)
            || receiver is PyType type && type.IsSubtypeOf(ownerType))
        {
            anchorType = ownerType;
            return true;
        }

        anchorType = null;
        receiver = null;
        return false;
    }
}
