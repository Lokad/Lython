using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// A funded, bounded workspace for the audited stored-child contract. It is not
// connected to MemoryGovernor relief. Complete execution roots, all bridge
// shapes, resource release and a capacity rule are still required before refunds.
internal sealed class MemoryStorageWalk : IDisposable
{
    private readonly object?[] _queue;
    private readonly object?[] _seen;
    private readonly MemoryGovernor.TemporaryMemoryReservation _funding;
    private int _count;
    private int _next;
    private int _remainingEdges;
    private int _workTick;
    private CancellationToken _cancellation;
    private bool _disposed;

    internal enum IncompleteReason { None, WorkspaceLimit, UnknownShape }
    internal IncompleteReason Incomplete { get; private set; }
    internal Type? UnsupportedType { get; private set; }
    internal ReadOnlySpan<object?> Visited => _queue.AsSpan(0, _count);
    internal long FundedBytes { get; }

    internal MemoryStorageWalk(MemoryGovernor governor, int capacity, LythonSourceSpan? span = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (capacity > 1 << 28) throw new ArgumentOutOfRangeException(nameof(capacity));
        var tableCapacity = checked((int)BitOperations.RoundUpToPowerOf2((uint)capacity * 2));
        // Workspace + reservation shells and both array headers; reference
        // slots are exact on the supported managed x64 execution platforms.
        FundedBytes = checked(192L + 8L * (capacity + (long)tableCapacity));
        _funding = governor.ReserveTemporary(FundedBytes, span);
        try
        {
            _queue = new object[capacity];
            _seen = new object[tableCapacity];
        }
        catch { _funding.Dispose(); throw; }
    }

    internal bool TryTraverse(ReadOnlySpan<object> roots, CancellationToken cancellation = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Reset();
        _remainingEdges = _queue.Length;
        _cancellation = cancellation;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var root in roots) if (!Add(root)) break;
            while (_next < _count && Incomplete == IncompleteReason.None)
            {
                if ((_next & 63) == 0) cancellation.ThrowIfCancellationRequested();
                var value = _queue[_next++]!;
                switch (value)
                {
                    case IMemoryStorageEdges edges: edges.VisitStorageChildren(this); break;
                    case object[] values:
                        foreach (var child in values) if (!Add(child)) break;
                        break;
                    case Array { Length: 0 }:
                        // An empty CLR array cannot retain a guest reference,
                        // including empty typed code/cache tables.
                        break;
                    case Dictionary<string, object> namedValues:
                        // Runtime namespace/default tables have CLR string keys.
                        foreach (var pair in namedValues) if (!Add(pair.Value)) break;
                        break;
                    case List<object> values:
                        foreach (var child in values) if (!Add(child)) break;
                        break;
                    case ExecutableCodeObject code:
                        // Index-only instructions, names and source spans carry
                        // no guest references. Capturable tables stay explicit;
                        // unaudited lowered nodes stop the walk as unknown shapes.
                        if (!Add(code.Constants) || !Add(code.Functions) ||
                            !Add(code.MatchCases) || !Add(code.StatementFallbacks) ||
                            !Add(code.ExpressionFallbacks) || !Add(code.LoopTargets)) break;
                        Add(code.UnpackingTargets);
                        break;
                    case List<PyType> types:
                        foreach (var child in types) if (!Add(child)) break;
                        break;
                    case List<LythonRuntime.ExecutableCell> cells:
                        foreach (var child in cells) if (!Add(child)) break;
                        break;
                    case PyBytes or PyComplex or BigInteger:
                        // Immutable payload owners have no guest-value children.
                        break;
                    default:
                        Incomplete = IncompleteReason.UnknownShape;
                        UnsupportedType = value.GetType();
                        break;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            return Incomplete == IncompleteReason.None;
        }
        catch { Reset(); throw; }
    }

    internal bool Add(object? value)
    {
        if (!InspectStorageItem()) return false;
        if (value is null or PyNone or bool or double or string ||
            value is BigInteger && !ChargeReclamationPool.IsTrackedValue(value)) return true;
        var mask = _seen.Length - 1;
        var index = RuntimeHelpers.GetHashCode(value) & mask;
        while (_seen[index] is { } previous)
        {
            if (!InspectStorageItem()) return false;
            if (ReferenceEquals(previous, value)) return true;
            index = (index + 1) & mask;
        }
        if (_count == _queue.Length)
        {
            Incomplete = IncompleteReason.WorkspaceLimit;
            return false;
        }
        _seen[index] = value;
        _queue[_count++] = value;
        return true;
    }

    // Bounds scans of stored records even when they contain no references
    // (for example, CSV history with thousands of empty rows).
    internal bool InspectStorageItem()
    {
        if (Incomplete != IncompleteReason.None) return false;
        if ((++_workTick & 63) == 0) _cancellation.ThrowIfCancellationRequested();
        if (_remainingEdges-- != 0) return true;
        Incomplete = IncompleteReason.WorkspaceLimit;
        return false;
    }

    private void Reset()
    {
        Array.Clear(_queue, 0, _count);
        Array.Clear(_seen);
        _count = _next = 0;
        _remainingEdges = _workTick = 0;
        _cancellation = default;
        Incomplete = IncompleteReason.None;
        UnsupportedType = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Reset();
        _funding.Dispose();
        _disposed = true;
    }
}
