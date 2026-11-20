using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal static class PyContextManagers
{
    public static IPyContextManager Resolve(object target, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ResolveCoreAsync(target, span, context, false).GetAwaiter().GetResult();

    public static ValueTask<IPyContextManager> ResolveAsync(object target, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ResolveCoreAsync(target, span, context, true);

    private static async ValueTask<IPyContextManager> ResolveCoreAsync(object target, LythonSourceSpan span,
        LythonRuntime.ExecutionContext context, bool asynchronous)
    {
        if (target is IPyContextManager manager) return manager;
        if (target is not PyInstance instance)
        {
            // Native providers publish fixed runtime methods, with no guest
            // instance lookup or descriptor hooks (for example gzip wrappers).
            if (target is IPyDynamicAttributes native && native.TryGetMember("__enter__", out var nativeEnter) &&
                native.TryGetMember("__exit__", out var nativeExit))
                return new CallableContextManager(nativeEnter, nativeExit, span, context);
            throw MissingProtocol(target, span);
        }
        if (!instance.Type.TryLookupInMro("__enter__", 0, out var enterSlot, out _)) throw MissingProtocol(target, span);
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
