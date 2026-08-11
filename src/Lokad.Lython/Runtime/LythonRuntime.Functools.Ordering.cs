using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyCmpKeyFactory : ICallable, IPyRenderableValue
    {
        private readonly ICallable _comparer;

        public PyCmpKeyFactory(ICallable comparer)
        {
            _comparer = comparer;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "functools.cmp_to_key(cmp)(value) expects one positional argument.", span);
            }

            return new PyCmpKey(_comparer, arguments[0].Value);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.KeyWrapper>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyCmpKey : IPyRenderableValue
    {
        public PyCmpKey(ICallable comparer, object value)
        {
            Comparer = comparer;
            Value = value;
        }

        public ICallable Comparer { get; }

        public object Value { get; }

        public int CompareTo(PyCmpKey other, LythonSourceSpan span, ExecutionContext context)
        {
            var result = CallableInvocation.InvokeBinary(Comparer, Value, other.Value, span, context);
            if (!Numbers.PyNumberOps.TryAsInteger(result, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "cmp_to_key comparator must return an integer.", span);
            }

            return integer.Sign;
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<functools.KeyWrapper>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private enum OrderingMethod
    {
        Lt,
        Le,
        Gt,
        Ge,
    }

    private sealed class TotalOrderingMethod : IPyBindableCallable
    {
        private readonly OrderingMethod _rootMethod;
        private readonly OrderingMethod _generatedMethod;

        public TotalOrderingMethod(OrderingMethod rootMethod, OrderingMethod generatedMethod)
        {
            _rootMethod = rootMethod;
            _generatedMethod = generatedMethod;
        }

        public object Bind(object self) => new PyBoundMethod(self, this);

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => instance is null ? this : Bind(instance);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "generated ordering method expects self and other.", span);
            }

            var self = arguments[0].Value;
            var other = arguments[1].Value;
            var equal = InvokeEq(self, other, context, span);
            return _generatedMethod switch
            {
                OrderingMethod.Lt => ComputeLt(self, other, equal, context, span),
                OrderingMethod.Le => ComputeLt(self, other, equal, context, span) || equal,
                OrderingMethod.Gt => ComputeGt(self, other, equal, context, span),
                OrderingMethod.Ge => ComputeGt(self, other, equal, context, span) || equal,
                _ => throw new InvalidOperationException("Unknown generated ordering method.")
            };
        }

        private bool ComputeLt(object self, object other, bool equal, ExecutionContext context, LythonSourceSpan span)
        {
            return _rootMethod switch
            {
                OrderingMethod.Lt => InvokeBool(self, OrderingMethod.Lt, other, context, span),
                OrderingMethod.Le => InvokeBool(self, OrderingMethod.Le, other, context, span) && !equal,
                OrderingMethod.Gt => InvokeBool(other, OrderingMethod.Gt, self, context, span),
                OrderingMethod.Ge => InvokeBool(other, OrderingMethod.Ge, self, context, span) && !equal,
                _ => false
            };
        }

        private bool ComputeGt(object self, object other, bool equal, ExecutionContext context, LythonSourceSpan span)
        {
            return _rootMethod switch
            {
                OrderingMethod.Lt => InvokeBool(other, OrderingMethod.Lt, self, context, span),
                OrderingMethod.Le => InvokeBool(other, OrderingMethod.Le, self, context, span) && !equal,
                OrderingMethod.Gt => InvokeBool(self, OrderingMethod.Gt, other, context, span),
                OrderingMethod.Ge => InvokeBool(self, OrderingMethod.Ge, other, context, span) && !equal,
                _ => false
            };
        }

        private static bool InvokeEq(object self, object other, ExecutionContext context, LythonSourceSpan span)
            => InvokeNamedBool(self, "__eq__", other, context, span);

        private static bool InvokeBool(object self, OrderingMethod method, object other, ExecutionContext context, LythonSourceSpan span)
            => InvokeNamedBool(self, OrderingMethodName(method), other, context, span);

        private static bool InvokeNamedBool(object self, string methodName, object other, ExecutionContext context, LythonSourceSpan span)
        {
            if (!PyMemberAccess.TryResolve(self, methodName, context, span, out var member) || member is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", $"Object has no callable '{methodName}' method.", span);
            }

            var result = CallableInvocation.InvokeUnary(callable, other, span, context);
            return result switch
            {
                bool boolean => boolean,
                _ => throw new LythonRuntimeException("TypeError", $"'{methodName}' must return bool.", span)
            };
        }

    }

    private static object TotalOrdering(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not PyType type)
        {
            throw new LythonRuntimeException("TypeError", "functools.total_ordering(cls) expects one class argument.", span);
        }

        if (!type.TryGetOwnMember("__eq__", out _))
        {
            throw new LythonRuntimeException("TypeError", "functools.total_ordering requires __eq__ to be defined.", span);
        }

        var root = GetRootOrderingMethod(type, span);
        foreach (var method in OrderingMethods)
        {
            var methodName = OrderingMethodName(method);
            if (method == root || type.TryGetOwnMember(methodName, out _))
            {
                continue;
            }

            type.TrySetMember(methodName, new TotalOrderingMethod(root, method));
        }

        return type;
    }

    private static OrderingMethod GetRootOrderingMethod(PyType type, LythonSourceSpan span)
    {
        if (type.TryGetOwnMember("__lt__", out _))
        {
            return OrderingMethod.Lt;
        }

        if (type.TryGetOwnMember("__le__", out _))
        {
            return OrderingMethod.Le;
        }

        if (type.TryGetOwnMember("__gt__", out _))
        {
            return OrderingMethod.Gt;
        }

        if (type.TryGetOwnMember("__ge__", out _))
        {
            return OrderingMethod.Ge;
        }

        throw new LythonRuntimeException("TypeError", "functools.total_ordering requires one of __lt__, __le__, __gt__, or __ge__.", span);
    }

    private static object Reduce(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.reduce(function, iterable[, initializer]) expects a callable, an iterable, and an optional initializer.", span);
        }

        using var enumerator = ToSequence(arguments[1], span).GetEnumerator();
        object accumulator;
        if (arguments.Length == 3)
        {
            accumulator = arguments[2];
        }
        else
        {
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "functools.reduce() of empty sequence with no initial value.", span);
            }

            accumulator = RuntimeValue(enumerator.Current);
        }

        while (enumerator.MoveNext())
        {
            accumulator = CallableInvocation.InvokeBinary(
                callable,
                accumulator,
                RuntimeValue(enumerator.Current),
                span,
                context);
        }

        return accumulator;
    }

    private static object CmpToKey(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 1 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.cmp_to_key(mycmp) expects one callable argument.", span);
        }

        return new PyCmpKeyFactory(callable);
    }

    private static string OrderingMethodName(OrderingMethod method)
    {
        return method switch
        {
            OrderingMethod.Lt => "__lt__",
            OrderingMethod.Le => "__le__",
            OrderingMethod.Gt => "__gt__",
            OrderingMethod.Ge => "__ge__",
            _ => throw new InvalidOperationException("Unknown ordering method.")
        };
    }

}
