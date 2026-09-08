namespace Lokad.Lython.Runtime;

/// <summary>
/// Internal failure for unhashable values reaching hash-based containers.
/// Replaces matching on the message text: catchers use this type instead of
/// comparing <see cref="System.Exception.Message"/>.
/// </summary>
internal sealed class PyUnhashableException : InvalidOperationException
{
    public PyUnhashableException()
        : base("unhashable value")
    {
    }
}
