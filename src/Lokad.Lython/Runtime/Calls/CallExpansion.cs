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
            argument => evaluateExpression(argument.Expression, context));
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
            argument => evaluateExpression(argument.Expression, context));
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
            argument => evaluateExpression(argument.Expression, context));
    }

    private static CallArgumentValue[] ExpandArguments<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, object> evaluateValue)
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

        var expanded = new CallArgumentAccumulator(arguments.Count);
        foreach (var argument in arguments)
        {
            var form = getForm(argument);
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(evaluateValue(argument))));
                    break;
                case CallArgumentKind.Keyword:
                    expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(evaluateValue(argument))));
                    break;
                case CallArgumentKind.StarredList:
                    foreach (var value in PyIteration.ToSequence(evaluateValue(argument), getSpan(argument)))
                    {
                        expanded.Add(CallArgumentValue.Positional(value));
                    }

                    break;
                case CallArgumentKind.StarredDictionary:
                    AppendStarredDictionary(evaluateValue(argument), getSpan(argument), ref expanded);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        return expanded.ToArray();
    }

    private static async ValueTask<CallArgumentValue[]> ExpandArgumentsAsync<TArgument>(
        IReadOnlyList<TArgument> arguments,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, ValueTask<object>> evaluateValue)
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

        var expanded = new CallArgumentAccumulator(arguments.Count);
        foreach (var argument in arguments)
        {
            var form = getForm(argument);
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    expanded.Add(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))));
                    break;
                case CallArgumentKind.Keyword:
                    expanded.Add(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))));
                    break;
                case CallArgumentKind.StarredList:
                    await foreach (var value in PyIteration.ToSequenceAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument)).ConfigureAwait(false))
                    {
                        expanded.Add(CallArgumentValue.Positional(value));
                    }

                    break;
                case CallArgumentKind.StarredDictionary:
                    AppendStarredDictionary(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument), ref expanded);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        return expanded.ToArray();
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

    private static void AppendStarredDictionary(
        object value,
        LythonSourceSpan span,
        ref CallArgumentAccumulator expanded)
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

            expanded.Add(CallArgumentValue.Keyword(key.AsString(), pair.Value));
        }
    }

    private struct CallArgumentAccumulator
    {
        private CallArgumentValue[] _values;
        private int _count;

        public CallArgumentAccumulator(int sourceArgumentCount)
        {
            _values = new CallArgumentValue[Math.Max(sourceArgumentCount, 4)];
            _count = 0;
        }

        public void Add(CallArgumentValue value)
        {
            if (_count == _values.Length)
            {
                Array.Resize(ref _values, checked(_values.Length * 2));
            }

            _values[_count++] = value;
        }

        public CallArgumentValue[] ToArray()
        {
            if (_count == _values.Length)
            {
                return _values;
            }

            return _values[.._count];
        }
    }
}
