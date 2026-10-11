using Lokad.Lython;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Tracks committed charges for values whose owner reclaims them once they
// become unreachable: sweeps release charges for entries whose targets were
// collected while keeping every live entry charged. One funded collection holds
// both recent appends and retained entries. Sweeps visit all recent appends
// and a bounded retained quantum, resuming where the previous quantum stopped.
// Survivors stay in place: sweeping requires no allocation or admission.
// A full sweep visits every entry (used at exhaustion).
// Each entry also commits a registry charge covering its own tracking nodes,
// released on prune; the backing array commits per-slot on growth and releases
// when the owning run abandons the pool.
//
// Pooled mutables (lists, dictionaries) snapshot their backing charges at
// registration. Wholesale storage replacement (Clear, slice-assignment)
// releases those charges through the value itself and commits the
// replacement, so those paths re-snapshot the pool entry; incremental growth
// only ever leaves a safe residual behind. Callers own the sweep cadence; an
// abandoned source releases its scratch, while live returned values keep the
// pool funded until its backing and registration can be released together.
// Single-threaded like the rest of the runtime.
internal sealed class ChargeReclamationPool
{
    // Per-entry registry charge: the entry record, the weak handle, the table node
    // and amortized table capacity. Measured ~108 B marginal per entry at 100k scale
    // (~155 B at 20k where fixed table minimums dominate); 128 B covers the marginal
    // rate with headroom while backing below owns the fixed capacity separately.
    internal const long EntryChargeBytes = 128L;

    // Retained entries visited at most once per sweep, up to the quantum;
    // bounds per-sweep work while every entry is revisited within
    // live/quantum sweeps.
    private const int RetainedQuantum = 4096;

    // Strings and lists carry their entry directly, avoiding an additional
    // weak table handle per value. Other pooled values use this identity table.
    // Both paths keep the target weak and the same conservative registry charge.
    private static readonly ConditionalWeakTable<object, ReclamationEntry> TrackedStorage = new();

    private readonly MemoryGovernor _governor;
    private readonly List<ReclamationEntry> _entries = new();
    // Keep transient tracking arrays below the large-object threshold. The
    // first page keeps ordinary tiny-budget growth; more pages are lazy.
    private const int PageBits = 13;
    private const int PageSize = 1 << PageBits;
    private List<List<ReclamationEntry>>? _pages;
    private int _count;
    private int _recentStart;
    private int _sweepCursor;
    private long _backingBytes;

    internal sealed class ReclamationEntry
    {
        public ReclamationEntry(object target, long valueCharge)
        {
            Target = new WeakReference<object>(target, trackResurrection: target is IPyFinalizableOwnership);
            ValueCharge = valueCharge;
        }

        public WeakReference<object> Target { get; }

        public long ValueCharge { get; set; }
    }

    public ChargeReclamationPool(MemoryGovernor governor)
    {
        _governor = governor;
    }

    public int Count => _count;

    private List<ReclamationEntry> EntryPage(int index) =>
        _pages is { } pages ? pages[index >> PageBits] : _entries;

    private ReclamationEntry this[int index]
    {
        get => EntryPage(index)[index & (PageSize - 1)];
        set => EntryPage(index)[index & (PageSize - 1)] = value;
    }

    internal bool OwnsGovernor(MemoryGovernor governor) => ReferenceEquals(_governor, governor);

    // The pending container already committed this entry's fee and payload.
    // Transfer publishes an ordinary independent entry without charging either
    // twice. The caller removes its aggregate snapshot only after success.
    internal void TrackPrefundedString(PyString value, LythonSourceSpan? span)
    {
        if (!ReferenceEquals(value.OwnerMemoryGovernor, _governor) || value.ReclamationEntry is not null)
        {
            throw new InvalidOperationException("Pending string is not a fresh value owned by this governor.");
        }

        var fundedGrowth = ReserveEntryInsertion(span);
        var growthReserved = fundedGrowth > 0;
        try
        {
            var entry = new ReclamationEntry(value, value.CommittedOwnedBytes);
            var published = false;
            try
            {
                PublishEntry(value, entry);
                published = true;
                var actualGrowth = AddEntry(entry);
                CommitEntryInsertion(fundedGrowth, actualGrowth, span);
            }
            catch
            {
                if (published) UnpublishEntry(value, entry);
                throw;
            }

            growthReserved = false;
            _governor.NotePrefundedAllocation();
        }
        catch
        {
            if (growthReserved) _governor.ReleaseReserved(checked(fundedGrowth * 8));
            throw;
        }
    }

    // Reports whether a value already owns a reclamation entry, so fresh-value
    // helpers can alias-dedup before committing instead of double-charging a
    // shared box (whose sweep would then release only once).
    public bool IsTracked(object value) => IsTrackedValue(value);

    // Reference-identity ownership probe for container adoption (N06):
    // containers cannot thread a pool reference through every mutation path,
    // so they ask the shared registration directly. Tracked values stay under their
    // existing owner; only untracked scalars earn container coupons.
    internal static bool IsTrackedValue(object value) => TryGetEntry(value, out _);

    private static bool TryGetEntry(object value, [NotNullWhen(true)] out ReclamationEntry? entry)
    {
        if (value is PyString text)
        {
            entry = text.ReclamationEntry;
            return entry is not null;
        }

        if (value is PyList list)
        {
            entry = list.ReclamationEntry;
            return entry is not null;
        }

        if (value is PyTuple tuple)
        {
            entry = tuple.ReclamationEntry;
            return entry is not null;
        }

        return TrackedStorage.TryGetValue(value, out entry);
    }

    private static void PublishEntry(object value, ReclamationEntry entry)
    {
        if (value is PyString text)
        {
            text.ReclamationEntry = entry;
        }
        else if (value is PyList list)
        {
            list.ReclamationEntry = entry;
        }
        else if (value is PyTuple tuple)
        {
            tuple.ReclamationEntry = entry;
        }
        else
        {
            TrackedStorage.Add(value, entry);
        }
    }

    private static void RemoveEntry(object value)
    {
        if (value is PyString text)
        {
            text.ReclamationEntry = null;
        }
        else if (value is PyList list)
        {
            list.ReclamationEntry = null;
        }
        else if (value is PyTuple tuple)
        {
            tuple.ReclamationEntry = null;
        }
        else
        {
            TrackedStorage.Remove(value);
        }
    }

    // Re-snapshots a pooled value whose backing charges were released and
    // recommitted through the value itself, so a later sweep releases exactly
    // the current backing. Untracked values cost one lookup and no entry.
    public static void NotifyStorageReplaced(object value, long currentCharge)
    {
        if (TryGetEntry(value, out var entry))
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
    // registration belongs to the string identity, so value-equal but distinct
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

    // Only for a fresh value in a construction transaction that has exposed
    // nothing to guest code. The transaction keeps its values strongly alive,
    // so no sweep can have released these coupons. Capacity remains owned
    // by the pool, exactly as after ordinary pruning.
    internal void RefundUnpublishedValue(object value)
    {
        if (TryGetEntry(value, out var entry))
        {
            var index = IndexOfEntry(entry);
            if (index < 0)
                throw new InvalidOperationException("Unpublished value belongs to another reclamation pool.");
            RemovePublishedEntryAt(index);
            RemoveEntry(value);
            _governor.Release(entry.ValueCharge + EntryChargeBytes);
        }
        else if (value is IPyOwnershipSnapshot snapshot && snapshot.TrySnapshotOwnership(out var charge))
        {
            _governor.Release(charge);
        }
    }

    // Registers an arbitrary call result for whatever it currently owns.
    // The snapshot rule lives on the value (IPyOwnershipSnapshot) instead of
    // a pool-side concrete-type switch: strings report their construction
    // charge, mutables their backing snapshot, views their fixed shell, and
    // unowned or unknown results report nothing. Plain (non-refunding)
    // registration: some results alias stored values, so a denial must never
    // refund live charges.
    public void TrackCallResult(object result, LythonSourceSpan? span = null)
    {
        if (result is IPyOwnershipSnapshot snapshot &&
            snapshot.TrySnapshotOwnership(out var charge) &&
            charge > 0)
        {
            Track(result, charge, span);
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

    // Committed backing bytes currently owned by this pool.
    internal long CommittedBackingBytes => _backingBytes;

    // Releases charges for entries whose targets have been collected and
    // prunes them; returns the released bytes. A full sweep visits every
    // entry instead of one retained quantum. Capacity is intentionally never
    // trimmed here: trim-and-regrow churn on every exhaustion cycle measured
    // +11% allocated on the 200K scan; empty capacity reconciles explicitly
    // through CommittedBackingBytes and releases on pool abandonment.
    public long Sweep(bool full = false)
    {
        long released = 0;
        // Recent entries must not wait behind a large retained population.
        // Reverse removal only swaps in entries already visited in this pass.
        for (var i = _count - 1; i >= (full ? 0 : _recentStart); i--)
        {
            var entry = this[i];
            if (!entry.Target.TryGetTarget(out _))
            {
                released += entry.ValueCharge + EntryChargeBytes;
                RemoveAtSwap(i);
            }
        }
        _recentStart = _count;
        if (full)
        {
            _sweepCursor = 0;
        }
        else
        {
            // Dead removals consume probes just like live advances. Do not wrap
            // within a sweep: a tiny population should be visited only once.
            var probed = 0;
            while (probed < RetainedQuantum && _sweepCursor < _recentStart)
            {
                var entry = this[_sweepCursor];
                probed++;
                if (!entry.Target.TryGetTarget(out _))
                {
                    released += entry.ValueCharge + EntryChargeBytes;
                    RemoveAtSwap(_sweepCursor);
                    _recentStart--;
                }
                else
                {
                    _sweepCursor++;
                }
            }
            if (_sweepCursor >= _recentStart) _sweepCursor = 0;
        }
        _governor.Release(released);
        return released;
    }

    // Registers one pooled value transactionally (R08): every stage below is owned
    // by this operation until the final commit, so any denial strands nothing
    // and publishes no mark, and a funded retry registers cleanly.
    private void TrackCore(object value, long valueCharge, LythonSourceSpan? span = null)
    {
        if (TryGetEntry(value, out _))
        {
            return;
        }

        // Every stage rolls back on denial: the entry reservation, the backing-growth
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
            fundedGrowth = ReserveEntryInsertion(span);
            growthReserved = fundedGrowth > 0;
            var entry = new ReclamationEntry(value, valueCharge);
            var published = false;
            try
            {
                PublishEntry(value, entry);
                published = true;
                var actualGrowth = AddEntry(entry);
                CommitEntryInsertion(fundedGrowth, actualGrowth, span);
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
        RemoveEntry(value);
        var index = IndexOfEntry(entry);
        if (index >= 0) RemovePublishedEntryAt(index);
    }

    // Rare publication refunds preserve the order between retained/recent
    // regions and adjust the scan position when an earlier entry is removed.
    private void RemovePublishedEntryAt(int index)
    {
        for (var i = index; i < _count - 1; i++) this[i] = this[i + 1];
        RemoveLastEntry();
        if (index < _recentStart) _recentStart--;
        if (index < _sweepCursor) _sweepCursor--;
        if (_sweepCursor >= _recentStart) _sweepCursor = 0;
    }

    // Backing pages never shrink on prune, so committed capacity rides the
    // pool lifetime. Growth funds before the insertion becomes visible; List<T>
    // grows 0->4 then doubles, and actual growth is verified after the fact with
    // a defensive top-up so a policy change can never strand capacity.
    private long ReserveEntryInsertion(LythonSourceSpan? span)
    {
        long fundedGrowth = 0;
        try
        {
            while (true)
            {
                var growth = RequiredInsertionGrowth();
                if (growth <= fundedGrowth)
                {
                    if (growth < fundedGrowth)
                        _governor.ReleaseReserved(checked((fundedGrowth - growth) * 8));
                    return growth;
                }

                // Relief can prune entries. Revalidate after reserving so
                // insertion always has its actual capacity funded first.
                _governor.Reserve(checked((growth - fundedGrowth) * 8), span);
                fundedGrowth = growth;
            }
        }
        catch
        {
            if (fundedGrowth > 0) _governor.ReleaseReserved(checked(fundedGrowth * 8));
            throw;
        }
    }

    private void CommitEntryInsertion(long fundedGrowth, long actualGrowth, LythonSourceSpan? span)
    {
        if (actualGrowth != fundedGrowth)
        {
            if (actualGrowth > fundedGrowth)
            {
                var extra = checked((actualGrowth - fundedGrowth) * 8);
                _governor.Reserve(extra, span);
                _governor.Commit(extra);
                _backingBytes += extra;
            }
            else
            {
                // Reserving predicted growth can reclaim entries,
                // freeing existing slots before insertion. Return unused
                // funding instead of committing backing that never grew.
                _governor.ReleaseReserved(checked((fundedGrowth - actualGrowth) * 8));
                fundedGrowth = actualGrowth;
            }
        }

        var bytes = checked(fundedGrowth * 8);
        _governor.Commit(bytes);
        _backingBytes += bytes;
    }

    // Called once when the owning run abandons this pool: its pages become
    // garbage with it, so their committed backing releases instead of stranding.
    // Live pools keep their backing through exhaustion sweeps.
    internal void ReleaseBacking()
    {
        if (_backingBytes > 0)
        {
            _governor.Release(_backingBytes);
            _backingBytes = 0;
        }
    }

    private long RequiredInsertionGrowth()
    {
        if (_pages is not { } pages)
            return _count == PageSize ? 8L : ListGrowth(_entries);
        var pageIndex = _count >> PageBits;
        return pageIndex == pages.Count ? 4L + ListGrowth(pages) : ListGrowth(pages[pageIndex]);
    }

    private static long ListGrowth<T>(List<T> list) =>
        list.Count == list.Capacity ? (list.Capacity == 0 ? 4L : list.Capacity) : 0L;

    // Build a new page/registry locally before publication. A failed CLR
    // allocation leaves the previous collection intact and funding refundable.
    private long AddEntry(ReclamationEntry entry)
    {
        if (_pages is null && _count == PageSize)
        {
            var page = new List<ReclamationEntry>(4) { entry };
            var pages = new List<List<ReclamationEntry>>(4) { _entries, page };
            _pages = pages;
            _count++;
            return 8;
        }
        if (_pages is { } registry && (_count >> PageBits) == registry.Count)
        {
            var page = new List<ReclamationEntry>(4) { entry };
            var before = registry.Capacity;
            registry.Add(page);
            _count++;
            return 4L + registry.Capacity - before;
        }
        var current = EntryPage(_count);
        var capacityBefore = current.Capacity;
        current.Add(entry);
        _count++;
        return (long)current.Capacity - capacityBefore;
    }

    private int IndexOfEntry(ReclamationEntry entry)
    {
        if (_pages is not { } pages) return _entries.IndexOf(entry);
        for (var page = 0; page < pages.Count; page++)
        {
            var index = pages[page].IndexOf(entry);
            if (index >= 0) return (page << PageBits) + index;
        }
        return -1;
    }

    private void RemoveLastEntry()
    {
        _count--;
        EntryPage(_count).RemoveAt(_count & (PageSize - 1));
    }

    private void RemoveAtSwap(int index)
    {
        this[index] = this[_count - 1];
        RemoveLastEntry();
    }
}
