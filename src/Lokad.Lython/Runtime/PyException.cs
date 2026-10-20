using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed record PyException(
    PythonExceptionIdentity Identity,
    string Message,
    object Value,
    PyTuple? ExplicitArgs) : IPyHashableValue, IPyGovernedValue
{
    // Fixed record storage, including inline provenance and its governor link.
    // Referenced args, payloads, attributes and exception chains own their own
    // storage; this shell owns those references, never their aliased payloads.
    internal const long ShellBytes = 160;
    public MemoryGovernor? OwnerMemoryGovernor { get; private set; }
    private bool _chainOwned;
    public LythonSourceSpan? AllocationSpan => null;

    internal static PyException CreateOwned(
        PythonExceptionIdentity identity, string message, object value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan? span,
        PyTuple? explicitArgs = null)
    {
        var governor = context.MemoryGovernor;
        governor.Reserve(ShellBytes, span);
        PyException result;
        try
        {
            result = new(identity, message, value, explicitArgs) { OwnerMemoryGovernor = governor, _chainOwned = true };
            governor.Commit(ShellBytes);
        }
        catch
        {
            governor.ReleaseReserved(ShellBytes);
            throw;
        }
        return result;
    }

    // Engine-control exceptions are unowned until they become guest values.
    // Constructor results and already-published aliases are already registered.
    // Fund the root before descending; a partial retry reuses that owner entry.
    internal void Adopt(LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
    {
        var pool = context.State.MemoryGovernor;
        if (_chainOwned) return;
        if (OwnerMemoryGovernor is null)
        {
            context.MemoryGovernor.Reserve(ShellBytes, span);
            context.MemoryGovernor.Commit(ShellBytes);
            OwnerMemoryGovernor = context.MemoryGovernor;
        }
        if (Cause is not null || Context is not null)
        {
            ExecutionGuards.EnsureInterpreterStack(span);
            Cause?.Adopt(context, span);
            Context?.Adopt(context, span);
        }
        // Partial graph admission is retried without charging the root again.
        // Guest-created cyclic chains already contain fully owned records;
        // transient control chains are constructed only from earlier errors.
        _chainOwned = true;
    }
    public PyException(PythonExceptionIdentity identity, string message, object value)
        : this(identity, message, value, null)
    {
    }

    public PyException(string typeName, string message, object value)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(typeName), message, value, null)
    {
    }

    public string TypeName => Identity.TypeName;

    // Internal provenance follows this actual exception across catches and
    // reraises. Python attributes/args cannot manufacture or replace it.
    internal LythonFailureAttribution? Attribution { get; set; }

    // Exceptions hash by identity like CPython, independent of
    // their record shape, so mutation never moves a live key.
    public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    // Explicit raise causes ride alongside the value; handlers rewrap
    // through the thrown CLR exception, which carries the same slot.
    // Direct __cause__ assignment writes this slot like CPython.
    public PyException? Cause { get; set; }

    // Implicit chaining follows the active handler like CPython: the context
    // is captured at raise time while suppression comes from `raise ... from`.
    // Direct __context__ assignment writes this slot like CPython.
    public PyException? Context { get; set; }

    // Direct __suppress_context__ assignment writes this slot like CPython.
    public bool SuppressContext { get; set; }

    // Custom attributes live in a governed dict like CPython's instance
    // __dict__; reads check it before fixed members so assignments shadow,
    // while the args slot stays separate like CPython.
    public PyDict? CustomDict { get; set; }

    // Assigned args replace the construction slot without entering the dict,
    // so __dict__ stays clean like CPython.
    public PyTuple? ArgsOverride { get; set; }
}
