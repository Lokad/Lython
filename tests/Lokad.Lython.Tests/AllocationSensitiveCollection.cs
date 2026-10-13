namespace Lokad.Lython.Tests;

// Process-wide collection/finalizer and allocation-counter tests share one
// quiet collection. A GC invoked by another fixture changes their lifetime
// and generation observations even when each fixture has its own governor.
// Ordinary semantic tests retain parallel execution; no budget is weakened.
[CollectionDefinition("AllocationSensitive", DisableParallelization = true)]
public sealed class AllocationSensitiveCollection
{
}
