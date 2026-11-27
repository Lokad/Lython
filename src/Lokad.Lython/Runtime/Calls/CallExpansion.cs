using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Calls;

internal static class CallExpansion
{
    public static CallArgumentValue[] ExpandLoweredArguments(IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context, object target, MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        if (!HasStarExpansion(arguments))
        {
            if (arguments.Count == 0) return [];
            var direct = AllocateDirectArguments(arguments.Count, context, arguments[0].Expression.Span, storage);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var value = LythonRuntime.EvaluateLoweredExpression(argument.Expression, context);
                direct[i] = CreateDirectArgument(argument.Form, LythonRuntime.RuntimeValue(value));
            }
            return direct;
        }

        using var expanded = new CallArgumentAccumulator(arguments.Count, context, target,
            deferSingleStar: deferSingleStar && HasSingleStarArgument(arguments), storage: storage);
        foreach (var argument in arguments)
        {
            var form = argument.Form;
            var span = argument.Expression.Span;
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(
                        LythonRuntime.EvaluateLoweredExpression(argument.Expression, context))), span);
                    break;
                case CallArgumentKind.Keyword:
                    expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(
                        LythonRuntime.EvaluateLoweredExpression(argument.Expression, context))), span);
                    break;
                case CallArgumentKind.StarredList:
                    expanded.AppendAsync(form, LythonRuntime.EvaluateLoweredExpression(argument.Expression, context),
                        span, false).GetAwaiter().GetResult();
                    break;
                case CallArgumentKind.StarredDictionary:
                    AppendStarredDictionary(LythonRuntime.EvaluateLoweredExpression(argument.Expression, context),
                        span, expanded, target, context);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        expanded.FinishDeferredStarAsync(false).GetAwaiter().GetResult();
        return expanded.ToArray();
    }

    public static async ValueTask<CallArgumentValue[]> ExpandLoweredArgumentsAsync(IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context, object target, MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        if (!HasStarExpansion(arguments))
        {
            if (arguments.Count == 0) return [];
            var direct = AllocateDirectArguments(arguments.Count, context, arguments[0].Expression.Span, storage);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var value = await LythonRuntime.EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false);
                direct[i] = CreateDirectArgument(argument.Form, LythonRuntime.RuntimeValue(value));
            }
            return direct;
        }

        using var expanded = new CallArgumentAccumulator(arguments.Count, context, target,
            deferSingleStar: deferSingleStar && HasSingleStarArgument(arguments), storage: storage);
        foreach (var argument in arguments)
        {
            var form = argument.Form;
            var span = argument.Expression.Span;
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    var positional = await LythonRuntime.EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false);
                    expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(positional)), span);
                    break;
                case CallArgumentKind.Keyword:
                    var keyword = await LythonRuntime.EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false);
                    expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(keyword)), span);
                    break;
                case CallArgumentKind.StarredList:
                    var sequence = await LythonRuntime.EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false);
                    await expanded.AppendAsync(form, sequence, span, true).ConfigureAwait(false);
                    break;
                case CallArgumentKind.StarredDictionary:
                    var mapping = await LythonRuntime.EvaluateLoweredExpressionAsync(argument.Expression, context).ConfigureAwait(false);
                    await AppendStarredDictionaryAsync(mapping, span, expanded, target, context).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        await expanded.FinishDeferredStarAsync(true).ConfigureAwait(false);
        return expanded.ToArray();
    }

    private static CallArgumentValue[] AllocateDirectArguments(int count, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        if (count > 3) storage.Grow(32L + 32L * count, span);
        context.CheckExecution(span);
        return new CallArgumentValue[count];
    }

    private static bool HasSingleStarArgument(IReadOnlyList<LoweredCallArgument> arguments)
    {
        var count = 0;
        var starred = false;
        foreach (var argument in arguments)
        {
            var kind = argument.Form.Kind;
            if (kind is CallArgumentKind.Positional or CallArgumentKind.StarredList)
            {
                count++;
                starred = kind == CallArgumentKind.StarredList;
            }
        }
        return count == 1 && starred;
    }

    private static bool HasStarExpansion(IReadOnlyList<LoweredCallArgument> arguments)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].Form.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
            {
                return true;
            }
        }

        return false;
    }

    private static CallArgumentValue CreateDirectArgument(CallArgumentForm form, object value)
        => form.Kind switch
        {
            CallArgumentKind.Positional => CallArgumentValue.Positional(value),
            CallArgumentKind.Keyword => CallArgumentValue.Keyword(form.KeywordName, value),
            _ => throw new InvalidOperationException($"Unknown direct call argument kind: {form.Kind}")
        };

    private static void AppendStarredDictionary(object value, LythonSourceSpan span,
        CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        try { AppendStarredDictionaryCore(value, span, expanded, target, context); }
        catch (LythonRuntimeException error) when (error.ExceptionType == "KeyError")
        {
            var converted = MappingKeyError(error, target, context, span);
            if (converted is null) throw;
            throw converted;
        }
    }

    private static async ValueTask AppendStarredDictionaryAsync(object value, LythonSourceSpan span,
        CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        try { await AppendStarredDictionaryCoreAsync(value, span, expanded, target, context).ConfigureAwait(false); }
        catch (LythonRuntimeException error) when (error.ExceptionType == "KeyError")
        {
            var converted = MappingKeyError(error, target, context, span);
            if (converted is null) throw;
            throw converted;
        }
    }

    private static LythonRuntimeException? MappingKeyError(LythonRuntimeException error, object target,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // CPython's call unpacking reports one-argument KeyErrors from merging
        // as duplicate keywords, including guest keys()/getitem failures. Other
        // arities keep their KeyError, and the replaced error is not chained.
        var args = error.OriginalPythonException?.GetArgs(context.MemoryGovernor, span);
        if (args is not null && args.Count != 1) return null;
        var key = args is null ? error.Payload ?? PyString.FromString(error.Message) : args[0];
        var name = PyRendering.ToInterpolatedPyString(key, new PyRenderingContext(context)).AsString();
        return new LythonRuntimeException("TypeError", (CallsiteCallableName(target, context) ?? "call") +
            " got multiple values for keyword argument '" + name + "'", span);
    }

    private static void AppendStarredDictionaryCore(
        object value,
        LythonSourceSpan span,
        CallArgumentAccumulator expanded,
        object target,
        LythonRuntime.ExecutionContext context)
    {
        if (value is PyInstance instance)
        {
            if (!LythonRuntime.TryResolveRuntimeMember(instance, "keys", context, span, out var keysMember))
                throw MappingUnpackingError(value, target, context, span);
            var keys = LythonRuntime.InvokeCallableTarget(keysMember, span, span, context, () => []);
            using var keyStorage = context.MemoryGovernor.ReserveTemporary(0, span);
            var snapshot = new List<object>();
            foreach (var key in PyIteration.Cursor.Create(keys, span, context))
                AppendMappingKey(snapshot, key, keyStorage, context, span);
            foreach (var key in snapshot)
            {
                var argument = PrepareMappingKeyword(expanded, key, span);
                var item = LythonRuntime.GetUserItem(instance, key, context, span);
                AppendMappingArgument(expanded, argument, item, span);
            }
            return;
        }
        // N16: the Try signal tells absent mappings apart from guest failures by result,
        // never by message text: a guest keys() raising the same TypeError wording now
        // propagates with its own message instead of becoming a call-site diagnostic.
        if (!LythonRuntime.TryEnumerateMappingItems(value, context, span, out var pairs))
        {
            throw MappingUnpackingError(value, target, context, span);
        }

        // An exact dictionary already enforces uniqueness among its string
        // keys. Only another keyword source can require a separate index.
        var uniqueKeywordSource = value is PyDict && expanded.CanDeferKeywordIndex;
        foreach (var pair in pairs)
        {
            if (pair.Key is not PyString key)
            {
                throw new LythonRuntimeException("TypeError", "keywords must be strings", span);
            }

            expanded.Add(expanded.PrepareKeyword(key, pair.Value, span), span, uniqueKeywordSource);
        }

    }

    private static async ValueTask AppendStarredDictionaryCoreAsync(object value,
        LythonSourceSpan span, CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        if (value is not PyInstance instance)
        {
            AppendStarredDictionaryCore(value, span, expanded, target, context);
            return;
        }
        var keysMember = await LythonRuntime.TryResolveRuntimeMemberAsync(instance, "keys", context, span).ConfigureAwait(false);
        if (!keysMember.Found) throw MappingUnpackingError(value, target, context, span);
        var keys = await LythonRuntime.InvokeCallableTargetAsync(keysMember.Value, span, span, context,
            () => new ValueTask<CallArgumentValue[]>([])).ConfigureAwait(false);
        using var keyStorage = context.MemoryGovernor.ReserveTemporary(0, span);
        var snapshot = new List<object>();
        await foreach (var key in PyIteration.Cursor.ForAsyncEnumeration(keys, span, context).ConfigureAwait(false))
            AppendMappingKey(snapshot, key, keyStorage, context, span);
        foreach (var key in snapshot)
        {
            var argument = PrepareMappingKeyword(expanded, key, span);
            var item = await LythonRuntime.GetUserItemAsync(instance, key, context, span).ConfigureAwait(false);
            AppendMappingArgument(expanded, argument, item, span);
        }
    }

    private static void AppendMappingKey(List<object> keys, object key, MemoryGovernor.TemporaryMemoryReservation storage,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (keys.Count == keys.Capacity)
        {
            var nextCapacity = keys.Capacity == 0 ? 4 : checked(keys.Capacity * 2);
            storage.Grow(16L * (nextCapacity - keys.Capacity), span);
            using var preflight14815 = context.MemoryGovernor.ReserveTemporary(16L * keys.Capacity, span);
            keys.Capacity = nextCapacity;
        }
        keys.Add(key);
        context.CheckExecution(span);
    }

    private static CallArgumentValue PrepareMappingKeyword(CallArgumentAccumulator expanded, object key, LythonSourceSpan span)
    {
        if (key is not PyString text) return default;
        var argument = expanded.PrepareKeyword(text, PyNone.Instance, span);
        expanded.CheckDuplicateKeyword(argument, span);
        return argument;
    }

    private static void AppendMappingArgument(CallArgumentAccumulator expanded, CallArgumentValue argument, object value,
        LythonSourceSpan span)
    {
        if (!argument.IsKeyword) throw new LythonRuntimeException("TypeError", "keywords must be strings", span);
        expanded.Add(argument.WithValue(value), span);
    }

    private static LythonRuntimeException MappingUnpackingError(object value, object target,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var name = CallsiteCallableName(target, context);
        return new LythonRuntimeException("TypeError", name is null ? "Call ** unpacking expects a dictionary."
            : name + " argument after ** must be a mapping, not " + RuntimeErrors.OperandTypeName(value), span);
    }

    // Call-site splat failures name the callee like CPython: module-qualified
    // Python functions, bare C names, and the repr for values without a qualname.
    // The name resolves lazily so success paths never render.
    private static string? CallsiteCallableName(object target, LythonRuntime.ExecutionContext context)
    {
        if (target is IPyDynamicAttributes attributes &&
            attributes.TryGetMember("__qualname__", out var qualname) &&
            qualname is PyString qualnameText)
        {
            var name = qualnameText.AsString();
            if (attributes.TryGetMember("__module__", out var module) &&
                module is PyString moduleText &&
                moduleText.AsString() is string moduleName &&
                moduleName.Length != 0 &&
                moduleName != "builtins")
            {
                return moduleName + "." + name + "()";
            }

            return name + "()";
        }

        try
        {
            return PyRendering.ToReprPyString(target, new PyRenderingContext(context)).AsString();
        }
        catch (LythonRuntimeException)
        {
            return null;
        }
    }

    internal sealed class CallArgumentAccumulator : ExecutableTemporaryValue
    {

        private CallArgumentValue[] _values;
        private int _count;
        private LythonSourceSpan? _span;
        private readonly MemoryGovernor _governor;
        private LythonRuntime.ExecutionContext? _context;
        private LythonRuntime.ExecutionContext Context => _context ?? throw new ObjectDisposedException(nameof(CallArgumentAccumulator));
        private readonly MemoryGovernor.TemporaryMemoryReservation? _reservation;
        private long _retainedBytes;
        private readonly bool _deferSingleStar;
        private readonly bool _bindsNativeKeywords;
        private object? _deferredStar;
        private LythonSourceSpan? _deferredSpan;
        private object? _target;
        private PyDict? _keywordNames;
        private int _unindexedKeywords;

        public bool CanDeferKeywordIndex => _keywordNames is null && _unindexedKeywords == 0;

        // Lowered callers keep their borrowed storage through invocation.
        // Executable callers keep funding on this temporary stack value.
        public CallArgumentAccumulator(int sourceArgumentCount, LythonRuntime.ExecutionContext context, object target,
            bool deferSingleStar = false, MemoryGovernor.TemporaryMemoryReservation? storage = null,
            LythonSourceSpan? span = null)
        {
            var capacity = Math.Max(sourceArgumentCount, 4);
            _count = 0;
            _span = span;
            _context = context;
            _deferSingleStar = deferSingleStar;
            _bindsNativeKeywords = target is PyFunctionBase or PyBoundMethod { Function: PyFunctionBase };
            _target = target;
            _keywordNames = null;
            _governor = context.MemoryGovernor;
            _reservation = storage;
            Grow(EstimateArgumentBytes(capacity), span);
            try
            {
                context.CheckExecution(span);
                _values = new CallArgumentValue[capacity];
            }
            catch
            {
                if (_retainedBytes != 0) _governor.Release(_retainedBytes);
                throw;
            }
        }

        private void Grow(long bytes, LythonSourceSpan? span)
        {
            if (_reservation is not null) { _reservation.Grow(bytes, span); return; }
            _governor.Reserve(bytes, span);
            _governor.Commit(bytes);
            _retainedBytes = checked(_retainedBytes + bytes);
        }

        public void Add(CallArgumentValue value, LythonSourceSpan? span, bool uniqueKeywordSource = false)
        {
            var context = Context;
            if (value.IsKeyword)
            {
                if (uniqueKeywordSource)
                {
                    _unindexedKeywords++;
                }
                else
                {
                    var keywordSpan = span ?? PyDict.UnknownKeySpan;
                    var names = EnsureKeywordIndex(context, keywordSpan);
                    var key = value.GetKeywordKey(context, span);
                    if (!names.TryAddNativeString(key, PyNone.Instance, context, keywordSpan))
                        throw DuplicateKeyword(value.GetKeywordName(context, span), context, span);
                    if (value.OriginalKeyword is null)
                        value = _bindsNativeKeywords
                            ? CallArgumentValue.Keyword(key, value.Value)
                            : CallArgumentValue.Keyword(value.KeywordName, value.Value, key);
                }
            }
            if (_count == _values.Length)
            {
                var previousCapacity = _values.Length;
                var newCapacity = checked(previousCapacity * 2);
                // The old buffer stays live until every reference is copied.
                using var overlap = _governor.ReserveTemporary(32L + 32L * previousCapacity, span);
                Grow(EstimateArgumentBytes(newCapacity) - EstimateArgumentBytes(previousCapacity), span);
                context.CheckExecution(span);
                var expanded = new CallArgumentValue[newCapacity];
                for (var offset = 0; offset < previousCapacity;)
                {
                    context.CheckExecution(span);
                    var length = Math.Min(1024, previousCapacity - offset);
                    _values.AsSpan(offset, length).CopyTo(expanded.AsSpan(offset));
                    offset += length;
                }
                _values = expanded;
            }

            _values[_count++] = value;
            _span = span;
            context.CheckExecution(span);
        }

        public CallArgumentValue PrepareKeyword(PyString key, object value, LythonSourceSpan span)
        {
            var argument = CallArgumentValue.Keyword(key, value);
            return _bindsNativeKeywords
                ? argument
                : CallArgumentValue.Keyword(argument.GetKeywordName(Context, span), value, key);
        }

        public void CheckDuplicateKeyword(CallArgumentValue argument, LythonSourceSpan span)
        {
            var context = Context;
            if (CanDeferKeywordIndex) return;
            if (EnsureKeywordIndex(context, span).TryReadNativeStringValue(argument.OriginalKeyword!, context, span, out _))
                throw DuplicateKeyword(argument.GetKeywordName(context, span), context, span);
        }

        private PyDict EnsureKeywordIndex(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            if (_keywordNames is not null) return _keywordNames;

            context.CheckExecution(span);
            var names = new PyDict(_unindexedKeywords, _governor, span, keysOnly: true);
            _keywordNames = names;
            if (_unindexedKeywords != 0)
            {
                foreach (var value in _values.AsSpan(0, _count))
                {
                    context.CheckExecution(span);
                    if (value.OriginalKeyword is { } key)
                        names.TryAddNativeString(key, PyNone.Instance, context, span);
                }
            }
            _unindexedKeywords = 0;
            return names;
        }

        private LythonRuntimeException DuplicateKeyword(string name, LythonRuntime.ExecutionContext context,
            LythonSourceSpan? span) =>
            new("TypeError", (CallsiteCallableName(_target!, context) ?? "call") +
                " got multiple values for keyword argument '" + name + "'", span);

        internal ReadOnlySpan<CallArgumentValue> Arguments => _values.AsSpan(0, _count);

        internal async ValueTask AppendPositionalIterableAsync(object source, LythonSourceSpan span, bool asynchronous)
        {
            var context = Context;
            if (source is LythonRuntime.PyGenerator generator && generator.CanBufferFor(context))
            {
                // The first pull starts with the consumer's checks, as it would
                // through a cursor. The generator checks every frame entry and
                // each buffered yield while borrowing only admitted capacity.
                context.CheckExecution(span);
                context.State.CheckInterpreterStack(span);
                var initialCount = _count;
                while (true)
                {
                    var next = asynchronous
                        ? await generator.AdvanceBufferedAsync(this, _values.Length).ConfigureAwait(false)
                        : generator.AdvanceBuffered(this, _values.Length);
                    if (!next.HasValue)
                    {
                        if (_count != initialCount) _span = span;
                        return;
                    }
                    Add(CallArgumentValue.Positional(next.Value), span);
                }
            }

            using var cursor = asynchronous
                ? await PyIteration.Cursor.CreateAsync(source, span, context).ConfigureAwait(false)
                : PyIteration.Cursor.Create(source, span, context);
            while (true)
            {
                var next = asynchronous ? await cursor.TryMoveNextAsync().ConfigureAwait(false) : cursor.TryMoveNext();
                if (!next.HasValue) return;
                Add(CallArgumentValue.Positional(next.Value), span);
            }
        }

        internal bool TryAppendBufferedPositional(LythonRuntime.ExecutableSlot value, int limit)
        {
            // Leave the final item for ordinary advancement. Appends here
            // consume only capacity the caller has already admitted.
            if (_count >= limit - 1) return false;
            _values[_count++] = CallArgumentValue.Positional(value.Object.RequireNotNull());
            return true;
        }

        private async ValueTask AppendStarredValuesAsync(object value, LythonSourceSpan span, bool asynchronous)
        {
            try { await AppendPositionalIterableAsync(value, span, asynchronous).ConfigureAwait(false); }
            catch (PyNotIterableException)
            {
                var calleeName = CallsiteCallableName(_target!, Context);
                if (calleeName is null) throw;
                throw new LythonRuntimeException("TypeError", calleeName + " argument after * must be an iterable, not " + RuntimeErrors.OperandTypeName(value), span);
            }
        }

        public CallArgumentValue[] ToArray()
        {
            _ = Context;
            if (_count == _values.Length)
            {
                return _values;
            }

            // The exact array coexists briefly with the growth buffer, so
            // reserve both before making the final allocation.
            Grow(32L + (32L * _count), _span);
            return _values[.._count];
        }

        internal async ValueTask AppendAsync(CallArgumentForm form, object value, LythonSourceSpan span, bool asynchronous)
        {
            switch (form.Kind)
            {
                case CallArgumentKind.Positional: Add(CallArgumentValue.Positional(value), span); break;
                case CallArgumentKind.Keyword: Add(CallArgumentValue.Keyword(form.KeywordName, value), span); break;
                case CallArgumentKind.StarredList:
                    if (_deferSingleStar)
                    {
                        _deferredStar = value;
                        _deferredSpan = span;
                        break;
                    }
                    await AppendStarredValuesAsync(value, span, asynchronous).ConfigureAwait(false);
                    break;
                case CallArgumentKind.StarredDictionary:
                    if (asynchronous) await AppendStarredDictionaryAsync(value, span, this, _target!, Context).ConfigureAwait(false);
                    else AppendStarredDictionary(value, span, this, _target!, Context);
                    break;
            }
        }

        internal async ValueTask FinishDeferredStarAsync(bool asynchronous)
        {
            if (_deferredStar is not { } source) return;
            try
            {
                await AppendStarredValuesAsync(source, _deferredSpan!, asynchronous).ConfigureAwait(false);
            }
            finally { _deferredStar = null; _deferredSpan = null; }
        }

        internal async ValueTask<object> InvokeAsync(LythonSourceSpan targetSpan, LythonSourceSpan callSpan, bool asynchronous)
        {
            await FinishDeferredStarAsync(asynchronous).ConfigureAwait(false);
            if (_target is LythonRuntime.ISpanCallable)
            {
                // Binding consumes the live prefix into owned slots before
                // suspension. The caller owns the accumulator through cleanup.
                return asynchronous
                    ? await LythonRuntime.InvokeBorrowedCallableTargetAsync(_target!, targetSpan, callSpan,
                        Arguments, Context).ConfigureAwait(false)
                    : LythonRuntime.InvokeBorrowedCallableTarget(_target!, targetSpan, callSpan,
                        Arguments, Context);
            }
            return asynchronous
                ? await LythonRuntime.InvokeCallableTargetAsync(_target!, targetSpan, callSpan, Context,
                    () => new ValueTask<CallArgumentValue[]>(ToArray())).ConfigureAwait(false)
                : LythonRuntime.InvokeCallableTarget(_target!, targetSpan, callSpan, Context, ToArray);
        }

        public override void Dispose()
        {
            _values = [];
            _count = 0;
            _deferredStar = null;
            _deferredSpan = null;
            if (_keywordNames is { } names)
            {
                _keywordNames = null;
                _governor.RefundUnpublishedValue(names);
            }
            _unindexedKeywords = 0;
            _context = null;
            _target = null;
            if (_retainedBytes == 0) return;
            _governor.Release(_retainedBytes);
            _retainedBytes = 0;
        }

        // Empty construction allocates 264 bytes on managed x64 (shell, lease,
        // minimum array). Cover its full fixed storage before publication.
        private static long EstimateArgumentBytes(int count) => 192L + (32L * count);
    }
}
