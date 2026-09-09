namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static ProjectionBudget? CreateProjectionBudget(LythonRunOptions? options)
    {
        var maxProjectionBytes = ExecutionLimits.NonNegativeOrDefault(
            options?.MaxProjectionMemoryBytes?.Bytes,
            options?.DisableDefaultLimits == true ? null : LythonRunOptions.DefaultMaxProjectionMemoryBytes,
            nameof(LythonRunOptions.MaxProjectionMemoryBytes));
        return maxProjectionBytes is null
            ? null
            : new ProjectionBudget(maxProjectionBytes.Value);
    }

    private static object? NormalizePublicValue(object? value, ProjectionBudget? budget)
        => PublicProjection.NormalizeValue(value, budget);
}
