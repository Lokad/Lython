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
            if (arguments.Length == 1 && arguments[0].IsPositional && arguments[0].Value is ICallable callable)
            {
                return CreateCacheWrapper(callable, maxSize: 128, CacheKeyMode.ValuesOnly, context, span);
            }

            var parameters = ParseLruCacheParameters(arguments, defaultMaxSize: 128, span);
            return new LruCacheDecorator(parameters.MaxSize, parameters.KeyMode);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function lru_cache>");
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
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.cache(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, maxSize: null, CacheKeyMode.ValuesOnly, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function cache>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class LruCacheDecorator : ICallable, IPyRenderableValue
    {
        private readonly int? _maxSize;
        private readonly CacheKeyMode _keyMode;

        public LruCacheDecorator(int? maxSize, CacheKeyMode keyMode)
        {
            _maxSize = maxSize;
            _keyMode = keyMode;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.lru_cache(...)(user_function) expects one callable argument.", span);
            }

            return CreateCacheWrapper(callable, _maxSize, _keyMode, context, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<functools.lru_cache decorator>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private enum CacheKeyMode
    {
        ValuesOnly,
        ValuesAndTypes
    }

    private readonly record struct LruCacheParameters(int? MaxSize, CacheKeyMode KeyMode);
}
