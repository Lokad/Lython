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

        var expanded = new CallArgumentAccumulator(arguments.Count, context);
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

        var expanded = new CallArgumentAccumulator(arguments.Count, context);
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
                        expanded = AppendStarredDictionary(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), expanded, target, context);
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

    private static CallArgumentAccumulator AppendStarredDictionary(
        object value,
        LythonSourceSpan span,
        CallArgumentAccumulator expanded,
        object target,
        LythonRuntime.ExecutionContext context)
    {
        // N16: the Try signal tells absent mappings apart from guest failures by result,
        // never by message text: a guest keys() raising the same TypeError wording now
        // propagates with its own message instead of becoming a call-site diagnostic.
        if (!LythonRuntime.TryEnumerateMappingItems(value, context, span, out var pairs))
        {
            var calleeName = CallsiteCallableName(target, context);
            if (calleeName is null)
            {
                throw new LythonRuntimeException("TypeError", "Call ** unpacking expects a dictionary.", span);
            }

            throw new LythonRuntimeException("TypeError", calleeName + " argument after ** must be a mapping, not " + RuntimeErrors.OperandTypeName(value), span);
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

        public CallArgumentAccumulator(int sourceArgumentCount, LythonRuntime.ExecutionContext context)
        {
            _values = new CallArgumentValue[Math.Max(sourceArgumentCount, 4)];
            _count = 0;
            _addedSinceBudgetCheck = 0;
            _span = null;
            _context = context;
            _governor = context.MemoryGovernor;
            _reservation = _governor.ReserveTemporary(EstimateArgumentBytes(_values.Length), null);
        }

        public void Add(CallArgumentValue value, LythonSourceSpan? span)
        {
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
