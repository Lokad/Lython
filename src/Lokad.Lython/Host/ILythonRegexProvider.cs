namespace Lokad.Lython;

/// <summary>Supplies Python string-pattern semantics to a Lython engine.</summary>
/// <remarks>
/// The provider is borrowed and must outlive its compiled scripts and executions.
/// Patterns and metadata must be immutable and safe for concurrent callers.
/// Successful compilation, preparation and cursor creation return non-null handles.
/// Every operation must honor its current token while working, finish its users
/// before returning or throwing, and admit substantial storage before allocation.
/// Owners are borrowed: do not dispose them or introduce an independent allowance.
/// Retained patterns use the supplied compilation/cache owner; working storage may
/// use child scopes. Refused admission must throw <see cref="LythonRegexException"/>
/// with <see cref="LythonRegexFailure.MemoryAdmission"/>. No retry is permitted.
/// </remarks>
public interface ILythonRegexProvider
{
    /// <summary>Validates UTF-8 pattern bytes and transfers a fresh owned Python pattern handle.</summary>
    /// <remarks>
    /// Lython supplies valid UTF-8, unchanged throughout this call. Direct callers
    /// must uphold the same input contract. Providers must copy and fund any
    /// pattern bytes retained after the call; the input itself is only borrowed.
    /// The caller disposes the handle after its operations, prepared subjects,
    /// cursors and metadata borrows end. A provider cache may retain underlying
    /// programs under a separate surviving owner; closing a handle must not refund
    /// storage still retained there. Failed compilation finishes its own cleanup.
    /// </remarks>
    ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
        ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken);
}

/// <summary>Contains Python flag bits for pattern metadata, parsing and matching.</summary>
/// <remarks>
/// Lython performs Python's flag-operator protocol, including implicit Unicode.
/// Verbose is separate because a guest flag object's earlier bitwise result can
/// differ from its final integer bits. Providers preserve <see cref="Flags"/>
/// in reported flags, applying global inline flags as Python does.
/// Parse the pattern before reporting string-pattern mode conflicts with
/// <see cref="LythonRegexFailure.Flags"/>.
/// </remarks>
public readonly record struct LythonRegexOptions(int Flags, bool Verbose)
{
    /// <summary>Gets the optional separate Python flag bits used to compile matching behavior.</summary>
    /// <remarks>
    /// A guest flag object can return different integers for the two flag unions.
    /// These bits select the root program's case, line, dot and character modes
    /// independently of the reported <see cref="Flags"/>. When absent, compile
    /// using the ordinary flag and inline-mode rules.
    /// </remarks>
    public int? ProgramFlags { get; init; }

    /// <summary>Identifies the Unicode bit that Python adds when no mode was requested.</summary>
    /// <remarks>
    /// Global inline ASCII can replace implicit Unicode. Explicit Unicode and
    /// global inline ASCII conflict. This distinction affects validation only.
    /// </remarks>
    public bool ImplicitUnicode { get; init; }
}

/// <summary>Owns one immutable compiled Python pattern handle, without a saved caller token.</summary>
/// <remarks>
/// Drain operations, subjects, cursors and borrowed metadata before disposal.
/// Dispose is idempotent, nonthrowing and synchronous: it retires this handle
/// and refunds only storage relinquished by its owner. It starts no background
/// cleanup and does not dispose the borrowed parent allowance. New uses of a
/// disposed handle fail explicitly. Metadata is borrowed for the handle lifetime.
/// </remarks>
public interface ILythonRegexPattern : IDisposable
{
    /// <summary>Gets effective Python flags, including leading inline flags and implicit Unicode.</summary>
    int Flags { get; }

    /// <summary>Gets the capture-slot count, including the whole match at slot zero.</summary>
    int CaptureSlotCount { get; }

    /// <summary>Gets immutable UTF-8 Python group names in ascending capture-slot order.</summary>
    /// <remarks>
    /// Names are unique valid UTF-8 identifiers, borrowed for this handle's lifetime.
    /// The list has at most <see cref="CaptureSlotCount"/> minus one entry.
    /// </remarks>
    IReadOnlyList<LythonRegexNamedGroup> NamedGroups { get; }

    /// <summary>Gets an optional immutable key for Python compiled-program equality.</summary>
    /// <remarks>
    /// Lython first compares original pattern text and effective flags. Among
    /// handles compiled by this provider with those facts equal, keys must compare
    /// equal exactly when Python's compiled programs compare equal. Supply a
    /// complete key, independent of matching accelerators, allocation layout
    /// and compilation cache state. A digest alone cannot prove equality.
    /// The key is prepared during compilation under its token and funded by
    /// the pattern owner. Reading it must be constant time. The bytes are
    /// borrowed only while the pattern handle is alive. Equal inputs and
    /// options must compile to equal keys. Providers that omit this metadata
    /// allow Lython to prove identity for identical inputs and options.
    /// </remarks>
    ReadOnlyMemory<byte>? ProgramEqualityKey => null;

    /// <summary>Matches once against the original UTF-8 subject and writes caller-owned capture slots.</summary>
    /// <remarks>
    /// The caller supplies valid UTF-8 and keeps it unchanged and funded through
    /// this call. The provider must finish all work and release the input and
    /// capture buffer borrows before returning or throwing.
    /// Bounds are independently clamped to the subject's scalar length. Preserve
    /// the original prefix for anchors/lookbehind and limit forward work at EndPos.
    /// Pos may exceed EndPos: Python match can still succeed without consuming text.
    /// Byte bounds identify the same scalar boundaries without another conversion.
    /// Captures use absolute original-subject byte coordinates, including lookbehind
    /// captures before Pos. A capture cannot end beyond EndPos, except that a
    /// reversed anchored match may include an empty capture at Pos.
    /// The buffer length equals CaptureSlotCount. On success slot zero
    /// participates, unmatched groups are null and lastIndex identifies the
    /// last-closed participating positive group, or null if none participated.
    /// Buffer contents are unspecified on false or failure.
    /// </remarks>
    bool TryMatch(ReadOnlyMemory<byte> subject, LythonRegexWindow window, LythonRegexMatchMode mode,
        Span<LythonRegexCapture?> captures, out int? lastIndex, ILythonMemoryBudget memoryOwner,
        CancellationToken cancellationToken);

    /// <summary>Prepares an original UTF-8 subject for cursor iteration.</summary>
    /// <remarks>
    /// The subject and window follow the same rules as <see cref="TryMatch"/>.
    /// The caller keeps the input valid, unchanged and funded until the subject
    /// and all its cursors are disposed. The provider releases the borrow at
    /// subject disposal.
    /// </remarks>
    ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, LythonRegexWindow window, ILythonMemoryBudget memoryOwner,
        CancellationToken cancellationToken);
}

/// <summary>A borrowed UTF-8 Python group name and its positive numeric capture slot.</summary>
public readonly record struct LythonRegexNamedGroup(ReadOnlyMemory<byte> Name, int Index);

/// <summary>Independently clamped scalar and UTF-8 byte bounds within the original subject.</summary>
/// <remarks>Pos/StartByte may exceed EndPos/EndByte. All byte bounds are scalar boundaries.</remarks>
public readonly record struct LythonRegexWindow(int Pos, int EndPos, int StartByte, int EndByte);

/// <summary>Owns one operation's preparation and working storage.</summary>
/// <remarks>
/// The caller owns and funds capture buffers. Providers must not retain them.
/// Dispose cursors before their subject. Disposal must be nonthrowing, finish cleanup synchronously and refund
/// only storage whose users have ended; no background work may be abandoned.
/// </remarks>
public interface ILythonRegexSubject : IDisposable
{
    /// <summary>Begins Python finditer progression, including adjacent empty/nonempty matches.</summary>
    ILythonRegexCursor Find(CancellationToken cancellationToken);
}

/// <summary>A disposable Python finditer cursor writing caller-owned capture storage.</summary>
/// <remarks>Disposal is nonthrowing and finishes all cursor users before returning.</remarks>
public interface ILythonRegexCursor : IDisposable
{
    /// <summary>Advances under the current token; false means exhaustion.</summary>
    /// <remarks>Buffer and output rules are the same as <see cref="ILythonRegexPattern.TryMatch"/>.</remarks>
    bool TryNext(Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken cancellationToken);
}

/// <summary>Selects one Python re matching operation.</summary>
public enum LythonRegexMatchMode { Search, Match, FullMatch }

/// <summary>A participating capture's absolute original-subject UTF-8 byte bounds.</summary>
/// <remarks>
/// Bounds are scalar boundaries. Empty captures have equal bounds; a null slot did
/// not participate. Lython projects Python scalar positions and retained text from
/// the original subject; providers need no copied values or dense coordinate map.
/// </remarks>
public readonly record struct LythonRegexCapture(int StartByte, int EndByte);

/// <summary>Identifies a provider failure without relying on backend exception types.</summary>
public enum LythonRegexFailure { Pattern, Capability, MemoryAdmission, Flags }

/// <summary>Reports a Python pattern or flag error, unavailable capability, or terminal admission refusal.</summary>
public sealed class LythonRegexException : Exception
{
    /// <summary>Creates a provider error. Pattern messages exclude position/line suffixes; Lython formats those from the scalar position. Requested bytes describe the first refused storage admission.</summary>
    public LythonRegexException(LythonRegexFailure failure, string message, int? position = null, long? requestedBytes = null)
        : base(message)
    {
        Failure = failure;
        Position = position;
        RequestedBytes = requestedBytes;
    }

    /// <summary>Gets the failure category.</summary>
    public LythonRegexFailure Failure { get; }
    /// <summary>Gets an optional Unicode-scalar pattern position.</summary>
    public int? Position { get; }
    /// <summary>Gets the first refused estimate, when known.</summary>
    public long? RequestedBytes { get; }
}
