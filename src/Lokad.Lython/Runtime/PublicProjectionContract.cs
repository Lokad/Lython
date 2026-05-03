using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PublicProjectionContract
{
    public static bool CanProjectDictionaryKey(object value) => PublicProjection.NormalizeValue(value) is not null;

    public static string Describe(object value)
    {
        return value switch
        {
            PyNone => "null",
            PyString => "string",
            PyList => "List<object?>",
            PyTuple => "object?[]",
            PyDict => "Dictionary<object, object?>",
            PySet => "HashSet<object?>",
            LythonRuntime.ReFindAllResult => "ReFindAllResult",
            _ => value.GetType().Name
        };
    }
}
