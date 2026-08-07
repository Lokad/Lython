using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class CopyModule : PyModule
    {
        public static readonly CopyModule Instance = new();
        private static readonly ExceptionTypeValue CopyError = new("Error");

        private CopyModule() : base("copy")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "copy" => new BuiltinCallable(LythonKnownCallableSignatures.CopyCopy, ShallowCopy),
                "deepcopy" => new BuiltinCallable(LythonKnownCallableSignatures.CopyDeepCopy, DeepCopy),
                "replace" => CopyReplaceCallable.Instance,
                "dispatch_table" => new PyDict(),
                "Error" or "error" => CopyError,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object ShallowCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "copy.copy(x) expects one argument.", span);
            }

            return CopyValue(arguments[0], deep: false, context, span, new CopyMemo(context, span));
        }

        private object DeepCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "copy.deepcopy(x, memo=None) expects one or two arguments.", span);
            }

            var memo = arguments.Length == 2 && !ReferenceEquals(arguments[1], PyNone.Instance)
                ? CopyMemo.FromExternal(arguments[1], context, span)
                : new CopyMemo(context, span);

            return CopyValue(arguments[0], deep: true, context, span, memo);
        }
    }

    private sealed class CopyReplaceCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CopyReplaceCallable Instance = new();

        private CopyReplaceCallable()
        {
        }

        public string Name => "copy.replace";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "copy.replace(obj, /, **changes) expects an object.", span);
            }

            if (arguments[0].Name is not null)
            {
                throw new LythonRuntimeException("TypeError", "copy.replace(obj, /, **changes) requires obj as a positional-only argument.", span);
            }

            if (arguments.Length > 1 && arguments[1].Name is null)
            {
                throw new LythonRuntimeException("TypeError", "copy.replace(obj, /, **changes) accepts only one positional argument.", span);
            }

            var changes = new List<CallArgumentValue>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 1; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "copy.replace(obj, /, **changes) accepts only keyword changes after obj.", span);
                }

                if (!seen.Add(argument.Name))
                {
                    throw CallErrors.MultipleValues("Builtin", "copy.replace", argument.Name, span);
                }

                changes.Add(argument);
            }

            var target = arguments[0].Value;
            if (target is PyInstance instance && instance.Type.DataclassFields is not null)
            {
                return PyDataclass.ReplaceInstance(instance, changes, span, context, "copy.replace");
            }

            if (PyMemberAccess.TryResolve(target, "__replace__", context, span, out var replaceMember) &&
                !ReferenceEquals(replaceMember, PyNone.Instance))
            {
                if (replaceMember is not ICallable replaceCallable)
                {
                    throw new LythonRuntimeException("TypeError", "__replace__ must be callable.", span);
                }

                return replaceCallable.Invoke(changes.ToArray(), span, context);
            }

            if (PyMemberAccess.TryResolve(target, "_replace", context, span, out var namedTupleReplace) &&
                !ReferenceEquals(namedTupleReplace, PyNone.Instance))
            {
                if (namedTupleReplace is not ICallable replaceCallable)
                {
                    throw new LythonRuntimeException("TypeError", "_replace must be callable.", span);
                }

                return replaceCallable.Invoke(changes.ToArray(), span, context);
            }

            throw new LythonRuntimeException("TypeError", "copy.replace(obj, /, **changes) expects a dataclass, namedtuple-like object, or object with __replace__.", span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("copy.replace");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CopyMemo
    {
        private readonly Dictionary<object, object> _references = new(ReferenceEqualityComparer.Instance);
        private readonly PyDict _external;

        public CopyMemo(ExecutionContext context, LythonSourceSpan span)
        {
            _external = new PyDict(context.MemoryGovernor, span);
        }

        private CopyMemo(PyDict external)
        {
            _external = external;
        }

        public PyDict ExternalView => _external;

        public static CopyMemo FromExternal(object value, ExecutionContext context, LythonSourceSpan span)
        {
            _ = context;
            if (value is not PyDict dict)
            {
                throw new LythonRuntimeException("TypeError", "copy.deepcopy(..., memo=...) expects a dict or None.", span);
            }

            return new CopyMemo(dict);
        }

        public bool TryGet(object original, [MaybeNullWhen(false)] out object copied)
        {
            if (_references.TryGetValue(original, out copied))
            {
                return true;
            }

            var key = IdentityKey(original);
            if (_external.TryGetValue(key, out copied))
            {
                _references[original] = copied;
                return true;
            }

            copied = PyNone.Instance;
            return false;
        }

        public void Remember(object original, object copied)
        {
            _references[original] = copied;
            _external.SetItem(IdentityKey(original), copied);
        }

        private static BigInteger IdentityKey(object value)
            => new(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value));
    }

    private static object CopyValue(object value, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        if (value is PyList or PyDict or PySet or PyDefaultDict or PyCounter or PyDeque or PyTuple or PyInstance)
        {
            if (memo.TryGet(value, out var existing))
            {
                return existing;
            }
        }

        if (value is PyInstance instance)
        {
            if (TryInvokeCopyHook(instance, deep, context, span, memo, out var copied))
            {
                memo.Remember(value, copied);
                return copied;
            }

            RejectUnsupportedCopyProtocols(instance, context, span);

            var clone = new PyInstance(instance.Type);
            memo.Remember(value, clone);
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
            OpenPyxlStyleValue style => style.Copy(deep),
            _ => value
        };
    }

    private static object CopyTuple(PyTuple tuple, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var items = new object[tuple.Count];
        var clone = PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
        memo.Remember(tuple, clone);
        var changed = false;
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = CopyValue(tuple[i], deep: true, context, span, memo);
            changed |= !ReferenceEquals(items[i], tuple[i]);
        }

        if (!changed)
        {
            memo.Remember(tuple, tuple);
            return tuple;
        }

        return clone;
    }

    private static object CopyList(PyList list, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var clone = new PyList([], context.MemoryGovernor, span);
        memo.Remember(list, clone);
        foreach (var item in list)
        {
            clone.Add(deep ? CopyValue(item, deep: true, context, span, memo) : item);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDict(PyDict dict, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var clone = new PyDict(context.MemoryGovernor, span);
        memo.Remember(dict, clone);
        foreach (var pair in dict)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopySet(PySet set, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var clone = new PySet(context.MemoryGovernor, span);
        memo.Remember(set, clone);
        foreach (var item in set)
        {
            clone.Add(ValidateSetItem(deep ? CopyValue(item, deep: true, context, span, memo) : item, span));
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDefaultDict(PyDefaultDict dict, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var items = new PyDict(context.MemoryGovernor, span);
        var clone = new PyDefaultDict(dict.DefaultFactory, items);
        memo.Remember(dict, clone);
        foreach (var pair in dict.Items)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyCounter(PyCounter counter, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var clone = new PyCounter(context.MemoryGovernor, span);
        memo.Remember(counter, clone);
        foreach (var pair in counter.Items)
        {
            var key = deep ? CopyValue(pair.Key, deep: true, context, span, memo) : pair.Key;
            var value = deep ? CopyValue(pair.Value, deep: true, context, span, memo) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.ObserveCollectionCount(clone.Count, span);
        }

        return clone;
    }

    private static object CopyDeque(PyDeque deque, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo)
    {
        var clone = new PyDeque(deque.MaxLength);
        memo.Remember(deque, clone);
        foreach (var item in deque)
        {
            clone.Append(deep ? CopyValue(item, deep: true, context, span, memo) : item);
        }
        context.ObserveCollectionCount(clone.Count, span);
        return clone;
    }

    private static bool TryInvokeCopyHook(PyInstance instance, bool deep, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, [MaybeNullWhen(false)] out object value)
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
            ? callable.Invoke([new CallArgumentValue(null, memo.ExternalView)], span, context)
            : callable.Invoke([], span, context);
        return true;
    }

    private static void RejectUnsupportedCopyProtocols(PyInstance instance, ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var protocol in CopyProtocolFacts.UnsupportedReductionHooks)
        {
            if (PyMemberAccess.TryResolve(instance, protocol, context, span, out var member) &&
                !ReferenceEquals(member, PyNone.Instance))
            {
                throw new LythonRuntimeException("NotImplementedError", $"copy protocol {protocol} is not supported by Lython; define __copy__ or __deepcopy__ instead.", span);
            }
        }
    }
}
