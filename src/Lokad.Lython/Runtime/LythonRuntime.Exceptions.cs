namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static object ResolvePythonExceptionType(
        LythonRuntimeException exception,
        ExecutionContext context)
    {
        if (context.State.BuiltinVariables.TryGetValue(exception.ExceptionType, out var exceptionType) &&
            exceptionType is ExceptionTypeValue)
        {
            return exceptionType;
        }

        // Module-specific exception classes are not part of the builtin table,
        // but __exit__ must still receive a Python-shaped type rather than the
        // runtime's internal CLR string discriminator.
        return new ExceptionTypeValue(exception.ExceptionType);
    }

    internal static PyException CreatePythonExceptionInstance(LythonRuntimeException exception)
        => new(exception.ExceptionType, exception.Message, exception.Payload ?? PyNone.Instance);
}
