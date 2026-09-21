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

        // Constructions and earlier cache builds share one coupon: later builds
        // re-snapshot through NoteCacheBuilt.
        Track(value, value.CommittedOwnedBytes, span);
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

    // Adopts attribute-style growth the construction site never saw (instances
    // attribute after construction): re-snapshots tracked owners at their current
    // charges, adopts untracked values outright. Plain registration (no refund):
    // the owner stays live in the caller, so a denial must never release live
    // charges; denial leaves prior ownership exactly as it was.
    public void TrackGrowth(object value, long currentCharge, LythonSourceSpan? span = null)
    {
        NotifyStorageReplaced(value, currentCharge);
        TrackMutable(value, currentCharge, span);
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
            case PyCounter counter when counter.OwnerMemoryGovernor is not null:
                // Plain registration only, like deques: factories track with refund.
                TrackMutable(counter, counter.CommittedStorageBytes, span);
                break;
            case PyDefaultDict defaultdict when defaultdict.OwnerMemoryGovernor is not null:
                TrackMutable(defaultdict, defaultdict.CommittedStorageBytes, span);
                break;
            case PyInstance instance when instance.OwnerMemoryGovernor is not null:
                // Plain registration only: attribute stores adopt with TrackGrowth at
                // their own sites, and this dedups to a no-op for those values.
                TrackMutable(instance, instance.CommittedAttributeBytes, span);
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

        var charge = value.CommittedOwnedBytes;
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
    // tier instead of visiting one quantum. Tier capacity is intentionally never
    // trimmed here: trim-and-regrow churn on every exhaustion cycle measured
    // +11% allocated on the 200K scan; empty capacity reconciles explicitly
    // through CommittedBackingBytes and releases on pool abandonment.
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

    // Registers one pooled value transactionally (R08): every stage below is owned
    // by this operation until the final commit, so any denial strands nothing
    // and publishes no mark, and a funded retry registers cleanly.
    private void TrackCore(object value, long valueCharge, LythonSourceSpan? span = null)
    {
        if (TrackedStorage.TryGetValue(value, out _))
        {
            return;
        }

        // Every stage rolls back on denial: the entry reservation, the tier-growth
        // reservation, and the table/list publication are all owned by this
        // operation until the final commit. A denied registration therefore strands
        // no reserved bytes and publishes no mark, so a funded retry registers
        // cleanly. Only LythonRuntimeException (budget denial) is expected here;
        // any other failure rolls back identically before propagating.
        _governor.Reserve(EntryChargeBytes, span);
        long fundedGrowth = 0;
        var entryReserved = true;
        var growthReserved = false;
        try
        {
            fundedGrowth = ReserveTierInsertion(_young, span);
            growthReserved = fundedGrowth > 0;
            var entry = new ReclamationEntry(value, valueCharge);
            var capacityBefore = _young.Capacity;
            var published = false;
            try
            {
                TrackedStorage.Add(value, entry);
                _young.Add(entry);
                published = true;
                CommitTierInsertion(_young, fundedGrowth, capacityBefore, span);
            }
            catch
            {
                if (published)
                {
                    UnpublishEntry(value, entry);
                }

                throw;
            }

            entryReserved = false;
            growthReserved = false;
            _governor.Commit(EntryChargeBytes);
        }
        catch
        {
            if (growthReserved)
            {
                _governor.ReleaseReserved(checked(fundedGrowth * 8));
            }

            if (entryReserved)
            {
                _governor.ReleaseReserved(EntryChargeBytes);
            }

            throw;
        }
    }

    private void UnpublishEntry(object value, ReclamationEntry entry)
    {
        TrackedStorage.Remove(value);
        // The entry was appended last with no interleaving publication on this
        // single-threaded runtime; fall back to a linear remove defensively.
        if (_young.Count > 0 && ReferenceEquals(_young[_young.Count - 1], entry))
        {
            _young.RemoveAt(_young.Count - 1);
        }
        else
        {
            _young.Remove(entry);
        }
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

    // Old-tier-only drain for exhaustion relief after collecting: with a full
    // old backlog the young scan below would abort on promotion funding before
    // reaching any drain, so free the collected backlog first and let the
    // re-sweep's promotions fit. Releasing needs no funding and cannot deny.
    internal long DrainOldTier()
    {
        var released = DrainTier(_old);
        if (released > 0)
        {
            _governor.Release(released);
        }

        return released;
    }

    private long SweepTier(List<ReclamationEntry> tier, List<ReclamationEntry>? promoteTo)
    {
        var released = 0L;
        // Detach-then-decide: funding a promotion slot can deny into exhaustion
        // relief, whose inner sweep prunes and moves these same tier lists. An
        // index held across that call goes stale; an entry held locally stays
        // exact no matter what the inner sweep removes.
        List<ReclamationEntry>? retained = null;
        while (tier.Count > 0)
        {
            var entry = tier[tier.Count - 1];
            tier.RemoveAt(tier.Count - 1);
            if (!entry.Target.TryGetTarget(out _))
            {
                released += entry.ValueCharge + EntryChargeBytes;
            }
            else if (promoteTo is null)
            {
                retained ??= new List<ReclamationEntry>();
                retained.Add(entry);
            }
            else
            {
                // Fund the old-tier slot before moving: a denial re-queues the entry
                // in the young tier for the next sweep instead of stranding it, and
                // releases whatever dead entries were already removed so no reclaimed
                // charge strands across the exceptional exit.
                try
                {
                    var fundedGrowth = ReserveTierInsertion(promoteTo, null);
                    var capacityBefore = promoteTo.Capacity;
                    promoteTo.Add(entry);
                    CommitTierInsertion(promoteTo, fundedGrowth, capacityBefore, null);
                }
                catch (LythonRuntimeException)
                {
                    tier.Add(entry);
                    if (released > 0)
                    {
                        _governor.Release(released);
                        released = 0;
                    }

                    throw;
                }
            }
        }

        if (retained is not null)
        {
            tier.AddRange(retained);
        }

        return released;
    }

    // Visits at most one quantum of old entries per sweep, counting dead removals
    // against the same budget as survivor advances: the window advances without
    // wrapping, so a lone live entry costs one probe instead of a full quantum of
    // revisits. Removal compacts from the end, which can only pull unvisited
    // entries into the window. Every sweep makes progress while anything remains
    // (the cursor advances or the tier shrinks), so dead entries still release
    // exactly, just over more sweeps.
    private long SweepOldQuantum()
    {
        var released = 0L;
        var probed = 0;
        while (probed < OldQuantum && _oldCursor < _old.Count)
        {
            var entry = _old[_oldCursor];
            probed++;
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
