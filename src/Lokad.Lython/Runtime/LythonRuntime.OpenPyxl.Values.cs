using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object OptionalStringValue(string? value)
        => value is null ? PyNone.Instance : PyString.FromString(value);

    private static string? NullableStringValue(object value, string owner)
        => value is PyNone ? null : ExpectString(value, owner, null);
}
