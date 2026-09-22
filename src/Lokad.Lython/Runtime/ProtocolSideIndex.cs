namespace Lokad.Lython.Runtime;

// Shared protocol-key side index (N10): hash-bucketed candidate positions so
// lookups dispatch guest == only over same-hash entries instead of rescanning
// every key. Dicts and sets home every protocol entry here under both its
// frozen protocol hash and its frozen structural hash (either key can match,
// like the dual-key linear scans this replaces), so per-entry cost covers two
// bucket references. Positions track the insertion-ordered side list exactly:
// removals shift survivors, and the fixup decrements every later position (the
// same O(n) bound the rescans already paid; inserts and lookups drop to their
// bucket). Snapshots copy only candidates, never the whole list.
//
// Accounting mirrors the coupon pattern: a flat per-entry rate commits on
// insert (deny-before-mutate) and releases on clear, drop (via the container
// snapshot) and denial rollback. Steady-state removals keep their share
// (capacity retained, like tier backing and shrink-kept storage: safe
// direction); wholesale clear and rollback release exactly.
internal sealed class ProtocolSideIndex
{
    // Two hash-table nodes plus bucket slots plus amortized capacity, safe
    // direction. Removals keep it (see above); clear, rollback and drop sweep
    // release it.
    internal const long IndexBytesPerEntry = 96L;

    private readonly Dictionary<int, List<int>> _buckets = new();
    private int _liveCount;
    private long _committedBytes;

    public long CommittedBytes => _committedBytes;

    public int LiveCount => _liveCount;

    // Indexes the side-list position about to be appended. Denies before the
    // caller mutates: a denied coupon leaves the map exactly as it was.
    public void Add(int position, int hash, int structuralHash, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is not null)
        {
            governor.Reserve(IndexBytesPerEntry, span);
            governor.Commit(IndexBytesPerEntry);
            _committedBytes += IndexBytesPerEntry;
        }

        try
        {
            GetBucket(hash).Add(position);
            GetBucket(structuralHash).Add(position);
            _liveCount++;
        }
        catch
        {
            // Host-level failure (OOM) after committing: unwind exactly.
            RemovePosition(hash, position);
            RemovePosition(structuralHash, position);
            if (governor is not null)
            {
                _committedBytes -= IndexBytesPerEntry;
                governor.Release(IndexBytesPerEntry);
            }

            throw;
        }
    }

    // Drops one entry's references after its side-list removal and shifts every
    // later position down, preserving the positional invariant. Charges stay
    // (capacity retained); clear and drop reconcile them.
    public void RemoveAt(int removedPosition, int hash, int structuralHash)
    {
        RemovePosition(hash, removedPosition);
        RemovePosition(structuralHash, removedPosition);
        _liveCount--;
        foreach (var bucket in _buckets.Values)
        {
            for (var i = 0; i < bucket.Count; i++)
            {
                if (bucket[i] > removedPosition)
                {
                    bucket[i]--;
                }
            }
        }

        PruneEmptyBuckets();
    }

    // Exact inverse of Add for denial rollback: the operation never happened,
    // so its charge releases too. Only for rollback (steady-state removals use
    // RemoveAt and keep their share).
    public void RemoveForRollback(int position, int hash, int structuralHash, MemoryGovernor? governor)
    {
        RemovePosition(hash, position);
        RemovePosition(structuralHash, position);
        _liveCount--;
        PruneEmptyBuckets();
        if (governor is not null)
        {
            _committedBytes -= IndexBytesPerEntry;
            governor.Release(IndexBytesPerEntry);
        }
    }

    // Merges both buckets into one candidate set (positions may repeat across
    // buckets; the set dedups). Pure reads for the caller to snapshot from.
    public void CollectCandidates(int hash, int structuralHash, HashSet<int> into)
    {
        if (_buckets.TryGetValue(hash, out var primary))
        {
            foreach (var position in primary)
            {
                _ = into.Add(position);
            }
        }

        if (structuralHash != hash && _buckets.TryGetValue(structuralHash, out var secondary))
        {
            foreach (var position in secondary)
            {
                _ = into.Add(position);
            }
        }
    }

    // Bulk-clones another index (copy construction): one commit up front denies
    // cleanly before building anything; the caller refunds its own storage.
    public void CloneFrom(ProtocolSideIndex other, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var bytes = checked((long)other._liveCount * IndexBytesPerEntry);
        if (governor is not null && bytes > 0)
        {
            governor.Reserve(bytes, span);
            governor.Commit(bytes);
            _committedBytes += bytes;
        }

        foreach (var pair in other._buckets)
        {
            _buckets.Add(pair.Key, new List<int>(pair.Value));
        }

        _liveCount = other._liveCount;
    }

    // Releases every index charge at once for wholesale replacement or rollback.
    public void ReleaseAll(MemoryGovernor? governor)
    {
        if (governor is not null && _committedBytes > 0)
        {
            governor.Release(_committedBytes);
            _committedBytes = 0;
        }

        _buckets.Clear();
        _liveCount = 0;
    }

    private List<int> GetBucket(int hash)
    {
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            bucket = new List<int>();
            _buckets.Add(hash, bucket);
        }

        return bucket;
    }

    private void RemovePosition(int hash, int position)
    {
        if (_buckets.TryGetValue(hash, out var bucket))
        {
            bucket.Remove(position);
        }
    }

    private void PruneEmptyBuckets()
    {
        List<int>? empty = null;
        foreach (var pair in _buckets)
        {
            if (pair.Value.Count == 0)
            {
                empty ??= new List<int>();
                empty.Add(pair.Key);
            }
        }

        if (empty is not null)
        {
            foreach (var key in empty)
            {
                _buckets.Remove(key);
            }
        }
    }
}