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
}
