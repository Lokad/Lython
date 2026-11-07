using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Frontend;

// Analysis populates this owner before publication. Executions only borrow its
// immutable programs/metadata; disposing the script requires all runs to finish.
internal sealed class CompiledRegexPrograms(MemoryGovernor? governor) : IDisposable
{
    private ILythonMemoryScope? _owner;
    private List<ILythonRegexPattern>? _handles;
    private Dictionary<(PyString Pattern, LythonRegexOptions Options), ILythonRegexPattern>? _entries;

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

    internal void Own(ILythonRegexPattern program) => (_handles ??= new()).Add(program);

    internal void Add(byte[] encoded, LythonRegexOptions options, ILythonRegexPattern program,
        CancellationToken token, LythonSourceSpan span)
    {
        // The analysis preflight owns the input, key, table capacity and overlap.
        // Borrow the same encoded input instead of decoding runtime lookup keys.
        var key = PyString.FromOwnedUtf8(encoded);
        _ = key.GetHashCode(token, span);
        (_entries ??= new())[(key, options)] = program;
    }

    internal bool TryGet(PyString pattern, LythonRegexOptions options, out ILythonRegexPattern? entry)
    {
        if (_entries is not null) return _entries.TryGetValue((pattern, options), out entry);
        entry = null;
        return false;
    }

    public void Dispose()
    {
        var handles = _handles;
        _handles = null;
        _entries = null;
        try
        {
            if (handles is not null)
                foreach (var program in handles) program.Dispose();
        }
        finally { _owner?.Dispose(); _owner = null; }
    }
}
