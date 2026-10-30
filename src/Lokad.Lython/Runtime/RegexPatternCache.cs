using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// Bounded per-execution regex compilation cache (N19): module-level calls with the
// same immutable UTF-8 pattern and compile options share one compiled pattern,
// mirroring CPython's 512-entry re cache without going global (per-run isolation).
// Cached patterns keep their existing execution ownership: the cache holds
// references without additional pattern charges, so eviction and
// purge never disturb retained patterns/matches, which keep independent lifetimes.
// Keys borrow the pattern's existing text ownership; no decoded key copy is needed.
// Cache slots charge a flat per-entry rate once: eviction
// retains its capacity charge while explicit purge releases it (like
// Clear). Structures die with the run governor. Single-threaded like the rest of
// the runtime.
internal sealed class RegexPatternCache
{

    // CPython parity bound: at most this many compiled patterns stay shared.
    internal const int MaxEntries = 512;

    // Dictionary slot plus LRU node, amortized; safe direction.
    internal const long SlotBytesPerEntry = 64L;

    private readonly Dictionary<(PyString PatternText, LythonRegexOptions Options), LinkedListNode<CacheEntry>> _entries = new();
    private readonly LinkedList<CacheEntry> _recent = new();
    private long _committedSlotBytes;

    private sealed record CacheEntry((PyString PatternText, LythonRegexOptions Options) Key, LythonRuntime.RePatternObject Pattern);

    public int Count => _entries.Count;

    public bool TryGet(PyString patternText, LythonRegexOptions options, out LythonRuntime.RePatternObject? pattern)
    {
        if (_entries.TryGetValue((patternText, options), out var node))
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
    public void Add(
        PyString patternText,
        LythonRegexOptions options,
        LythonRuntime.RePatternObject pattern,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        // Eviction replaces an already-funded slot. Only growing the cache
        // needs new capacity funding; charging every miss strands slot charges
        // and can prevent a full cache from replacing its least-recent entry.
        if (governor is not null && _entries.Count < MaxEntries)
        {
            governor.Reserve(SlotBytesPerEntry, span);
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

        var node = _recent.AddFirst(new CacheEntry((patternText, options), pattern));
        _entries.Add((patternText, options), node);
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
