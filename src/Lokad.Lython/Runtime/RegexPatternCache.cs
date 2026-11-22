using Lokad.Lython.Runtime.Text;

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

    internal readonly record struct Key(PyString PatternText, LythonRegexOptions Options, int? RequestedFlags);

    private Dictionary<Key, LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _recent = new();
    private long _committedSlotBytes;

    private sealed record CacheEntry(Key Key, LythonRuntime.RePatternObject Pattern);

    public int Count => _entries.Count;

    public bool TryGet(Key key, [NotNullWhen(true)] out LythonRuntime.RePatternObject? pattern)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
            pattern = node.Value.Pattern;
            return true;
        }

        pattern = null;
        return false;
    }

    // Caches a freshly compiled pattern under its exact key. Slot refusal is a
    // command failure, never a reason to fall back to uncached execution.
    public LythonRuntime.RePatternObject GetOrAdd(
        Key key,
        LythonRuntime.RePatternObject pattern,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        if (TryGet(key, out var existing)) return existing;
        // Eviction replaces an already-funded slot. Only growing the cache
        // needs new capacity funding; charging every miss strands slot charges
        // and can prevent a full cache from replacing its least-recent entry.
        if (governor is not null && _entries.Count < MaxEntries)
        {
            governor.Reserve(SlotBytesPerEntry, span);
            if (TryGet(key, out existing))
            {
                // The admission callback published the canonical wrapper.
                governor.ReleaseReserved(SlotBytesPerEntry);
                return existing;
            }
            // If a callback filled the last slot, keep the extra admission
            // until purge so publication needs no intervening refund callback.
            governor.Commit(SlotBytesPerEntry);
            _committedSlotBytes += SlotBytesPerEntry;
        }

        if (_entries.Count >= MaxEntries)
        {
            // Evict the coldest entry: its reference drops while its own ownership
            // (and any retained users) stay exactly as they were.
            var last = _recent.Last;
            if (last is not null)
            {
                _entries.Remove(last.Value.Key);
                _recent.RemoveLast();
            }
        }

        var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, pattern));
        _entries.Add(key, node);
        _recent.AddFirst(node);
        return pattern;
    }

    // Explicit clear (re.purge): drops every reference and releases slot charges.
    // Retained patterns and matches keep working on their own ownership.
    public void Clear(MemoryGovernor? governor)
    {
        // Clear alone keeps the table's backing. Relinquish it before refunding
        // capacity; callers cannot borrow the cache's private slots.
        _entries = new();
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
