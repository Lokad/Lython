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
public sealed class LythonExecutionResult
{
    private LythonExecutionResult(
        ExecutionState state,
        string standardOutput,
        string standardError,
        IReadOnlyList<LythonDiagnostic> diagnostics)
    {
        State = state;
        StandardOutput = standardOutput;
        StandardError = standardError;
        Diagnostics = diagnostics;
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
    {
        if (!diagnostics.Any(diagnostic => diagnostic.Severity == LythonDiagnosticSeverity.Error))
        {
            throw new ArgumentException("Compilation failure requires at least one error diagnostic.", nameof(diagnostics));
        }

        return new(new CompilationFailedState(exitCode), standardOutput, standardError, diagnostics);
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
    public ExecutionState State { get; }

    /// <summary>Gets the execution outcome.</summary>
    public LythonExecutionOutcome Outcome => State.Outcome;

    /// <summary>Gets whether execution succeeded.</summary>
    public bool Success => State is SucceededState;

    /// <summary>Gets the CLR projection of the returned Python value, when present.</summary>
    public object? ReturnValue => (State as SucceededState)?.ReturnValue;

    /// <summary>Gets UTF-8 standard output decoded as text.</summary>
    public string StandardOutput { get; }

    /// <summary>Gets UTF-8 standard error decoded as text.</summary>
    public string StandardError { get; }

    /// <summary>Gets the process-style exit code for a failed execution.</summary>
    public int? ExitCode => State switch
    {
        CompilationFailedState failed => failed.ExitCode,
        RuntimeFailedState failed => failed.ExitCode,
        _ => null,
    };

    /// <summary>Gets diagnostics produced before execution.</summary>
    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    /// <summary>Gets projected Python exception details for a runtime failure.</summary>
    public LythonRuntimeFailure? Failure => (State as RuntimeFailedState)?.Failure;

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
        public CompilationFailedState(int exitCode) => ExitCode = exitCode;

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
