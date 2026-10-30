namespace Lokad.Lython;

/// <summary>A guest path before host resolution, together with its contained working directory.</summary>
/// <remarks>Dot segments and a trailing slash are significant. The host resolves them,
/// checks intermediate entries and enforces containment; it must not use an ambient Cwd.</remarks>
public readonly record struct LythonPathRequest(string Path, string Cwd);

/// <summary>The access requested from an opened byte file.</summary>
public enum LythonFileAccess { Read, Write, ReadWrite }

/// <summary>Independent access and atomic creation/publication intent.</summary>
public readonly record struct LythonFileOpenOptions
{
    public LythonFileOpenOptions(LythonFileAccess access, bool create = false,
        bool truncate = false, bool append = false, bool exclusive = false)
    {
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
        if (access == LythonFileAccess.Read && (create || truncate || append || exclusive))
            throw new ArgumentException("Read-only access cannot create or modify a file.");
        if (truncate && append) throw new ArgumentException("Truncate and append are mutually exclusive.");
        if (exclusive && !create) throw new ArgumentException("Exclusive creation requires create.");
        Access = access; Create = create; Truncate = truncate; Append = append; Exclusive = exclusive;
    }

    public LythonFileAccess Access { get; }
    public bool Create { get; }
    public bool Truncate { get; }
    public bool Append { get; }
    public bool Exclusive { get; }
}

/// <summary>The accepted prefix of one byte-file write and its optional failure.</summary>
/// <remarks>A successful write reports a positive count for nonempty input. On failure,
/// zero guarantees no effects; null means the accepted prefix is unknown. Never return
/// success with an unknown count. Throwing instead also makes acceptance unknown.
/// Admission refusal must precede effects. The caller never replays a failed partial
/// or indeterminate write.</remarks>
public readonly record struct LythonFileWriteResult(int? AcceptedBytes, LythonHostException? Failure = null);

/// <summary>One owned opened-file identity and independent cursor.</summary>
/// <remarks>
/// Rename/replacement of its path must not redirect subsequent operations. The host
/// defines visibility of in-place changes. Read/write buffers are borrowed until the
/// actual operation completes, including cancellation/failure. Zero read means EOF;
/// a short read does not. File storage and acquisition scratch require appropriate
/// surviving owners; the open allowance is borrowed, not transferred.
/// DisposeAsync has no cancellation token: await outstanding users, retire even on
/// failure, and release private storage only after cleanup. Repeated disposal is inert.
/// Hosts supporting synchronous runs also implement ILythonSynchronousHostCapability
/// on the file. No seeking is implied by this contract.
/// </remarks>
public interface ILythonByteFile : IAsyncDisposable
{
    /// <summary>Gets the byte cursor position, including append's initial end position.</summary>
    long Position { get; }
    /// <summary>Gets an optional size hint for the opened identity; never an allocation authority.</summary>
    long? LengthHint { get; }
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
    ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    /// <summary>Resets this writable identity to position zero and empty content.</summary>
    /// <remarks>Optional whole-archive rewrite capability, not guest seek support.
    /// The caller admits output and replacement overlap first. Refusal precedes
    /// mutation; failures may leave prior content changed and never authorize replay.
    /// Apply the active token and retain the opened identity across path changes.</remarks>
    ValueTask RewindAndTruncateAsync(CancellationToken cancellationToken)
        => throw new LythonHostCapabilityUnavailableException("owned file rewriting");
    ValueTask FlushAsync(CancellationToken cancellationToken);
}
