using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class CopyModule : PyModule
    {
        public static readonly CopyModule Instance = new();

        private CopyModule() : base("copy")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "copy" => new BuiltinCallable("copy.copy", ShallowCopy, ["x"]),
                "deepcopy" => new BuiltinCallable("copy.deepcopy", DeepCopy, ["x"]),
                "Error" => new ExceptionTypeValue("Error"),
                _ => null!,
            };

            return value is not null;
        }

        private object ShallowCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "copy.copy(x) expects one argument.", span);
            }

            return CopyValue(arguments[0], deep: false, context, span, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));
        }

        private object DeepCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "copy.deepcopy(x) expects one argument.", span);
            }

            return CopyValue(arguments[0], deep: true, context, span, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));
        }
    }

    private static object CopyValue(object value, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        if (value is PyList or PyDict or PySet or PyDefaultDict or PyCounter or PyDeque or PyInstance)
        {
            if (memo.TryGetValue(value, out var existing))
            {
                return existing;
            }
        }

        if (value is PyInstance instance)
        {
            if (TryInvokeCopyHook(instance, deep, context, span, memo, out var copied))
            {
                memo[value] = copied;
                return copied;
            }

            var clone = new PyInstance(instance.Type);
            memo[value] = clone;
            foreach (var pair in instance.EnumerateOwnAttributes())
            {
                clone.SetAttribute(pair.Key, deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value);
            }

            return clone;
        }

        return value switch
        {
            PyList list => CopyList(list, deep, context, span, memo),
            PyDict dict => CopyDict(dict, deep, context, span, memo),
            PySet set => CopySet(set, deep, context, span, memo),
            PyDefaultDict defaultDict => CopyDefaultDict(defaultDict, deep, context, span, memo),
            PyCounter counter => CopyCounter(counter, deep, context, span, memo),
            PyDeque deque => CopyDeque(deque, deep, context, span, memo),
            PyTuple tuple when deep => CopyTuple(tuple, context, span, memo),
            _ => value
        };
    }

    private static object CopyTuple(PyTuple tuple, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var items = new object[tuple.Count];
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = CopyValue(tuple[i], deep: true, context, span, memo);
        }

        return new PyTuple(items, context.MemoryGovernor, span);
    }

    private static object CopyList(PyList list, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var clone = new PyList([], context.MemoryGovernor, span);
        memo[list] = clone;
        foreach (var item in list)
        {
            clone.Add(deep ? CopyValue(item, deep: true, context, span, memo) : item);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDict(PyDict dict, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var clone = new PyDict(context.MemoryGovernor, span);
        memo[dict] = clone;
        foreach (var pair in dict)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopySet(PySet set, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var clone = new PySet(context.MemoryGovernor, span);
        memo[set] = clone;
        foreach (var item in set)
        {
            clone.Add(ValidateSetItem(deep ? CopyValue(item, deep: true, context, span, memo) : item, span));
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDefaultDict(PyDefaultDict dict, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var items = new PyDict(context.MemoryGovernor, span);
        var clone = new PyDefaultDict(dict.DefaultFactory, items);
        memo[dict] = clone;
        foreach (var pair in dict.Items)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyCounter(PyCounter counter, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var clone = new PyCounter(context.MemoryGovernor, span);
        memo[counter] = clone;
        foreach (var pair in counter.Items)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDeque(PyDeque deque, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo)
    {
        var items = new object[deque.Count];
        var index = 0;
        foreach (var item in deque)
        {
            items[index++] = deep ? CopyValue(item, deep: true, context, span, memo) : item;
        }
        var clone = new PyDeque(items);
        memo[deque] = clone;
        context.ObserveCollectionCount(clone.Count, span);
        return clone;
    }

    private static bool TryInvokeCopyHook(PyInstance instance, bool deep, ExecutionContext context, LythonSourceSpan span, Dictionary<object, object> memo, out object value)
    {
        var hookName = deep ? "__deepcopy__" : "__copy__";
        if (!PyMemberAccess.TryResolve(instance, hookName, context, span, out var member))
        {
            value = PyNone.Instance;
            return false;
        }

        if (member is not ICallable callable)
        {
            throw new LythonRuntimeException("TypeError", $"{hookName} must be callable.", span);
        }

        value = deep
            ? callable.Invoke([new CallArgumentValue(null, BuildMemoView(memo, context))], span, context)
            : callable.Invoke([], span, context);
        return true;
    }

    private static PyDict BuildMemoView(Dictionary<object, object> memo, ExecutionContext context)
    {
        var dict = new PyDict(context.MemoryGovernor, null);
        foreach (var pair in memo)
        {
            dict.SetItem(new BigInteger(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(pair.Key)), pair.Value);
        }

        return dict;
    }
}
