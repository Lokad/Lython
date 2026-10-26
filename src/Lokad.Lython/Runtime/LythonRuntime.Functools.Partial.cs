using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PyPartial : ICallable, IPyRenderableValue, IPyMutableDynamicAttributes, IPyContextualDynamicAttributes, IPyGovernedValue
    {
        private readonly object _callable;
        private readonly PyTuple _args;
        private PyDict? _keywords;
        private PyDict? _metadata;
        private readonly MemoryGovernor _memoryGovernor;
        private readonly LythonSourceSpan _allocationSpan;

        public PyPartial(object callable, ReadOnlySpan<CallArgumentValue> arguments, ExecutionContext context, LythonSourceSpan span)
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

        internal async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, object? receiver = null, int argumentStart = 0)
        {
            context.CheckExecution(span);
            var callable = _callable as ICallable ?? throw NotCallableError(_callable, context, span);
            var receiverCount = receiver is null ? 0 : 1;
            if (receiver is null && argumentStart == 0 && _args.Count == 0 && _keywords is not { Count: > 0 })
                return asynchronous ? await callable.InvokeAsync(arguments, span, context).ConfigureAwait(false)
                    : callable.Invoke(arguments, span, context);
            var keywordCount = _keywords?.Count ?? 0;
            var incomingKeywords = 0;
            for (var input = argumentStart; input < arguments.Length; input++) { var argument = arguments[input]; context.CheckExecution(span); if (argument.IsKeyword) incomingKeywords++; }
            var maximum = PartialArgumentLength((long)_args.Count + arguments.Length - argumentStart + keywordCount + receiverCount, span);
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
                for (var input = argumentStart; input < arguments.Length; input++)
                {
                    var argument = arguments[input];
                    context.CheckExecution(span);
                    if (argument.IsKeyword) merged[argument.KeywordName] = argument.Value;
                }
            }
            var length = merged is null ? _args.Count + arguments.Length - argumentStart + receiverCount
                : _args.Count + arguments.Length - argumentStart + receiverCount - incomingKeywords + merged.Count;
            var combined = new CallArgumentValue[length];
            var index = 0;
            if (receiver is not null) combined[index++] = CallArgumentValue.Positional(receiver);
            foreach (var value in _args) { context.CheckExecution(span); combined[index++] = CallArgumentValue.Positional(value); }
            for (var input = argumentStart; input < arguments.Length; input++) { var argument = arguments[input]; context.CheckExecution(span); if (argument.IsPositional) combined[index++] = argument; }
            if (merged is null)
            {
                for (var input = argumentStart; input < arguments.Length; input++) { var argument = arguments[input]; context.CheckExecution(span); if (argument.IsKeyword) combined[index++] = argument; }
            }
            else
            {
                foreach (var pair in merged) { context.CheckExecution(span); combined[index++] = CallArgumentValue.Keyword(pair.Key, pair.Value); }
            }
            return asynchronous ? await callable.InvokeAsync(combined, span, context).ConfigureAwait(false)
                : callable.Invoke(combined, span, context);
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
        private readonly object _callable;
        private readonly CallArgumentValue[] _boundArguments;

        internal static long EstimateObjectBytes(int boundCount) => checked(64L + 32L + (32L * boundCount));

        public PyPartialMethod(object callable, ReadOnlySpan<CallArgumentValue> boundArguments, MemoryGovernor governor, LythonSourceSpan allocationSpan)
        {
            // Own the shell and the copied bound-argument array like partial objects.
            var backingBytes = EstimateObjectBytes(boundArguments.Length);
            using var storage = governor.ReserveTemporary(backingBytes, allocationSpan);
            _callable = callable;
            _boundArguments = boundArguments.ToArray();
            storage.Commit(backingBytes);
        }

        public object Get(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => context is null || span is null ? this : GetCoreAsync(instance, owner, context, span, false).GetAwaiter().GetResult();
        public ValueTask<object> GetAsync(object? instance, PyType owner, ExecutionContext? context, LythonSourceSpan? span)
            => context is null || span is null ? ValueTask.FromResult<object>(this) : GetCoreAsync(instance, owner, context, span, true);
        private async ValueTask<object> GetCoreAsync(object? instance, PyType owner, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            context.CheckExecution(span);
            var resolved = asynchronous ? await PyAttributeLookup.BindDescriptorAsync(_callable, instance, owner, context, span).ConfigureAwait(false)
                : PyAttributeLookup.TryBindDynamicDescriptor(_callable, instance, owner, context, span, out var member) ? member : _callable;
            var partial = new PyPartial(resolved, _boundArguments, context, span);
            if (!ReferenceEquals(resolved, _callable)) return partial;
            var unbound = new UnboundPartialMethod(partial);
            return instance is null ? unbound : new PyBoundMethod(instance, unbound);
        }

        private sealed class UnboundPartialMethod(PyPartial partial) : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCoreAsync(arguments, span, context, true);
            private ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                context.CheckExecution(span);
                if (arguments.Length == 0 || arguments[0].IsKeyword) throw RuntimeErrors.Type("partialmethod requires an instance argument", span);
                return partial.InvokeCoreAsync(arguments, span, context, asynchronous, arguments[0].Value, 1);
            }
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

            var callable = arguments[0].Value;
            if (!(IsCallable(callable) || HasDescriptorGetter(callable)))
                throw RuntimeErrors.Type(PyRendering.ToReprPyString(callable, new PyRenderingContext(context)).AsString() + " is not callable or a descriptor", span);

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
            => InvokeCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => InvokeCoreAsync(arguments, span, context, true);
        private static async ValueTask<object> InvokeCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var parsed = ParseUpdateWrapperArguments(arguments, span);
            if (parsed.Wrapper is null || parsed.Wrapped is null)
                throw RuntimeErrors.Type("functools.update_wrapper requires wrapper and wrapped arguments.", span);
            await ApplyUpdateWrapperAsync(parsed.Wrapper, parsed.Wrapped, parsed.Assigned, parsed.Updated, context, span, asynchronous).ConfigureAwait(false);
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

    private static async ValueTask<(bool Found, object Value)> TryReadWrapperAttributeAsync(object target, string name,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        try
        {
            return asynchronous ? await TryResolveRuntimeMemberAsync(target, name, context, span).ConfigureAwait(false)
                : TryResolveRuntimeMember(target, name, context, span, out var value) ? (true, value) : (false, PyNone.Instance);
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "AttributeError")
        {
            context.CheckExecution(span);
            return (false, PyNone.Instance);
        }
    }

    private static async ValueTask<object> ReadWrapperAttributeAsync(object target, string name,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var result = asynchronous ? await TryResolveRuntimeMemberAsync(target, name, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(target, name, context, span, out var value) ? (Found: true, Value: value) : (Found: false, Value: (object)PyNone.Instance);
        return result.Found ? result.Value : throw PyMemberAccess.CreateMissingMemberError(target, name, span, context);
    }

    private static async ValueTask AssignWrapperAttributeAsync(object target, string name, object value,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        context.CheckExecution(span);
        var assigned = asynchronous ? await PyMemberAccess.TryAssignAsync(target, name, value, context, span).ConfigureAwait(false)
            : PyMemberAccess.TryAssign(target, name, value, context, span);
        if (!assigned) throw PyMemberAccess.CreateMissingMemberError(target, name, span, context, MissingMemberOperation.Write);
        context.CheckExecution(span);
    }

    private static async ValueTask ApplyUpdateWrapperAsync(object wrapper, object wrapped, object assigned, object updated,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        foreach (var (names, copying) in new[] { (assigned, true), (updated, false) })
        {
            await using var cursor = asynchronous ? await PyIteration.Cursor.CreateAsync(names, span, context).ConfigureAwait(false)
                : PyIteration.Cursor.Create(names, span, context);
            while (true)
            {
                context.CheckExecution(span);
                var next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false)
                    : cursor.TryMoveNext(out var item) ? PyIterationResult.Yield(item) : PyIterationResult.End;
                if (!next.HasValue) break;
                if (next.Value is not PyString text) throw RuntimeErrors.Type("attribute name must be string", span);
                var nameBytes = checked(64L + 2L * text.GetUtf16CodeUnitCount() + text.Utf8Bytes.Length);
                using var decoding = context.MemoryGovernor.ReserveTemporary(nameBytes, span);
                // Attribute hooks and stores can retain name representations.
                // Their conservative owner is this execution batch.
                decoding.Commit(nameBytes);
                var name = text.AsString();
                if (copying)
                {
                    var source = await TryReadWrapperAttributeAsync(wrapped, name, context, span, asynchronous).ConfigureAwait(false);
                    if (source.Found) await AssignWrapperAttributeAsync(wrapper, name, source.Value, context, span, asynchronous).ConfigureAwait(false);
                }
                else
                {
                    var target = await ReadWrapperAttributeAsync(wrapper, name, context, span, asynchronous).ConfigureAwait(false);
                    var update = await ReadWrapperAttributeAsync(target, "update", context, span, asynchronous).ConfigureAwait(false);
                    var source = await TryReadWrapperAttributeAsync(wrapped, name, context, span, asynchronous).ConfigureAwait(false);
                    var value = source.Found ? source.Value : new PyDict(context.MemoryGovernor, span);
                    if (asynchronous) await CallableInvocation.InvokeUnaryAsync(update, value, span, context).ConfigureAwait(false);
                    else CallableInvocation.InvokeUnary(update, value, span, context);
                }
            }
        }
        await AssignWrapperAttributeAsync(wrapper, "__wrapped__", wrapped, context, span, asynchronous).ConfigureAwait(false);
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
