namespace Lokad.Lython.Runtime;

internal sealed class ProjectionException : Exception
{
    public ProjectionException(string message)
        : base(message)
    {
    }

    internal LythonFailureAttribution Attribution { get; init; }
        = new(LythonFailureReason.ProjectionRejected);
}
