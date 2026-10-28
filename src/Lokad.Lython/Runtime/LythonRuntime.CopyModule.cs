using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class CopyModule : PyModule
    {
        private static readonly ExceptionTypeValue CopyError = new(ModuleException("copy", "Error"));

        public CopyModule() : base("copy")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "copy" => BuiltinCallable.Create(LythonKnownCallableSignatures.CopyCopy, ShallowCopy, ShallowCopyAsync),
                "deepcopy" => BuiltinCallable.Create(LythonKnownCallableSignatures.CopyDeepCopy, DeepCopy, DeepCopyAsync),
                "replace" => CopyReplaceCallable.Instance,
                "Error" or "error" => CopyError,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object ShallowCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CopyCoreAsync(arguments, CopyDepth.Shallow, context, span, false).GetAwaiter().GetResult();
        private ValueTask<object> ShallowCopyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CopyCoreAsync(arguments, CopyDepth.Shallow, context, span, true);
        private object DeepCopy(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CopyCoreAsync(arguments, CopyDepth.Deep, context, span, false).GetAwaiter().GetResult();
        private ValueTask<object> DeepCopyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CopyCoreAsync(arguments, CopyDepth.Deep, context, span, true);

        private static async ValueTask<object> CopyCoreAsync(object[] arguments, CopyDepth depth,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var maximum = depth == CopyDepth.Deep ? 2 : 1;
            if (arguments.Length < 1 || arguments.Length > maximum)
                throw RuntimeErrors.Type(depth == CopyDepth.Deep ? "copy.deepcopy(x, memo=None) expects one or two arguments." : "copy.copy(x) expects one argument.", span);
            var hasExternalMemo = arguments.Length == 2 && arguments[1] is not PyNone;
            var needsMemo = depth == CopyDepth.Deep && arguments[0] is PyList or PyDict or PySet or PyDefaultDict or PyCounter or PyDeque or PyTuple or PyInstance;
            using var memo = hasExternalMemo ? CopyMemo.FromExternal(arguments[1], context, span)
                : needsMemo ? new CopyMemo(context, span) : null;
            return await CopyValueAsync(arguments[0], depth, context, span, memo, 0, asynchronous).ConfigureAwait(false);
        }
    }

    private sealed class CopyReplaceCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly CopyReplaceCallable Instance = new();
        public string Name => "copy.replace";
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length == 0 || arguments[0].IsKeyword)
                throw RuntimeErrors.Type("copy.replace(obj, /, **changes) expects a positional object.", span);
            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(64L + 160L * arguments.Length), span);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 1; i < arguments.Length; i++)
            {
                context.CheckExecution(span);
                if (arguments[i].IsPositional) throw RuntimeErrors.Type("copy.replace accepts only one positional argument.", span);
                if (!seen.Add(arguments[i].KeywordName))
                    throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "copy.replace", arguments[i].KeywordName, span);
            }
            var target = arguments[0].Value;
            var cls = asynchronous ? await TryResolveRuntimeMemberAsync(target, "__class__", context, span).ConfigureAwait(false)
                : TryResolveRuntimeMember(target, "__class__", context, span, out var type) ? (true, type) : (false, (object)PyNone.Instance);
            if (cls.Item1)
            {
                var method = cls.Item2 is PyType owner && owner.TryLookupInMro("__replace__", 0, out var raw, out _)
                    ? (true, asynchronous ? await PyAttributeLookup.BindForTypeAsync(owner, raw, context, span).ConfigureAwait(false)
                        : PyAttributeLookup.BindForType(owner, raw, context, span))
                    : asynchronous ? await TryResolveRuntimeMemberAsync(cls.Item2, "__replace__", context, span).ConfigureAwait(false)
                        : TryResolveRuntimeMember(cls.Item2, "__replace__", context, span, out var member) ? (true, member) : (false, (object)PyNone.Instance);
                if (method.Item1)
                {
                    if (method.Item2 is not ICallable callable) throw RuntimeErrors.Type("__replace__ must be callable.", span);
                    return asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false)
                        : callable.Invoke(arguments, span, context);
                }
            }
            // Native dataclass/namedtuple operations supply the default type protocol.
            // Explicit class overrides above always take precedence.
            if (target is PyInstance instance && ReferenceEquals(cls.Item2, instance.Type) && instance.Type.DataclassFields is not null)
                return asynchronous ? await PyDataclass.ReplaceInstanceAsync(instance, arguments[1..], span, context, "copy.replace").ConfigureAwait(false)
                    : PyDataclass.ReplaceInstance(instance, arguments[1..], span, context, "copy.replace");
            if (target is PyNamedTupleObject && PyMemberAccess.TryResolve(target, "_replace", context, span, out var replace) && replace is ICallable native)
                return asynchronous ? await native.InvokeAsync(arguments[1..], span, context).ConfigureAwait(false) : native.Invoke(arguments[1..], span, context);
            throw RuntimeErrors.Type("copy.replace expects a dataclass, namedtuple, or class with __replace__.", span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function replace>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CopyMemo : IDisposable
    {
        private PyDict? _values;
        private readonly ExecutionState _state;
        private readonly bool _ownsValues;
        private bool _exposed;

        public CopyMemo(ExecutionContext context, LythonSourceSpan span)
        {
            _values = new PyDict(context.MemoryGovernor, span);
            _state = context.State;
            _ownsValues = true;
        }

        private CopyMemo(PyDict values, ExecutionContext context, LythonSourceSpan span)
        {
            values.AttachMemoryGovernor(context.MemoryGovernor, span);
            _values = values;
            _state = context.State;
        }

        public void Dispose()
        {
            var values = _values;
            _values = null;
            // No hook saw this dictionary, so no user can retain its storage.
            // Values/results retain their independent execution owners.
            if (values is not null && _ownsValues && !_exposed)
                _state.MemoryGovernor.Release(values.CommittedStorageBytes);
        }

        public PyDict ExternalView
        {
            get { _exposed = true; return Values; }
        }

        private PyDict Values => _values ?? throw new ObjectDisposedException(nameof(CopyMemo));

        public static CopyMemo FromExternal(object value, ExecutionContext context, LythonSourceSpan span)
            => value is PyDict dict ? new CopyMemo(dict, context, span)
                : throw RuntimeErrors.Type("copy.deepcopy(..., memo=...) expects a dict or None.", span);

        public bool TryGet(object original, [MaybeNullWhen(false)] out object copied)
            => Values.TryGetValue(_state.GetObjectId(original), out copied);

        public void Remember(object original, object copied)
            => Values.SetItem(_state.GetObjectId(original), copied);
    }

    internal enum CopyDepth
    {
        Shallow,
        Deep
    }

    internal static async ValueTask<object> CopyDataclassLeafAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        // Atomic native values need neither a memo nor a wrapper allocation.
        if (value is not (PyInstance or PySet or PyDefaultDict or PyCounter or PyDeque or PyMappingProxy or OpenPyxlStyleValue)) return value;
        using var memo = new CopyMemo(context, span);
        return await CopyValueAsync(value, CopyDepth.Deep, context, span, memo, 0, asynchronous).ConfigureAwait(false);
    }

    private static async ValueTask<object> CopyValueAsync(
        object value,
        CopyDepth depth,
        ExecutionContext context,
        LythonSourceSpan span,
        CopyMemo? memo,
        int graphDepth, bool asynchronous)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded", span, LythonFailureReason.InterpreterStackLimit);
        }

        if (value is PyList or PyDict or PySet or PyDefaultDict or PyCounter or PyDeque or PyTuple or PyInstance)
        {
            if (memo is not null && memo.TryGet(value, out var existing))
            {
                return existing;
            }

            // Deep graphs built iteratively never enter the interpreter-frame
            // guard, so bound CLR recursion here before descending. Shallow
            // copies never recurse past this frame.
            if (graphDepth > LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
            {
                throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded", span,
                    LythonFailureReason.InterpreterStackLimit, LythonRuntime.ExecutionLimits.MaxInterpreterDepth);
            }
        }

        if (value is PyInstance instance)
        {
            var hook = await TryInvokeCopyHookAsync(instance, depth, context, span, memo, asynchronous).ConfigureAwait(false);
            if (hook.Found)
            {
                var copied = hook.Value;
                // CPython does not memoize self-copies (if y is not x): the hook
                // runs again per occurrence instead of aliasing through the memo.
                if (!ReferenceEquals(copied, instance))
                {
                    memo?.Remember(value, copied);
                }

                return copied;
            }

            await RejectUnsupportedCopyProtocolsAsync(instance, context, span, asynchronous).ConfigureAwait(false);

            var clone = new PyInstance(instance.Type, context.MemoryGovernor, span);
            memo?.Remember(value, clone);
            if (depth == CopyDepth.Shallow)
            {
                foreach (var pair in instance.EnumerateOwnAttributes())
                    clone.SetAttribute(pair.Key, pair.Value);
                return clone;
            }

            // Like copying Python's instance state, hooks see the memoized
            // empty destination until every attribute value has been copied.
            // The temporary buffer owns only references; copied values retain
            // their independent owners even if a hook keeps a memo alias.
            var count = instance.OwnAttributeCount;
            using var scratch = context.MemoryGovernor.ReserveTemporary(checked(24L + 16L * count), span);
            var attributes = new KeyValuePair<string, object>[count];
            var index = 0;
            foreach (var pair in instance.EnumerateOwnAttributes())
            {
                attributes[index++] = new(pair.Key, await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false));
            }

            foreach (var pair in attributes)
            {
                context.CheckExecution(span);
                clone.SetAttribute(pair.Key, pair.Value);
            }

            return clone;
        }

        return value switch
        {
            PyList list => await CopyListAsync(list, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PyDict dict => await CopyDictAsync(dict, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PySet set => await CopySetAsync(set, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PyDefaultDict defaultDict => await CopyDefaultDictAsync(defaultDict, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PyCounter counter => await CopyCounterAsync(counter, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PyDeque deque => await CopyDequeAsync(deque, depth, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            PyTuple tuple when depth == CopyDepth.Deep => await CopyTupleAsync(tuple, context, span, memo, graphDepth, asynchronous).ConfigureAwait(false),
            OpenPyxlStyleValue style => style.Copy(depth, context, span),
            PyMappingProxy => throw RuntimeErrors.Type("cannot copy a mappingproxy", span),
            _ => value
        };
    }

    private static async ValueTask<object> CopyTupleAsync(PyTuple tuple, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        using var backing = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(tuple.Count), span);
        var items = new object[tuple.Count];
        var clone = PyTuple.FromReservedOwnedArray(items, context.MemoryGovernor, span, backing);
        memo?.Remember(tuple, clone);
        var changed = false;
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = await CopyValueAsync(tuple[i], CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false);
            changed |= !ReferenceEquals(items[i], tuple[i]);
        }

        if (!changed)
        {
            memo?.Remember(tuple, tuple);
            return tuple;
        }

        return clone;
    }

    private static async ValueTask<object> CopyListAsync(PyList list, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyList([], context.MemoryGovernor, span);
        memo?.Remember(list, clone);
        foreach (var item in list)
        {
            clone.Add(depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyDictAsync(PyDict dict, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDict(context.MemoryGovernor, span);
        memo?.Remember(dict, clone);
        foreach (var pair in dict)
        {
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }
    private static async ValueTask<object> CopySetAsync(PySet set, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PySet(context.MemoryGovernor, span);
        memo?.Remember(set, clone);
        foreach (var item in set)
        {
            clone.Add(ValidateSetItem(
                depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item,
                span));
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyDefaultDictAsync(PyDefaultDict dict, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDefaultDict(dict.DefaultFactory, context.MemoryGovernor, span);
        memo?.Remember(dict, clone);
        foreach (var pair in dict.Items)
        {
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyCounterAsync(PyCounter counter, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyCounter(context.MemoryGovernor, span);
        memo?.Remember(counter, clone);
        foreach (var pair in counter.Items)
        {
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }
    private static async ValueTask<object> CopyDequeAsync(PyDeque deque, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo? memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDeque(deque.MaxLength, context.MemoryGovernor, span);
        memo?.Remember(deque, clone);
        foreach (var item in deque)
        {
            clone.Append(depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item);
        }
        context.CheckExecution(span);
        return clone;
    }

    private static async ValueTask<(bool Found, object Value)> TryInvokeCopyHookAsync(
        PyInstance instance, CopyDepth depth, ExecutionContext context, LythonSourceSpan span,
        CopyMemo? memo, bool asynchronous)
    {
        var name = depth == CopyDepth.Deep ? "__deepcopy__" : "__copy__";
        (bool Found, object Value) member;
        if (depth == CopyDepth.Shallow)
        {
            if (!instance.Type.TryLookupInMro(name, 0, out var raw, out _)) return (false, PyNone.Instance);
            member = (true, asynchronous ? await PyAttributeLookup.BindForTypeAsync(instance.Type, raw, context, span).ConfigureAwait(false)
                : PyAttributeLookup.BindForType(instance.Type, raw, context, span));
        }
        else member = asynchronous ? await TryResolveRuntimeMemberAsync(instance, name, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(instance, name, context, span, out var value) ? (true, value) : (false, PyNone.Instance);
        if (!member.Found || member.Value is PyNone) return (false, PyNone.Instance);
        if (member.Value is not ICallable callable) throw NotCallableError(member.Value, context, span);
        // The shallow hook is obtained from the type and receives the source;
        // the deep hook is already bound to the instance and receives the memo.
        var argument = depth == CopyDepth.Deep ? (object)memo.RequireNotNull().ExternalView : instance;
        var copied = asynchronous ? await CallableInvocation.InvokeUnaryAsync(callable, argument, span, context).ConfigureAwait(false)
            : CallableInvocation.InvokeUnary(callable, argument, span, context);
        context.CheckExecution(span);
        return (true, copied);
    }

    private static async ValueTask RejectUnsupportedCopyProtocolsAsync(PyInstance instance, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        foreach (var name in CopyProtocolFacts.UnsupportedReductionHooks)
        {
            var member = asynchronous ? await TryResolveRuntimeMemberAsync(instance, name, context, span).ConfigureAwait(false)
                : TryResolveRuntimeMember(instance, name, context, span, out var value) ? (true, value) : (false, (object)PyNone.Instance);
            if (member.Item1 && member.Item2 is not PyNone)
                throw new LythonRuntimeException("NotImplementedError", $"copy protocol {name} is not supported by Lython; define __copy__ or __deepcopy__ instead.", span);
        }
    }
}
