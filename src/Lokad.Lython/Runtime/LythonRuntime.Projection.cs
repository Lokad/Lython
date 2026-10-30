namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static ProjectionBudget CreateProjectionBudget(LythonRunOptions? options, CancellationToken cancellationToken = default)
        => new(options?.ProjectionMemoryOwner, cancellationToken);

    private static object? NormalizePublicValue(object? value, ProjectionBudget? budget)
        => PublicProjection.NormalizeValue(value, budget);
}
