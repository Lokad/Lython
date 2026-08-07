namespace Lokad.Lython;

public enum LythonExecutionOutcome
{
    Succeeded,
    CompilationFailed,
    RuntimeFailed,
}

public sealed class LythonExecutionResult
{
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

    public LythonExecutionOutcome Outcome { get; }

    public bool Success => Outcome == LythonExecutionOutcome.Succeeded;

    public object? ReturnValue { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }

    public int? ExitCode { get; }

    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    public LythonRuntimeFailure? Failure { get; }
}
