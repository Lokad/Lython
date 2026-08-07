namespace Lokad.Lython.Runtime;

internal readonly record struct BoundCallArguments(object[] Values, bool[] Assigned);

internal static class CallBinder
{
    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        string callableKind,
        string[]? parameterNames,
        int requiredCount)
    {
        return BindNamedArgumentsCore(
            arguments,
            span,
            callableName,
            callableKind,
            parameterNames,
            parameterNames is null ? null : CreateParameterIndices(parameterNames),
            requiredCount,
            parameterNames?.Length,
            parameterNames?.Length,
            allowsExtraKeywords: false,
            allowsExtraPositional: false,
            positionalOnlyCount: 0).Values;
    }

    public static object[] BindNamedArguments(CallArgumentValue[] arguments, LythonSourceSpan span, LythonCallableSignature signature, string callableKind)
        => BindNamedArguments(arguments, span, signature, callableKind, null);

    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        LythonCallableSignature signature,
        string callableKind,
        IReadOnlyDictionary<string, int>? parameterIndices)
    {
        return BindNamedArgumentsCore(
            arguments,
            span,
            signature.Name,
            callableKind,
            signature.ParameterNames,
            parameterIndices ?? (signature.ParameterNames is null ? null : CreateParameterIndices(signature.ParameterNames)),
            signature.MinimumArgumentCount,
            signature.MaximumArgumentCount,
            signature.MaxPositionalCount,
            signature.AllowsExtraKeywords,
            signature.AllowsExtraPositional,
            signature.PositionalOnlyCount).Values;
    }

    public static BoundCallArguments BindNamedArgumentsWithPresence(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        LythonCallableSignature signature,
        string callableKind)
        => BindNamedArgumentsCore(
            arguments,
            span,
            signature.Name,
            callableKind,
            signature.ParameterNames,
            signature.ParameterNames is null ? null : CreateParameterIndices(signature.ParameterNames),
            signature.MinimumArgumentCount,
            signature.MaximumArgumentCount,
            signature.MaxPositionalCount,
            signature.AllowsExtraKeywords,
            signature.AllowsExtraPositional,
            signature.PositionalOnlyCount);

    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        string callableKind,
        string[]? parameterNames,
        IReadOnlyDictionary<string, int>? parameterIndices,
        int requiredCount)
        => BindNamedArgumentsCore(arguments, span, callableName, callableKind, parameterNames, parameterIndices, requiredCount, parameterNames?.Length, parameterNames?.Length, allowsExtraKeywords: false, allowsExtraPositional: false, positionalOnlyCount: 0).Values;

    private static BoundCallArguments BindNamedArgumentsCore(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        string callableKind,
        string[]? parameterNames,
        IReadOnlyDictionary<string, int>? parameterIndices,
        int requiredCount,
        int? maxArgumentCount,
        int? maxPositionalCount,
        bool allowsExtraKeywords,
        bool allowsExtraPositional,
        int positionalOnlyCount)
    {
        if (parameterNames is null)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].Name is not null)
                {
                    throw CallErrors.NoKeywordArguments(callableKind, callableName, span);
                }
            }

            var positionalOnly = new object[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                positionalOnly[i] = arguments[i].Value;
            }

            return new BoundCallArguments(positionalOnly, Array.Empty<bool>());
        }

        var bound = new object[parameterNames.Length];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new bool[parameterNames.Length];
        List<object>? extraPositional = null;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                if (maxPositionalCount.HasValue && positionalIndex >= maxPositionalCount.Value)
                {
                    throw CallErrors.TooManyPositional(callableKind, callableName, span);
                }

                while (positionalIndex < assigned.Length && assigned[positionalIndex])
                {
                    positionalIndex++;
                }

                if (positionalIndex >= bound.Length)
                {
                    if (!allowsExtraPositional)
                    {
                        throw CallErrors.TooManyPositional(callableKind, callableName, span);
                    }

                    extraPositional ??= [];
                    extraPositional.Add(argument.Value);
                    continue;
                }

                bound[positionalIndex] = argument.Value;
                assigned[positionalIndex] = true;
                positionalIndex++;
                continue;
            }

            if (parameterIndices is null || !parameterIndices.TryGetValue(argument.Name, out var index))
            {
                if (allowsExtraKeywords)
                {
                    continue;
                }

                throw CallErrors.UnexpectedKeyword(callableKind, callableName, argument.Name, span);
            }

            if (index < positionalOnlyCount)
            {
                throw CallErrors.UnexpectedKeyword(callableKind, callableName, argument.Name, span);
            }

            if (assigned[index])
            {
                throw CallErrors.MultipleValues(callableKind, callableName, argument.Name, span);
            }

            bound[index] = argument.Value;
            assigned[index] = true;
        }

        for (var i = 0; i < requiredCount; i++)
        {
            if (!assigned[i])
            {
                throw CallErrors.MissingArgument(callableKind, callableName, parameterNames[i], span);
            }
        }

        var count = parameterNames.Length;
        while (count > requiredCount && !assigned[count - 1])
        {
            count--;
        }

        if (maxArgumentCount.HasValue && count > maxArgumentCount.Value)
        {
            throw CallErrors.TooManyPositional(callableKind, callableName, span);
        }

        if (extraPositional is null || extraPositional.Count == 0)
        {
            return new BoundCallArguments(bound[..count], assigned[..count]);
        }

        var result = new object[count + extraPositional.Count];
        Array.Copy(bound, result, count);
        for (var i = 0; i < extraPositional.Count; i++)
        {
            result[count + i] = extraPositional[i];
        }

        var resultAssigned = new bool[result.Length];
        Array.Copy(assigned, resultAssigned, count);
        Array.Fill(resultAssigned, true, count, extraPositional.Count);
        return new BoundCallArguments(result, resultAssigned);
    }

    private static Dictionary<string, int> CreateParameterIndices(string[] parameterNames)
    {
        var indices = new Dictionary<string, int>(parameterNames.Length, StringComparer.Ordinal);
        for (var i = 0; i < parameterNames.Length; i++)
        {
            indices[parameterNames[i]] = i;
        }

        return indices;
    }
}
