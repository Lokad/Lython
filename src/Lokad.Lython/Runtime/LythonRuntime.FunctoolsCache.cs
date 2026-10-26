using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class LruCacheFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly LruCacheFactory Instance = new();
        public string Name => "functools.lru_cache";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var bound = CallBinder.BindNamedArgumentsWithPresence(arguments, span, LythonKnownCallableSignatures.FunctoolsLruCache, PythonCallableKind.Builtin);
            var maxSize = bound.Assigned[0] ? bound.Values[0] : new BigInteger(128);
            var typed = bound.Assigned[1] ? bound.Values[1] : false;
            if (IsCallable(maxSize) && typed is bool)
                return await CreateCacheWrapperAsync((ICallable)maxSize, 128, typed, context, span, asynchronous).ConfigureAwait(false);
            return new LruCacheDecorator(ParseCacheMaxSize(maxSize, span), typed);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function lru_cache>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CacheFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CacheFactory Instance = new();
        public string Name => "functools.cache";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || !IsCallable(arguments[0].Value))
                throw RuntimeErrors.Type("functools.cache(user_function) expects one callable argument.", span);
            return CreateCacheWrapperAsync((ICallable)arguments[0].Value, null, false, context, span, asynchronous);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function cache>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class LruCacheDecorator(int? maxSize, object typed) : ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || !IsCallable(arguments[0].Value))
                throw RuntimeErrors.Type("functools.lru_cache(...)(user_function) expects one callable argument.", span);
            return CreateCacheWrapperAsync((ICallable)arguments[0].Value, maxSize, typed, context, span, asynchronous);
        }
        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.lru_cache decorator>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private enum CacheKeyMode
    {
        ValuesOnly,
        ValuesAndTypes
    }

}
