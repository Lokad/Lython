using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Calls;

internal static class CallExpansion
{
    public static CallArgumentValue[] ExpandRawArguments(
        IReadOnlyList<CallArgumentSyntax> arguments,
        LythonRuntime.ExecutionContext context,
        Func<ExpressionSyntax, LythonRuntime.ExecutionContext, object> evaluateExpression,
        object target)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target);
    }

    public static CallArgumentValue[] ExpandLoweredArguments(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, object> evaluateExpression,
        object target)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target);
    }

    public static ValueTask<CallArgumentValue[]> ExpandLoweredArgumentsAsync(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, ValueTask<object>> evaluateExpression,
        object target)
    {
        return ExpandArgumentsAsync(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context,
            target);
    }

    private static CallArgumentValue[] ExpandArguments<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, object> evaluateValue,
        LythonRuntime.ExecutionContext context,
        object target)
    {
        if (!HasStarExpansion(arguments, getForm))
        {
            var direct = new CallArgumentValue[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var form = getForm(argument);
                direct[i] = CreateDirectArgument(form, LythonRuntime.RuntimeValue(evaluateValue(argument)));
            }

            return direct;
        }

        var expanded = new CallArgumentAccumulator(arguments.Count, context, target);
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
                        expanded = AppendStarredValues(evaluateValue(argument), getSpan(argument), expanded, target, context);
                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = AppendStarredDictionary(evaluateValue(argument), getSpan(argument), expanded, target, context);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
                }
            }

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
        object target)
    {
        if (!HasStarExpansion(arguments, getForm))
        {
            var direct = new CallArgumentValue[arguments.Count];
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

        var expanded = new CallArgumentAccumulator(arguments.Count, context, target);
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
                        expanded = await AppendStarredValuesAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), expanded, target, context).ConfigureAwait(false);
                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = await AppendStarredDictionaryAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), expanded, target, context).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
                }
            }

            return expanded.ToArray();
        }
        finally
        {
            expanded.Dispose();
        }
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
        var args = error.OriginalPythonException?.ArgsOverride ?? error.PythonExplicitArgs;
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
            foreach (var key in PyIteration.ToSequence(keys, span, context))
                AppendMappingKey(snapshot, key, keyStorage, context, span);
            foreach (var key in snapshot)
            {
                if (key is PyString text) expanded.CheckDuplicateKeyword(text.AsString(), span);
                var item = LythonRuntime.GetUserItem(instance, key, context, span);
                AppendMappingArgument(ref expanded, key, item, span);
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

        foreach (var pair in pairs)
        {
            if (pair.Key is not PyString key)
            {
                throw new LythonRuntimeException("TypeError", "keywords must be strings", span);
            }

            expanded.Add(CallArgumentValue.Keyword(key.AsString(), pair.Value), span);
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
        await foreach (var key in PyIteration.ToSequenceAsync(keys, span, context).ConfigureAwait(false))
            AppendMappingKey(snapshot, key, keyStorage, context, span);
        foreach (var key in snapshot)
        {
            if (key is PyString text) expanded.CheckDuplicateKeyword(text.AsString(), span);
            var item = await LythonRuntime.GetUserItemAsync(instance, key, context, span).ConfigureAwait(false);
            AppendMappingArgument(ref expanded, key, item, span);
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
            context.MemoryGovernor.EnsureCanReserve(16L * keys.Capacity, span);
            keys.Capacity = nextCapacity;
        }
        keys.Add(key);
        context.ObserveCollectionCount(keys.Count, span);
        if (keys.Count % 64 == 0) context.CheckExecutionBudget(span);
    }

    private static void AppendMappingArgument(ref CallArgumentAccumulator expanded, object key, object value, LythonSourceSpan span)
    {
        if (key is not PyString text) throw new LythonRuntimeException("TypeError", "keywords must be strings", span);
        expanded.Add(CallArgumentValue.Keyword(text.AsString(), value), span);
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
            foreach (var item in PyIteration.ToSequence(value, span, context))
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
            await foreach (var item in PyIteration.ToSequenceAsync(value, span, context).ConfigureAwait(false))
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

    private struct CallArgumentAccumulator : IDisposable
    {
        private const int BudgetCheckInterval = 64;

        private CallArgumentValue[] _values;
        private int _count;
        private int _addedSinceBudgetCheck;
        private LythonSourceSpan? _span;
        private readonly MemoryGovernor _governor;
        private readonly LythonRuntime.ExecutionContext _context;
        private readonly MemoryGovernor.TemporaryMemoryReservation _reservation;
        private readonly object _target;
        private HashSet<string>? _keywordNames;

        public CallArgumentAccumulator(int sourceArgumentCount, LythonRuntime.ExecutionContext context, object target)
        {
            var capacity = Math.Max(sourceArgumentCount, 4);
            _count = 0;
            _addedSinceBudgetCheck = 0;
            _span = null;
            _context = context;
            _target = target;
            _keywordNames = null;
            _governor = context.MemoryGovernor;
            _reservation = _governor.ReserveTemporary(EstimateArgumentBytes(capacity), null);
            _values = new CallArgumentValue[capacity];
        }

        public void Add(CallArgumentValue value, LythonSourceSpan? span)
        {
            if (value.IsKeyword)
            {
                // ** collisions are call-site errors even for constructors
                // that accept arbitrary keywords. Fund the borrowed-name
                // set before allocation, including its resize overlap.
                if (_keywordNames is null)
                {
                    _reservation.Grow(128, span);
                    _keywordNames = new HashSet<string>(StringComparer.Ordinal);
                }
                _reservation.Grow(96, span);
                if (!_keywordNames.Add(value.KeywordName))
                    throw new LythonRuntimeException("TypeError",
                        (CallsiteCallableName(_target, _context) ?? "call") +
                        " got multiple values for keyword argument '" + value.KeywordName + "'", span);
            }
            if (_count == _values.Length)
            {
                var previousCapacity = _values.Length;
                var newCapacity = checked(previousCapacity * 2);
                _reservation.Grow(EstimateArgumentBytes(newCapacity) - EstimateArgumentBytes(previousCapacity), span);
                Array.Resize(ref _values, newCapacity);
            }

            _values[_count++] = value;
            _span = span;
            _context.ObserveCollectionCount(_count, span);
            if (++_addedSinceBudgetCheck >= BudgetCheckInterval)
            {
                _addedSinceBudgetCheck = 0;
                _context.CheckExecutionBudget(span);
            }
        }

        public void CheckDuplicateKeyword(string name, LythonSourceSpan span)
        {
            if (_keywordNames?.Contains(name) == true)
                throw new LythonRuntimeException("TypeError", (CallsiteCallableName(_target, _context) ?? "call") +
                    " got multiple values for keyword argument '" + name + "'", span);
        }

        public CallArgumentValue[] ToArray()
        {
            if (_count == _values.Length)
            {
                return _values;
            }

            // The exact array coexists briefly with the growth buffer, so
            // reserve both before making the final allocation.
            _reservation.Grow(EstimateArgumentBytes(_count), _span);
            return _values[.._count];
        }

        public void Dispose() => _reservation.Dispose();

        private static long EstimateArgumentBytes(int count) => 64L + (32L * count);
    }
}
