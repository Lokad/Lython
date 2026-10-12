namespace Lokad.Lython.PublicApi.Tests;

// Sustained constrained-runtime loops intentionally exercise process-wide GC.
// Keep their finite cancellation deadlines independent of parallel suite load.
[CollectionDefinition("RuntimeStress", DisableParallelization = true)]
public sealed class RuntimeStressCollection
{
}
