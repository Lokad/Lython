namespace Lokad.Lython;

/// <summary>Separates parsing and program construction for requests with guest flag operators.</summary>
/// <remarks>
/// Exact flag requests continue to use <see cref="ILythonRegexProvider.Compile"/>.
/// Work inline and await flag operations in parser order. A synchronous Lython run
/// supplies flag operations that complete synchronously. Finish all work and
/// callbacks before returning or throwing.
/// </remarks>
public interface ILythonRegexStagedProvider : ILythonRegexProvider
{
    /// <summary>Parses valid UTF-8 and transfers an owned parsed pattern.</summary>
    /// <remarks>
    /// Borrow the input and flag state for this call. Copy and fund any input
    /// retained by the parsed pattern. Keep substantial parse storage admitted
    /// until its owner ends. On failure, release parser-owned storage.
    /// </remarks>
    ValueTask<ILythonRegexParsedPattern> ParseAsync(ReadOnlyMemory<byte> pattern, ILythonRegexParserFlags flags,
        ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken);
}

/// <summary>Performs Python flag operations at the parser's actual decision points.</summary>
/// <remarks>Await calls serially. Release the reference when parsing ends.</remarks>
public interface ILythonRegexParserFlags
{
    /// <summary>Reads the verbose operand at entry, after a legal global directive or at a top-level alternative.</summary>
    ValueTask<LythonRegexVerboseFlag> ReadVerboseAsync();

    /// <summary>Applies the bits of a syntactically valid global directive before validating placement.</summary>
    ValueTask AddGlobalAsync(int flagBits);

    /// <summary>Validates and completes string-pattern mode flags.</summary>
    /// <remarks>
    /// Call once after parsing the expression and before validating the final
    /// delimiter and outstanding group references. A successful parse calls
    /// this method and then finishes without further flag operations.
    /// </remarks>
    ValueTask FinishAsync();
}

/// <summary>A verbose operand borrowed during staged parsing.</summary>
/// <remarks>
/// Hold this operand for its branch and query it wherever the grammar tests
/// verbose mode. Querying can run a guest truth operation. Release it when
/// parsing ends.
/// </remarks>
public readonly struct LythonRegexVerboseFlag
{
    private readonly bool _constant;
    private readonly Func<ValueTask<bool>>? _read;

    /// <summary>Creates a constant verbose operand.</summary>
    public LythonRegexVerboseFlag(bool value) => _constant = value;

    internal LythonRegexVerboseFlag(Func<ValueTask<bool>> read) => _read = read;

    /// <summary>Evaluates the operand's current Python truth value.</summary>
    public ValueTask<bool> IsEnabledAsync() => _read is { } read ? read() : new(_constant);
}

/// <summary>Owns temporary parsed regex storage through construction or failure.</summary>
/// <remarks>Dispose synchronously and idempotently without throwing.</remarks>
public interface ILythonRegexParsedPattern : IDisposable
{
    /// <summary>Compiles the parsed expression with the given root matching flags.</summary>
    /// <remarks>
    /// Keep the parsed operations and scoped flag deltas. The root flags have already
    /// gone through guest flag operators. The resulting program owns its storage
    /// under the supplied allowance and survives disposal of this parsed pattern.
    /// Lython resolves guest-visible flags after program construction succeeds.
    /// </remarks>
    ILythonRegexPattern Compile(int programFlags, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken);
}
