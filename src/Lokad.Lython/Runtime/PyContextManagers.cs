using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyContextManagers
{
    public static object ExecuteWith(
        object manager,
        string? variableName,
        IReadOnlyList<LoweredStatement> body,
        LythonSourceSpan span,
        LythonSourceSpan expressionSpan,
        LythonRuntime.ExecutionContext context)
    {
        var protocol = Resolve(manager, expressionSpan, context);
        var entered = protocol.Enter();

        if (variableName is not null)
        {
            LythonRuntime.StoreName(variableName, entered, context, span);
        }

        try
        {
            var signal = LythonRuntime.ExecuteStatements(body, context);
            if (signal is not null)
            {
                throw signal;
            }

            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            return PyNone.Instance;
        }
        catch (LythonRuntime.ControlSignal)
        {
            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            throw;
        }
        catch (LythonRuntimeException ex)
        {
            if (!protocol.Exit(
                    LythonRuntime.ResolvePythonExceptionType(ex, context),
                    LythonRuntime.CreatePythonExceptionInstance(ex),
                    PyNone.Instance))
            {
                throw;
            }

            return PyNone.Instance;
        }
    }

    public static async ValueTask<object> ExecuteWithAsync(
        object manager,
        string? variableName,
        IReadOnlyList<LoweredStatement> body,
        LythonSourceSpan span,
        LythonSourceSpan expressionSpan,
        LythonRuntime.ExecutionContext context)
    {
        var protocol = Resolve(manager, expressionSpan, context);
        var entered = await EnterAsync(protocol, span, context).ConfigureAwait(false);

        if (variableName is not null)
        {
            LythonRuntime.StoreName(variableName, entered, context, span);
        }

        try
        {
            var signal = await LythonRuntime.ExecuteStatementsAsync(body, context).ConfigureAwait(false);
            if (signal is not null)
            {
                throw signal;
            }

            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            return PyNone.Instance;
        }
        catch (LythonRuntime.ControlSignal)
        {
            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            throw;
        }
        catch (LythonRuntimeException ex)
        {
            if (!await ExitAsync(
                    protocol,
                    LythonRuntime.ResolvePythonExceptionType(ex, context),
                    LythonRuntime.CreatePythonExceptionInstance(ex),
                    PyNone.Instance,
                    span,
                    context).ConfigureAwait(false))
            {
                throw;
            }

            return PyNone.Instance;
        }
    }

    public static IPyContextManager Resolve(object target, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (target is IPyContextManager manager)
        {
            return manager;
        }

        if (!PyMemberAccess.TryResolve(target, "__enter__", context, span, out var enter) || enter is not LythonRuntime.ICallable enterCallable ||
            !PyMemberAccess.TryResolve(target, "__exit__", context, span, out var exit) || exit is not LythonRuntime.ICallable exitCallable)
        {
            throw RuntimeErrors.Type("Object does not support the context manager protocol.", span);
        }

        return new CallableContextManager(enterCallable, exitCallable, span, context);
    }

    private sealed partial class CallableContextManager(
        LythonRuntime.ICallable enter,
        LythonRuntime.ICallable exit,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context) : IPyContextManager
    {
        public object Enter() => enter.Invoke(Array.Empty<CallArgumentValue>(), span, context);

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            var result = exit.Invoke(
                [CallArgumentValue.Positional(exceptionType), CallArgumentValue.Positional(exceptionValue), CallArgumentValue.Positional(traceback)],
                span,
                context);
            return PyTruthiness.IsTruthy(result);
        }
    }

    private static ValueTask<object> EnterAsync(IPyContextManager manager, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        if (manager is CallableContextManager callable)
        {
            return callable.EnterAsync();
        }

        if (manager is IPyAsyncContextManager asyncManager)
        {
            return asyncManager.EnterAsync();
        }

        return ValueTask.FromResult(manager.Enter());
    }

    private static ValueTask<bool> ExitAsync(
        IPyContextManager manager,
        object exceptionType,
        object exceptionValue,
        object traceback,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context)
    {
        if (manager is CallableContextManager callable)
        {
            return callable.ExitAsync(exceptionType, exceptionValue, traceback);
        }

        if (manager is IPyAsyncContextManager asyncManager)
        {
            return asyncManager.ExitAsync(exceptionType, exceptionValue, traceback);
        }

        return ValueTask.FromResult(manager.Exit(exceptionType, exceptionValue, traceback));
    }

    private sealed partial class CallableContextManager
    {
        public async ValueTask<object> EnterAsync()
            => await enter.InvokeAsync(Array.Empty<CallArgumentValue>(), span, context).ConfigureAwait(false);

        public async ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            var result = await exit.InvokeAsync(
                    [CallArgumentValue.Positional(exceptionType), CallArgumentValue.Positional(exceptionValue), CallArgumentValue.Positional(traceback)],
                    span,
                    context)
                .ConfigureAwait(false);
            return PyTruthiness.IsTruthy(result);
        }
    }
}
