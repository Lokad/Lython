namespace Lokad.Lython.Runtime;

/// <summary>Provides stable sorting without relying on CLR comparer callbacks for Python effects.</summary>
internal static class PyStableSort
{
    private const long BaseTemporaryBytes = 128;
    private const long TemporaryBytesPerEntry = 56;

    internal readonly record struct Entry(object Value, object Key);

    internal sealed class Buffer : IReadOnlyCollection<object>, IDisposable
    {
        private readonly List<Entry> _entries;
        private readonly MemoryGovernor.TemporaryMemoryReservation _reservation;

        public Buffer(MemoryGovernor governor, LythonSourceSpan? span)
        {
            _reservation = governor.ReserveTemporary(BaseTemporaryBytes, span);
            _entries = [];
        }

        public int Count => _entries.Count;

        public void Add(Entry entry, LythonSourceSpan? span)
        {
            // Reserve enough for List<T>'s geometric over-allocation as well as
            // the stable merge scratch array before retaining another entry.
            _reservation.Grow(TemporaryBytesPerEntry, span);
            _entries.Add(entry);
        }

        public void Sort(bool reverse, Func<object, object, bool> isLessThan)
        {
            if (_entries.Count < 2)
            {
                return;
            }

            var scratch = new Entry[_entries.Count];
            MergePasses(_entries, scratch, reverse, isLessThan);
        }

        public async ValueTask SortAsync(bool reverse, Func<object, object, ValueTask<bool>> isLessThan)
        {
            if (_entries.Count < 2)
            {
                return;
            }

            var scratch = new Entry[_entries.Count];
            await MergePassesAsync(_entries, scratch, reverse, isLessThan).ConfigureAwait(false);
        }

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var entry in _entries)
            {
                yield return entry.Value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose() => _reservation.Dispose();
    }

    private static void MergePasses(
        List<Entry> entries,
        Entry[] scratch,
        bool reverse,
        Func<object, object, bool> isLessThan)
    {
        var sourceIsEntries = true;
        for (long width = 1; width < entries.Count; width *= 2)
        {
            IReadOnlyList<Entry> source = sourceIsEntries ? entries : scratch;
            IList<Entry> destination = sourceIsEntries ? scratch : entries;
            for (long offset = 0; offset < entries.Count; offset += 2 * width)
            {
                Merge(
                    source,
                    destination,
                    (int)offset,
                    (int)Math.Min(offset + width, entries.Count),
                    (int)Math.Min(offset + (2 * width), entries.Count),
                    reverse,
                    isLessThan);
            }

            sourceIsEntries = !sourceIsEntries;
        }

        if (!sourceIsEntries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i] = scratch[i];
            }
        }
    }

    private static async ValueTask MergePassesAsync(
        List<Entry> entries,
        Entry[] scratch,
        bool reverse,
        Func<object, object, ValueTask<bool>> isLessThan)
    {
        var sourceIsEntries = true;
        for (long width = 1; width < entries.Count; width *= 2)
        {
            IReadOnlyList<Entry> source = sourceIsEntries ? entries : scratch;
            IList<Entry> destination = sourceIsEntries ? scratch : entries;
            for (long offset = 0; offset < entries.Count; offset += 2 * width)
            {
                await MergeAsync(
                        source,
                        destination,
                        (int)offset,
                        (int)Math.Min(offset + width, entries.Count),
                        (int)Math.Min(offset + (2 * width), entries.Count),
                        reverse,
                        isLessThan)
                    .ConfigureAwait(false);
            }

            sourceIsEntries = !sourceIsEntries;
        }

        if (!sourceIsEntries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                entries[i] = scratch[i];
            }
        }
    }

    private static void Merge(
        IReadOnlyList<Entry> source,
        IList<Entry> destination,
        int start,
        int middle,
        int end,
        bool reverse,
        Func<object, object, bool> isLessThan)
    {
        var left = start;
        var right = middle;
        for (var output = start; output < end; output++)
        {
            if (left >= middle)
            {
                destination[output] = source[right++];
            }
            else if (right >= end || !ComesBefore(source[right].Key, source[left].Key, reverse, isLessThan))
            {
                // Taking the left item when keys compare equal is what makes both
                // normal and reverse sorting stable.
                destination[output] = source[left++];
            }
            else
            {
                destination[output] = source[right++];
            }
        }
    }

    private static async ValueTask MergeAsync(
        IReadOnlyList<Entry> source,
        IList<Entry> destination,
        int start,
        int middle,
        int end,
        bool reverse,
        Func<object, object, ValueTask<bool>> isLessThan)
    {
        var left = start;
        var right = middle;
        for (var output = start; output < end; output++)
        {
            if (left >= middle)
            {
                destination[output] = source[right++];
            }
            else if (right >= end ||
                     !await ComesBeforeAsync(source[right].Key, source[left].Key, reverse, isLessThan).ConfigureAwait(false))
            {
                destination[output] = source[left++];
            }
            else
            {
                destination[output] = source[right++];
            }
        }
    }

    private static bool ComesBefore(
        object candidate,
        object current,
        bool reverse,
        Func<object, object, bool> isLessThan)
        => reverse
            ? isLessThan(current, candidate)
            : isLessThan(candidate, current);

    private static ValueTask<bool> ComesBeforeAsync(
        object candidate,
        object current,
        bool reverse,
        Func<object, object, ValueTask<bool>> isLessThan)
        => reverse
            ? isLessThan(current, candidate)
            : isLessThan(candidate, current);
}
