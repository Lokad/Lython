using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyContextManagers
{
    public static object ExecuteWith(
        object manager,
        LoweredStoreTarget? target,
        IReadOnlyList<LoweredStatement> body,
        LythonSourceSpan span,
        LythonSourceSpan expressionSpan,
        LythonRuntime.ExecutionContext context)
    {
        var protocol = Resolve(manager, expressionSpan, context);
        var entered = protocol.Enter();

        LythonRuntime.LoweredBlockFlow flow;
        try
        {
            if (target is not null) LythonRuntime.AssignTarget(target.Syntax, entered, context);
            flow = LythonRuntime.ExecuteStatements(body, context);
        }
        catch (LythonRuntime.ReturnSignal)
        {
            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            throw;
        }
        catch (LythonRuntime.ControlSignal)
        {
            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            throw;
        }
        catch (LythonRuntimeException ex)
        {
            var previous = context.Services.CurrentException;
            var active = LythonRuntime.CreatePythonExceptionInstance(ex);
            context.Services.SetCurrentException(active);
            try
            {
                if (!protocol.Exit(LythonRuntime.ResolvePythonExceptionType(ex, context), active, PyNone.Instance)) throw;
            }
            catch (LythonRuntimeException cleanup)
            {
                if (!ReferenceEquals(cleanup, ex) && !ReferenceEquals(cleanup.OriginalPythonException, active))
                    cleanup.PythonContext ??= active;
                throw;
            }
            finally { context.Services.SetCurrentException(previous); }
            return PyNone.Instance;
        }

        if (flow.Control is not null)
        {
            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            throw flow.Control;
        }

        if (flow.Return is not null)
        {
            _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
            throw flow.Return;
        }

        _ = protocol.Exit(PyNone.Instance, PyNone.Instance, PyNone.Instance);
        return PyNone.Instance;
    }

    public static async ValueTask<object> ExecuteWithAsync(
        object manager,
        LoweredStoreTarget? target,
        IReadOnlyList<LoweredStatement> body,
        LythonSourceSpan span,
        LythonSourceSpan expressionSpan,
        LythonRuntime.ExecutionContext context)
    {
        var protocol = await ResolveAsync(manager, expressionSpan, context).ConfigureAwait(false);
        var entered = await EnterAsync(protocol, span, context).ConfigureAwait(false);

        LythonRuntime.LoweredBlockFlow flow;
        try
        {
            if (target is not null) await LythonRuntime.AssignTargetAsync(target, entered, context).ConfigureAwait(false);
            flow = await LythonRuntime.ExecuteStatementsAsync(body, context).ConfigureAwait(false);
        }
        catch (LythonRuntime.ReturnSignal)
        {
            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            throw;
        }
        catch (LythonRuntime.ControlSignal)
        {
            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            throw;
        }
        catch (LythonRuntimeException ex)
        {
            var previous = context.Services.CurrentException;
            var active = LythonRuntime.CreatePythonExceptionInstance(ex);
            context.Services.SetCurrentException(active);
            try
            {
                if (!await ExitAsync(protocol, LythonRuntime.ResolvePythonExceptionType(ex, context), active,
                    PyNone.Instance, span, context).ConfigureAwait(false)) throw;
            }
            catch (LythonRuntimeException cleanup)
            {
                if (!ReferenceEquals(cleanup, ex) && !ReferenceEquals(cleanup.OriginalPythonException, active))
                    cleanup.PythonContext ??= active;
                throw;
            }
            finally { context.Services.SetCurrentException(previous); }
            return PyNone.Instance;
        }

        if (flow.Control is not null)
        {
            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            throw flow.Control;
        }

        if (flow.Return is not null)
        {
            _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
            throw flow.Return;
        }

        _ = await ExitAsync(protocol, PyNone.Instance, PyNone.Instance, PyNone.Instance, span, context).ConfigureAwait(false);
        return PyNone.Instance;
    }

    public static IPyContextManager Resolve(object target, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ResolveCoreAsync(target, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<IPyContextManager> ResolveAsync(object target, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ResolveCoreAsync(target, span, context, true);

    private static async ValueTask<IPyContextManager> ResolveCoreAsync(object target, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (target is IPyContextManager manager) return manager;
        if (target is not PyInstance instance || !instance.Type.TryLookupInMro("__enter__", 0, out var enterSlot, out _))
            throw MissingProtocol(target, span);
        // Special methods resolve on the type, bypassing instance attributes and
        // __getattribute__. Descriptor effects keep Python's enter/exit order.
        var enter = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, enterSlot, context, span).ConfigureAwait(false)
            : PyAttributeLookup.BindForInstance(instance, enterSlot, context, span);
        if (!instance.Type.TryLookupInMro("__exit__", 0, out var exitSlot, out _)) throw MissingProtocol(target, span);
        var exit = asynchronous ? await PyAttributeLookup.BindForInstanceAsync(instance, exitSlot, context, span).ConfigureAwait(false)
            : PyAttributeLookup.BindForInstance(instance, exitSlot, context, span);
        return new CallableContextManager(enter, exit, span, context);
    }

    private static LythonRuntimeException MissingProtocol(object target, LythonSourceSpan span)
        => RuntimeErrors.Type("'" + RuntimeErrors.OperandTypeName(target) + "' object does not support the context manager protocol", span);

    private sealed class CallableContextManager(object enter, object exit, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context) : IPyAsyncContextManager
    {
        public object Enter() => LythonRuntime.InvokeCallableTarget(enter, span, span, context, static () => []);
        public ValueTask<object> EnterAsync() => LythonRuntime.InvokeCallableTargetAsync(enter, span, span, context, Array.Empty<CallArgumentValue>());

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionValue is PyException exception) exception.Adopt(context, span);
            if (exit is not LythonRuntime.ICallable callable) throw LythonRuntime.NotCallableError(exit, context, span);
            var result = CallableInvocation.InvokeTernary(callable, exceptionType, exceptionValue, traceback, span, context);
            // Normal cleanup ignores the result, including user truth hooks.
            return exceptionValue is not PyNone && LythonRuntime.IsTruthy(result, context, span);
        }

        public async ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            if (exceptionValue is PyException exception) exception.Adopt(context, span);
            if (exit is not LythonRuntime.ICallable callable) throw LythonRuntime.NotCallableError(exit, context, span);
            var result = await CallableInvocation.InvokeTernaryAsync(callable, exceptionType, exceptionValue, traceback, span, context).ConfigureAwait(false);
            return exceptionValue is not PyNone && await LythonRuntime.IsTruthyAsync(result, context, span).ConfigureAwait(false);
        }
    }

    internal static ValueTask<object> EnterAsync(IPyContextManager manager, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => manager is IPyAsyncContextManager asyncManager ? asyncManager.EnterAsync() : ValueTask.FromResult(manager.Enter());

    internal static ValueTask<bool> ExitAsync(IPyContextManager manager, object exceptionType, object exceptionValue,
        object traceback, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => manager is IPyAsyncContextManager asyncManager ? asyncManager.ExitAsync(exceptionType, exceptionValue, traceback)
            : ValueTask.FromResult(manager.Exit(exceptionType, exceptionValue, traceback));
}
