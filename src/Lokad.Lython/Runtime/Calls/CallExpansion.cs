using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Calls;

internal static class CallExpansion
{
    public static CallArgumentValue[] ExpandRawArguments(
        IReadOnlyList<CallArgumentSyntax> arguments,
        LythonRuntime.ExecutionContext context,
        Func<ExpressionSyntax, LythonRuntime.ExecutionContext, object> evaluateExpression,
        object target, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target, storage);
    }

    public static CallArgumentValue[] ExpandLoweredArguments(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, object> evaluateExpression,
        object target,
        MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target, storage, deferSingleStar);
    }

    public static ValueTask<CallArgumentValue[]> ExpandLoweredArgumentsAsync(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, ValueTask<object>> evaluateExpression,
        object target,
        MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        return ExpandArgumentsAsync(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target, storage, deferSingleStar);
    }

    private static CallArgumentValue[] ExpandArguments<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, object> evaluateValue,
        LythonRuntime.ExecutionContext context,
        object target,
        MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        if (!HasStarExpansion(arguments, getForm))
        {
            if (arguments.Count == 0) return [];
            var direct = AllocateDirectArguments(arguments.Count, context, getSpan(arguments[0]), storage);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var form = getForm(argument);
                direct[i] = CreateDirectArgument(form, LythonRuntime.RuntimeValue(evaluateValue(argument)));
            }

            return direct;
        }

        var expanded = new CallArgumentAccumulator(arguments.Count, context, target,
            deferSingleStar: deferSingleStar && HasSingleStarArgument(arguments, getForm), storage: storage);
        try
        {
            foreach (var argument in arguments)
            {
                var form = getForm(argument);
                var span = getSpan(argument);
                switch (form.Kind)
                {
                    case CallArgumentKind.Positional:
                        expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(evaluateValue(argument))), span);
                        break;
                    case CallArgumentKind.Keyword:
                        expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(evaluateValue(argument))), span);
                        break;
                    case CallArgumentKind.StarredList:
                        expanded.AppendAsync(form, evaluateValue(argument), getSpan(argument), false).GetAwaiter().GetResult();
                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = AppendStarredDictionary(evaluateValue(argument), getSpan(argument), expanded, target, context);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
                }
            }

            expanded.FinishDeferredStarAsync(false).GetAwaiter().GetResult();
            return expanded.ToArray();
        }
        finally
        {
            expanded.Dispose();
        }
    }

    private static async ValueTask<CallArgumentValue[]> ExpandArgumentsAsync<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, ValueTask<object>> evaluateValue,
        LythonRuntime.ExecutionContext context,
        object target,
        MemoryGovernor.TemporaryMemoryReservation storage,
        bool deferSingleStar = true)
    {
        if (!HasStarExpansion(arguments, getForm))
        {
            if (arguments.Count == 0) return [];
            var direct = AllocateDirectArguments(arguments.Count, context, getSpan(arguments[0]), storage);
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var form = getForm(argument);
                direct[i] = CreateDirectArgument(
                    form,
                    LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false)));
            }

            return direct;
        }

        var expanded = new CallArgumentAccumulator(arguments.Count, context, target,
            deferSingleStar: deferSingleStar && HasSingleStarArgument(arguments, getForm), storage: storage);
        try
        {
            foreach (var argument in arguments)
            {
                var form = getForm(argument);
                var span = getSpan(argument);
                switch (form.Kind)
                {
                    case CallArgumentKind.Positional:
                        expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))), span);
                        break;
                    case CallArgumentKind.Keyword:
                        expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))), span);
                        break;
                    case CallArgumentKind.StarredList:
                        await expanded.AppendAsync(form, await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), true).ConfigureAwait(false);
                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = await AppendStarredDictionaryAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), expanded, target, context).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
                }
            }

            await expanded.FinishDeferredStarAsync(true).ConfigureAwait(false);
            return expanded.ToArray();
        }
        finally
        {
            expanded.Dispose();
        }
    }

    private static CallArgumentValue[] AllocateDirectArguments(int count, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        if (count > 3) storage.Grow(32L + 32L * count, span);
        context.CheckExecution(span);
        return new CallArgumentValue[count];
    }

    private static bool HasSingleStarArgument<TArgument>(IReadOnlyList<TArgument> arguments, Func<TArgument, CallArgumentForm> getForm)
    {
        var count = 0;
        var starred = false;
        foreach (var argument in arguments)
        {
            var kind = getForm(argument).Kind;
            if (kind is CallArgumentKind.Positional or CallArgumentKind.StarredList)
            {
                count++;
                starred = kind == CallArgumentKind.StarredList;
            }
        }
        return count == 1 && starred;
    }

    private static bool HasStarExpansion<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (getForm(arguments[i]).Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
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

    private static CallArgumentAccumulator AppendStarredDictionary(object value, LythonSourceSpan span,
        CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        try { return AppendStarredDictionaryCore(value, span, expanded, target, context); }
        catch (LythonRuntimeException error) when (error.ExceptionType == "KeyError")
        {
            var converted = MappingKeyError(error, target, context, span);
            if (converted is null) throw;
            throw converted;
        }
    }

    private static async ValueTask<CallArgumentAccumulator> AppendStarredDictionaryAsync(object value, LythonSourceSpan span,
        CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        try { return await AppendStarredDictionaryCoreAsync(value, span, expanded, target, context).ConfigureAwait(false); }
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

    private static CallArgumentAccumulator AppendStarredDictionaryCore(
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
            return expanded;
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

        return expanded;
    }

    private static async ValueTask<CallArgumentAccumulator> AppendStarredDictionaryCoreAsync(object value,
        LythonSourceSpan span, CallArgumentAccumulator expanded, object target, LythonRuntime.ExecutionContext context)
    {
        if (value is not PyInstance instance) return AppendStarredDictionaryCore(value, span, expanded, target, context);
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
        return expanded;
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

    private static CallArgumentAccumulator AppendStarredValues(
        object value,
        LythonSourceSpan span,
        CallArgumentAccumulator expanded,
        object target,
        LythonRuntime.ExecutionContext context)
    {
        try
        {
            foreach (var item in PyIteration.Cursor.Create(value, span, context))
            {
                expanded.Add(CallArgumentValue.Positional(item), span);
            }
        }
        catch (PyNotIterableException)
        {
            var calleeName = CallsiteCallableName(target, context);
            if (calleeName is null)
            {
                throw;
            }

            throw new LythonRuntimeException("TypeError", calleeName + " argument after * must be an iterable, not " + RuntimeErrors.OperandTypeName(value), span);
        }

        return expanded;
    }

    private static async ValueTask<CallArgumentAccumulator> AppendStarredValuesAsync(
        object value,
        LythonSourceSpan span,
        CallArgumentAccumulator expanded,
        object target,
        LythonRuntime.ExecutionContext context)
    {
        try
        {
            await foreach (var item in PyIteration.Cursor.ForAsyncEnumeration(value, span, context).ConfigureAwait(false))
            {
                expanded.Add(CallArgumentValue.Positional(item), span);
            }
        }
        catch (PyNotIterableException)
        {
            var calleeName = CallsiteCallableName(target, context);
            if (calleeName is null)
            {
                throw;
            }

            throw new LythonRuntimeException("TypeError", calleeName + " argument after * must be an iterable, not " + RuntimeErrors.OperandTypeName(value), span);
        }

        return expanded;
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
            bool deferSingleStar = false, MemoryGovernor.TemporaryMemoryReservation? storage = null)
        {
            var capacity = Math.Max(sourceArgumentCount, 4);
            _count = 0;
            _span = null;
            _context = context;
            _deferSingleStar = deferSingleStar;
            _bindsNativeKeywords = target is PyFunctionBase or PyBoundMethod { Function: PyFunctionBase };
            _target = target;
            _keywordNames = null;
            _governor = context.MemoryGovernor;
            _reservation = storage;
            Grow(EstimateArgumentBytes(capacity), null);
            _values = new CallArgumentValue[capacity];
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
                    if (asynchronous) await AppendStarredValuesAsync(value, span, this, _target!, Context).ConfigureAwait(false);
                    else AppendStarredValues(value, span, this, _target!, Context);
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
                if (asynchronous) await AppendStarredValuesAsync(source, _deferredSpan!, this, _target!, Context).ConfigureAwait(false);
                else AppendStarredValues(source, _deferredSpan!, this, _target!, Context);
            }
            finally { _deferredStar = null; _deferredSpan = null; }
        }

        internal async ValueTask<object> InvokeAsync(LythonSourceSpan targetSpan, LythonSourceSpan callSpan, bool asynchronous)
        {
            await FinishDeferredStarAsync(asynchronous).ConfigureAwait(false);
            if (_bindsNativeKeywords)
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
