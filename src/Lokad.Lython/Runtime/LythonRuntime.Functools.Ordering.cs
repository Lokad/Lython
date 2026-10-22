using System.Numerics;
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

        public object Compare(PyCmpKey other, string method, LythonSourceSpan span, ExecutionContext context)
            => EvaluateBinaryOperator(ComparisonOperation(method),
                CallableInvocation.InvokeBinary(Comparer, Value, other.Value, span, context), BigInteger.Zero, context, span);

        public async ValueTask<object> CompareAsync(PyCmpKey other, string method, LythonSourceSpan span, ExecutionContext context)
        {
            var result = await CallableInvocation.InvokeBinaryAsync(Comparer, Value, other.Value, span, context).ConfigureAwait(false);
            return await EvaluateBinaryOperatorAsync(ComparisonOperation(method), result, BigInteger.Zero, context, span).ConfigureAwait(false);
        }

        private static Frontend.BinaryOperatorSyntax ComparisonOperation(string method) => method switch
        {
            "__lt__" => Frontend.BinaryOperatorSyntax.Less,
            "__le__" => Frontend.BinaryOperatorSyntax.LessEqual,
            "__gt__" => Frontend.BinaryOperatorSyntax.Greater,
            "__ge__" => Frontend.BinaryOperatorSyntax.GreaterEqual,
            "__eq__" => Frontend.BinaryOperatorSyntax.Equal,
            "__ne__" => Frontend.BinaryOperatorSyntax.NotEqual,
            _ => throw new InvalidOperationException("Invalid cmp_to_key comparison slot.")
        };

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

    private sealed class TotalOrderingMethod : IPyBindableCallable, INamedRuntimeCallable, IPyDynamicAttributes
    {
        // Generated ordering methods live in functools like their CPython
        // counterparts, which functools.total_ordering synthesizes.
        public PyString? ModuleName => LythonRuntime.ExceptionTypeValue.SharedModuleLabel("functools");

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__" && ModuleName is not null)
            {
                value = ModuleName;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public string Name => _generatedMethod switch
        {
            OrderingMethod.Lt => "__lt__",
            OrderingMethod.Le => "__le__",
            OrderingMethod.Gt => "__gt__",
            OrderingMethod.Ge => "__ge__",
            _ => throw new InvalidOperationException("Unsupported ordering method."),
        };

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
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (arguments.Length != 2 || arguments[0].IsKeyword || arguments[1].IsKeyword)
                throw new LythonRuntimeException("TypeError", "generated ordering method expects self and other.", span);
            var self = arguments[0].Value;
            var other = arguments[1].Value;
            var name = OrderingMethodName(_rootMethod);
            object result;
            if (self is PyInstance instance)
            {
                if (!instance.Type.TryLookupInMro(name, 0, out var raw, out _))
                    throw new LythonRuntimeException("AttributeError", "type has no attribute '" + name + "'", span);
                // Python calls type(self).root(self, other), bypassing reflected
                // operators and instance shadows. Class descriptors bind to None.
                var member = asynchronous
                    ? await PyAttributeLookup.BindForTypeAsync(instance.Type, raw, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForType(instance.Type, raw, context, span);
                if (member is not ICallable callable) throw NotCallableError(member, context, span);
                result = asynchronous
                    ? await CallableInvocation.InvokeBinaryAsync(callable, self, other, span, context).ConfigureAwait(false)
                    : CallableInvocation.InvokeBinary(callable, self, other, span, context);
            }
            else
            {
                // Preserve direct calls with builtin receivers and their native slots.
                if (!PyMemberAccess.TryResolve(self, name, context, span, out var member))
                    throw new LythonRuntimeException("AttributeError", "type has no attribute '" + name + "'", span);
                if (member is not ICallable callable) throw NotCallableError(member, context, span);
                result = asynchronous
                    ? await CallableInvocation.InvokeUnaryAsync(callable, other, span, context).ConfigureAwait(false)
                    : CallableInvocation.InvokeUnary(callable, other, span, context);
            }
            if (result is PyNotImplemented) return result;
            var truth = asynchronous ? await IsTruthyAsync(result, context, span).ConfigureAwait(false) : IsTruthy(result, context, span);
            bool unequal;
            switch (_rootMethod, _generatedMethod)
            {
                case (OrderingMethod.Lt, OrderingMethod.Ge):
                case (OrderingMethod.Le, OrderingMethod.Gt):
                case (OrderingMethod.Gt, OrderingMethod.Le):
                case (OrderingMethod.Ge, OrderingMethod.Lt):
                    return !truth;
                case (OrderingMethod.Lt, OrderingMethod.Le):
                case (OrderingMethod.Gt, OrderingMethod.Ge):
                    if (truth) return result;
                    unequal = false;
                    break;
                case (OrderingMethod.Le, OrderingMethod.Ge):
                case (OrderingMethod.Ge, OrderingMethod.Le):
                    if (!truth) return true;
                    unequal = false;
                    break;
                case (OrderingMethod.Lt, OrderingMethod.Gt):
                case (OrderingMethod.Gt, OrderingMethod.Lt):
                    if (truth) return false;
                    unequal = true;
                    break;
                case (OrderingMethod.Le, OrderingMethod.Lt):
                case (OrderingMethod.Ge, OrderingMethod.Gt):
                    if (!truth) return result;
                    unequal = true;
                    break;
                default: throw new InvalidOperationException("Invalid total_ordering root/generated pair.");
            }
            return asynchronous
                ? await EvaluateBinaryOperatorAsync(unequal ? Frontend.BinaryOperatorSyntax.NotEqual : Frontend.BinaryOperatorSyntax.Equal,
                    self, other, context, span).ConfigureAwait(false)
                : EvaluateBinaryOperator(unequal ? Frontend.BinaryOperatorSyntax.NotEqual : Frontend.BinaryOperatorSyntax.Equal,
                    self, other, context, span);
        }
    }

    private static object TotalOrdering(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TotalOrderingCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
    private static ValueTask<object> TotalOrderingAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => TotalOrderingCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> TotalOrderingCoreAsync(object[] arguments, LythonSourceSpan span,
        ExecutionContext context, bool asynchronous)
    {
        if (arguments.Length != 1 || arguments[0] is not PyType type)
            throw new LythonRuntimeException("TypeError", "functools.total_ordering(cls) expects one class argument.", span);
        context.TryGetBuiltinType("object", out var objectType);
        var roots = 0;
        OrderingMethod? root = null;
        foreach (var method in OrderingMethods)
        {
            context.CheckExecution(span);
            var name = OrderingMethodName(method);
            if (!type.TryLookupInMro(name, 0, out var raw, out _)) continue;
            object member;
            try
            {
                member = asynchronous
                    ? await PyAttributeLookup.BindForTypeAsync(type, raw, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForType(type, raw, context, span);
            }
            catch (LythonRuntimeException error) when (error.ExceptionType == "AttributeError")
            { member = PyNone.Instance; }
            object? defaultMember = null;
            if (objectType is not null && objectType.TryLookupInMro(name, 0, out var defaultRaw, out _))
                defaultMember = asynchronous
                    ? await PyAttributeLookup.BindForTypeAsync(objectType, defaultRaw, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForType(objectType, defaultRaw, context, span);
            if (ReferenceEquals(member, defaultMember)) continue;
            roots |= 1 << (int)method;
            root ??= method; // Lt, Le, Gt, Ge preference; inherited roots count.
        }
        if (root is null)
            throw new LythonRuntimeException("ValueError", "must define at least one ordering operation: < > <= >=", span);
        foreach (var method in OrderingMethods)
            if ((roots & (1 << (int)method)) == 0)
                type.TrySetMember(OrderingMethodName(method), new TotalOrderingMethod(root.Value, method));
        return type;
    }

    private static object Reduce(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.reduce(function, iterable[, initializer]) expects a callable, an iterable, and an optional initializer.", span);
        }

        using var enumerator = ToSequence(arguments[1], span, context).GetEnumerator();
        object accumulator;
        if (arguments.Length == 3)
        {
            accumulator = arguments[2];
        }
        else
        {
            if (!enumerator.MoveNext())
            {
                throw new LythonRuntimeException("TypeError", "reduce() of empty iterable with no initial value", span);
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

    private static async ValueTask<object> ReduceAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 2 or > 3 || arguments[0] is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", "functools.reduce(function, iterable[, initializer]) expects a callable, an iterable, and an optional initializer.", span);
        }

        await using var enumerator = ToSequenceAsync(arguments[1], span, context).GetAsyncEnumerator();
        object accumulator;
        if (arguments.Length == 3)
        {
            accumulator = arguments[2];
        }
        else
        {
            if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
            {
                throw new LythonRuntimeException("TypeError", "reduce() of empty iterable with no initial value", span);
            }

            accumulator = RuntimeValue(enumerator.Current);
        }

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            accumulator = await CallableInvocation.InvokeBinaryAsync(
                callable,
                accumulator,
                RuntimeValue(enumerator.Current),
                span,
                context).ConfigureAwait(false);
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
