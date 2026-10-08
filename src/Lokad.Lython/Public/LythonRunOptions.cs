using System.Threading;

namespace Lokad.Lython;

/// <summary>Configures one isolated Lython execution and its resource limits.</summary>
public sealed class LythonRunOptions
{
    /// <summary>The historical instruction allowance, retained for source compatibility.</summary>
    /// <remarks>
    /// Ordinary execution has no implicit instruction allowance. This value
    /// applies only when explicitly assigned to <see cref="MaxExecutionSteps"/>.
    /// </remarks>
    public const int DefaultMaxExecutionSteps = 50_000_000;

    /// <summary>The default Python call-depth limit.</summary>
    public const int DefaultMaxRecursionDepth = 1_000;

    /// <summary>The default number of calls into host capabilities.</summary>
    public const int DefaultMaxHostCalls = 100_000;

    /// <summary>The default maximum number of items in one runtime collection.</summary>
    public const int DefaultMaxCollectionSize = 10_000_000;

    /// <summary>The default maximum Python string length.</summary>
    public const int DefaultMaxStringLength = 256 * 1024 * 1024;

    /// <summary>The default maximum size of one host read.</summary>
    public const int DefaultMaxHostReadBytes = 256 * 1024 * 1024;

    /// <summary>The default captured standard-output limit.</summary>
    public const int DefaultMaxStandardOutputBytes = 16 * 1024 * 1024;

    /// <summary>The default captured standard-error limit.</summary>
    public const int DefaultMaxStandardErrorBytes = 16 * 1024 * 1024;

    /// <summary>The default approximate runtime memory budget.</summary>
    public const long DefaultMaxExecutionMemoryBytes = 1024L * 1024L * 1024L;

    /// <summary>The default memory budget for projecting a return value to CLR objects.</summary>
    public const long DefaultMaxProjectionMemoryBytes = 1024L * 1024L * 1024L;

    /// <summary>
    /// Gets initial global values exposed to the script. Values are copied into
    /// Lython-owned scalars and collections; unsupported CLR objects and cyclic
    /// object graphs are rejected before execution.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Globals { get; init; }

    /// <summary>
    /// Gets the complete list exposed through <c>sys.argv</c>, including
    /// <c>argv[0]</c> when supplied by the embedder. No source path is prepended;
    /// null or an empty list exposes an empty <c>sys.argv</c>.
    /// </summary>
    public IReadOnlyList<string>? Args { get; init; }

    /// <summary>Gets the environment exposed through the mediated <c>os.environ</c> surface.</summary>
    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    /// <summary>Gets the logical source path used in diagnostics, imports, and stack frames.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Gets the host-controlled token that can cancel execution.</summary>
    /// <remarks>Supply a cancellable token when the run must have a finite deadline.</remarks>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>Gets whether unspecified resource limits are unbounded.</summary>
    public bool DisableDefaultLimits { get; init; }

    /// <summary>Gets whether all local-module imports are disabled.</summary>
    public bool DisableLocalModuleImports { get; init; }

    /// <summary>Gets the allowlist of host-local module names or paths.</summary>
    public IReadOnlySet<string>? AllowedLocalModules { get; init; }

    /// <summary>Gets an optional instruction allowance for callers requiring deterministic fuel.</summary>
    /// <remarks>
    /// Null leaves interpreter work unbounded regardless of <see cref="DisableDefaultLimits"/>.
    /// Cancellation and other configured resource limits still apply.
    /// </remarks>
    public LythonCountLimit? MaxExecutionSteps { get; init; }

    /// <summary>Gets the Python call-depth override.</summary>
    public LythonCountLimit? MaxRecursionDepth { get; init; }

    /// <summary>Gets the host-call budget override.</summary>
    public LythonCountLimit? MaxHostCalls { get; init; }

    /// <summary>Gets the per-collection item limit override.</summary>
    public LythonCountLimit? MaxCollectionSize { get; init; }

    /// <summary>Gets the Python string-length limit override.</summary>
    public LythonCountLimit? MaxStringLength { get; init; }

    /// <summary>Gets the per-operation host-read byte limit override.</summary>
    public LythonByteLimit? MaxHostReadBytes { get; init; }

    /// <summary>Gets the captured standard-output byte limit override.</summary>
    public LythonByteLimit? MaxStandardOutputBytes { get; init; }

    /// <summary>Gets the captured standard-error byte limit override.</summary>
    public LythonByteLimit? MaxStandardErrorBytes { get; init; }

    /// <summary>Gets the approximate runtime-memory limit override.</summary>
    public LythonByteLimit? MaxExecutionMemoryBytes { get; init; }

    /// <summary>Gets the CLR projection-memory limit override.</summary>
    public LythonByteLimit? MaxProjectionMemoryBytes { get; init; }

    /// <summary>
    /// Copies every option while replacing the cancellation token. This is the
    /// single copy site for cancellation merging (R32): add future options here
    /// rather than duplicating the list at call sites.
    /// </summary>
    internal LythonRunOptions WithCancellation(CancellationToken cancellationToken)
        => new()
        {
            Globals = Globals,
            Args = Args,
            Environment = Environment,
            SourcePath = SourcePath,
            CancellationToken = cancellationToken,
            DisableDefaultLimits = DisableDefaultLimits,
            DisableLocalModuleImports = DisableLocalModuleImports,
            AllowedLocalModules = AllowedLocalModules,
            MaxExecutionSteps = MaxExecutionSteps,
            MaxRecursionDepth = MaxRecursionDepth,
            MaxHostCalls = MaxHostCalls,
            MaxCollectionSize = MaxCollectionSize,
            MaxStringLength = MaxStringLength,
            MaxHostReadBytes = MaxHostReadBytes,
            MaxStandardOutputBytes = MaxStandardOutputBytes,
            MaxStandardErrorBytes = MaxStandardErrorBytes,
            MaxExecutionMemoryBytes = MaxExecutionMemoryBytes,
            MaxProjectionMemoryBytes = MaxProjectionMemoryBytes,
        };
}
