namespace Lokad.Lython;

/// <summary>Owns a completed subprocess's captured byte storage.</summary>
/// <remarks>
/// The runner transfers one fresh result to its caller. Admit retained capacity
/// and acquisition/replacement overlap through the supplied owner before allocating
/// output, and transfer that owner here. Output memories are borrowed until Dispose;
/// finish all readers before disposal. Copies retained beyond that borrow require
/// their own surviving funding. Disposal closes storage access and refunds the
/// owner synchronously; the supplied scope must support nonthrowing cleanup.
/// </remarks>
public sealed class LythonSubprocessResult : IDisposable
{
    private ILythonMemoryScope? _owner;
    private ReadOnlyMemory<byte> _stdout;
    private ReadOnlyMemory<byte> _stderr;

    // Execution-owned buffered Popen results use the existing resource cleanup
    // boundary. An intrusive link needs no separate capacity ledger or registry.
    internal LythonSubprocessResult? NextResource;

    /// <summary>Transfers an already-funded output owner and its captured buffers.</summary>
    public LythonSubprocessResult(int returnCode, ReadOnlyMemory<byte> standardOutputUtf8,
        ReadOnlyMemory<byte> standardErrorUtf8, ILythonMemoryScope owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ReturnCode = returnCode;
        _stdout = standardOutputUtf8;
        _stderr = standardErrorUtf8;
        _owner = owner;
    }

    /// <summary>Gets the exit code of the process that ran.</summary>
    public int ReturnCode { get; }

    /// <summary>Gets borrowed captured stdout bytes, valid until disposal.</summary>
    public ReadOnlyMemory<byte> StandardOutputUtf8
    {
        get { ObjectDisposedException.ThrowIf(_owner is null, this); return _stdout; }
    }

    /// <summary>Gets borrowed captured stderr bytes, valid until disposal.</summary>
    public ReadOnlyMemory<byte> StandardErrorUtf8
    {
        get { ObjectDisposedException.ThrowIf(_owner is null, this); return _stderr; }
    }

    /// <summary>Retires the result and returns its allowance after all readers have ended.</summary>
    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        _stdout = default;
        _stderr = default;
        owner?.Dispose();
    }
}
