using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython.PublicApi.Tests;

internal sealed class AdmissionGateBudget : ILythonMemoryBudget
{
    public LythonMemoryBudget Root { get; } = new(64 * 1024 * 1024);
    public long Ceiling { get; private set; } = long.MaxValue;
    public long AtGate { get; private set; }
    public void Gate(long headroom) { AtGate = Root.ReservedBytes; Ceiling = checked(AtGate + headroom); }
    public ILythonMemoryScope CreateScope() => new Scope(this, Root.CreateScope());
    public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => Reserve(Root, bytes, out reservation);
    private bool Reserve(ILythonMemoryBudget owner, long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
    { reservation = null; if (bytes > Ceiling - Root.ReservedBytes || !owner.TryReserve(bytes, out var inner)) return false; reservation = new Reservation(this, inner); return true; }
    private sealed class Reservation(AdmissionGateBudget root, ILythonMemoryReservation inner) : ILythonMemoryReservation
    {
        public long Bytes => inner.Bytes;
        public bool TryResize(long bytes) => bytes - inner.Bytes <= root.Ceiling - root.Root.ReservedBytes && inner.TryResize(bytes);
        public void Dispose() => inner.Dispose();
    }
    private sealed class Scope(AdmissionGateBudget root, ILythonMemoryScope inner) : ILythonMemoryScope
    {
        public ILythonMemoryScope CreateScope() => new Scope(root, inner.CreateScope());
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation) => root.Reserve(inner, bytes, out reservation);
        public void Dispose() => inner.Dispose();
    }
}

internal sealed class AdmissionGateHost(AdmissionGateBudget budget, long headroom) : ILythonHost, ILythonSynchronousHostCapability
{
    private int _calls;
    public long AllocatedAtGate { get; private set; }
    public long AllocatedAtSecondGate { get; private set; }
    public long AtSecondGate { get; private set; }
    public bool CompletesSynchronously => true;
    public string Cwd => "/";
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow { get { if (_calls++ == 0) { budget.Gate(headroom); AllocatedAtGate = GC.GetTotalAllocatedBytes(true); } else { AtSecondGate = budget.Root.ReservedBytes; AllocatedAtSecondGate = GC.GetTotalAllocatedBytes(true); } return DateTimeOffset.UnixEpoch; } }
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string p, CancellationToken t) => throw new NotSupportedException();
    public ValueTask WriteTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => throw new NotSupportedException();
    public ValueTask AppendTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => throw new NotSupportedException();
    public ValueTask<bool> ExistsAsync(string p, CancellationToken t) => ValueTask.FromResult(false);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => throw new NotSupportedException();
    public ValueTask MkDirAsync(string p, CancellationToken t) => throw new NotSupportedException();
    public ValueTask RemoveAsync(string p, CancellationToken t) => throw new NotSupportedException();
    public ValueTask CopyAsync(string a, string b, CancellationToken t) => throw new NotSupportedException();
    public ValueTask MoveAsync(string a, string b, CancellationToken t) => throw new NotSupportedException();
    public ValueTask<LythonPathStat> StatAsync(string p, CancellationToken t) => throw new NotSupportedException();
}
