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

internal sealed class AdmissionGateHost(AdmissionGateBudget budget, long headroom, ILythonHost? files = null) : ILythonHost, ILythonSynchronousHostCapability
{
    private int _calls;
    public long AllocatedAtGate { get; private set; }
    public long AllocatedAtSecondGate { get; private set; }
    public long AtSecondGate { get; private set; }
    public bool CompletesSynchronously => files is null || files is ILythonSynchronousHostCapability { CompletesSynchronously: true };
    public string Cwd => "/";
    public DateTimeOffset LocalNow => DateTimeOffset.UnixEpoch;
    public DateTimeOffset UtcNow { get { if (_calls++ == 0) { budget.Gate(headroom); AllocatedAtGate = GC.GetTotalAllocatedBytes(true); } else { AtSecondGate = budget.Root.ReservedBytes; AllocatedAtSecondGate = GC.GetTotalAllocatedBytes(true); } return DateTimeOffset.UnixEpoch; } }
    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest p, LythonFileOpenOptions o, ILythonMemoryBudget b, CancellationToken t)
        => files!.OpenFileAsync(p,o,b,t);
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string p, CancellationToken t) => files!.ReadTextUtf8Async(p,t);
    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string p,long o,int c,CancellationToken t) => files!.ReadTextUtf8RangeAsync(p,o,c,t);
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string p,CancellationToken t) => files!.ReadBytesAsync(p,t);
    public ValueTask<ReadOnlyMemory<byte>> ReadBytesRangeAsync(string p,long o,int c,CancellationToken t) => files!.ReadBytesRangeAsync(p,o,c,t);
    public ValueTask WriteBytesAsync(string p,ReadOnlyMemory<byte> b,CancellationToken t) => files!.WriteBytesAsync(p,b,t);
    public ValueTask AppendBytesAsync(string p,ReadOnlyMemory<byte> b,CancellationToken t) => files!.AppendBytesAsync(p,b,t);
    public ValueTask WriteTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => files!.WriteTextUtf8Async(p,b,t);
    public ValueTask AppendTextUtf8Async(string p, ReadOnlyMemory<byte> b, CancellationToken t) => files!.AppendTextUtf8Async(p,b,t);
    public ValueTask<bool> ExistsAsync(string p, CancellationToken t) => files is null ? ValueTask.FromResult(false) : files.ExistsAsync(p,t);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string p, CancellationToken t) => files!.ListDirAsync(p,t);
    public ValueTask MkDirAsync(string p, CancellationToken t) => files!.MkDirAsync(p,t);
    public ValueTask RemoveAsync(string p, CancellationToken t) => files!.RemoveAsync(p,t);
    public ValueTask CopyAsync(string a, string b, CancellationToken t) => files!.CopyAsync(a,b,t);
    public ValueTask MoveAsync(string a, string b, CancellationToken t) => files!.MoveAsync(a,b,t);
    public ValueTask<LythonPathStat> StatAsync(string p, CancellationToken t) => files!.StatAsync(p,t);

}
