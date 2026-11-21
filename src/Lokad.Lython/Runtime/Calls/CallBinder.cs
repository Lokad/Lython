using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal readonly record struct BoundBuiltinArguments(
    object[] Values,
    ArgumentPresence Assigned);

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
        ReadOnlySpan<CallArgumentValue> arguments,
        LythonSourceSpan span,
        LythonCallableSignature signature,
        PythonCallableKind callableKind)
    {
        return BindNamedArgumentsCore(
            new PacketInputs(arguments),
            span,
            signature.Name,
            callableKind,
            signature.Parameters,
            out _).Values;
    }

    public static BoundBuiltinArguments BindNamedArgumentsWithPresence(
        ReadOnlySpan<CallArgumentValue> arguments,
        LythonSourceSpan span,
        LythonCallableSignature signature,
        PythonCallableKind callableKind)
        => BindNamedArgumentsCore(new PacketInputs(arguments), span, signature.Name, callableKind,
            signature.Parameters, out _, preservePresence: true);

    internal static BoundBuiltinArguments BindNamedArgumentsWithExtras<T>(T arguments,
        LythonSourceSpan span, LythonCallableSignature signature, PythonCallableKind callableKind,
        out KeyValuePair<string, object>[] extras)
        where T : IBindingInputs, allows ref struct
        => BindNamedArgumentsCore(arguments, span, signature.Name, callableKind, signature.Parameters,
            out extras, preservePresence: true, collectExtras: true);

    internal static BoundBuiltinArguments BindExecutableArguments(Span<LythonRuntime.ExecutableSlot> operands,
        ExecutableCallSite call, LythonCallableSignature signature, PythonCallableKind callableKind,
        LythonRuntime.ExecutionContext context, bool preservePresence)
        => BindNamedArgumentsCore(new OperandInputs(operands, call.Arguments, context), call.CallSpan,
            signature.Name, callableKind, signature.Parameters, out _, preservePresence);

    // Both input shapes are borrowed only during binding. The constrained
    // calls let the JIT specialize the loops without a shape check per read.
    internal interface IBindingInputs
    {
        int Length { get; }
        string? KeywordAt(int index);
        object ValueAt(int index);
        void CheckExecution(int index, LythonSourceSpan span);
    }

    internal readonly ref struct PacketInputs : IBindingInputs
    {
        private readonly ReadOnlySpan<CallArgumentValue> _arguments;

        internal PacketInputs(ReadOnlySpan<CallArgumentValue> arguments) => _arguments = arguments;

        public int Length => _arguments.Length;
        public string? KeywordAt(int index) => _arguments[index].IsPositional ? null : _arguments[index].KeywordName;
        public object ValueAt(int index) => _arguments[index].Value;
        public void CheckExecution(int index, LythonSourceSpan span) { }
    }

    internal readonly ref struct OperandInputs : IBindingInputs
    {
        private readonly Span<LythonRuntime.ExecutableSlot> _operands;
        private readonly CallArgumentForm[] _forms;
        private readonly LythonRuntime.ExecutionContext _context;

        internal OperandInputs(Span<LythonRuntime.ExecutableSlot> operands, CallArgumentForm[] forms,
            LythonRuntime.ExecutionContext context)
        {
            _operands = operands;
            _forms = forms;
            _context = context;
        }

        public int Length => _operands.Length;
        public string? KeywordAt(int index)
        {
            var form = _forms[index];
            return form.Kind switch
            {
                CallArgumentKind.Positional => null,
                CallArgumentKind.Keyword => form.KeywordName,
                _ => throw new InvalidOperationException($"Executable call site contains unsupported argument kind {form.Kind}.")
            };
        }
        // Publish scalar operands in their original slots to preserve identity.
        public object ValueAt(int index) => _operands[index].Object.RequireNotNull();
        public void CheckExecution(int index, LythonSourceSpan span)
        {
            if ((index & 1023) == 0) _context.CheckExecution(span);
        }
    }

    private static BoundBuiltinArguments BindNamedArgumentsCore<T>(
        T arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
        CallableParameterLayout parameters,
        out KeyValuePair<string, object>[] extras,
        bool preservePresence = false,
        bool collectExtras = false)
        where T : IBindingInputs, allows ref struct
    {
        extras = [];
        if (parameters is PositionalCallableParameterLayout)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments.CheckExecution(i, span);
                if (arguments.KeywordAt(i) is not null)
                {
                    throw CallErrors.NoKeywordArguments(callableKind, callableName, span);
                }
            }

            var positionalOnly = new object[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments.CheckExecution(i, span);
                positionalOnly[i] = arguments.ValueAt(i);
            }

            return new BoundBuiltinArguments(positionalOnly, ArgumentPresence.Empty);
        }

        var named = (NamedCallableParameterLayout)parameters;
        var parameterNames = named.ParameterNames;
        var assigned = new ArgumentPresence(parameterNames.Length);
        var extraPositionalCount = 0;
        var extraKeywordCount = 0;
        var positionalIndex = 0;

        // Validate the already-expanded arguments before allocating their final
        // shape. No guest code runs during either pass, so the second pass can
        // place values directly without a full temporary array or extra list.
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments.CheckExecution(i, span);
            var keywordName = arguments.KeywordAt(i);
            if (keywordName is null)
            {
                if (!named.MaximumPositionalArgumentCount.Accepts(positionalIndex + 1))
                {
                    throw CallErrors.TooManyPositional(callableKind, callableName, span);
                }

                if (positionalIndex < assigned.Length && assigned[positionalIndex])
                {
                    throw CallErrors.MultipleValues(callableKind, callableName, parameterNames[positionalIndex], span);
                }

                if (positionalIndex >= parameterNames.Length)
                {
                    if (!named.AllowsExtraPositional)
                    {
                        throw CallErrors.TooManyPositional(callableKind, callableName, span);
                    }

                    extraPositionalCount++;
                    continue;
                }

                assigned[positionalIndex] = true;
                positionalIndex++;
                continue;
            }

            if (!named.ParameterIndices.TryGetValue(keywordName, out var index))
            {
                if (named.AllowsExtraKeywords)
                {
                    if (collectExtras) extraKeywordCount++;
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

        var length = preservePresence && extraPositionalCount == 0
            ? parameterNames.Length : count + extraPositionalCount;
        var result = length == 0 ? Array.Empty<object>() : new object[length];
        if (extraKeywordCount != 0) extras = new KeyValuePair<string, object>[extraKeywordCount];
        Array.Fill(result, PyNone.Instance);
        positionalIndex = 0;
        var extraIndex = count;
        var extraKeywordIndex = 0;
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments.CheckExecution(i, span);
            var keywordName = arguments.KeywordAt(i);
            if (keywordName is null)
            {
                result[positionalIndex < parameterNames.Length ? positionalIndex++ : extraIndex++] = arguments.ValueAt(i);
            }
            else if (named.ParameterIndices.TryGetValue(keywordName, out var index))
            {
                result[index] = arguments.ValueAt(i);
            }
            else if (extraKeywordCount != 0)
            {
                extras[extraKeywordIndex++] = new(keywordName, arguments.ValueAt(i));
            }
        }

        var resultAssigned = assigned.WithLength(length);
        resultAssigned.MarkRange(count, extraPositionalCount);
        return new BoundBuiltinArguments(result, resultAssigned);
    }

}
