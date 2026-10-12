namespace Lokad.Lython;

/// <summary>Contains the existing runtime counters at execution completion.</summary>
/// <param name="ExecutionSteps">
/// Interpreter checkpoints when an explicit instruction allowance was supplied;
/// otherwise null. This includes the checkpoint that exceeded the allowance.
/// Counts depend on the execution backend and source layout, and are not Python
/// bytecodes or a performance comparison metric.
/// </param>
/// <param name="HostCalls">Registered host-call attempts, including a host-call limit trip.</param>
/// <param name="StandardOutputBytes">UTF-8 bytes retained in the runtime's standard-output capture.</param>
/// <param name="StandardErrorBytes">UTF-8 bytes retained in the runtime's standard-error capture.</param>
/// <remarks>
/// Capture counts describe accepted bytes even when public projection truncates
/// output or a host write fails. Cleanup/publication calls are included. These
/// counters add no instruction counting to runs without explicit fuel. Memory
/// peaks and denied reservations remain the separate accounting properties on
/// LythonExecutionResult; they do not measure CLR heap size or process RSS.
/// </remarks>
public readonly record struct LythonExecutionConsumption(
    long? ExecutionSteps,
    long HostCalls,
    long StandardOutputBytes,
    long StandardErrorBytes);
