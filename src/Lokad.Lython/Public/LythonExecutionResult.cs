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
    /// <summary>Creates an execution result and validates that its fields agree with <paramref name="outcome"/>.</summary>
    public LythonExecutionResult(
        LythonExecutionOutcome outcome,
        object? returnValue,
        string standardOutput,
        string standardError,
        int? exitCode,
        IReadOnlyList<LythonDiagnostic> diagnostics,
        LythonRuntimeFailure? failure)
    {
        switch (outcome)
        {
            case LythonExecutionOutcome.Succeeded when exitCode is not null || failure is not null:
                throw new ArgumentException("Successful execution cannot have an exit code or runtime failure.", nameof(outcome));
            case LythonExecutionOutcome.CompilationFailed when
                returnValue is not null || failure is not null || exitCode is null:
                throw new ArgumentException("Compilation failure requires an exit code and cannot have a return value or runtime failure.", nameof(outcome));
            case LythonExecutionOutcome.CompilationFailed when
                !diagnostics.Any(diagnostic => diagnostic.Severity == LythonDiagnosticSeverity.Error):
                throw new ArgumentException("Compilation failure requires at least one error diagnostic.", nameof(diagnostics));
            case LythonExecutionOutcome.RuntimeFailed when
                returnValue is not null || failure is null || exitCode is null:
                throw new ArgumentException("Runtime failure requires an exit code and failure details, and cannot have a return value.", nameof(outcome));
            case LythonExecutionOutcome.Succeeded:
            case LythonExecutionOutcome.CompilationFailed:
            case LythonExecutionOutcome.RuntimeFailed:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }

        Outcome = outcome;
        ReturnValue = returnValue;
        StandardOutput = standardOutput;
        StandardError = standardError;
        ExitCode = exitCode;
        Diagnostics = diagnostics;
        Failure = failure;
    }

    /// <summary>Gets the execution outcome.</summary>
    public LythonExecutionOutcome Outcome { get; }

    /// <summary>Gets whether execution succeeded.</summary>
    public bool Success => Outcome == LythonExecutionOutcome.Succeeded;

    /// <summary>Gets the CLR projection of the returned Python value, when present.</summary>
    public object? ReturnValue { get; }

    /// <summary>Gets UTF-8 standard output decoded as text.</summary>
    public string StandardOutput { get; }

    /// <summary>Gets UTF-8 standard error decoded as text.</summary>
    public string StandardError { get; }

    /// <summary>Gets the process-style exit code for a failed execution.</summary>
    public int? ExitCode { get; }

    /// <summary>Gets diagnostics produced before execution.</summary>
    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    /// <summary>Gets projected Python exception details for a runtime failure.</summary>
    public LythonRuntimeFailure? Failure { get; }
}
