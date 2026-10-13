namespace Lokad.Lython.PublicApi.Tests;

// Explicit collection/finalizer observations and original stress scales share
// one quiet collection. Keep cancellation deadlines and lifetime assertions
// independent of competing process-wide collections. Stress is selected with
// Category=Stress; membership here does not exclude a normal regression.
[CollectionDefinition("RuntimeStress", DisableParallelization = true)]
public sealed class RuntimeStressCollection
{
}
