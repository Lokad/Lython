using System.Threading;

namespace Lokad.Lython;

public sealed class LythonRunOptions
{
    public const int DefaultMaxExecutionSteps = 50_000_000;

    public const int DefaultMaxRecursionDepth = 1_000;

    public const int DefaultMaxHostCalls = 100_000;

    public const int DefaultMaxCollectionSize = 10_000_000;

    public const int DefaultMaxStringLength = 256 * 1024 * 1024;

    public const int DefaultMaxHostReadBytes = 256 * 1024 * 1024;

    public const int DefaultMaxStandardOutputBytes = 16 * 1024 * 1024;

    public const int DefaultMaxStandardErrorBytes = 16 * 1024 * 1024;

    public const long DefaultMaxExecutionMemoryBytes = 1024L * 1024L * 1024L;

    public const long DefaultMaxProjectionMemoryBytes = 1024L * 1024L * 1024L;

    public IReadOnlyDictionary<string, object?>? Globals { get; init; }

    public IReadOnlyList<string>? Args { get; init; }

    public IReadOnlyDictionary<string, string>? Environment { get; init; }

    public string? SourcePath { get; init; }

    public CancellationToken CancellationToken { get; init; }

    public bool DisableDefaultLimits { get; init; }

    public bool DisableLocalModuleImports { get; init; }

    public IReadOnlySet<string>? AllowedLocalModules { get; init; }

    public LythonCountLimit? MaxExecutionSteps { get; init; }

    public LythonCountLimit? MaxRecursionDepth { get; init; }

    public LythonCountLimit? MaxHostCalls { get; init; }

    public LythonCountLimit? MaxCollectionSize { get; init; }

    public LythonCountLimit? MaxStringLength { get; init; }

    public LythonByteLimit? MaxHostReadBytes { get; init; }

    public LythonByteLimit? MaxStandardOutputBytes { get; init; }

    public LythonByteLimit? MaxStandardErrorBytes { get; init; }

    public LythonByteLimit? MaxExecutionMemoryBytes { get; init; }

    public LythonByteLimit? MaxProjectionMemoryBytes { get; init; }
}
