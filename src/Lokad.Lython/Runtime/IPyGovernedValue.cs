namespace Lokad.Lython.Runtime;

internal interface IPyGovernedValue
{
    MemoryGovernor? OwnerMemoryGovernor { get; }

    LythonSourceSpan? AllocationSpan { get; }
}
