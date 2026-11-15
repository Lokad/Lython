namespace Lokad.Lython.Runtime;

// Function binding owns final invocation storage through completion and cleanup.
// Builtin object-array binding has a separate record and no workspace owner.
internal readonly record struct BoundFunctionArguments(
    LythonRuntime.ExecutableSlot[] Values,
    ArgumentPresence Assigned,
    MemoryGovernor? StorageOwner = null);
