namespace Lokad.Lython.Runtime;

internal readonly record struct BoundCallArguments(object[] Values, ArgumentPresence Assigned);

internal struct ArgumentPresence
{
    // Almost every Python-shaped signature fits in the inline word. The overflow
    // array exists only for unusually wide generated signatures, so ordinary calls
    // do not allocate a parallel bool[] merely to distinguish omitted arguments.
    private ulong _firstAssignments;
    private readonly bool[] _remainingAssignments;

    public ArgumentPresence(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Length = length;
        _firstAssignments = 0;
        _remainingAssignments = length > 64 ? new bool[length - 64] : [];
    }

    public static ArgumentPresence Empty { get; } = new(0);

    public int Length { get; }

    public bool this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length);
            return index < 64
                ? (_firstAssignments & (1UL << index)) != 0
                : _remainingAssignments[index - 64];
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Length);
            if (index < 64)
            {
                var mask = 1UL << index;
                _firstAssignments = value ? _firstAssignments | mask : _firstAssignments & ~mask;
                return;
            }

            _remainingAssignments[index - 64] = value;
        }
    }

    public ArgumentPresence WithLength(int length)
    {
        if (length == Length)
        {
            return this;
        }

        var resized = new ArgumentPresence(length);
        resized._firstAssignments = length >= 64
            ? _firstAssignments
            : _firstAssignments & ((1UL << length) - 1);
        // A default ArgumentPresence has Length == 0 and no initialized overflow
        // storage. Length is therefore the invariant that guards every array access.
        if (Length > 64)
        {
            Array.Copy(
                _remainingAssignments,
                resized._remainingAssignments,
                Math.Min(_remainingAssignments.Length, resized._remainingAssignments.Length));
        }

        return resized;
    }

    public void MarkRange(int start, int count)
    {
        for (var index = start; index < start + count; index++)
        {
            this[index] = true;
        }
    }
}

internal static class CallBinder
{
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
            signature.Parameters).Values;
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
            signature.Parameters);
        if (signature.Parameters is not NamedCallableParameterLayout named ||
            bound.Values.Length >= named.ParameterNames.Length)
        {
            return bound;
        }

        // Presence-aware consumers must distinguish an omitted optional argument
        // from an explicit Python None, so retain the complete signature shape.
        var values = new object[named.ParameterNames.Length];
        Array.Fill(values, PyNone.Instance);
        Array.Copy(bound.Values, values, bound.Values.Length);
        return new BoundCallArguments(values, bound.Assigned.WithLength(named.ParameterNames.Length));
    }

    private static BoundCallArguments BindNamedArgumentsCore(
        CallArgumentValue[] arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
        CallableParameterLayout parameters)
    {
        if (parameters is PositionalCallableParameterLayout)
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

        var named = (NamedCallableParameterLayout)parameters;
        var parameterNames = named.ParameterNames;
        var bound = new object[parameterNames.Length];
        Array.Fill(bound, PyNone.Instance);
        var assigned = new ArgumentPresence(parameterNames.Length);
        List<object>? extraPositional = null;
        var positionalIndex = 0;

        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                if (!named.MaximumPositionalArgumentCount.Accepts(positionalIndex + 1))
                {
                    throw CallErrors.TooManyPositional(callableKind, callableName, span);
                }

                while (positionalIndex < assigned.Length && assigned[positionalIndex])
                {
                    positionalIndex++;
                }

                if (positionalIndex >= bound.Length)
                {
                    if (!named.AllowsExtraPositional)
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
            if (!named.ParameterIndices.TryGetValue(keywordName, out var index))
            {
                if (named.AllowsExtraKeywords)
                {
                    continue;
                }

                throw CallErrors.UnexpectedKeyword(callableKind, callableName, keywordName, span);
            }

            if (index < named.PositionalOnlyCount)
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

        for (var i = 0; i < named.RequiredCount; i++)
        {
            if (!assigned[i])
            {
                throw CallErrors.MissingArgument(callableKind, callableName, parameterNames[i], span);
            }
        }

        var count = parameterNames.Length;
        // Plain bound callables model defaults by omitting only the unassigned suffix.
        // An explicitly supplied None remains present and prevents this trimming.
        while (count > named.RequiredCount && !assigned[count - 1])
        {
            count--;
        }

        if (!named.MaximumArgumentCount.Accepts(count))
        {
            throw CallErrors.TooManyPositional(callableKind, callableName, span);
        }

        if (extraPositional is null || extraPositional.Count == 0)
        {
            if (count != bound.Length)
            {
                Array.Resize(ref bound, count);
            }

            return new BoundCallArguments(bound, assigned.WithLength(count));
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

}
