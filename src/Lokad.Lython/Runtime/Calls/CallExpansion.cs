using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime.Calls;

internal static class CallExpansion
{
    public static CallArgumentValue[] ExpandRawArguments(
        IReadOnlyList<CallArgumentSyntax> arguments,
        LythonRuntime.ExecutionContext context,
        Func<ExpressionSyntax, LythonRuntime.ExecutionContext, object> evaluateExpression)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context);
    }

    public static CallArgumentValue[] ExpandLoweredArguments(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, object> evaluateExpression)
    {
        return ExpandArguments(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context);
    }

    public static ValueTask<CallArgumentValue[]> ExpandLoweredArgumentsAsync(
        IReadOnlyList<LoweredCallArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<LoweredExpression, LythonRuntime.ExecutionContext, ValueTask<object>> evaluateExpression)
    {
        return ExpandArgumentsAsync(
            arguments,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context),
            context);
    }

    private static CallArgumentValue[] ExpandArguments<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, object> evaluateValue,
        LythonRuntime.ExecutionContext context)
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
                        foreach (var value in PyIteration.ToSequence(evaluateValue(argument), getSpan(argument), context))
                        {
                            expanded.Add(CallArgumentValue.Positional(value), getSpan(argument));
                        }

                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = AppendStarredDictionary(evaluateValue(argument), getSpan(argument), expanded);
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
        LythonRuntime.ExecutionContext context)
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
                        await foreach (var value in PyIteration.ToSequenceAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), context).ConfigureAwait(false))
                        {
                            expanded.Add(CallArgumentValue.Positional(value), getSpan(argument));
                        }

                        break;
                    case CallArgumentKind.StarredDictionary:
                        expanded = AppendStarredDictionary(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), expanded);
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
        CallArgumentAccumulator expanded)
    {
        if (value is not PyDict mapping)
        {
            throw new LythonRuntimeException("TypeError", "Call ** unpacking expects a dictionary.", span);
        }

        foreach (var pair in mapping)
        {
            if (pair.Key is not PyString key)
            {
                throw new LythonRuntimeException("TypeError", "Call ** unpacking expects string keys.", span);
            }

            expanded.Add(CallArgumentValue.Keyword(key.AsString(), pair.Value), span);
        }

        return expanded;
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
