using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyPartial : ICallable, IPyRenderableValue, IPyMutableDynamicAttributes, IPyContextualDynamicAttributes, IPyGovernedValue
    {
        private readonly ICallable _callable;
        private readonly PyTuple _args;
        private PyDict? _keywords;
        private PyDict? _metadata;
        private readonly MemoryGovernor _memoryGovernor;
        private readonly LythonSourceSpan _allocationSpan;

        public PyPartial(ICallable callable, ReadOnlySpan<CallArgumentValue> arguments, ExecutionContext context, LythonSourceSpan span)
        {
            var governor = context.MemoryGovernor;
            using var shell = governor.ReserveTemporary(64, span);
            var inherited = callable is PyPartial partial && partial._metadata is null ? partial : null;
            _callable = inherited?._callable ?? callable;
            _memoryGovernor = governor;
            _allocationSpan = span;
            var previous = inherited?._args ?? PyTuple.Empty;
            PyTuple positional = PyTuple.Empty;
            PyDict? keywords = null;
            try
            {
                positional = CreatePartialArgs(previous, arguments, context, span);
                if (inherited?._keywords is { Count: > 0 } source)
                    keywords = new PyDict(source, governor, span);
                foreach (var argument in arguments)
                {
                    context.CheckExecution(span);
                    if (!argument.IsKeyword) continue;
                    keywords ??= new PyDict(governor, span);
                    StoreFunctoolsStringKey(keywords, argument.KeywordName, argument.Value, governor, span);
                }
                _args = positional;
                _keywords = keywords;
                shell.Commit(64);
            }
            catch
            {
                if (!ReferenceEquals(positional, previous)) governor.RefundUnpublishedValue(positional);
                if (keywords is not null) governor.RefundUnpublishedValue(keywords);
                throw;
            }
        }

        private static PyTuple CreatePartialArgs(PyTuple previous, ReadOnlySpan<CallArgumentValue> arguments,
            ExecutionContext context, LythonSourceSpan span)
        {
            var added = 0;
            long scalarBytes = 0;
            foreach (var argument in arguments)
            {
                context.CheckExecution(span);
                if (!argument.IsPositional) continue;
                added++;
                scalarBytes = checked(scalarBytes + RuntimeMemoryEstimates.EstimateScalarStorageBytes(argument.Value));
            }
            if (added == 0) return previous;
            var length = PartialArgumentLength((long)previous.Count + added, span);
            foreach (var value in previous)
            {
                context.CheckExecution(span);
                scalarBytes = checked(scalarBytes + RuntimeMemoryEstimates.EstimateScalarStorageBytes(value));
            }
            using var storage = context.MemoryGovernor.ReserveTemporary(checked(PyTuple.EstimateApproximateBytes(length) + scalarBytes), span);
            var values = new object[length];
            var index = 0;
            foreach (var value in previous) { context.CheckExecution(span); values[index++] = value; }
            foreach (var argument in arguments)
            {
                context.CheckExecution(span);
                if (argument.IsPositional) values[index++] = RuntimeValue(argument.Value);
            }
            return PyTuple.FromReservedOwnedArray(values, context.MemoryGovernor, span, storage, scalarBytes);
        }

        public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;
        public LythonSourceSpan? AllocationSpan => _allocationSpan;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);

        private async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (_args.Count == 0 && _keywords is not { Count: > 0 })
                return asynchronous ? await _callable.InvokeAsync(arguments, span, context).ConfigureAwait(false)
                    : _callable.Invoke(arguments, span, context);
            var keywordCount = _keywords?.Count ?? 0;
            var incomingKeywords = 0;
            foreach (var argument in arguments) { context.CheckExecution(span); if (argument.IsKeyword) incomingKeywords++; }
            var maximum = PartialArgumentLength((long)_args.Count + arguments.Length + keywordCount, span);
            long bytes = checked(32L + 32L * maximum);
            if (keywordCount != 0)
            {
                bytes = checked(bytes + 64L + 128L * (keywordCount + (long)incomingKeywords));
                foreach (var pair in _keywords!)
                {
                    context.CheckExecution(span);
                    if (pair.Key is not PyString name) throw new LythonRuntimeException("TypeError", "keywords must be strings", span);
                    bytes = checked(bytes + 32L + 2L * name.Utf8Bytes.Length);
                }
            }
            using var working = context.MemoryGovernor.ReserveTemporary(bytes, span);
            Dictionary<string, object>? merged = null;
            if (keywordCount != 0)
            {
                merged = new Dictionary<string, object>(checked(keywordCount + incomingKeywords), StringComparer.Ordinal);
                foreach (var pair in _keywords!)
                {
                    context.CheckExecution(span);
                    merged.Add(((PyString)pair.Key).AsString(), pair.Value);
                }
                foreach (var argument in arguments)
                {
                    context.CheckExecution(span);
                    if (argument.IsKeyword) merged[argument.KeywordName] = argument.Value;
                }
            }
            var length = merged is null ? _args.Count + arguments.Length
                : _args.Count + arguments.Length - incomingKeywords + merged.Count;
            var combined = new CallArgumentValue[length];
            var index = 0;
            foreach (var value in _args) { context.CheckExecution(span); combined[index++] = CallArgumentValue.Positional(value); }
            foreach (var argument in arguments) { context.CheckExecution(span); if (argument.IsPositional) combined[index++] = argument; }
            if (merged is null)
            {
                foreach (var argument in arguments) { context.CheckExecution(span); if (argument.IsKeyword) combined[index++] = argument; }
            }
            else
            {
                foreach (var pair in merged) { context.CheckExecution(span); combined[index++] = CallArgumentValue.Keyword(pair.Key, pair.Value); }
            }
            return asynchronous ? await _callable.InvokeAsync(combined, span, context).ConfigureAwait(false)
                : _callable.Invoke(combined, span, context);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, null, _allocationSpan, out value);
        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            => TryGetMemberCore(name, context, span, out value);

        private bool TryGetMemberCore(string name, ExecutionContext? context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            switch (name)
            {
                case "func": value = _callable; return true;
                case "args": value = _args; return true;
                case "keywords": value = _keywords ??= new PyDict(_memoryGovernor, span); return true;
                case "__dict__": value = _metadata ??= new PyDict(_memoryGovernor, span); return true;
            }
            if (TryReadFunctoolsMetadata(_metadata, name, _memoryGovernor, span, context, out value)) return true;
            if (name == "__doc__") { value = PyNone.Instance; return true; }
            value = name == "__module__" ? ExceptionTypeValue.SharedModuleLabel("functools") : PyNone.Instance;
            return value is not PyNone;
        }

        public bool TrySetMember(string name, object value)
            => name is not ("func" or "args" or "keywords") && SetFunctoolsMetadata(ref _metadata, name, value, _memoryGovernor, _allocationSpan);

        public PyString RenderPython(PyRenderingContext context)
        {
            var traversal = context.Context.Services.State.StructuralTraversal;
            if (traversal.Singles?.Contains(this) == true)
                return PyString.FromString("...", context.Context.MemoryGovernor, _allocationSpan);
            using var recursion = PyStructuralGuard.EnterSingle(this, _allocationSpan, context.Context);
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, _allocationSpan);
            try
            {
                builder.AppendAscii("functools.partial(");
                builder.Append(PyRendering.ToReprPyString(_callable, context));
                foreach (var value in _args)
                {
                    context.Context.CheckExecution(_allocationSpan);
                    builder.AppendAscii(", ");
                    builder.Append(PyRendering.ToReprPyString(value, context));
                }
                if (_keywords is not null)
                    foreach (var pair in _keywords)
                    {
                        context.Context.CheckExecution(_allocationSpan);
                        builder.AppendAscii(", ");
                        builder.Append(PyRendering.ToPythonPyString(pair.Key, context));
                        builder.Append((byte)'=');
                        builder.Append(PyRendering.ToReprPyString(pair.Value, context));
                    }
                builder.Append((byte)')');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static int PartialArgumentLength(long length, LythonSourceSpan span)
    {
        if (length > Array.MaxLength) throw RuntimeErrors.Memory("partial arguments exceed managed array capacity", span);
        return (int)length;
    }

    internal sealed class PartialFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly PartialFactory Instance = new();

        public string Name => "functools.partial";

        internal LythonRuntime.TypeNewMethod? NewSlot { get; set; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length == 0 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "functools.partial(func, ...) expects the first argument to be callable.", span);
            }

            if (arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "the first argument must be callable", span);
            }

            EnsureNoUnsupportedPlaceholder(arguments.AsSpan(1), span);
            var partial = new PyPartial(callable, arguments.AsSpan(1), context, span);
            return partial;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'functools.partial'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class PyPartialMethod : IPyDescriptor, IPyRenderableValue, IClassOwnedMember
    {
        private readonly ICallable _callable;
        private readonly CallArgumentValue[] _boundArguments;

        internal static long EstimateObjectBytes(int boundCount) => checked(64L + 32L + (32L * boundCount));

        public PyPartialMethod(ICallable callable, ReadOnlySpan<CallArgumentValue> boundArguments, MemoryGovernor governor, LythonSourceSpan allocationSpan)
        {
            // Own the shell and the copied bound-argument array like partial objects.
            var backingBytes = EstimateObjectBytes(boundArguments.Length);
            using var storage = governor.ReserveTemporary(backingBytes, allocationSpan);
            _callable = callable;
            _boundArguments = boundArguments.ToArray();
            storage.Commit(backingBytes);
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
        {
            if (context is null || span is null)
            {
                return this;
            }

            if (instance is null)
            {
                return this;
            }

            var resolved = _callable is IPyDescriptor descriptor
                ? descriptor.Get(instance, owner, context, span)
                : new PyBoundMethod(instance, _callable);
            if (resolved is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", "functools.partialmethod target must resolve to a callable.", span);
            }

            var bound = new PyPartial(callable, _boundArguments, context, span);
            return bound;
        }

        public void BindOwner(PyType owner)
        {
            if (_callable is IClassOwnedMember owned)
            {
                owned.BindOwner(owner);
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.partialmethod(...)");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class PartialMethodFactory : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly PartialMethodFactory Instance = new();

        public string Name => "functools.partialmethod";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length == 0 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "functools.partialmethod(func, ...) expects the first argument to be callable.", span);
            }

            if (arguments[0].Value is not ICallable callable)
            {
                throw new LythonRuntimeException("TypeError", PyRendering.ToReprPyString(arguments[0].Value, new PyRenderingContext(context)).AsString() + " is not callable or a descriptor", span);
            }

            EnsureNoUnsupportedPlaceholder(arguments.AsSpan(1), span);
            var method = new PyPartialMethod(callable, arguments.AsSpan(1), context.MemoryGovernor, span);
            return method;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'functools.partialmethod'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class UpdateWrapperCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly UpdateWrapperCallable Instance = new();

        public string Name => "functools.update_wrapper";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var parsed = ParseUpdateWrapperArguments(arguments, span);
            if (parsed.Wrapper is not IPyMutableDynamicAttributes mutableWrapper || parsed.Wrapped is null)
            {
                throw new LythonRuntimeException("TypeError", "functools.update_wrapper(wrapper, wrapped) expects a mutable callable wrapper and a wrapped object.", span);
            }

            var assigned = ReadWrapperMemberNames(parsed.Assigned, "assigned", span, context);
            var updated = ReadWrapperMemberNames(parsed.Updated, "updated", span, context);
            ApplyUpdateWrapper(mutableWrapper, parsed.Wrapper, parsed.Wrapped, assigned, updated, context, span);
            return parsed.Wrapper;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function update_wrapper>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class WrapsCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        public static readonly WrapsCallable Instance = new();

        public string Name => "functools.wraps";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "functools.wraps(wrapped[, ...]) expects at least the wrapped callable.", span);
            }

            using var working = context.MemoryGovernor.ReserveTemporary(checked(32L + 32L * arguments.Length), span);
            var bound = new CallArgumentValue[arguments.Length];
            bound[0] = CallArgumentValue.Keyword("wrapped", arguments[0].Value);
            for (var i = 1; i < arguments.Length; i++)
            {
                bound[i] = arguments[i];
            }

            var wrapper = new PyPartial(UpdateWrapperCallable.Instance, bound, context, span);
            return wrapper;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<built-in function wraps>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static IReadOnlyList<string> ReadWrapperMemberNames(object value, string parameterName, LythonSourceSpan span, ExecutionContext context)
    {
        var result = new List<string>();
        foreach (var item in ToSequence(value, span, context))
        {
            if (!PyStringOps.TryAsString(item, out var name))
            {
                throw new LythonRuntimeException("TypeError", $"functools.update_wrapper(..., {parameterName}=...) expects an iterable of strings.", span);
            }

            result.Add(name.AsString());
        }

        return result;
    }

    private static void ApplyUpdateWrapper(
        IPyMutableDynamicAttributes mutableWrapper,
        object wrapper,
        object wrapped,
        IReadOnlyList<string> assigned,
        IReadOnlyList<string> updated,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        foreach (var name in assigned)
        {
            if (TryReadWrapperMetadata(wrapped, name, context, span, out var value))
            {
                mutableWrapper.TrySetMember(name, value);
            }
        }

        foreach (var name in updated)
        {
            if (TryReadWrapperMetadata(wrapper, name, context, span, out var wrapperValue) &&
                TryReadWrapperMetadata(wrapped, name, context, span, out var wrappedValue) &&
                wrapperValue is PyDict wrapperDict &&
                wrappedValue is PyDict wrappedDict)
            {
                foreach (var pair in wrappedDict)
                {
                    wrapperDict.SetItem(pair.Key, pair.Value);
                }

                mutableWrapper.TrySetMember(name, wrapperDict);
            }
        }

        mutableWrapper.TrySetMember("__wrapped__", wrapped);
    }

    private static bool TryReadWrapperMetadata(object target, string memberName, ExecutionContext? context, LythonSourceSpan? span, [MaybeNullWhen(false)] out object value)
    {
        if (context is not null && span is not null && PyMemberAccess.TryResolve(target, memberName, context, span, out value))
        {
            return true;
        }

        return target switch
        {
            IPyDynamicAttributes dynamicAttributes when dynamicAttributes.TryGetMember(memberName, out value) => true,
            PyBuiltinRuntimeType builtinType when builtinType.TryGetMember(memberName, out value) => true,
            PyType type when type.TryGetMember(memberName, out value) => true,
            _ => (value = PyNone.Instance) is not null && false
        };
    }

    private static LruCacheParameters ParseLruCacheParameters(
        CallArgumentValue[] arguments,
        int? defaultMaxSize,
        LythonSourceSpan span)
    {
        object maxSizeValue = defaultMaxSize is int maxSize ? new BigInteger(maxSize) : PyNone.Instance;
        object typedValue = false;
        var seenMaxSize = false;
        var seenTyped = false;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                switch (positionalIndex++)
                {
                    case 0:
                        if (seenMaxSize)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "maxsize", span);
                        }

                        maxSizeValue = argument.Value;
                        seenMaxSize = true;
                        break;
                    case 1:
                        if (seenTyped)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "typed", span);
                        }

                        typedValue = argument.Value;
                        seenTyped = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", "functools.lru_cache(maxsize=128, typed=False) expects at most two arguments.", span);
                }

                continue;
            }

            switch (argument.KeywordName)
            {
                case "maxsize":
                    if (seenMaxSize)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "maxsize", span);
                    }

                    maxSizeValue = argument.Value;
                    seenMaxSize = true;
                    break;
                case "typed":
                    if (seenTyped)
                    {
                        throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "functools.lru_cache", "typed", span);
                    }

                    typedValue = argument.Value;
                    seenTyped = true;
                    break;
                default:
                    throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "functools.lru_cache", argument.KeywordName, span);
            }
        }

        var keyMode = IsTruthy(typedValue) ? CacheKeyMode.ValuesAndTypes : CacheKeyMode.ValuesOnly;
        return new LruCacheParameters(ParseCacheMaxSize(maxSizeValue, span), keyMode);
    }

    private static void EnsureNoUnsupportedPlaceholder(ReadOnlySpan<CallArgumentValue> arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (ReferenceEquals(argument.Value, UnsupportedPartialPlaceholder.Instance))
            {
                throw new LythonRuntimeException("NotImplementedError", "functools.Placeholder is not supported by Lython partial objects.", span);
            }
        }
    }

    private readonly record struct UpdateWrapperArguments(object? Wrapper, object? Wrapped, object Assigned, object Updated);
}
