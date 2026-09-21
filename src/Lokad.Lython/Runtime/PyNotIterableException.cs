namespace Lokad.Lython.Runtime;

// Typed protocol-absence signal (R17): thrown where iteration support is
// missing (never for user-raised failures, even with identical text), so
// catchers recognize it by type instead of comparing English message text.
// The public TypeError identity, message, span, frames, and payload flow
// through the base like any other guest-visible failure.
internal sealed class PyNotIterableException : LythonRuntimeException
{
    public PyNotIterableException(string message, LythonSourceSpan? span)
        : base("TypeError", message, span)
    {
    }

    public PyNotIterableException(string message, LythonSourceSpan? span, Exception? innerException)
        : base("TypeError", message, span, innerException)
    {
    }
}
