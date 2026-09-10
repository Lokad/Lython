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
        => new PyException(exception.Identity, exception.Message, exception.Payload ?? PyNone.Instance) with
        {
            Cause = exception.PythonCause,
        };

    private static bool MatchesCaughtException(
        IReadOnlyList<string>? caughtTypeNames,
        LythonRuntimeException thrown,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (caughtTypeNames is null)
        {
            return true;
        }

        foreach (var caughtTypeName in caughtTypeNames)
        {
            var parts = caughtTypeName.Split('.');
            object caughtType = ResolveName(parts[0], span, context);
            for (var i = 1; i < parts.Length; i++)
            {
                if (!TryResolveRuntimeMember(caughtType, parts[i], context, span, out var member))
                {
                    throw PyMemberAccess.CreateMissingMemberError(caughtType, parts[i], span);
                }

                caughtType = member;
            }

            if (caughtType is not IPythonExceptionType pythonExceptionType)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "catching classes that do not inherit from BaseException is not allowed",
                    span);
            }

            if (MatchesExceptionType(pythonExceptionType.ExceptionIdentity, thrown.Identity))
            {
                return true;
            }
        }

        return false;
    }
}
