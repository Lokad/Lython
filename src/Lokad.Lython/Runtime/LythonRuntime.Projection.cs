namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static ProjectionBudget? CreateProjectionBudget(LythonRunOptions? options)
        => options?.ProjectionMemoryOwner is { } owner ? new ProjectionBudget(owner) : null;

    private static object? NormalizePublicValue(object? value, ProjectionBudget? budget)
        => PublicProjection.NormalizeValue(value, budget);
}
