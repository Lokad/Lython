using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

// The wrapper cache keys on the requested flags. Compiled frontend artifacts
// separately key on normalized provider options. Keys borrow execution-owned
// values, so removing a slot never refunds a retained pattern or flag.
internal sealed class RegexPatternCache
{
    internal const int MaxEntries = 512;

    // Funds the entry, hash-table capacity and replacement overlap.
    internal const long SlotBytesPerEntry = 256L;

    private static readonly object OmittedFlags = BigInteger.Zero;

    internal readonly record struct Key(PyString Pattern, object Flags, int? ExactBits, bool Protocol, int Hash)
    {
        internal static Key Exact(PyString pattern, object? flags, int bits,
            CancellationToken token, LythonSourceSpan? span)
            => new(pattern, flags ?? OmittedFlags, bits, false,
                HashCode.Combine(pattern.GetHashCode(token, span), PyValueComparer.NormalizeHash(new BigInteger(bits).GetHashCode())));

        internal static Key Prepared(PyString pattern, object flags, int hash, bool protocol,
            CancellationToken token, LythonSourceSpan? span)
            => new(pattern, flags, null, protocol, HashCode.Combine(pattern.GetHashCode(token, span), hash));
    }

    private sealed class Entry(Key key, LythonRuntime.RePatternObject pattern)
    {
        internal Key Key = key;
        internal LythonRuntime.RePatternObject Pattern = pattern;
        internal Entry? Collision, Older, Newer;
        internal long Sequence;
    }

    // The same entry is in one hash bucket and one recency list. Guest
    // comparisons never run inside a Dictionary comparer.
    private Dictionary<int, Entry> _buckets = new();
    private Entry? _oldest, _newest;
    private long _version;
    private long _committedSlotBytes;
    internal int Count { get; private set; }

    private readonly record struct Probe(LythonRuntime.RePatternObject? Match, object? CompareFlags, long Sequence);

    internal ValueTask<LythonRuntime.RePatternObject?> Find(Key key, CancellationToken token, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context = null, bool asynchronous = false)
    {
        var probe = ProbeBucket(key, 0, token, span);
        return probe.CompareFlags is null ? new(probe.Match)
            : CompareFlagsAsync(key, probe, token, span.RequireNotNull(), context.RequireNotNull(), asynchronous);
    }

    private Probe ProbeBucket(Key key, long after, CancellationToken token, LythonSourceSpan? span)
    {
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        _buckets.TryGetValue(key.Hash, out var first);
        for (var entry = first; entry is not null; entry = entry.Collision)
        {
            if (entry.Sequence <= after) continue;
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            var stored = entry.Key;
            if (!RegexPatternKey.TextEquals(stored.Pattern, key.Pattern, token, span)) continue;

            if (stored.ExactBits is { } left && key.ExactBits is { } right)
            {
                if (left == right) return new(Touch(entry), null, 0);
                continue;
            }
            if (ReferenceEquals(stored.Flags, key.Flags)) return new(Touch(entry), null, 0);
            if (stored.Protocol || key.Protocol) return new(null, stored.Flags, entry.Sequence);
            if (PyValueComparer.Instance.Equals(stored.Flags, key.Flags)) return new(Touch(entry), null, 0);
        }
        return default;
    }

    private async ValueTask<LythonRuntime.RePatternObject?> CompareFlagsAsync(Key key, Probe probe,
        CancellationToken token, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        while (probe.CompareFlags is { } flags)
        {
            // Only execution-owned flag values cross the callback. A purge may
            // have already released the old bucket when the comparison resumes.
            var equal = asynchronous
                ? await LythonRuntime.MembershipEqualsAsync(flags, key.Flags, context, span).ConfigureAwait(false)
                : LythonRuntime.MembershipEquals(flags, key.Flags, context, span);
            context.CheckExecution(span);
            probe = ResumeProbe(key, probe.Sequence, equal, token, span);
        }
        return probe.Match;
    }

    private Probe ResumeProbe(Key key, long sequence, bool equal, CancellationToken token, LythonSourceSpan span)
    {
        var entry = FindEntry(key.Hash, sequence);
        // An unrelated insertion does not invalidate the compared key.
        // Eviction or purge requires probing the live bucket from its head.
        return entry is not null && equal
            ? new(Touch(entry), null, 0)
            : ProbeBucket(key, entry?.Sequence ?? 0, token, span);
    }

    private Entry? FindEntry(int hash, long sequence)
    {
        _buckets.TryGetValue(hash, out var entry);
        while (entry is not null && entry.Sequence < sequence) entry = entry.Collision;
        return entry is not null && entry.Sequence == sequence ? entry : null;
    }

    private LythonRuntime.RePatternObject Touch(Entry entry)
    {
        if (entry != _newest)
        {
            UnlinkRecency(entry);
            AppendRecent(entry);
        }
        return entry.Pattern;
    }

    internal async ValueTask<LythonRuntime.RePatternObject> GetOrAdd(Key key, LythonRuntime.RePatternObject pattern,
        MemoryGovernor governor, LythonSourceSpan span, CancellationToken token = default,
        LythonRuntime.ExecutionContext? context = null, bool asynchronous = false)
    {
        governor.ThrowIfAdmissionFailed();
        var existing = await Find(key, token, span, context, asynchronous).ConfigureAwait(false);
        if (existing is not null) return existing;

        // A filled cache replaces an already-funded slot. An admission
        // callback may publish or purge entries before the reservation returns.
        var bytes = Count < MaxEntries ? SlotBytesPerEntry : 0;
        var version = _version;
        governor.Reserve(bytes, span);
        try
        {
            governor.ThrowIfAdmissionFailed();
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            if (_version != version)
            {
                existing = await Find(key, token, span, context, asynchronous).ConfigureAwait(false);
                if (existing is not null) return existing;
            }
            Insert(key, pattern);
            governor.Commit(bytes);
            _committedSlotBytes += bytes;
            bytes = 0;
            return pattern;
        }
        finally { if (bytes != 0) governor.ReleaseReserved(bytes); }
    }

    private void Insert(Key key, LythonRuntime.RePatternObject pattern)
    {
        Entry entry;
        if (Count == MaxEntries)
        {
            entry = _oldest.RequireNotNull();
            RemoveFromBucket(entry);
            UnlinkRecency(entry);
            entry.Key = key;
            entry.Pattern = pattern;
            entry.Collision = null;
        }
        else
        {
            entry = new(key, pattern);
            Count++;
        }
        // Preserve insertion order within a bucket independently of recency.
        entry.Sequence = ++_version;
        if (!_buckets.TryGetValue(key.Hash, out var tail)) _buckets.Add(key.Hash, entry);
        else
        {
            while (tail.Collision is { } next) tail = next;
            tail.Collision = entry;
        }
        AppendRecent(entry);
    }

    private void RemoveFromBucket(Entry entry)
    {
        var hash = entry.Key.Hash;
        var head = _buckets[hash];
        if (head == entry)
        {
            if (entry.Collision is { } next) _buckets[hash] = next;
            else _buckets.Remove(hash);
            return;
        }
        var previous = head;
        while (previous.Collision != entry) previous = previous.Collision.RequireNotNull();
        previous.Collision = entry.Collision;
    }

    private void UnlinkRecency(Entry entry)
    {
        if (entry.Older is { } older) older.Newer = entry.Newer;
        else _oldest = entry.Newer;
        if (entry.Newer is { } newer) newer.Older = entry.Older;
        else _newest = entry.Older;
    }

    private void AppendRecent(Entry entry)
    {
        entry.Older = _newest;
        entry.Newer = null;
        if (_newest is { } recent) recent.Newer = entry;
        else _oldest = entry;
        _newest = entry;
    }

    internal void Clear(MemoryGovernor? governor)
    {
        _buckets = new();
        _oldest = _newest = null;
        Count = 0;
        _version++;
        // Retire the previous balance before the refund callback reenters.
        var bytes = _committedSlotBytes;
        _committedSlotBytes = 0;
        if (bytes > 0) governor?.Release(bytes);
    }
}
