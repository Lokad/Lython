using System.Diagnostics.CodeAnalysis;

namespace Lokad.Lython.Runtime;

internal static class HostOperation
{
    public static T Invoke<T>(Func<T> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span);
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
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span);
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
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            ThrowTranslated<object>(exception, name, span);
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
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            return ThrowTranslated<T>(exception, name, span);
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
        catch (Exception exception) when (RequiresTranslation(exception))
        {
            ThrowTranslated<object>(exception, name, span);
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

    private static bool RequiresTranslation(Exception exception)
        => exception is not LythonRuntimeException and not OutOfMemoryException;

    [DoesNotReturn]
    private static T ThrowTranslated<T>(
        Exception exception,
        string name,
        LythonSourceSpan? span)
        => exception switch
        {
            OperationCanceledException => throw RuntimeErrors.Runtime("execution canceled", span),
            LythonSubprocessOutputLimitException outputLimit => throw RuntimeErrors.Runtime(outputLimit.Message, span),
            LythonHostCapabilityUnavailableException unavailable => throw RuntimeErrors.Runtime($"host {unavailable.Capability} is not available in this host.", span),
            _ => throw RuntimeErrors.Host(name, exception, span),
        };
}
