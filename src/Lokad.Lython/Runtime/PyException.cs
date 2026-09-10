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
}
