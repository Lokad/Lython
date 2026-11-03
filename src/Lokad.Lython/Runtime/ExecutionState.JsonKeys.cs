using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class ExecutionState
{
    // Names are immutable and already execution/result-owned. Sharing their
    // storage also shares the lazy Python hash; mutable records stay separate.
    private Dictionary<ReadOnlyMemory<byte>, PyString>? _jsonPropertyNames;

    internal PyString GetJsonPropertyName(ReadOnlyMemory<byte> bytes, LythonSourceSpan span, PyString? decoded = null)
    {
        if (_jsonPropertyNames is not null && _jsonPropertyNames.TryGetValue(bytes, out var existing))
        {
            // The decoder alone can supply an unpublished duplicate. Never
            // refund the stored key, its aliases, or borrowed input storage.
            if (decoded is not null && !ReferenceEquals(decoded, existing))
                MemoryGovernor.RefundUnpublishedValue(decoded);
            return existing;
        }

        EnsureJsonPropertyNameCapacity(span);
        var owned = decoded ?? PyString.CopyFromUtf8(bytes.Span, MemoryGovernor, span);
        // Only independent owned bytes enter the table. The original
        // document/window is used for lookup, never retained as a table key.
        _jsonPropertyNames!.Add(owned.Utf8Bytes, owned);
        return owned;
    }

    private void EnsureJsonPropertyNameCapacity(LythonSourceSpan span)
    {
        var capacity = _jsonPropertyNames?.EnsureCapacity(0) ?? 0;
        if (_jsonPropertyNames is not null && _jsonPropertyNames.Count < capacity) return;

        var requested = capacity == 0 ? 4 : checked(capacity * 2);
        // Dictionary rounds to a prime below twice the requested capacity on
        // the supported runtimes. Fund that conservative bound beside the old
        // table before growth; retain only major live capacity afterward.
        using var storage = MemoryGovernor.ReserveTemporary(checked(160L + 96L * requested), span);
        var names = _jsonPropertyNames ?? new(Utf8PropertyNameComparer.Instance);
        var actualCapacity = names.EnsureCapacity(requested);
        storage.Commit(checked(160L + 48L * actualCapacity));
        _jsonPropertyNames = names;
        if (capacity != 0) MemoryGovernor.Release(checked(160L + 48L * capacity));
    }

    private sealed class Utf8PropertyNameComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        internal static readonly Utf8PropertyNameComparer Instance = new();

        public bool Equals(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
            => left.Span.SequenceEqual(right.Span);

        public int GetHashCode(ReadOnlyMemory<byte> value)
        {
            var hash = new HashCode();
            hash.AddBytes(value.Span);
            return hash.ToHashCode();
        }
    }
}
