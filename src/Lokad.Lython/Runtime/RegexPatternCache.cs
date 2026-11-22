namespace Lokad.Lython.Runtime;

// Bounded per-execution regex compilation cache (N19): module-level calls with the
// same immutable UTF-8 pattern, requested flags and compile options share a wrapper,
// mirroring CPython's 512-entry re cache without going global (per-run isolation).
// Cached patterns keep their existing execution ownership: the cache holds
// references without additional pattern charges, so eviction and
// purge never disturb retained patterns/matches, which keep independent lifetimes.
// Keys borrow the pattern's existing text ownership; no decoded key copy is needed.
// Cache slots charge a flat per-entry rate once: eviction retains its capacity
// charge while explicit purge relinquishes the backing and refunds capacity.
// Structures die with the run governor. Single-threaded like the runtime.
internal sealed class RegexPatternCache
{

    // CPython parity bound: at most this many compiled patterns stay shared.
    internal const int MaxEntries = 512;

    // Hash-table capacity/replacement overlap, entry record and LRU node.
    // This is conservative capacity funding, not an exact object-size claim.
    internal const long SlotBytesPerEntry = 256L;

    private Dictionary<RegexPatternKey, LinkedListNode<CacheEntry>> _entries = new(RegexPatternKey.Comparer.Instance);
    private readonly LinkedList<CacheEntry> _recent = new();
    private long _committedSlotBytes;

    private readonly record struct CacheEntry(RegexPatternKey Key, LythonRuntime.RePatternObject Pattern);

    public int Count => _entries.Count;

    public bool TryGet(RegexPatternKey key, [NotNullWhen(true)] out LythonRuntime.RePatternObject? pattern,
        CancellationToken token = default, LythonSourceSpan? span = null)
    {
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        var found = token.CanBeCanceled
            ? _entries.GetAlternateLookup<RegexPatternKey.Lookup>().TryGetValue(new(key, token, span), out var node)
            : _entries.TryGetValue(key, out node);
        if (found)
        {
            var entry = node.RequireNotNull();
            _recent.Remove(entry);
            _recent.AddFirst(entry);
            pattern = entry.Value.Pattern;
            return true;
        }

        pattern = null;
        return false;
    }

    // Caches a freshly compiled pattern under its exact key. Slot refusal is a
    // command failure, never a reason to fall back to uncached execution.
    public LythonRuntime.RePatternObject GetOrAdd(
        RegexPatternKey key,
        LythonRuntime.RePatternObject pattern,
        MemoryGovernor governor,
        LythonSourceSpan? span,
        CancellationToken cancellationToken = default)
    {
        governor.ThrowIfAdmissionFailed();
        if (TryGet(key, out var existing, cancellationToken, span)) return existing;
        // Eviction replaces an already-funded slot. Only growing the cache
        // needs new capacity funding; charging every miss strands slot charges
        // and can prevent a full cache from replacing its least-recent entry.
        var bytes = _entries.Count < MaxEntries ? SlotBytesPerEntry : 0;
        governor.Reserve(bytes, span);
        try
        {
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            // The admission callback can publish the canonical wrapper.
            if (bytes != 0 && TryGet(key, out existing, cancellationToken, span)) return existing;

            var entry = new CacheEntry(key, pattern);
            LinkedListNode<CacheEntry> node;
            if (_entries.Count >= MaxEntries)
            {
                // Replace only the cache's reference. Any live pattern or
                // match keeps its existing owner.
                node = _recent.Last.RequireNotNull();
                _entries.Remove(node.Value.Key);
                _recent.Remove(node);
                node.Value = entry;
            }
            else node = new(entry);

            _entries.Add(key, node);
            _recent.AddFirst(node);
            // If a callback filled the last slot, keep the extra admission
            // until purge so publication needs no intervening refund callback.
            governor.Commit(bytes);
            _committedSlotBytes += bytes;
            bytes = 0;
            return pattern;
        }
        finally { if (bytes != 0) governor.ReleaseReserved(bytes); }
    }

    // Explicit clear (re.purge): drops every reference and releases slot charges.
    // Retained patterns and matches keep working on their own ownership.
    public void Clear(MemoryGovernor? governor)
    {
        // Clear alone keeps the table's backing. Relinquish it before refunding
        // capacity; callers cannot borrow the cache's private slots.
        _entries = new(RegexPatternKey.Comparer.Instance);
        _recent.Clear();
        if (governor is not null && _committedSlotBytes > 0)
        {
            // A host refund can reenter and grow the empty cache. Retire
            // the previous balance before that new capacity is admitted.
            var bytes = _committedSlotBytes;
            _committedSlotBytes = 0;
            governor.Release(bytes);
        }
    }
}
