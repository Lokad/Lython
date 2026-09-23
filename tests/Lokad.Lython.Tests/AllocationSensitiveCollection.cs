namespace Lokad.Lython.Tests;

// N35: allocation-threshold tests measure the process-wide GC allocation
// counter, so parallel siblings allocating inside the measurement window trip
// the budget. This collection opts out of parallelization to keep that
// window quiet without weakening any budget.
[CollectionDefinition("AllocationSensitive", DisableParallelization = true)]
public sealed class AllocationSensitiveCollection
{
}
