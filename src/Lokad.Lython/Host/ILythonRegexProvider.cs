namespace Lokad.Lython;

/// <summary>Supplies Python string-pattern semantics to a Lython engine.</summary>
/// <remarks>
/// The provider is borrowed and must outlive its compiled scripts and executions.
/// Patterns and metadata must be immutable and safe for concurrent callers.
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
    /// The caller disposes the handle after its operations, prepared subjects,
    /// cursors and metadata borrows end. A provider cache may retain underlying
    /// programs under a separate surviving owner; closing a handle must not refund
    /// storage still retained there. Failed compilation finishes its own cleanup.
    /// </remarks>
    ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
        ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken);
}

/// <summary>Contains normalized Python flag bits and the parsing-time verbose setting.</summary>
/// <remarks>
/// Lython performs Python's flag-operator protocol, including implicit Unicode.
/// Verbose is separate because a guest flag object's earlier bitwise result can
/// differ from its final integer bits. Providers preserve the supplied bits in
/// reported flags, applying leading inline flags as Python does.
/// </remarks>
public readonly record struct LythonRegexOptions(int Flags, bool Verbose);

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

    /// <summary>Gets immutable Python group names and their numeric slots.</summary>
    IReadOnlyDictionary<string, int> NamedGroups { get; }

    /// <summary>Prepares an original UTF-8 subject with Python scalar pos/endpos bounds.</summary>
    /// <remarks>
    /// Bounds are independently clamped to the subject's scalar length. Preserve
    /// the original prefix for anchors/lookbehind and limit forward work at EndPos.
    /// Pos may exceed EndPos: Python match can still succeed without consuming text.
    /// Byte bounds identify the same scalar boundaries without another conversion.
    /// Captures use absolute original-subject byte coordinates, including lookbehind
    /// captures before Pos. The caller funds the subject through disposal.
    /// </remarks>
    ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, LythonRegexWindow window, ILythonMemoryBudget memoryOwner,
        CancellationToken cancellationToken);
}

/// <summary>Independently clamped scalar and UTF-8 byte bounds within the original subject.</summary>
/// <remarks>Pos/StartByte may exceed EndPos/EndByte. All byte bounds are scalar boundaries.</remarks>
public readonly record struct LythonRegexWindow(int Pos, int EndPos, int StartByte, int EndByte);

/// <summary>Owns one operation's preparation and working storage.</summary>
/// <remarks>
/// Results are borrowed until the next operation or disposal. Dispose cursors
/// before their subject. Disposal must be nonthrowing, finish cleanup synchronously and refund
/// only storage whose users have ended; no background work may be abandoned.
/// </remarks>
public interface ILythonRegexSubject : IDisposable
{
    /// <summary>Matches once using the requested Python operation; null means no match.</summary>
    LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken cancellationToken);

    /// <summary>Begins Python finditer progression, including adjacent empty/nonempty matches.</summary>
    ILythonRegexCursor Find(CancellationToken cancellationToken);

    /// <summary>Applies Python template rules within the window; returns only its rewritten text.</summary>
    /// <remarks>Validate templates even without matches; preserve original-subject assertion context.</remarks>
    LythonRegexSubstitution Substitute(ReadOnlyMemory<byte> replacement, int count,
        CancellationToken cancellationToken);

    /// <summary>Splits the window with captured separators; null entries denote unmatched captures.</summary>
    IReadOnlyList<ReadOnlyMemory<byte>?> Split(int maxSplit, CancellationToken cancellationToken);
}

/// <summary>A disposable Python finditer cursor with borrowed current-match storage.</summary>
/// <remarks>Disposal is nonthrowing and finishes all cursor users before returning.</remarks>
public interface ILythonRegexCursor : IDisposable
{
    /// <summary>Advances under the current token; null means exhaustion.</summary>
    LythonRegexMatch? Next(CancellationToken cancellationToken);
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

/// <summary>Contains numeric capture slots (zero included) and the last participating group.</summary>
/// <remarks>Null slots did not participate. Values and the slot list are borrowed.</remarks>
public sealed record LythonRegexMatch(IReadOnlyList<LythonRegexCapture?> Captures, int? LastIndex);

/// <summary>A borrowed UTF-8 substitution result and its replacement count.</summary>
public readonly record struct LythonRegexSubstitution(ReadOnlyMemory<byte> Value, int Count);

/// <summary>Identifies a provider failure without relying on backend exception types.</summary>
public enum LythonRegexFailure { Pattern, Capability, MemoryAdmission }

/// <summary>Reports a Python pattern error, unavailable provider capability, or terminal admission refusal.</summary>
public sealed class LythonRegexException : Exception
{
    /// <summary>Creates a provider error. Requested bytes describe the first refused storage admission.</summary>
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
