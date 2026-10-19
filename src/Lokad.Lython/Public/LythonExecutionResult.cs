namespace Lokad.Lython;

/// <summary>Identifies the phase in which a Lython execution completed or failed.</summary>
public enum LythonExecutionOutcome
{
    /// <summary>The script completed successfully.</summary>
    Succeeded,
    /// <summary>Compilation or host-capability validation rejected the script.</summary>
    CompilationFailed,
    /// <summary>The script raised a Python exception at runtime.</summary>
    RuntimeFailed,
}

/// <summary>Contains the complete, host-facing result of one script execution.</summary>
public sealed class LythonExecutionResult : IDisposable
{
    private ExecutionState? _state;
    private string? _standardOutput;
    private string? _standardError;
    private IReadOnlyList<LythonDiagnostic>? _diagnostics;
    private ILythonMemoryScope? _memoryScope;

    private LythonExecutionResult(
        ExecutionState state,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics)
    {
        _state = state;
        _standardOutput = standardOutput;
        _standardError = standardError;
        _diagnostics = diagnostics;
    }

    /// <summary>Creates a successful execution result.</summary>
    public static LythonExecutionResult Succeeded(
        object? returnValue,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics)
        => new(new SucceededState(returnValue), standardOutput, standardError, diagnostics);

    /// <summary>Creates a compilation-failure result.</summary>
    public static LythonExecutionResult CompilationFailed(
        int exitCode,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics)
        => CompilationFailed(exitCode, standardOutput, standardError, diagnostics,
            new LythonFailureAttribution(LythonFailureReason.Compilation));

    internal static LythonExecutionResult CompilationFailed(
        int exitCode,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics,
        LythonFailureAttribution attribution)
    {
        if (!diagnostics.Any(diagnostic => diagnostic.Severity == LythonDiagnosticSeverity.Error))
        {
            throw new ArgumentException("Compilation failure requires at least one error diagnostic.", nameof(diagnostics));
        }

        return new(new CompilationFailedState(exitCode, attribution), standardOutput, standardError, diagnostics);
    }

    /// <summary>Creates a runtime-failure result.</summary>
    public static LythonExecutionResult RuntimeFailed(
        int exitCode,
        LythonRuntimeFailure failure,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics)
        => new(new RuntimeFailedState(exitCode, failure), standardOutput, standardError, diagnostics);

    /// <summary>Gets the outcome-specific state without nullable cross-outcome fields.</summary>
    public ExecutionState State => _state ?? throw new ObjectDisposedException(nameof(LythonExecutionResult));

    /// <summary>Gets the execution outcome.</summary>
    public LythonExecutionOutcome Outcome => State.Outcome;

    /// <summary>Gets whether execution succeeded.</summary>
    public bool Success => State is SucceededState;

    /// <summary>Gets the CLR projection of the returned Python value, when present.</summary>
    public object? ReturnValue => (State as SucceededState)?.ReturnValue;

    /// <summary>Gets UTF-8 standard output decoded as text.</summary>
    public string StandardOutput => _standardOutput ?? throw new ObjectDisposedException(nameof(LythonExecutionResult));

    /// <summary>Gets UTF-8 standard error decoded as text.</summary>
    public string StandardError => _standardError ?? throw new ObjectDisposedException(nameof(LythonExecutionResult));

    /// <summary>Gets the process-style exit code for a failed execution.</summary>
    public int? ExitCode => State switch
    {
        CompilationFailedState failed => failed.ExitCode,
        RuntimeFailedState failed => failed.ExitCode,
        _ => null,
    };

    /// <summary>Gets diagnostics produced before execution.</summary>
    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics ?? throw new ObjectDisposedException(nameof(LythonExecutionResult));

    internal LythonExecutionResult OwnMemoryScope(ILythonMemoryScope scope)
    {
        _memoryScope = scope;
        return this;
    }

    /// <summary>Relinquishes this result and disposes its owned memory scope.</summary>
    /// <remarks>
    /// Call only after all extracted return values, output strings, outcome states
    /// and other aliases are no longer in use. Do not dispose concurrently with a
    /// result reader. Payload access after disposal throws. Forgotten disposal
    /// conservatively retains the allowance; CLR collection does not release it.
    /// </remarks>
    public void Dispose()
    {
        _state = null;
        _standardOutput = null;
        _standardError = null;
        _diagnostics = null;
        Interlocked.Exchange(ref _memoryScope, null)?.Dispose();
    }

    /// <summary>
    /// Gets the peak accounted execution memory in bytes observed during the run.
    /// This is the high-water mark of the conservative execution governor, not a
    /// managed-heap or process-RSS measurement. Provider child-scope charges are
    /// included in the borrowed root balance, not this governor-only peak. See the SPEC memory-budget section
    /// for the combined peak envelope.
    /// </summary>
    public long PeakExecutionMemoryBytes { get; internal set; }

    /// <summary>
    /// Gets the peak accounted projection memory in bytes observed while building
    /// this result. Projection accounting only grows, so this equals the total
    /// projected return value and captured output charged against the projection
    /// budget.
    /// </summary>
    public long PeakProjectionMemoryBytes { get; internal set; }

    /// <summary>
    /// Gets the requested size in bytes of the first refused execution memory
    /// reservation. Refusal is terminal even through guest exception handling.
    /// Use <see cref="FailureAttribution"/> to identify its origin. Zero when
    /// no execution reservation was refused (including projection-only failures).
    /// </summary>
    public long DeniedReservationBytes { get; internal set; }

    /// <summary>Gets runtime consumption for an admitted run, including unsuccessful runs.</summary>
    /// <remarks>
    /// Null means execution did not reach runtime admission or this result was
    /// constructed through a public factory. Unmeasured instruction work is null
    /// within the snapshot, distinct from a measured zero.
    /// </remarks>
    public LythonExecutionConsumption? Consumption { get; internal set; }

    /// <summary>Gets projected Python exception details for a runtime failure.</summary>
    public LythonRuntimeFailure? Failure => (State as RuntimeFailedState)?.Failure;

    /// <summary>Gets typed terminal-failure attribution, or null for a successful execution.</summary>
    public LythonFailureAttribution? FailureAttribution => State switch
    {
        RuntimeFailedState failed => failed.Failure.Attribution ?? new(LythonFailureReason.GuestException),
        CompilationFailedState failed => failed.Attribution,
        _ => null,
    };

    /// <summary>Represents one valid outcome-specific execution state.</summary>
    public abstract record ExecutionState
    {
        private protected ExecutionState() { }

        /// <summary>Gets the outcome represented by this state.</summary>
        public abstract LythonExecutionOutcome Outcome { get; }
    }

    /// <summary>Represents successful execution, including a possibly-None projected return value.</summary>
    public sealed record SucceededState : ExecutionState
    {
        /// <summary>Creates successful outcome state.</summary>
        public SucceededState(object? returnValue) => ReturnValue = returnValue;

        /// <summary>Gets the projected Python return value.</summary>
        public object? ReturnValue { get; }

        /// <inheritdoc />
        public override LythonExecutionOutcome Outcome => LythonExecutionOutcome.Succeeded;
    }

    /// <summary>Represents rejection before runtime execution.</summary>
    public sealed record CompilationFailedState : ExecutionState
    {
        /// <summary>Creates compilation-failure outcome state.</summary>
        public CompilationFailedState(int exitCode)
            : this(exitCode, new(LythonFailureReason.Compilation)) { }

        internal CompilationFailedState(int exitCode, LythonFailureAttribution attribution)
        {
            ExitCode = exitCode;
            Attribution = attribution;
        }

        /// <summary>Gets the compile or host-preflight rejection origin.</summary>
        public LythonFailureAttribution Attribution { get; }

        /// <summary>Gets the process-style failure code.</summary>
        public int ExitCode { get; }

        /// <inheritdoc />
        public override LythonExecutionOutcome Outcome => LythonExecutionOutcome.CompilationFailed;
    }

    /// <summary>Represents a Python runtime failure.</summary>
    public sealed record RuntimeFailedState : ExecutionState
    {
        /// <summary>Creates runtime-failure outcome state.</summary>
        public RuntimeFailedState(int exitCode, LythonRuntimeFailure failure)
        {
            ExitCode = exitCode;
            Failure = failure;
        }

        /// <summary>Gets the process-style failure code.</summary>
        public int ExitCode { get; }

        /// <summary>Gets the projected Python exception details.</summary>
        public LythonRuntimeFailure Failure { get; }

        /// <inheritdoc />
        public override LythonExecutionOutcome Outcome => LythonExecutionOutcome.RuntimeFailed;
    }
}
