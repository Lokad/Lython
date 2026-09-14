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
// Each entry also commits a small registry charge covering its own tracking
// nodes, released on prune.
//
// Pooled mutables (lists, dictionaries) snapshot their backing charges at
// registration. Wholesale storage replacement (Clear, slice-assignment)
// releases those charges through the value itself and commits the
// replacement, so those paths re-snapshot the pool entry; incremental growth
// only ever leaves a safe residual behind. Callers own the sweep cadence; an
// abandoned pool simply stops sweeping and keeps its charges (safe
// direction). Single-threaded like the rest of the runtime.
internal sealed class ChargeReclamationPool
{
    // Per-entry registry charge: the entry, the weak handle and the table node.
    private const long EntryChargeBytes = 64L;

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

    public void Track(object value, long charge)
    {
        if (charge > 0)
        {
            TrackCore(value, charge);
        }
    }

    // Registers a governed string for its exact construction charge; shared
    // empties and unowned values carry no charge and stay untracked. The
    // shared table keys by reference identity, so value-equal but distinct
    // strings track (and release) independently through this same path.
    public void TrackString(PyString value)
    {
        if (value.OwnerMemoryGovernor is null)
        {
            return;
        }

        Track(value, PyString.EstimateApproximateBytes(value.Utf8Bytes.Length));
    }

    // Registers a pooled mutable for its current backing charges, snapshotted
    // by the caller right after construction. Empty backing tracks nothing:
    // later growth charges itself through the value.
    public void TrackMutable(object value, long backingCharge)
    {
        if (backingCharge > 0)
        {
            TrackCore(value, backingCharge);
        }
    }

    // Registers a freshly built value, refunding its snapshot charges when the
    // registry charge itself is denied. Only for values the in-flight denial
    // orphans (fresh factory/slice results the caller drops on failure):
    // anything else retaining the value would over-release. The refund keeps
    // denial headroom identical to a construction-time denial, so caught
    // failures recover exactly as they did before tracking.
    public void TrackFreshMutable(object value, long backingCharge)
    {
        try
        {
            TrackMutable(value, backingCharge);
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
    public void TrackCallResult(object result)
    {
        switch (result)
        {
            case PyString text:
                TrackString(text);
                break;
            case PyList list when list.OwnerMemoryGovernor is not null:
                TrackMutable(list, list.CommittedStorageBytes);
                break;
            case PyDict dict when dict.OwnerMemoryGovernor is not null:
                TrackMutable(dict, dict.CommittedStorageBytes);
                break;
            case PySet set when set.OwnerMemoryGovernor is not null:
                TrackMutable(set, set.CommittedStorageBytes);
                break;
            case PyTuple tuple when tuple.OwnerMemoryGovernor is not null:
                TrackMutable(tuple, tuple.CommittedStorageBytes);
                break;
            case LythonRuntime.DictKeysView keysView:
                Track(keysView, 64L);
                break;
            case LythonRuntime.DictValuesView valuesView:
                Track(valuesView, 64L);
                break;
            case LythonRuntime.DictItemsView itemsView:
                // View wrappers commit a fixed shell charge at construction with
                // no backing to snapshot; the coupon is exact and immutable.
                Track(itemsView, 64L);
                break;
        }
    }

    // Fresh-string twin of TrackFreshMutable: the construction charge is
    // exact for values built (or adopted) through the governed string paths.
    public void TrackFreshString(PyString value)
    {
        if (value.OwnerMemoryGovernor is null)
        {
            return;
        }

        var charge = PyString.EstimateApproximateBytes(value.Utf8Bytes.Length);
        try
        {
            Track(value, charge);
        }
        catch (LythonRuntimeException)
        {
            value.OwnerMemoryGovernor.Release(charge);
            throw;
        }
    }

    // Releases charges for entries whose targets have been collected and
    // prunes them; returns the released bytes. A full sweep drains the old
    // tier instead of visiting one quantum.
    public long Sweep(bool full = false)
    {
        var released = SweepTier(_young, _old);
        released += full ? DrainTier(_old) : SweepOldQuantum();
        if (released > 0)
        {
            _governor.Release(released);
        }

        return released;
    }

    // Reserves the registry charge before publishing: a denial leaves no mark
    // behind, so a funded retry registers instead of stranding the value
    // charge without an entry. Publishing itself cannot fail halfway here:
    // the runtime is single-threaded, so a present mark implies an earlier
    // registration of this same value.
    private void TrackCore(object value, long valueCharge)
    {
        if (TrackedStorage.TryGetValue(value, out _))
        {
            return;
        }

        _governor.Reserve(EntryChargeBytes, null);
        var entry = new ReclamationEntry(value, valueCharge);
        TrackedStorage.Add(value, entry);
        _young.Add(entry);
        _governor.Commit(EntryChargeBytes);
    }

    private static long SweepTier(List<ReclamationEntry> tier, List<ReclamationEntry>? promoteTo)
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
                promoteTo.Add(entry);
                RemoveAtSwap(tier, i);
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
