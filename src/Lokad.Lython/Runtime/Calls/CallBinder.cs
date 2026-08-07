using System.Runtime.CompilerServices;

namespace Lokad.Lython.Runtime;

internal readonly record struct BoundCallArguments(object[] Values, bool[] Assigned);

internal static class CallBinder
{
    private static readonly ConditionalWeakTable<string[], Dictionary<string, int>> ParameterIndexCache = new();

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
            parameterNames is null ? null : GetParameterIndices(parameterNames),
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
            parameterIndices ?? (signature.ParameterNames is null ? null : GetParameterIndices(signature.ParameterNames)),
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
    {
        var bound = BindNamedArgumentsCore(
            arguments,
            span,
            signature.Name,
            callableKind,
            signature.ParameterNames,
            signature.ParameterNames is null ? null : GetParameterIndices(signature.ParameterNames),
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
        var assigned = new bool[signature.ParameterNames.Length];
        Array.Copy(bound.Assigned, assigned, bound.Assigned.Length);
        return new BoundCallArguments(values, assigned);
    }

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

    internal static IReadOnlyDictionary<string, int> GetParameterIndices(string[] parameterNames)
        => ParameterIndexCache.GetValue(parameterNames, static names =>
        {
            var indices = new Dictionary<string, int>(names.Length, StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++)
            {
                indices[names[i]] = i;
            }

            return indices;
        });
}
