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

    // MG03: an `except ... as` binding retains the exception value through guest
    // code, so the record shell is owned at bind time. Construction commits the
    // retained args tuple and rendered message through the pool instead: dropped
    // constructions reclaim on sweep while retained ones stay charged, and unbound
    // catching adds nothing beyond those construction transients.
    private const long BoundExceptionBytes = 64;

    internal static void ChargeBoundException(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is null)
        {
            return;
        }

        governor.Reserve(BoundExceptionBytes, span);
        governor.Commit(BoundExceptionBytes);
    }

    internal static PyException CreatePythonExceptionInstance(LythonRuntimeException exception)
    {
        if (exception.OriginalPythonException is { } original)
        {
            original.Cause = exception.PythonCause;
            original.Context = exception.PythonContext;
            original.SuppressContext = exception.SuppressPythonContext;
            return original;
        }

        return new PyException(exception.Identity, exception.Message, exception.Payload ?? PyNone.Instance) with
        {
            Cause = exception.PythonCause,
            Context = exception.PythonContext,
            SuppressContext = exception.SuppressPythonContext,
            ExplicitArgs = exception.PythonExplicitArgs,
        };
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
                return MatchesCaughtExceptionClasses(tupleItems, thrown, context, span);
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
            context.CheckExecutionBudget(span);
            caughtTypes[i] = ResolveCaughtExceptionTypeName(caughtTypeNames[i], context, span);
        }

        return MatchesCaughtExceptionClasses(caughtTypes, thrown, context, span);
    }

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
        LythonRuntimeException thrown,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        // CPython validates all members, including those after a matching class.
        // Nested tuples and lists are not valid members of an except tuple.
        for (var i = 0; i < caughtTypes.Count; i++)
        {
            context.CheckExecutionBudget(span);
            _ = RequireCaughtExceptionClass(caughtTypes[i], span);
        }

        for (var i = 0; i < caughtTypes.Count; i++)
        {
            context.CheckExecutionBudget(span);
            if (MatchesExceptionType(((IPythonExceptionType)caughtTypes[i]).ExceptionIdentity, thrown.Identity))
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
