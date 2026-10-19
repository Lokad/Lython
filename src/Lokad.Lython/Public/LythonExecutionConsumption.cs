namespace Lokad.Lython;

/// <summary>Contains retained output sizes at execution completion.</summary>
/// <param name="StandardOutputBytes">UTF-8 bytes retained in the runtime's standard-output capture.</param>
/// <param name="StandardErrorBytes">UTF-8 bytes retained in the runtime's standard-error capture.</param>
/// <remarks>
/// Capture counts describe accepted bytes even when public projection truncates
/// output or a host write fails. Memory
/// peaks and denied reservations remain the separate accounting properties on
/// LythonExecutionResult; they do not measure CLR heap size or process RSS.
/// </remarks>
public readonly record struct LythonExecutionConsumption(
    long StandardOutputBytes,
    long StandardErrorBytes);
