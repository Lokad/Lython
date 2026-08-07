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
            context,
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
            context,
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
            context,
            argument => argument.Form,
            argument => argument.Expression.Span,
            argument => evaluateExpression(argument.Expression, context));
    }

    private static CallArgumentValue[] ExpandArguments<TArgument>(
        IReadOnlyList<TArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, object> evaluateValue)
    {
        var hasStarExpansion = false;
        for (var i = 0; i < arguments.Count; i++)
        {
            var kind = getForm(arguments[i]).Kind;
            if (kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
            {
                hasStarExpansion = true;
                break;
            }
        }

        if (!hasStarExpansion)
        {
            var direct = new CallArgumentValue[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var form = getForm(argument);
                direct[i] = form.Kind switch
                {
                    CallArgumentKind.Positional => CallArgumentValue.Positional(LythonRuntime.RuntimeValue(evaluateValue(argument))),
                    CallArgumentKind.Keyword => CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(evaluateValue(argument))),
                    _ => throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}")
                };
            }

            return direct;
        }

        var expanded = new CallArgumentValue[Math.Max(arguments.Count, 4)];
        var count = 0;
        foreach (var argument in arguments)
        {
            var form = getForm(argument);
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    AddExpanded(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(evaluateValue(argument))));
                    break;
                case CallArgumentKind.Keyword:
                    AddExpanded(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(evaluateValue(argument))));
                    break;
                case CallArgumentKind.StarredList:
                    foreach (var value in PyIteration.ToSequence(evaluateValue(argument), getSpan(argument)))
                    {
                        AddExpanded(CallArgumentValue.Positional(value));
                    }

                    break;
                case CallArgumentKind.StarredDictionary:
                    if (evaluateValue(argument) is not PyDict mapping)
                    {
                        throw new LythonRuntimeException("TypeError", "Call ** unpacking expects a dictionary.", getSpan(argument));
                    }

                    foreach (var pair in mapping)
                    {
                        if (pair.Key is not PyString key)
                        {
                            throw new LythonRuntimeException("TypeError", "Call ** unpacking expects string keys.", getSpan(argument));
                        }

                        AddExpanded(CallArgumentValue.Keyword(key.AsString(), pair.Value));
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        if (count == expanded.Length)
        {
            return expanded;
        }

        return expanded[..count];

        void AddExpanded(CallArgumentValue value)
        {
            if (count == expanded.Length)
            {
                Array.Resize(ref expanded, checked(expanded.Length * 2));
            }

            expanded[count++] = value;
        }
    }

    private static async ValueTask<CallArgumentValue[]> ExpandArgumentsAsync<TArgument>(
        IReadOnlyList<TArgument> arguments,
        LythonRuntime.ExecutionContext context,
        Func<TArgument, CallArgumentForm> getForm,
        Func<TArgument, LythonSourceSpan> getSpan,
        Func<TArgument, ValueTask<object>> evaluateValue)
    {
        var hasStarExpansion = false;
        for (var i = 0; i < arguments.Count; i++)
        {
            var kind = getForm(arguments[i]).Kind;
            if (kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
            {
                hasStarExpansion = true;
                break;
            }
        }

        if (!hasStarExpansion)
        {
            var direct = new CallArgumentValue[arguments.Count];
            for (var i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var form = getForm(argument);
                direct[i] = form.Kind switch
                {
                    CallArgumentKind.Positional => CallArgumentValue.Positional(LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))),
                    CallArgumentKind.Keyword => CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))),
                    _ => throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}")
                };
            }

            return direct;
        }

        var expanded = new CallArgumentValue[Math.Max(arguments.Count, 4)];
        var count = 0;
        foreach (var argument in arguments)
        {
            var form = getForm(argument);
            switch (form.Kind)
            {
                case CallArgumentKind.Positional:
                    AddExpanded(CallArgumentValue.Positional(LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))));
                    break;
                case CallArgumentKind.Keyword:
                    AddExpanded(CallArgumentValue.Keyword(form.KeywordName, LythonRuntime.RuntimeValue(await evaluateValue(argument).ConfigureAwait(false))));
                    break;
                case CallArgumentKind.StarredList:
                    await foreach (var value in PyIteration.ToSequenceAsync(await evaluateValue(argument).ConfigureAwait(false), getSpan(argument)).ConfigureAwait(false))
                    {
                        AddExpanded(CallArgumentValue.Positional(value));
                    }

                    break;
                case CallArgumentKind.StarredDictionary:
                    if (await evaluateValue(argument).ConfigureAwait(false) is not PyDict mapping)
                    {
                        throw new LythonRuntimeException("TypeError", "Call ** unpacking expects a dictionary.", getSpan(argument));
                    }

                    foreach (var pair in mapping)
                    {
                        if (pair.Key is not PyString key)
                        {
                            throw new LythonRuntimeException("TypeError", "Call ** unpacking expects string keys.", getSpan(argument));
                        }

                        AddExpanded(CallArgumentValue.Keyword(key.AsString(), pair.Value));
                    }

                    break;
                default:
                    throw new InvalidOperationException($"Unknown call argument kind: {form.Kind}");
            }
        }

        if (count == expanded.Length)
        {
            return expanded;
        }

        return expanded[..count];

        void AddExpanded(CallArgumentValue value)
        {
            if (count == expanded.Length)
            {
                Array.Resize(ref expanded, checked(expanded.Length * 2));
            }

            expanded[count++] = value;
        }
    }
}
