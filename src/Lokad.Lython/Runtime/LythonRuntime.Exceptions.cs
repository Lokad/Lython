namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static object ResolvePythonExceptionType(
        LythonRuntimeException exception,
        ExecutionContext context)
    {
        if (exception.Identity.IsBuiltin &&
            context.State.BuiltinVariables.TryGetValue(exception.ExceptionType, out var exceptionType) &&
            exceptionType is ExceptionTypeValue)
        {
            return exceptionType;
        }

        // Module-specific exception classes are not part of the builtin table,
        // but __exit__ must still receive a Python-shaped type rather than the
        // runtime's internal CLR string discriminator.
        return new ExceptionTypeValue(exception.Identity);
    }

    internal static PyException CreatePythonExceptionInstance(LythonRuntimeException exception)
    {
        if (exception.OriginalPythonException is { } original)
        {
            return original;
        }

        // This transient control value is needed for bare raises and native
        // cleanup even when no guest allocation can fit. Publication through
        // a binding, sys.exc_info or guest callback adopts its actual shell.
        var created = new PyException(exception.Identity, exception.Message, exception.Payload, null);
        created.Cause = exception.PythonCause;
        created.Context = exception.PythonContext;
        created.SuppressContext = exception.SuppressPythonContext;
        created.Attribution = exception.Attribution;
        exception.OriginalPythonException = created;
        return created;
    }

    private static bool MatchesCaughtExceptionValue(object caughtType, PythonExceptionIdentity thrown,
        ExecutionContext context, LythonSourceSpan span)
        => PyTupleLike.TryGetItems(caughtType, out var tupleItems)
            ? MatchesCaughtExceptionClasses(tupleItems, thrown, context, span)
            : MatchesExceptionType(RequireCaughtExceptionClass(caughtType, span).ExceptionIdentity, thrown);

    private static bool MatchesCaughtExceptionClasses(
        IReadOnlyList<object> caughtTypes,
        PythonExceptionIdentity thrown,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // CPython validates all members, including those after a matching class.
        // Nested tuples and lists are not valid members of an except tuple.
        for (var i = 0; i < caughtTypes.Count; i++)
        {
            context.CheckExecution(span);
            _ = RequireCaughtExceptionClass(caughtTypes[i], span);
        }

        for (var i = 0; i < caughtTypes.Count; i++)
        {
            context.CheckExecution(span);
            if (MatchesExceptionType(((IPythonExceptionType)caughtTypes[i]).ExceptionIdentity, thrown))
                return true;
        }

        return false;
    }

    private static IPythonExceptionType RequireCaughtExceptionClass(object caughtType, LythonSourceSpan span)
        => caughtType as IPythonExceptionType ?? throw new LythonRuntimeException(
            "TypeError",
            "catching classes that do not inherit from BaseException is not allowed",
            span);
}
