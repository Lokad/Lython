namespace Lokad.Lython.Runtime;

internal sealed class GovernedHostBytes : IDisposable
{
    private readonly MemoryGovernor _memoryGovernor;
    private long _charge;

    public GovernedHostBytes(
        ReadOnlyMemory<byte> memory,
        MemoryGovernor memoryGovernor,
        LythonSourceSpan? allocationSpan)
    {
        Memory = memory;
        _memoryGovernor = memoryGovernor;
        _charge = memory.IsEmpty ? 0 : PyBytes.EstimateApproximateBytes(memory.Length);
        if (_charge > 0)
        {
            memoryGovernor.Reserve(_charge, allocationSpan);
            memoryGovernor.Commit(_charge);
        }
    }

    public ReadOnlyMemory<byte> Memory { get; }

    public ReadOnlySpan<byte> Span => Memory.Span;

    public void Dispose()
    {
        _memoryGovernor.Release(_charge);
        _charge = 0;
    }
}
