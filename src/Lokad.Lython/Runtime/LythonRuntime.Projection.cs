namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object? NormalizePublicValue(object value, LythonRunOptions? options)
    {
        var maxProjectionBytes = options?.MaxProjectionMemoryBytes is > 0
            ? options.MaxProjectionMemoryBytes
            : options?.DisableDefaultLimits == true
                ? null
                : LythonRunOptions.DefaultMaxProjectionMemoryBytes;
        var budget = maxProjectionBytes is null
            ? null
            : new ProjectionBudget(maxProjectionBytes.Value);
        return PublicProjection.NormalizeValue(value, budget);
    }
}
