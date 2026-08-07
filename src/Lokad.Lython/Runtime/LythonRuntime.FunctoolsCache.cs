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
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length == 1 && arguments[0].Name is null && arguments[0].Value is ICallable callable)
            {
                return CreateCacheWrapper(callable, maxSize: 128, typed: false, context, span);
            }

            var parameters = ParseLruCacheParameters(arguments, defaultMaxSize: 128, span);
            return new LruCacheDecorator(parameters.MaxSize, parameters.Typed);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.lru_cache");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CacheFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CacheFactory Instance = new();

        public string Name => "functools.cache";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.cache(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, maxSize: null, typed: false, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.cache");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class LruCacheDecorator : ICallable, IPyRenderableValue
    {
        private readonly int? _maxSize;
        private readonly bool _typed;

        public LruCacheDecorator(int? maxSize, bool typed)
        {
            _maxSize = maxSize;
            _typed = typed;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.lru_cache(...)(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, _maxSize, _typed, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.lru_cache decorator>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}
