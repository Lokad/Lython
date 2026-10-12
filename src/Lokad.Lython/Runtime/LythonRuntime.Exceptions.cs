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

    internal static PyException CreatePythonExceptionInstance(LythonRuntimeException exception, ExecutionContext? context = null)
    {
        if (exception.OriginalPythonException is { } original)
        {
            if (context is not null && original.OwnerMemoryGovernor is not null)
            {
                exception.PythonCause?.Adopt(context, exception.Span);
                exception.PythonContext?.Adopt(context, exception.Span);
            }
            original.Cause = exception.PythonCause;
            original.Context = exception.PythonContext;
            original.SuppressContext = exception.SuppressPythonContext;
            return original;
        }

        // This transient control value is needed for bare raises and native
        // cleanup even when no guest allocation can fit. Publication through
        // a binding, sys.exc_info or guest callback adopts its actual shell.
        var created = new PyException(exception.Identity, exception.Message, exception.Payload ?? PyNone.Instance,
            exception.PythonExplicitArgs);
        created.Cause = exception.PythonCause;
        created.Context = exception.PythonContext;
        created.SuppressContext = exception.SuppressPythonContext;
        created.Attribution = exception.Attribution;
        exception.OriginalPythonException = created;
        return created;
    }

    private static bool MatchesCaughtException(
        IReadOnlyList<string>? caughtTypeNames,
        bool caughtTypesAreTuple,
        LythonRuntimeException thrown,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (caughtTypeNames is null)
        {
            return true;
        }

        if (!caughtTypesAreTuple)
        {
            var caughtType = ResolveCaughtExceptionTypeName(caughtTypeNames[0], context, span);
            if (PyTupleLike.TryGetItems(caughtType, out var tupleItems))
            {
                return MatchesCaughtExceptionClasses(tupleItems, thrown.Identity, context, span);
            }

            return MatchesExceptionType(RequireCaughtExceptionClass(caughtType, span).ExceptionIdentity, thrown.Identity);
        }

        // Resolve every tuple element before validating or matching, as evaluating
        // the Python tuple expression would. A later missing name must not be
        // hidden by an earlier matching class.
        using var scratch = context.MemoryGovernor.ReserveTemporary(EstimateObjectArrayBytes(caughtTypeNames.Count), span);
        var caughtTypes = new object[caughtTypeNames.Count];
        for (var i = 0; i < caughtTypes.Length; i++)
        {
            context.CheckExecution(span);
            caughtTypes[i] = ResolveCaughtExceptionTypeName(caughtTypeNames[i], context, span);
        }

        return MatchesCaughtExceptionClasses(caughtTypes, thrown.Identity, context, span);
    }

    private static bool MatchesCaughtExceptionValue(object caughtType, PythonExceptionIdentity thrown,
        ExecutionContext context, LythonSourceSpan span)
        => PyTupleLike.TryGetItems(caughtType, out var tupleItems)
            ? MatchesCaughtExceptionClasses(tupleItems, thrown, context, span)
            : MatchesExceptionType(RequireCaughtExceptionClass(caughtType, span).ExceptionIdentity, thrown);

    private static object ResolveCaughtExceptionTypeName(string caughtTypeName, ExecutionContext context, LythonSourceSpan span)
    {
        var parts = caughtTypeName.Split('.');
        object caughtType = ResolveName(parts[0], span, context);
        for (var i = 1; i < parts.Length; i++)
        {
            if (!TryResolveRuntimeMember(caughtType, parts[i], context, span, out var member))
            {
                throw PyMemberAccess.CreateMissingMemberError(caughtType, parts[i], span, context);
            }

            caughtType = member;
        }

        return caughtType;
    }

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
