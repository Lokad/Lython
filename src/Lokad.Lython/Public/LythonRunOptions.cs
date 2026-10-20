using System.Threading;

namespace Lokad.Lython;

/// <summary>Configures isolated execution, borrowed memory ownership and cancellation.</summary>
public sealed class LythonRunOptions
{
    /// <summary>The default Python call-depth limit.</summary>
    public const int DefaultMaxRecursionDepth = 1_000;

    /// <summary>
    /// Gets initial global values exposed to the script. Values are copied into
    /// Lython-owned scalars and collections; unsupported CLR objects and cyclic
    /// object graphs are rejected before execution.
    /// </summary>
    /// <remarks>
    /// CLR integral values from sbyte through ulong have exact Python integer
    /// semantics, including inside supported collections. An unchanged scalar
    /// retains its CLR type when projected back to the host; integer arithmetic
    /// produces System.Numerics.BigInteger values. CLR float and decimal values
    /// can round-trip but do not participate in Python numeric operations; use
    /// double for Python floats and construct decimal.Decimal in the script.
    /// </remarks>
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

    /// <summary>Gets whether all local-module imports are disabled.</summary>
    public bool DisableLocalModuleImports { get; init; }

    /// <summary>Gets the allowlist of host-local module names or paths.</summary>
    public IReadOnlySet<string>? AllowedLocalModules { get; init; }

    /// <summary>Gets the built-in modules available to this invocation.</summary>
    /// <remarks>
    /// Null preserves the supported inventory; an empty set denies all built-in
    /// module imports. Names are ordinal and case-sensitive, must be supported,
    /// and must include every registered dotted ancestor. Invalid sets raise
    /// ArgumentException before guest execution. Admission takes an immutable
    /// snapshot. Grants do not supply host capabilities or affect intrinsics,
    /// future directives, or the independent local-module controls.
    /// </remarks>
    public IReadOnlySet<string>? AllowedBuiltinModules { get; init; }

    /// <summary>Gets the Python call-depth override.</summary>
    public int? MaxRecursionDepth { get; init; }

    /// <summary>Gets a borrowed memory budget shared with other executions and host-mediated child work.</summary>
    /// <remarks>
    /// Null uses an unbounded standalone root. The supplied budget and its ancestors
    /// must outlive all work and any results owned beneath them. Lython disposes only
    /// scopes it creates; it never disposes the supplied budget or copies its balance.
    /// Estimates cover major storage; allow headroom for minor and runtime memory.
    /// </remarks>
    public ILythonMemoryBudget? MemoryBudget { get; init; }

    /// <summary>Gets a borrowed longer-lived parent budget for projected results.</summary>
    /// <remarks>
    /// Null uses the invocation's supplied parent budget. Specify a separate parent
    /// when a result must survive disposal of the invocation's supplied ancestors.
    /// Result ownership is established before projection; no reparenting occurs.
    /// Projection reserves through this owner, without an independent cap.
    /// </remarks>
    public ILythonMemoryBudget? ResultMemoryBudget { get; init; }

    internal ILythonMemoryBudget? ExecutionMemoryOwner { get; init; }
    internal ILythonMemoryBudget? ProjectionMemoryOwner { get; init; }

    /// <summary>
    /// Copies every option while replacing the cancellation token. This is the
    /// single copy site for cancellation merging (R32): add future options here
    /// rather than duplicating the list at call sites.
    /// </summary>
    internal LythonRunOptions WithCancellation(CancellationToken cancellationToken)
        => Copy(cancellationToken, ExecutionMemoryOwner, ProjectionMemoryOwner);

    internal LythonRunOptions WithMemoryOwners(ILythonMemoryBudget execution, ILythonMemoryBudget projection)
        => Copy(CancellationToken, execution, projection);

    private LythonRunOptions Copy(CancellationToken cancellationToken, ILythonMemoryBudget? execution, ILythonMemoryBudget? projection)
        => new()
        {
            Globals = Globals,
            Args = Args,
            Environment = Environment,
            SourcePath = SourcePath,
            CancellationToken = cancellationToken,
            DisableLocalModuleImports = DisableLocalModuleImports,
            AllowedLocalModules = AllowedLocalModules,
            AllowedBuiltinModules = AllowedBuiltinModules,
            MaxRecursionDepth = MaxRecursionDepth,
            MemoryBudget = MemoryBudget,
            ResultMemoryBudget = ResultMemoryBudget,
            ExecutionMemoryOwner = execution,
            ProjectionMemoryOwner = projection,
        };
}
