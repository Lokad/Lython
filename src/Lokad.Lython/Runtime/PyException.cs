using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal sealed record PyException(
    PythonExceptionIdentity Identity,
    string Message,
    object Value,
    PyTuple? ExplicitArgs) : IPyHashableValue
{
    public PyException(PythonExceptionIdentity identity, string message, object value)
        : this(identity, message, value, null)
    {
    }

    public PyException(string typeName, string message, object value)
        : this(PythonExceptionIdentity.FromRuntimeTypeName(typeName), message, value, null)
    {
    }

    public string TypeName => Identity.TypeName;

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
