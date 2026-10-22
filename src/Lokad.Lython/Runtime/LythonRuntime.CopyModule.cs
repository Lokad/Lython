using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class CopyModule : PyModule
    {
        public static readonly CopyModule Instance = new();
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
                "dispatch_table" => new PyDict(),
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
            using var memo = arguments.Length == 2 && arguments[1] is not PyNone
                ? CopyMemo.FromExternal(arguments[1], context, span) : new CopyMemo(context, span);
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
                return PyDataclass.ReplaceInstance(instance, arguments[1..], span, context, "copy.replace");
            if (target is PyNamedTupleObject or PyTypingNamedTupleObject && PyMemberAccess.TryResolve(target, "_replace", context, span, out var replace) && replace is ICallable native)
                return asynchronous ? await native.InvokeAsync(arguments[1..], span, context).ConfigureAwait(false) : native.Invoke(arguments[1..], span, context);
            throw RuntimeErrors.Type("copy.replace expects a dataclass, namedtuple, or class with __replace__.", span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<built-in function replace>");
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CopyMemo : IDisposable
    {
        private readonly Dictionary<object, object> _references = new(ReferenceEqualityComparer.Instance);
        // Clear the local reference on disposal. An exposed view's storage
        // remains conservatively charged to the execution owner.
        private PyDict? _external;
        private readonly MemoryGovernor.TemporaryMemoryReservation? _scratch;
        private readonly LythonSourceSpan? _span;
        private readonly bool _trustExternalView;
        private readonly MemoryGovernor _governor;
        private readonly ExecutionState _state;
        private long _ownedEntryBytes;
        private bool _viewExposed;
        private bool _disposed;
        // One memo entry retains a CLR map slot, a view-dict entry and an
        // identity key. Both internal and user-supplied memos cover the CLR
        // mirror with transient scratch that releases when the copy completes;
        // user-supplied dicts additionally keep durable ownership of their own
        // entries.
        private const long MemoEntryBytes = 128;

        public CopyMemo(ExecutionContext context, LythonSourceSpan span)
        {
            _external = new PyDict();
            _scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            _span = span;
            _trustExternalView = false;
            _governor = context.MemoryGovernor;
            _state = context.State;
        }

        private CopyMemo(PyDict external, ExecutionContext context, LythonSourceSpan span)
        {
            _external = external;
            _scratch = context.MemoryGovernor.ReserveTemporary(0, span);
            _span = span;
            _trustExternalView = true;
            _governor = context.MemoryGovernor;
            _state = context.State;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scratch?.Dispose();
            _references.Clear();
            _external = null;
            if (_trustExternalView || _ownedEntryBytes == 0)
            {
                return;
            }

            if (!_viewExposed)
            {
                // No hook received the private view: its entries cannot escape.
                _governor.Release(_ownedEntryBytes);
                _ownedEntryBytes = 0;
                return;
            }

            // A hook may retain the view. Keep its entries charged until the
            // execution ends, independently of later CLR reachability.
        }

        // Hooks may retain this view; exposing it makes its entries durable
        // for the batch. User-supplied dicts own their own storage.
        public PyDict ExternalView
        {
            get
            {
                _viewExposed = true;
                return LiveView;
            }
        }

        private PyDict LiveView
        {
            get
            {
                if (_disposed || _external is null)
                {
                    throw new ObjectDisposedException(nameof(CopyMemo));
                }

                return _external;
            }
        }

        public static CopyMemo FromExternal(object value, ExecutionContext context, LythonSourceSpan span)
        {
            if (value is not PyDict dict)
            {
                throw new LythonRuntimeException("TypeError", "copy.deepcopy(..., memo=...) expects a dict or None.", span);
            }

            return new CopyMemo(dict, context, span);
        }

        public bool TryGet(object original, [MaybeNullWhen(false)] out object copied)
        {
            if (_references.TryGetValue(original, out copied))
            {
                return true;
            }

            if (!_trustExternalView)
            {
                copied = PyNone.Instance;
                return false;
            }

            // External views are keyed by the shared stable identity scheme,
            // so user-supplied entries (including pre-seeded ones) resolve
            // exactly like CPython: distinct live objects never collide, and
            // the exact per-call map above keeps shielding the copy itself.
            if (LiveView.TryGetValue(_state.GetObjectId(original), out copied))
            {
                _references[original] = copied;
                return true;
            }

            copied = PyNone.Instance;
            return false;
        }

        public void Remember(object original, object copied)
        {
            // Mint the stable identity before retaining anything: a denial
            // strands nothing, and the key doubles as the guest-visible memo
            // entry like the id-keyed memo in CPython.
            var key = _state.GetObjectId(original);
            if (!_trustExternalView)
            {
                // Reserve before the view retains anything: a denied entry
                // fails the copy instead of growing uncharged storage.
                // Disposal refunds entries only if no hook received the view.
                _governor.Reserve(MemoEntryBytes, _span);
                _governor.Commit(MemoEntryBytes);
                _ownedEntryBytes += MemoEntryBytes;
            }

            _scratch?.Grow(MemoEntryBytes, _span);
            _references[original] = copied;
            LiveView.SetItem(key, copied);
        }
    }

    internal enum CopyDepth
    {
        Shallow,
        Deep
    }

    private static async ValueTask<object> CopyValueAsync(
        object value,
        CopyDepth depth,
        ExecutionContext context,
        LythonSourceSpan span,
        CopyMemo memo,
        int graphDepth, bool asynchronous)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw RuntimeErrors.Attributed("RecursionError", "maximum recursion depth exceeded", span, LythonFailureReason.InterpreterStackLimit);
        }

        if (value is PyList or PyDict or PySet or PyDefaultDict or PyCounter or PyDeque or PyTuple or PyInstance)
        {
            if (memo.TryGet(value, out var existing))
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
                    memo.Remember(value, copied);
                }

                return copied;
            }

            await RejectUnsupportedCopyProtocolsAsync(instance, context, span, asynchronous).ConfigureAwait(false);

            var clone = new PyInstance(instance.Type, context.MemoryGovernor, span);
            memo.Remember(value, clone);
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

    private static async ValueTask<object> CopyTupleAsync(PyTuple tuple, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        using var backing = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(tuple.Count), span);
        var items = new object[tuple.Count];
        var clone = PyTuple.FromReservedOwnedArray(items, context.MemoryGovernor, span, backing);
        memo.Remember(tuple, clone);
        var changed = false;
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = await CopyValueAsync(tuple[i], CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false);
            changed |= !ReferenceEquals(items[i], tuple[i]);
        }

        if (!changed)
        {
            memo.Remember(tuple, tuple);
            return tuple;
        }

        return clone;
    }

    private static async ValueTask<object> CopyListAsync(PyList list, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyList([], context.MemoryGovernor, span);
        memo.Remember(list, clone);
        foreach (var item in list)
        {
            clone.Add(depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyDictAsync(PyDict dict, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDict(context.MemoryGovernor, span);
        memo.Remember(dict, clone);
        foreach (var pair in dict)
        {
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopySetAsync(PySet set, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PySet(context.MemoryGovernor, span);
        memo.Remember(set, clone);
        foreach (var item in set)
        {
            clone.Add(ValidateSetItem(
                depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item,
                span));
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyDefaultDictAsync(PyDefaultDict dict, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDefaultDict(dict.DefaultFactory, context.MemoryGovernor, span);
        memo.Remember(dict, clone);
        foreach (var pair in dict.Items)
        {
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyCounterAsync(PyCounter counter, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyCounter(context.MemoryGovernor, span);
        memo.Remember(counter, clone);
        foreach (var pair in counter.Items)
        {
            var key = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Key, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Key;
            var value = depth == CopyDepth.Deep ? await CopyValueAsync(pair.Value, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : pair.Value;
            clone.SetItem(ValidateDictionaryKey(key, span), value);
            context.CheckExecution(span);
        }

        return clone;
    }

    private static async ValueTask<object> CopyDequeAsync(PyDeque deque, CopyDepth depth, ExecutionContext context, LythonSourceSpan span, CopyMemo memo, int graphDepth, bool asynchronous)
    {
        var clone = new PyDeque(deque.MaxLength, context.MemoryGovernor, span);
        memo.Remember(deque, clone);
        foreach (var item in deque)
        {
            clone.Append(depth == CopyDepth.Deep ? await CopyValueAsync(item, CopyDepth.Deep, context, span, memo, graphDepth + 1, asynchronous).ConfigureAwait(false) : item);
        }
        context.CheckExecution(span);
        return clone;
    }

    private static async ValueTask<(bool Found, object Value)> TryInvokeCopyHookAsync(
        PyInstance instance, CopyDepth depth, ExecutionContext context, LythonSourceSpan span,
        CopyMemo memo, bool asynchronous)
    {
        var name = depth == CopyDepth.Deep ? "__deepcopy__" : "__copy__";
        var member = asynchronous ? await TryResolveRuntimeMemberAsync(instance, name, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(instance, name, context, span, out var value) ? (true, value) : (false, (object)PyNone.Instance);
        if (!member.Item1) return (false, PyNone.Instance);
        if (member.Item2 is not ICallable callable) throw RuntimeErrors.Type($"{name} must be callable.", span);
        var copied = depth == CopyDepth.Deep
            ? asynchronous ? await CallableInvocation.InvokeUnaryAsync(callable, memo.ExternalView, span, context).ConfigureAwait(false)
                : CallableInvocation.InvokeUnary(callable, memo.ExternalView, span, context)
            : asynchronous ? await callable.InvokeAsync([], span, context).ConfigureAwait(false) : callable.Invoke([], span, context);
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
