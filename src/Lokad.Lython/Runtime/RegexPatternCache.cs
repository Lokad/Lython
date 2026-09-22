namespace Lokad.Lython.Runtime;

// Bounded per-execution regex compilation cache (N19): module-level calls with the
// same pattern text and flags share one compiled pattern instead of recompiling,
// mirroring CPython's 512-entry re cache without going global (per-run isolation).
// Cached patterns keep the ownership they were built with (TrackFreshMutable at
// creation): the cache holds references without additional charges, so eviction and
// purge never disturb retained patterns/matches, which keep independent lifetimes.
// The decoded key text is bounded transient scratch per lookup; retained key copies
// stay under the entry cap. Cache slots charge a flat per-entry rate once: eviction
// retains it (capacity, like tier backing) while explicit purge releases it (like
// Clear). Structures die with the run governor. Single-threaded like the rest of
// the runtime.
internal sealed class RegexPatternCache
{
    // CPython parity bound: at most this many compiled patterns stay shared.
    internal const int MaxEntries = 512;

    // Dictionary slot plus LRU node, amortized; safe direction.
    internal const long SlotBytesPerEntry = 64L;

    private readonly Dictionary<(string PatternText, int Flags), LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _recent = new();
    private long _committedSlotBytes;

    private sealed record CacheEntry((string PatternText, int Flags) Key, LythonRuntime.RePatternObject Pattern);

    public int Count => _entries.Count;

    public bool TryGet(string patternText, int flags, out LythonRuntime.RePatternObject? pattern)
    {
        if (_entries.TryGetValue((patternText, flags), out var node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
            pattern = node.Value.Pattern;
            return true;
        }

        pattern = null;
        return false;
    }

    // Caches a freshly compiled pattern under its exact key. Slot denial skips
    // caching silently: the caller still returns its (already owned) result, so a
    // tight budget keeps patterns working while the cache stays small.
    public void Add(
        string patternText,
        int flags,
        LythonRuntime.RePatternObject pattern,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        if (governor is not null)
        {
            try
            {
                governor.Reserve(SlotBytesPerEntry, span);
                governor.Commit(SlotBytesPerEntry);
            }
            catch (LythonRuntimeException)
            {
                return;
            }

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

        var node = _recent.AddFirst(new CacheEntry((patternText, flags), pattern));
        _entries.Add((patternText, flags), node);
    }

    // Explicit clear (re.purge): drops every reference and releases slot charges.
    // Retained patterns and matches keep working on their own ownership.
    public void Clear(MemoryGovernor? governor)
    {
        _entries.Clear();
        _recent.Clear();
        if (governor is not null && _committedSlotBytes > 0)
        {
            governor.Release(_committedSlotBytes);
            _committedSlotBytes = 0;
        }
    }
}
