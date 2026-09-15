using Lokad.Lython;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Tracks committed charges for values whose owner reclaims them once they
// become unreachable: sweeps release charges for entries whose targets were
// collected while keeping every live entry charged. New entries land in the
// young tier, which scans fully every sweep; survivors promote to the old
// tier, which scans in bounded quanta every sweep (resuming where the last
// quantum stopped), so per-sweep work stays flat no matter how much stays
// retained. A full sweep drains the old tier instead (used at exhaustion).
// Each entry also commits a registry charge covering its own tracking nodes,
// released on prune; tier backing arrays commit per-slot on growth and release
// when the owning run abandons the pool.
//
// Pooled mutables (lists, dictionaries) snapshot their backing charges at
// registration. Wholesale storage replacement (Clear, slice-assignment)
// releases those charges through the value itself and commits the
// replacement, so those paths re-snapshot the pool entry; incremental growth
// only ever leaves a safe residual behind. Callers own the sweep cadence; an
// abandoned pool is swept fully with its backing and registration released.
// Single-threaded like the rest of the runtime.
internal sealed class ChargeReclamationPool
{
    // Per-entry registry charge: the entry record, the weak handle, the table node
    // and amortized table capacity. Measured ~108 B marginal per entry at 100k scale
    // (~155 B at 20k where fixed table minimums dominate); 128 B covers the marginal
    // rate with headroom while tier backing below owns the fixed capacity separately.
    internal const long EntryChargeBytes = 128L;

    // Old-tier entries visited at most once per sweep, up to the quantum;
    // bounds per-sweep work while every entry is revisited within
    // live/quantum sweeps.
    private const int OldQuantum = 4096;

    // Pooled values with a pending entry. Untracked values cost one lookup
    // and no entry on the notifying paths.
    private static readonly ConditionalWeakTable<object, ReclamationEntry> TrackedStorage = new();

    private readonly MemoryGovernor _governor;
    private readonly List<ReclamationEntry> _young = new();
    private readonly List<ReclamationEntry> _old = new();
    private int _oldCursor;
    private long _backingBytes;

    private sealed class ReclamationEntry
    {
        public ReclamationEntry(object target, long valueCharge)
        {
            Target = new WeakReference<object>(target);
            ValueCharge = valueCharge;
        }

        public WeakReference<object> Target { get; }

        public long ValueCharge { get; set; }
    }

    public ChargeReclamationPool(MemoryGovernor governor)
    {
        _governor = governor;
    }

    public int Count => _young.Count + _old.Count;

    // Re-snapshots a pooled value whose backing charges were released and
    // recommitted through the value itself, so a later sweep releases exactly
    // the current backing. Untracked values cost one lookup and no entry.
    public static void NotifyStorageReplaced(object value, long currentCharge)
    {
        if (TrackedStorage.TryGetValue(value, out var entry))
        {
            entry.ValueCharge = currentCharge;
        }
    }

    public void Track(object value, long charge, LythonSourceSpan? span = null)
    {
        if (charge > 0)
        {
            TrackCore(value, charge, span);
        }
    }

    // Registers a governed string for its exact construction charge; shared
    // empties and unowned values carry no charge and stay untracked. The
    // shared table keys by reference identity, so value-equal but distinct
    // strings track (and release) independently through this same path.
    public void TrackString(PyString value, LythonSourceSpan? span = null)
    {
        if (value.OwnerMemoryGovernor is null)
        {
            return;
        }

        Track(value, PyString.EstimateApproximateBytes(value.Utf8Bytes.Length), span);
    }

    // Registers a pooled mutable for its current backing charges, snapshotted
    // by the caller right after construction. Empty backing tracks nothing:
    // later growth charges itself through the value.
    public void TrackMutable(object value, long backingCharge, LythonSourceSpan? span = null)
    {
        if (backingCharge > 0)
        {
            TrackCore(value, backingCharge, span);
        }
    }

    // Registers a freshly built value, refunding its snapshot charges when the
    // registry charge itself is denied. Only for values the in-flight denial
    // orphans (fresh factory/slice results the caller drops on failure):
    // anything else retaining the value would over-release. The refund keeps
    // denial headroom identical to a construction-time denial, so caught
    // failures recover exactly as they did before tracking.
    public void TrackFreshMutable(object value, long backingCharge, LythonSourceSpan? span = null)
    {
        try
        {
            TrackMutable(value, backingCharge, span);
        }
        catch (LythonRuntimeException)
        {
            _governor.Release(backingCharge);
            throw;
        }
    }

    // Registers an arbitrary call result for whatever it currently owns: strings
    // for their construction charge, mutables for their backing snapshot. Coupons
    // mirror current committed charges (exact for fresh results, safe residuals
    // for later growth); wholesale replacement re-snapshots through the value,
    // and unowned results carry nothing to release. Plain (non-refunding)
    // registration: some results alias stored values, so a denial must never
    // refund live charges.
    public void TrackCallResult(object result, LythonSourceSpan? span = null)
    {
        switch (result)
        {
            case PyString text:
                TrackString(text, span);
                break;
            case PyList list when list.OwnerMemoryGovernor is not null:
                TrackMutable(list, list.CommittedStorageBytes, span);
                break;
            case PyDict dict when dict.OwnerMemoryGovernor is not null:
                TrackMutable(dict, dict.CommittedStorageBytes, span);
                break;
            case PySet set when set.OwnerMemoryGovernor is not null:
                TrackMutable(set, set.CommittedStorageBytes, span);
                break;
            case PyTuple tuple when tuple.OwnerMemoryGovernor is not null:
                TrackMutable(tuple, tuple.CommittedStorageBytes, span);
                break;
            case PyDeque deque when deque.OwnerMemoryGovernor is not null:
                // Plain registration only: fresh deque factories track with refund at
                // their own sites, and this dedups to a no-op for those values.
                TrackMutable(deque, deque.CommittedStorageBytes, span);
                break;
            case LythonRuntime.DictKeysView keysView:
                Track(keysView, 64L, span);
                break;
            case LythonRuntime.DictValuesView valuesView:
                Track(valuesView, 64L, span);
                break;
            case LythonRuntime.DictItemsView itemsView:
                // View wrappers commit a fixed shell charge at construction with
                // no backing to snapshot; the coupon is exact and immutable.
                Track(itemsView, 64L, span);
                break;
        }
    }

    // Fresh-string twin of TrackFreshMutable: the construction charge is
    // exact for values built (or adopted) through the governed string paths.
    public void TrackFreshString(PyString value, LythonSourceSpan? span = null)
    {
        if (value.OwnerMemoryGovernor is null)
        {
            return;
        }

        var charge = PyString.EstimateApproximateBytes(value.Utf8Bytes.Length);
        try
        {
            Track(value, charge, span);
        }
        catch (LythonRuntimeException)
        {
            value.OwnerMemoryGovernor.Release(charge);
            throw;
        }
    }

    // Committed tier-backing bytes currently owned by this pool.
    internal long CommittedBackingBytes => _backingBytes;

    // Releases charges for entries whose targets have been collected and
    // prunes them; returns the released bytes. A full sweep drains the old
    // tier instead of visiting one quantum, then trims both tiers to their
    // live counts so dropped populations reconcile to zero instead of pinning
    // empty capacity. Partial sweeps never trim (bounded per-sweep work).
    public long Sweep(bool full = false)
    {
        var released = SweepTier(_young, _old);
        released += full ? DrainTier(_old) : SweepOldQuantum();
        if (released > 0)
        {
            _governor.Release(released);
        }

        if (full)
        {
            released += ReconcileTierBacking();
        }

        return released;
    }

    private long ReconcileTierBacking()
    {
        var before = _backingBytes;
        _young.TrimExcess();
        _old.TrimExcess();
        _backingBytes = checked(((long)_young.Capacity + _old.Capacity) * 8);
        if (_backingBytes < before)
        {
            var released = before - _backingBytes;
            _governor.Release(released);
            return released;
        }

        return 0;
    }

    // Reserves the registry charge before publishing: a denial leaves no mark
    // behind, so a funded retry registers instead of stranding the value
    // charge without an entry. Publishing itself cannot fail halfway here:
    // the runtime is single-threaded, so a present mark implies an earlier
    // registration of this same value.
    private void TrackCore(object value, long valueCharge, LythonSourceSpan? span = null)
    {
        if (TrackedStorage.TryGetValue(value, out _))
        {
            return;
        }

        // Fund entry and tier growth before either becomes visible: a denial
        // strands nothing countable, and the funded retry registers cleanly.
        _governor.Reserve(EntryChargeBytes, span);
        var fundedGrowth = ReserveTierInsertion(_young, span);
        var entry = new ReclamationEntry(value, valueCharge);
        TrackedStorage.Add(value, entry);
        var capacityBefore = _young.Capacity;
        _young.Add(entry);
        CommitTierInsertion(_young, fundedGrowth, capacityBefore, span);
        _governor.Commit(EntryChargeBytes);
    }

    // Tier backing arrays never shrink on prune, so committed capacity rides the
    // pool lifetime. Growth funds before the insertion becomes visible; List<T>
    // grows 0->4 then doubles, and actual growth is verified after the fact with
    // a defensive top-up so a policy change can never strand capacity.
    private long ReserveTierInsertion(List<ReclamationEntry> tier, LythonSourceSpan? span)
    {
        var growth = tier.Count == tier.Capacity ? (long)(tier.Capacity == 0 ? 4 : tier.Capacity) : 0L;
        if (growth > 0)
        {
            _governor.Reserve(checked(growth * 8), span);
        }

        return growth;
    }

    private void CommitTierInsertion(List<ReclamationEntry> tier, long fundedGrowth, long capacityBefore, LythonSourceSpan? span)
    {
        var actualGrowth = (long)(tier.Capacity - capacityBefore);
        if (actualGrowth > fundedGrowth)
        {
            var extra = checked((actualGrowth - fundedGrowth) * 8);
            _governor.Reserve(extra, span);
            _governor.Commit(extra);
            _backingBytes += extra;
        }

        var bytes = checked(fundedGrowth * 8);
        _governor.Commit(bytes);
        _backingBytes += bytes;
    }

    // Called once when the owning run abandons this pool: the tier arrays become
    // garbage with it, so their committed backing releases instead of stranding.
    // Live pools keep their backing through exhaustion sweeps.
    internal void ReleaseTierBacking()
    {
        if (_backingBytes > 0)
        {
            _governor.Release(_backingBytes);
            _backingBytes = 0;
        }
    }

    private long SweepTier(List<ReclamationEntry> tier, List<ReclamationEntry>? promoteTo)
    {
        var released = 0L;
        for (var i = tier.Count - 1; i >= 0; i--)
        {
            var entry = tier[i];
            if (!entry.Target.TryGetTarget(out _))
            {
                released += entry.ValueCharge + EntryChargeBytes;
                RemoveAtSwap(tier, i);
            }
            else if (promoteTo is not null)
            {
                // Fund the old-tier slot before moving: a denial leaves the entry
                // in the young tier for the next sweep instead of stranding it.
                var fundedGrowth = ReserveTierInsertion(promoteTo, null);
                RemoveAtSwap(tier, i);
                var capacityBefore = promoteTo.Capacity;
                promoteTo.Add(entry);
                CommitTierInsertion(promoteTo, fundedGrowth, capacityBefore, null);
            }
        }

        return released;
    }

    // Visits each old entry at most once per sweep, up to the quantum: the
    // window advances without wrapping, so a lone live entry costs one probe
    // instead of a full quantum of revisits. Removal compacts from the end,
    // which can only pull unvisited entries into the window.
    private long SweepOldQuantum()
    {
        var released = 0L;
        var end = Math.Min(_oldCursor + OldQuantum, _old.Count);
        while (_oldCursor < end && _oldCursor < _old.Count)
        {
            var entry = _old[_oldCursor];
            if (!entry.Target.TryGetTarget(out _))
            {
                released += entry.ValueCharge + EntryChargeBytes;
                RemoveAtSwap(_old, _oldCursor);
            }
            else
            {
                _oldCursor++;
            }
        }

        if (_oldCursor >= _old.Count)
        {
            _oldCursor = 0;
        }

        return released;
    }

    // Exhaustion drain: releases everything unreachable with a full scan.
    // Live entries keep their charges and stay tracked (unlike a terminal
    // drain), so a run that survives the relief keeps exact ownership.
    private static long DrainTier(List<ReclamationEntry> tier)
    {
        var released = 0L;
        for (var i = tier.Count - 1; i >= 0; i--)
        {
            if (!tier[i].Target.TryGetTarget(out _))
            {
                released += tier[i].ValueCharge + EntryChargeBytes;
                RemoveAtSwap(tier, i);
            }
        }

        return released;
    }

    private static void RemoveAtSwap(List<ReclamationEntry> tier, int index)
    {
        tier[index] = tier[tier.Count - 1];
        tier.RemoveAt(tier.Count - 1);
    }
}
