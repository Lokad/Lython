namespace Lokad.Lython.Runtime;

internal readonly record struct BoundCallArguments(object[] Values, ArgumentPresence Assigned);

internal sealed class ArgumentPresence
{
    private readonly bool[] _assigned;

    public ArgumentPresence(int length)
    {
        _assigned = new bool[length];
    }

    public static ArgumentPresence Empty { get; } = new(0);

    public int Length => _assigned.Length;

    public bool this[int index]
    {
        get => _assigned[index];
        set => _assigned[index] = value;
    }

    public ArgumentPresence WithLength(int length)
    {
        var resized = new ArgumentPresence(length);
        Array.Copy(_assigned, resized._assigned, Math.Min(length, _assigned.Length));
        return resized;
    }

    public void MarkRange(int start, int count)
        => Array.Fill(_assigned, true, start, count);
}

internal static class CallBinder
{
    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
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

    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        LythonCallableSignature signature,
        PythonCallableKind callableKind)
    {
        return BindNamedArgumentsCore(
            arguments,
            span,
            signature.Name,
            callableKind,
            signature.ParameterNames,
            signature.ParameterIndices,
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
        PythonCallableKind callableKind)
    {
        var bound = BindNamedArgumentsCore(
            arguments,
            span,
            signature.Name,
            callableKind,
            signature.ParameterNames,
            signature.ParameterIndices,
            signature.MinimumArgumentCount,
            signature.MaximumArgumentCount,
            signature.MaxPositionalCount,
            signature.AllowsExtraKeywords,
            signature.AllowsExtraPositional,
            signature.PositionalOnlyCount);
        if (signature.ParameterNames is null || bound.Values.Length >= signature.ParameterNames.Length)
        {
            return bound;
        }

        var values = new object[signature.ParameterNames.Length];
        Array.Fill(values, PyNone.Instance);
        Array.Copy(bound.Values, values, bound.Values.Length);
        return new BoundCallArguments(values, bound.Assigned.WithLength(signature.ParameterNames.Length));
    }

    public static object[] BindNamedArguments(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
        string[]? parameterNames,
        IReadOnlyDictionary<string, int>? parameterIndices,
        int requiredCount)
        => BindNamedArgumentsCore(arguments, span, callableName, callableKind, parameterNames, parameterIndices, requiredCount, parameterNames?.Length, parameterNames?.Length, allowsExtraKeywords: false, allowsExtraPositional: false, positionalOnlyCount: 0).Values;

    private static BoundCallArguments BindNamedArgumentsCore(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
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
                if (arguments[i].IsKeyword)
                {
                    throw CallErrors.NoKeywordArguments(callableKind, callableName, span);
                }
            }

            var positionalOnly = new object[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                positionalOnly[i] = arguments[i].Value;
            }

            return new BoundCallArguments(positionalOnly, ArgumentPresence.Empty);
        }

        var bound = new object[parameterNames.Length];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new ArgumentPresence(parameterNames.Length);
        List<object>? extraPositional = null;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
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

            var keywordName = argument.KeywordName;
            if (parameterIndices is null || !parameterIndices.TryGetValue(keywordName, out var index))
            {
                if (allowsExtraKeywords)
                {
                    continue;
                }

                throw CallErrors.UnexpectedKeyword(callableKind, callableName, keywordName, span);
            }

            if (index < positionalOnlyCount)
            {
                throw CallErrors.UnexpectedKeyword(callableKind, callableName, keywordName, span);
            }

            if (assigned[index])
            {
                throw CallErrors.MultipleValues(callableKind, callableName, keywordName, span);
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
            return new BoundCallArguments(bound[..count], assigned.WithLength(count));
        }

        var result = new object[count + extraPositional.Count];
        Array.Copy(bound, result, count);
        for (var i = 0; i < extraPositional.Count; i++)
        {
            result[count + i] = extraPositional[i];
        }

        var resultAssigned = assigned.WithLength(result.Length);
        resultAssigned.MarkRange(count, extraPositional.Count);
        return new BoundCallArguments(result, resultAssigned);
    }

    private static IReadOnlyDictionary<string, int> CreateParameterIndices(string[] parameterNames)
    {
        var indices = new Dictionary<string, int>(parameterNames.Length, StringComparer.Ordinal);
        for (var index = 0; index < parameterNames.Length; index++)
        {
            indices[parameterNames[index]] = index;
        }

        return indices;
    }
}
