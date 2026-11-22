using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// Analysis populates this owner before publication. Executions only borrow its
// immutable programs/metadata; disposing the script requires all runs to finish.
internal sealed class CompiledRegexPrograms(MemoryGovernor? governor) : IDisposable
{
    internal readonly record struct Entry(ILythonRegexPattern Program, int CaptureSlotCount);

    private ILythonMemoryScope? _owner;
    private Dictionary<RegexPatternKey, Entry>? _entries;

    internal bool HasPrograms => _entries is { Count: > 0 };
    internal ILythonMemoryBudget StorageOwner => _owner ??=
        (governor?.Budget ?? new LythonMemoryBudget()).CreateScope();

    internal void Reserve(long bytes, LythonSourceSpan span)
    {
        if (!StorageOwner.TryReserve(bytes, out _))
            throw governor?.RefuseAdmission("shared memory budget refused regex analysis storage", bytes, span)
                ?? RuntimeErrors.Attributed("MemoryError", "shared memory budget refused regex analysis storage", span,
                    LythonFailureReason.SharedMemoryAllowance, requested: bytes);
    }

    internal void Add(byte[] encoded, LythonRegexOptions options, ILythonRegexPattern program, int captureSlotCount,
        CancellationToken token, LythonSourceSpan span)
    {
        // The analysis preflight owns the input, key, table capacity and overlap.
        // Borrow the same encoded input instead of decoding runtime lookup keys.
        var key = PyString.FromOwnedUtf8(encoded);
        _ = key.GetHashCode(token, span);
        // Once published, the lookup owns the handle. It must never replace
        // a program that a compiler fact may still borrow.
        (_entries ??= new(RegexPatternKey.Comparer.Instance)).Add(new(key, options), new Entry(program, captureSlotCount));
    }

    internal Entry? Find(PyString pattern, LythonRegexOptions options, CancellationToken token, LythonSourceSpan span)
    {
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        if (_entries is not { } entries) return null;
        var key = new RegexPatternKey(pattern, options);
        var found = token.CanBeCanceled
            ? entries.GetAlternateLookup<RegexPatternKey.Lookup>().TryGetValue(new(key, token, span), out var entry)
            : entries.TryGetValue(key, out entry);
        return found ? entry : null;
    }

    public void Dispose()
    {
        var entries = _entries;
        _entries = null;
        try
        {
            if (entries is not null)
                foreach (var entry in entries.Values) entry.Program.Dispose();
        }
        finally { _owner?.Dispose(); _owner = null; }
    }
}
