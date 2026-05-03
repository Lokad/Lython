using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class OperatorModule : PyModule
    {
        public static readonly OperatorModule Instance = new();

        private OperatorModule() : base("operator")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "add" => new BuiltinCallable("operator.add", (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateAdd(left, right, context, innerSpan)), ["a", "b"]),
                "sub" => new BuiltinCallable("operator.sub", (arguments, span, _) => Binary(arguments, span, EvaluateSubtract), ["a", "b"]),
                "mul" => new BuiltinCallable("operator.mul", (arguments, span, context) => Binary(arguments, span, (left, right, innerSpan) => EvaluateMultiply(left, right, context, innerSpan)), ["a", "b"]),
                "truediv" => new BuiltinCallable("operator.truediv", (arguments, span, _) => Binary(arguments, span, EvaluateDivide), ["a", "b"]),
                "eq" => new BuiltinCallable("operator.eq", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => AreEqual(left, right)), ["a", "b"]),
                "ne" => new BuiltinCallable("operator.ne", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => !AreEqual(left, right)), ["a", "b"]),
                "lt" => new BuiltinCallable("operator.lt", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) < 0), ["a", "b"]),
                "le" => new BuiltinCallable("operator.le", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) <= 0), ["a", "b"]),
                "gt" => new BuiltinCallable("operator.gt", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) > 0), ["a", "b"]),
                "ge" => new BuiltinCallable("operator.ge", (arguments, span, _) => CompareBool(arguments, span, static (left, right, innerSpan) => Compare(left, right, innerSpan) >= 0), ["a", "b"]),
                "getitem" => new BuiltinCallable("operator.getitem", GetItem, ["obj", "key"]),
                "setitem" => new BuiltinCallable("operator.setitem", SetItem, ["obj", "key", "value"]),
                "contains" => new BuiltinCallable("operator.contains", ContainsValue, ["obj", "value"]),
                "itemgetter" => new OperatorFactoryCallable("operator.itemgetter", CreateItemGetter),
                "attrgetter" => new OperatorFactoryCallable("operator.attrgetter", CreateAttrGetter),
                "methodcaller" => new OperatorFactoryCallable("operator.methodcaller", CreateMethodCaller),
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed class OperatorFactoryCallable : ICallable
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public OperatorFactoryCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _name = name;
            _implementation = implementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }

        public override string ToString() => _name;
    }

    private sealed class PyItemGetter : ICallable, IPyRenderableValue
    {
        private readonly object[] _items;

        public PyItemGetter(object[] items)
        {
            _items = items;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "operator.itemgetter(...)(obj) expects one positional argument.", span);
            }

            var target = arguments[0].Value;
            if (_items.Length == 1)
            {
                return ReadItem(target, _items[0], span, context);
            }

            var values = new object[_items.Length];
            for (var i = 0; i < _items.Length; i++)
            {
                values[i] = ReadItem(target, _items[i], span, context);
            }

            return new PyTuple(values, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<operator.itemgetter>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyAttrGetter : ICallable, IPyRenderableValue
    {
        private readonly string[][] _paths;

        public PyAttrGetter(string[][] paths)
        {
            _paths = paths;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(...)(obj) expects one positional argument.", span);
            }

            var target = arguments[0].Value;
            if (_paths.Length == 1)
            {
                return ReadPath(target, _paths[0], span, context);
            }

            var values = new object[_paths.Length];
            for (var i = 0; i < _paths.Length; i++)
            {
                values[i] = ReadPath(target, _paths[i], span, context);
            }

            return new PyTuple(values, context.MemoryGovernor, span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<operator.attrgetter>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private static object ReadPath(object target, IReadOnlyList<string> path, LythonSourceSpan span, ExecutionContext context)
        {
            var current = target;
            foreach (var part in path)
            {
                if (!PyMemberAccess.TryResolve(current, part, context, span, out current))
                {
                    throw PyMemberAccess.CreateMissingMemberError(current, part, span);
                }
            }

            return current;
        }
    }

    private sealed class PyMethodCaller : ICallable, IPyRenderableValue
    {
        private readonly string _name;
        private readonly CallArgumentValue[] _arguments;

        public PyMethodCaller(string name, CallArgumentValue[] arguments)
        {
            _name = name;
            _arguments = arguments;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1 || arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "operator.methodcaller(...)(obj) expects one positional argument.", span);
            }

            if (!PyMemberAccess.TryResolve(arguments[0].Value, _name, context, span, out var member))
            {
                throw PyMemberAccess.CreateMissingMemberError(arguments[0].Value, _name, span);
            }

            return InvokeCallableTarget(member, span, span, context, () => _arguments);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<operator.methodcaller>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static object Binary(object[] arguments, LythonSourceSpan span, Func<object, object, LythonSourceSpan, object> operation)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator function expects two arguments.", span);
        }

        return operation(arguments[0], arguments[1], span);
    }

    private static object CompareBool(object[] arguments, LythonSourceSpan span, Func<object, object, LythonSourceSpan, bool> operation)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator comparison expects two arguments.", span);
        }

        return operation(arguments[0], arguments[1], span);
    }

    private static object GetItem(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.getitem(obj, key) expects two arguments.", span);
        }

        return ReadItem(arguments[0], arguments[1], span, context);
    }

    private static object ReadItem(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span, context.MemoryGovernor), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static object SetItem(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 3)
        {
            throw new LythonRuntimeException("TypeError", "operator.setitem(obj, key, value) expects three arguments.", span);
        }

        var target = arguments[0];
        var index = arguments[1];
        var value = arguments[2];

        switch (target)
        {
            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, span), value);
                return PyNone.Instance;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                dict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(dict.Count, span);
                return PyNone.Instance;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                defaultDict.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(defaultDict.Count, span);
                return PyNone.Instance;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, span);
                counter.SetItem(ValidateDictionaryKey(index, span, context.MemoryGovernor), value);
                context.ObserveCollectionCount(counter.Count, span);
                return PyNone.Instance;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", span);
        }
    }

    private static object ContainsValue(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "operator.contains(obj, value) expects two arguments.", span);
        }

        return Contains(arguments[0], arguments[1], span);
    }

    private static object CreateItemGetter(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "operator.itemgetter(item[, ...]) expects one or more positional arguments.", span);
        }

        var items = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "operator.itemgetter(item[, ...]) expects one or more positional arguments.", span);
            }

            items[i] = RuntimeValue(arguments[i].Value);
        }

        return new PyItemGetter(items);
    }

    private static object CreateAttrGetter(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects one or more positional string arguments.", span);
        }

        var paths = new string[arguments.Length][];
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            if (argument.Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects one or more positional string arguments.", span);
            }

            if (!PyStringOps.TryAsString(argument.Value, out var text))
            {
                throw new LythonRuntimeException("TypeError", "operator.attrgetter(attr[, ...]) expects string arguments.", span);
            }

            paths[i] = text.AsString().Split('.', StringSplitOptions.RemoveEmptyEntries);
        }

        return new PyAttrGetter(paths);
    }

    private static object CreateMethodCaller(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length == 0 || arguments[0].Name is not null || !PyStringOps.TryAsString(arguments[0].Value, out var name))
        {
            throw new LythonRuntimeException("TypeError", "operator.methodcaller(name, ...) expects the first argument to be a method name string.", span);
        }

        return new PyMethodCaller(name.AsString(), arguments[1..]);
    }
}
