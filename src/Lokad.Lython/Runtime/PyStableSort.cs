namespace Lokad.Lython.Runtime;

/// <summary>Provides stable sorting without relying on CLR comparer callbacks for Python effects.</summary>
internal static class PyStableSort
{
    // Active scratch/comparer/context fields make the two-entry working shape
    // allocate 264 bytes on managed x64. Fund that shape before publication.
    private const long BaseTemporaryBytes = 160;
    private const long TemporaryBytesPerEntry = 56;

    internal readonly record struct Entry(object Value, object Key);

    internal sealed class Buffer : IReadOnlyCollection<object>, IDisposable
    {

        private readonly List<Entry> _entries;
        private readonly MemoryGovernor.TemporaryMemoryReservation _reservation;
        private Entry[]? _scratch;
        private Delegate? _comparison;
        private LythonRuntime.ExecutionContext? _sortContext;

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

        public void Sort(bool reverse, Func<object, object, bool> isLessThan,
            LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        {
            EnsureIdle();
            context.CheckExecution(span);
            if (_entries.Count < 2)
            {
                return;
            }

            try
            {
                _comparison = isLessThan;
                _sortContext = context;
                _scratch = new Entry[_entries.Count];
                MergePasses(_entries, _scratch, reverse, isLessThan, context, span);
                context.CheckExecution(span);
            }
            finally { _scratch = null; _comparison = null; _sortContext = null; }
        }

        public async ValueTask SortAsync(bool reverse, Func<object, object, ValueTask<bool>> isLessThan,
            LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        {
            EnsureIdle();
            context.CheckExecution(span);
            if (_entries.Count < 2)
            {
                return;
            }

            try
            {
                _comparison = isLessThan;
                _sortContext = context;
                _scratch = new Entry[_entries.Count];
                await MergePassesAsync(_entries, _scratch, reverse, isLessThan, context, span).ConfigureAwait(false);
                context.CheckExecution(span);
            }
            finally { _scratch = null; _comparison = null; _sortContext = null; }
        }

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var entry in _entries)
            {
                yield return entry.Value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            EnsureIdle();
            _entries.Clear();
            _entries.Capacity = 0;
            _reservation.Dispose();
        }

        private void EnsureIdle()
        {
            if (_comparison is not null) throw new InvalidOperationException("Sort buffer has an active operation.");
        }
    }

    private static void MergePasses(
        List<Entry> entries,
        Entry[] scratch,
        bool reverse,
        Func<object, object, bool> isLessThan,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
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
                    isLessThan, context, span);
            }

            sourceIsEntries = !sourceIsEntries;
        }

        if (!sourceIsEntries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if ((i & 63) == 0) context.CheckExecution(span);
                entries[i] = scratch[i];
            }
        }
    }

    private static async ValueTask MergePassesAsync(
        List<Entry> entries,
        Entry[] scratch,
        bool reverse,
        Func<object, object, ValueTask<bool>> isLessThan,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
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
                        isLessThan, context, span)
                    .ConfigureAwait(false);
            }

            sourceIsEntries = !sourceIsEntries;
        }

        if (!sourceIsEntries)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if ((i & 63) == 0) context.CheckExecution(span);
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
        Func<object, object, bool> isLessThan,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var left = start;
        var right = middle;
        for (var output = start; output < end; output++)
        {
            // Copy tails can run without further comparisons or guest calls.
            if ((output & 63) == 0) context.CheckExecution(span);
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
        Func<object, object, ValueTask<bool>> isLessThan,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var left = start;
        var right = middle;
        for (var output = start; output < end; output++)
        {
            if ((output & 63) == 0) context.CheckExecution(span);
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
