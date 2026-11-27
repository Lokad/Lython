using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

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
        PythonCallableKind callableKind,
        bool useNullForOmittedArguments = false)
    {
        return BindNamedArgumentsCore(
            new PacketInputs(arguments),
            span,
            signature.Name,
            callableKind,
            signature.Parameters,
            out _,
            useNullForOmittedArguments: useNullForOmittedArguments).Values;
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
        LythonRuntime.ExecutionContext context, bool preservePresence, bool useNullForOmittedArguments)
        => BindNamedArgumentsCore(new OperandInputs(operands, call.ArgumentForms, context), call.CallSpan,
            signature.Name, callableKind, signature.Parameters, out _, preservePresence,
            useNullForOmittedArguments: useNullForOmittedArguments);

    internal static BoundBuiltinArguments BindShortPositionalOperands(Span<LythonRuntime.ExecutableSlot> operands,
        LythonSourceSpan span, LythonCallableSignature signature, PythonCallableKind callableKind,
        bool preservePresence, bool useNullForOmittedArguments)
        => BindNamedArgumentsCore(new ShortPositionalOperands(operands), span,
            signature.Name, callableKind, signature.Parameters, out _, preservePresence,
            useNullForOmittedArguments: useNullForOmittedArguments);

    // These entry points have fixed named slots and allow extra keywords for
    // their user-supplied constructors. The cursor is gone before a constructor
    // or option callback can run. Only the final binding survives suspension.
    internal static BoundBuiltinArguments BindExactKeywordOptions(ReadOnlySpan<LythonRuntime.ExecutableSlot> positional,
        PyDict dictionary, NamedCallableParameterLayout parameters, LythonRuntime.ExecutionContext context,
        LythonSourceSpan mappingSpan, MemoryGovernor.TemporaryMemoryReservation storage,
        out IReadOnlyList<KeyValuePair<string, object>> extras, out int duplicate)
    {
        var size = parameters.ParameterNames.Length;
        var values = new object[size];
        Array.Fill(values, PyNone.Instance);
        var assigned = new ArgumentPresence(size);
        for (var i = 0; i < positional.Length; i++)
        {
            values[i] = positional[i].Object.RequireNotNull();
            assigned[i] = true;
        }

        List<KeyValuePair<string, object>>? extraKeywords = null;
        var sourceCount = dictionary.Count;
        var cursor = new PyDict.ItemCursor(dictionary);
        var parameterLookup = parameters.BorrowParameterLookup();
        Span<char> keyword = stackalloc char[32];
        duplicate = -1;
        try
        {
            while (cursor.MoveNext(out var pair, context, mappingSpan))
            {
                context.CheckExecution(mappingSpan);
                if (pair.Key is not PyString key) throw RuntimeErrors.Type("keywords must be strings", mappingSpan);
                string? name = null;
                var known = key.ByteLength <= keyword.Length
                    ? parameterLookup.TryGetValue(keyword[..key.CopyUtf16To(keyword)], out var index)
                    : parameters.ParameterIndices.TryGetValue(
                        name = CallArgumentValue.Keyword(key, pair.Value).GetKeywordName(context, mappingSpan), out index);
                if (known)
                {
                    if (assigned[index])
                    {
                        // Expansion must still validate later keys before
                        // the call can report a binding error.
                        if (duplicate < 0) duplicate = index;
                    }
                    else
                    {
                        values[index] = pair.Value;
                        assigned[index] = true;
                    }
                    continue;
                }

                name ??= CallArgumentValue.Keyword(key, pair.Value).GetKeywordName(context, mappingSpan);
                if (extraKeywords is null)
                {
                    storage.Grow(checked(64L + 16L * sourceCount), mappingSpan);
                    context.CheckExecution(mappingSpan);
                    extraKeywords = new List<KeyValuePair<string, object>>(sourceCount);
                }
                extraKeywords.Add(new(name, pair.Value));
            }
        }
        finally { cursor.Dispose(); }

        extras = extraKeywords is null ? [] : extraKeywords;
        return new BoundBuiltinArguments(values, assigned);
    }

    internal static void ValidateRequiredNamedArguments(ArgumentPresence assigned, NamedCallableParameterLayout named,
        PythonCallableKind callableKind, string callableName, LythonSourceSpan span)
    {
        for (var i = 0; i < named.RequiredCount; i++)
            if (!assigned[i]) throw CallErrors.MissingArgument(callableKind, callableName, named.ParameterNames[i], span);
    }

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

    // The iterator checks cancellation before binding its short operand row.
    private readonly ref struct ShortPositionalOperands : IBindingInputs
    {
        private readonly Span<LythonRuntime.ExecutableSlot> _operands;

        internal ShortPositionalOperands(Span<LythonRuntime.ExecutableSlot> operands) => _operands = operands;

        public int Length => _operands.Length;
        public string? KeywordAt(int index) => null;
        public object ValueAt(int index) => _operands[index].Object.RequireNotNull();
        public void CheckExecution(int index, LythonSourceSpan span) { }
    }

    internal readonly ref struct OperandInputs : IBindingInputs
    {
        private readonly Span<LythonRuntime.ExecutableSlot> _operands;
        private readonly CallArgumentForm[]? _forms;
        private readonly LythonRuntime.ExecutionContext _context;

        internal OperandInputs(Span<LythonRuntime.ExecutableSlot> operands, CallArgumentForm[]? forms,
            LythonRuntime.ExecutionContext context)
        {
            _operands = operands;
            _forms = forms;
            _context = context;
        }

        public int Length => _operands.Length;
        public string? KeywordAt(int index)
        {
            if (_forms is null) return null;
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

    internal static int BindFixedExecutableArguments(Span<LythonRuntime.ExecutableSlot> operands,
        ExecutableCallSite call, LythonCallableSignature signature, LythonRuntime.ExecutionContext context,
        scoped Span<object> values)
        => BindArgumentValues(new OperandInputs(operands, call.ArgumentForms, context),
            call.CallSpan, signature.Name, PythonCallableKind.Builtin, signature.Parameters,
            out _, out _, out _, values);

    private static BoundBuiltinArguments BindNamedArgumentsCore<T>(
        T arguments,
        LythonSourceSpan span,
        string callableName,
        PythonCallableKind callableKind,
        CallableParameterLayout parameters,
        out KeyValuePair<string, object>[] extras,
        bool preservePresence = false,
        bool collectExtras = false,
        bool useNullForOmittedArguments = false)
        where T : IBindingInputs, allows ref struct
    {
        _ = BindArgumentValues(arguments, span, callableName, callableKind, parameters, out extras,
            out var values, out var assigned, default, preservePresence, collectExtras, useNullForOmittedArguments);
        return new BoundBuiltinArguments(values!, assigned);
    }

    // Validate the input shape once, then write the selected final storage.
    // A synchronous fixed-width caller supplies its own managed stack slots.
    private static int BindArgumentValues<T>(T arguments, LythonSourceSpan span, string callableName,
        PythonCallableKind callableKind, CallableParameterLayout parameters,
        out KeyValuePair<string, object>[] extras, out object[]? values, out ArgumentPresence resultAssigned,
        scoped Span<object> destination, bool preservePresence = false, bool collectExtras = false,
        bool useNullForOmittedArguments = false) where T : IBindingInputs, allows ref struct
    {
        values = null;
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

            Span<object> positionalOnly = destination.IsEmpty
                ? values = new object[arguments.Length]
                : destination[..arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                arguments.CheckExecution(i, span);
                positionalOnly[i] = arguments.ValueAt(i);
            }

            resultAssigned = ArgumentPresence.Empty;
            return positionalOnly.Length;
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

        ValidateRequiredNamedArguments(assigned, named, callableKind, callableName, span);

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
        Span<object> result = destination.IsEmpty
            ? values = length == 0 ? [] : new object[length]
            : destination[..length];
        if (extraKeywordCount != 0) extras = new KeyValuePair<string, object>[extraKeywordCount];
        // Receiver windows use null for unbound slots. A supplied Python
        // None must remain distinguishable, including across sparse keywords.
        if (!useNullForOmittedArguments) result.Fill(PyNone.Instance);
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

        resultAssigned = assigned.WithLength(length);
        resultAssigned.MarkRange(count, extraPositionalCount);
        return length;
    }

}
