namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object? NormalizePublicValue(object value, LythonRunOptions? options)
    {
        var maxProjectionBytes = ExecutionLimits.NonNegativeOrDefault(
            options?.MaxProjectionMemoryBytes?.Bytes,
            options?.DisableDefaultLimits == true ? null : LythonRunOptions.DefaultMaxProjectionMemoryBytes,
            nameof(LythonRunOptions.MaxProjectionMemoryBytes));
        var budget = maxProjectionBytes is null
            ? null
            : new ProjectionBudget(maxProjectionBytes.Value);
        return PublicProjection.NormalizeValue(value, budget);
    }
}
