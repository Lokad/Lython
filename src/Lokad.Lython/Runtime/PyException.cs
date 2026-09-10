namespace Lokad.Lython.Runtime;

internal sealed record PyException(
    PythonExceptionIdentity Identity,
    string Message,
    object Value,
    PyTuple? ExplicitArgs)
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

    // Explicit raise causes ride alongside the value; handlers rewrap
    // through the thrown CLR exception, which carries the same slot.
    public PyException? Cause { get; init; }

    // Implicit chaining follows the active handler like CPython: the context
    // is captured at raise time while suppression comes from `raise ... from`.
    public PyException? Context { get; init; }

    public bool SuppressContext { get; init; }

    // Notes accumulate through add_note and back the __notes__ list.
    public PyList? Notes { get; set; }
}
