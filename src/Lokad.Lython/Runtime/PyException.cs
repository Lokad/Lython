namespace Lokad.Lython.Runtime;

internal sealed record PyException(
    string TypeName,
    string Message,
    object Value);
