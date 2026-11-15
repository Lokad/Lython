namespace Lokad.Lython.Runtime;

// Function binding owns final invocation storage through completion and cleanup.
// Null slots denote omitted arguments during binding; None is an ordinary value.
// Once binding completes, only the logical prefix length needs to travel with it.
// Builtin object-array binding has a separate record and no workspace owner.
internal readonly record struct BoundFunctionArguments(
    LythonRuntime.ExecutableSlot[] Values,
    int ParameterCount,
    MemoryGovernor? StorageOwner = null);
