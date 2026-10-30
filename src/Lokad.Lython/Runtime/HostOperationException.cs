namespace Lokad.Lython.Runtime;

// Typed host-operation failure signal (N16): thrown where a host capability
// reports failure (never for guest-raised failures, even with identical text),
// so catchers recognize it by type instead of comparing English message text.
// The Python identity, safe message, span, frames, inner exception,
// and payload flow through the base like any other guest-visible failure.
internal sealed class HostOperationException : LythonRuntimeException
{
    public HostOperationException(string exceptionType, string message, LythonSourceSpan? span, Exception? innerException)
        : base(exceptionType, message, span, innerException)
    {
        Attribution = new(LythonFailureReason.HostOperationFailed);
    }
}
