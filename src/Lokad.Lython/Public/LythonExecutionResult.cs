namespace Lokad.Lython;

public sealed class LythonExecutionResult
{
    public LythonExecutionResult(
        bool success,
        object? returnValue,
        string standardOutput,
        string standardError,
        int? exitCode,
        IReadOnlyList<LythonDiagnostic> diagnostics,
        LythonRuntimeFailure? failure)
    {
        Success = success;
        ReturnValue = returnValue;
        StandardOutput = standardOutput;
        StandardError = standardError;
        ExitCode = exitCode;
        Diagnostics = diagnostics;
        Failure = failure;
    }

    public bool Success { get; }

    public object? ReturnValue { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }

    public int? ExitCode { get; }

    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    public LythonRuntimeFailure? Failure { get; }
}
