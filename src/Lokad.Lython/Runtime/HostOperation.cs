using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython.Runtime;

internal static class HostOperation
{
    public static T Invoke<T>(Func<T> operation, string name, LythonSourceSpan? span, MemoryGovernor governor)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span, governor);
        }
    }

    public static T Await<T>(
        object capability,
        Func<ValueTask<T>> operation,
        string name,
        LythonSourceSpan? span, MemoryGovernor governor)
    {
        RequireSynchronousCapability(capability, name, span);
        try
        {
            var valueTask = operation();
            if (valueTask.IsCompletedSuccessfully)
            {
                return valueTask.Result;
            }

            var task = valueTask.AsTask();
            if (!task.IsCompleted)
            {
                throw SettleContractViolation(task, name, span, governor);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Cancelled(span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }

            return task.Result;
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span, governor);
        }
    }

    public static void Await(
        object capability,
        Func<ValueTask> operation,
        string name,
        LythonSourceSpan? span, MemoryGovernor governor)
    {
        RequireSynchronousCapability(capability, name, span);
        try
        {
            var valueTask = operation();
            if (valueTask.IsCompletedSuccessfully)
            {
                return;
            }

            var task = valueTask.AsTask();
            if (!task.IsCompleted)
            {
                throw SettleContractViolation(task, name, span, governor);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Cancelled(span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            ThrowTranslated<object>(exception, name, span, governor);
        }
    }

    // A false synchronous promise does not end the host's borrow. Settle
    // exactly the operation already started before any caller storage unwinds.
    private static SynchronousHostContractException SettleContractViolation(Task task, string name, LythonSourceSpan? span, MemoryGovernor governor)
    {
        Exception? completionFailure = null;
        try { task.GetAwaiter().GetResult(); }
        catch (Exception exception) when (exception is not OutOfMemoryException) { completionFailure = exception; }
        if (completionFailure is LythonHostException { Kind: LythonHostFailureKind.MemoryAdmission } refusal)
            throw governor.RefuseAdmission(refusal.Message, refusal.RequestedBytes, span, refusal);
        return new SynchronousHostContractException(name, span, completionFailure);
    }

    public static async ValueTask<T> AwaitAsync<T>(
        Func<ValueTask<T>> operation,
        string name,
        LythonSourceSpan? span, MemoryGovernor governor)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span, governor);
        }
    }

    public static async ValueTask AwaitAsync(
        Func<ValueTask> operation,
        string name,
        LythonSourceSpan? span, MemoryGovernor governor)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            ThrowTranslated<object>(exception, name, span, governor);
        }
    }

    public static void RequireSynchronousCapability(
        object capability,
        string name,
        LythonSourceSpan? span)
    {
        if (capability is not ILythonSynchronousHostCapability { CompletesSynchronously: true })
        {
            throw RuntimeErrors.Capability(
                $"{name} cannot run synchronously; use RunAsync because the host capability is asynchronous.",
                span);
        }
    }

    private static bool RequiresTranslation(Exception exception)
        => exception is not LythonRuntimeException and not OutOfMemoryException;

    [DoesNotReturn]
    private static T ThrowTranslated<T>(
        Exception exception,
        string name,
        LythonSourceSpan? span, MemoryGovernor governor)
    {
        governor.ThrowIfAdmissionFailed();
        return exception switch
        {
            OperationCanceledException => throw RuntimeErrors.Cancelled(span),
            LythonHostCapabilityUnavailableException unavailable => throw RuntimeErrors.Capability($"host {unavailable.Capability} is not available in this host.", span),
            LythonHostException { Kind: LythonHostFailureKind.MemoryAdmission } refusal =>
                throw governor.RefuseAdmission(refusal.Message, refusal.RequestedBytes, span, refusal),
            LythonHostException failure => throw new HostOperationException(failure.Kind switch
            {
                LythonHostFailureKind.NotFound => "FileNotFoundError",
                LythonHostFailureKind.PermissionDenied => "PermissionError",
                LythonHostFailureKind.IsDirectory => "IsADirectoryError",
                LythonHostFailureKind.NotDirectory => "NotADirectoryError",
                LythonHostFailureKind.AlreadyExists => "FileExistsError",
                LythonHostFailureKind.UnsupportedOperation => "NotImplementedError",
                LythonHostFailureKind.BrokenPipe => "BrokenPipeError",
                _ => "OSError"
            }, failure.Message, span, failure),
            _ => throw RuntimeErrors.Host(name, exception, span),
        };
    }
}
