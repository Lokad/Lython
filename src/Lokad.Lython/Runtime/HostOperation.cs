namespace Lokad.Lython.Runtime;

internal static class HostOperation
{
    public static T Invoke<T>(Func<T> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            return operation();
        }
        catch (Exception exception)
        {
            RethrowTranslated(exception, name, span);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    public static T Await<T>(
        object capability,
        Func<ValueTask<T>> operation,
        string name,
        LythonSourceSpan? span)
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
                throw RuntimeErrors.Runtime($"{name} violated its synchronous host capability contract.", span);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }

            return task.Result;
        }
        catch (Exception exception)
        {
            RethrowTranslated(exception, name, span);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    public static void Await(
        object capability,
        Func<ValueTask> operation,
        string name,
        LythonSourceSpan? span)
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
                throw RuntimeErrors.Runtime($"{name} violated its synchronous host capability contract.", span);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }
        }
        catch (Exception exception)
        {
            RethrowTranslated(exception, name, span);
        }
    }

    public static async ValueTask<T> AwaitAsync<T>(
        Func<ValueTask<T>> operation,
        string name,
        LythonSourceSpan? span)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RethrowTranslated(exception, name, span);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    public static async ValueTask AwaitAsync(
        Func<ValueTask> operation,
        string name,
        LythonSourceSpan? span)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            RethrowTranslated(exception, name, span);
        }
    }

    public static void RequireSynchronousCapability(
        object capability,
        string name,
        LythonSourceSpan? span)
    {
        if (capability is not ILythonSynchronousHostCapability { CompletesSynchronously: true })
        {
            throw RuntimeErrors.Runtime(
                $"{name} cannot run synchronously; use RunAsync because the host capability is asynchronous.",
                span);
        }
    }

    private static void RethrowTranslated(
        Exception exception,
        string name,
        LythonSourceSpan? span)
    {
        switch (exception)
        {
            case OperationCanceledException:
                throw RuntimeErrors.Runtime("execution canceled", span);
            case LythonRuntimeException:
            case OutOfMemoryException:
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
                break;
            case LythonSubprocessOutputLimitException outputLimit:
                throw RuntimeErrors.Runtime(outputLimit.Message, span);
            case NotSupportedException notSupported
                when notSupported.Message.Contains("binary file I/O", StringComparison.OrdinalIgnoreCase):
                throw RuntimeErrors.Runtime("host binary file I/O is not available in this host.", span);
            default:
                throw RuntimeErrors.Host(name, exception, span);
        }
    }
}
