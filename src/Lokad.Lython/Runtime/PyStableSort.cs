namespace Lokad.Lython.Runtime;

/// <summary>Provides stable sorting without relying on CLR comparer callbacks for Python effects.</summary>
internal static class PyStableSort
{
    internal readonly record struct Entry(object Value, object Key);

    public static void Sort(
        Entry[] entries,
        bool reverse,
        Func<object, object, bool> isLessThan)
    {
        if (entries.Length < 2)
        {
            return;
        }

        var scratch = new Entry[entries.Length];
        var source = entries;
        var destination = scratch;
        for (long width = 1; width < entries.Length; width *= 2)
        {
            for (long offset = 0; offset < entries.Length; offset += 2 * width)
            {
                Merge(
                    source,
                    destination,
                    (int)offset,
                    (int)Math.Min(offset + width, entries.Length),
                    (int)Math.Min(offset + (2 * width), entries.Length),
                    reverse,
                    isLessThan);
            }

            (source, destination) = (destination, source);
        }

        if (!ReferenceEquals(source, entries))
        {
            Array.Copy(source, entries, entries.Length);
        }
    }

    public static async ValueTask SortAsync(
        Entry[] entries,
        bool reverse,
        Func<object, object, ValueTask<bool>> isLessThan)
    {
        if (entries.Length < 2)
        {
            return;
        }

        var scratch = new Entry[entries.Length];
        var source = entries;
        var destination = scratch;
        for (long width = 1; width < entries.Length; width *= 2)
        {
            for (long offset = 0; offset < entries.Length; offset += 2 * width)
            {
                await MergeAsync(
                        source,
                        destination,
                        (int)offset,
                        (int)Math.Min(offset + width, entries.Length),
                        (int)Math.Min(offset + (2 * width), entries.Length),
                        reverse,
                        isLessThan)
                    .ConfigureAwait(false);
            }

            (source, destination) = (destination, source);
        }

        if (!ReferenceEquals(source, entries))
        {
            Array.Copy(source, entries, entries.Length);
        }
    }

    public static long EstimateTemporaryBytes(int count)
        => 128L + (56L * count);

    private static void Merge(
        Entry[] source,
        Entry[] destination,
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
        Entry[] source,
        Entry[] destination,
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
