using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object EvaluateConditional(ConditionalExpressionSyntax conditional, ExecutionContext context)
        => IsTruthy(EvaluateExpression(conditional.Condition, context), context, conditional.Condition.Span)
            ? EvaluateExpression(conditional.Consequent, context)
            : EvaluateExpression(conditional.Alternative, context);

    private static bool TryMatchPattern(LoweredPattern pattern, object subject, ExecutionContext context,
        Span<object> captures, Span<object> workspace)
    {
        context.EnterInterpreterFrame(pattern.Span);
        try
        {
            switch (pattern)
            {
                case LoweredValuePattern value:
                    return ElementEquals(subject, EvaluateLoweredExpression(value.Expression, context), context, pattern.Span);
                case LoweredSingletonPattern singleton:
                    return singleton.Singleton.Value switch
                    {
                        MatchSingletonKind.None => subject is PyNone,
                        MatchSingletonKind.True => subject is true,
                        MatchSingletonKind.False => subject is false,
                        _ => false
                    };
                case LoweredCapturePattern capture:
                    captures[capture.Slot] = RuntimeValue(subject);
                    return true;
                case LoweredWildcardPattern: return true;
                case LoweredAsPattern alias:
                    if (!TryMatchPattern(alias.Pattern, subject, context, captures, workspace)) return false;
                    captures[alias.Slot] = RuntimeValue(subject);
                    return true;
                case LoweredOrPattern alternatives:
                    foreach (var candidate in alternatives.Patterns)
                        if (TryMatchPattern(candidate, subject, context, captures, workspace)) return true;
                    return false;
                case LoweredSequencePattern sequence:
                    return TryMatchSequencePattern(sequence, subject, context, captures, workspace);
                case LoweredMappingPattern mapping:
                    return TryMatchMappingPattern(mapping, subject, context, captures, workspace);
                case LoweredClassPattern type:
                    return TryMatchClassPattern(type, subject, context, captures, workspace);
                default: throw new InvalidOperationException($"Unknown lowered pattern: {pattern.GetType().Name}");
            }
        }
        finally
        {
            context.LeaveInterpreterFrame();
            context.CheckExecution(pattern.Span);
        }
    }

    private static bool TryMatchSequencePattern(LoweredSequencePattern pattern, object subject,
        ExecutionContext context, Span<object> captures, Span<object> workspace)
    {
        IReadOnlyList<object>? items = subject switch
        {
            PyList list => list,
            PyTuple tuple => tuple,
            UrllibParseModule.UrlResult result => result,
            _ => null
        };
        if (items is null) return false;
        var count = pattern.Items.Length;
        var star = pattern.StarIndex;
        if (star < 0 ? items.Count != count : items.Count < count - 1) return false;
        var values = workspace[..count];
        // Extract all positions before invoking any subpattern. In particular,
        // a descriptor/equality callback may mutate a matched list.
        for (var i = 0; i < count; i++)
        {
            context.CheckExecution(pattern.Span);
            if (i == star)
            {
                if (pattern.Items[i] is LoweredWildcardPattern) continue;
                var length = items.Count - count + 1;
                var rest = new PyList(length, context.MemoryGovernor, pattern.Span);
                try
                {
                    for (var j = 0; j < length; j++)
                    {
                        context.CheckExecution(pattern.Span);
                        rest.Add(items[star + j]);
                    }
                    values[i] = rest;
                }
                catch { rest.DiscardUnpublishedStorage(); throw; }
            }
            else values[i] = items[star < 0 || i < star ? i : items.Count - count + i];
        }
        for (var i = 0; i < count; i++)
        {
            if (i == star && pattern.Items[i] is LoweredWildcardPattern) continue;
            if (!TryMatchPattern(pattern.Items[i], values[i], context, captures, workspace[count..])) return false;
        }
        return true;
    }

    private static bool TryMatchMappingPattern(LoweredMappingPattern pattern, object subject,
        ExecutionContext context, Span<object> captures, Span<object> workspace)
    {
        var count = pattern.Items.Length;
        if (subject is not PyDict dict || dict.Count < count) return false;
        using var ambient = PyStructuralGuard.PushAmbient(context, pattern.Span);
        var dynamic = pattern.ConstantKeys is null;
        ReadOnlySpan<object> keys = dynamic ? workspace[..count] : pattern.ConstantKeys;
        var offset = dynamic ? count : 0;
        var values = workspace.Slice(offset, count);
        PySet? seen = null;
        try
        {
            if (dynamic && count != 0)
            {
                seen = new PySet(context.MemoryGovernor, pattern.Span);
                seen.EnsureCapacity(count);
                for (var i = 0; i < count; i++)
                {
                    workspace[i] = EvaluateLoweredExpression(pattern.Items[i].Key, context);
                    context.CheckExecution(pattern.Items[i].Key.Span);
                }
            }
            for (var i = 0; i < count; i++)
            {
                var span = pattern.Items[i].Key.Span;
                var key = ValidateDictionaryKey(keys[i], span);
                if (seen is not null)
                {
                    var duplicate = seen.Contains(key);
                    context.CheckExecution(span);
                    if (duplicate)
                    {
                        var rendered = PyRendering.ToReprPyString(key, new(context));
                        context.CheckExecution(span);
                        throw new LythonRuntimeException("ValueError", "mapping pattern checks duplicate key (" + rendered + ")", span);
                    }
                    seen.Add(key);
                    context.CheckExecution(span);
                }
                var found = dict.TryGetValue(key, out var value, context, span);
                context.CheckExecution(span);
                if (!found) return false;
                values[i] = value!;
            }
            for (var i = 0; i < count; i++)
                if (!TryMatchPattern(pattern.Items[i].Pattern, values[i], context, captures, workspace[(offset + count)..])) return false;
            if (pattern.RestSlot < 0) return true;
            var rest = new PyDict(dict, context.MemoryGovernor, pattern.Span);
            var published = false;
            try
            {
                foreach (var key in keys)
                {
                    context.CheckExecution(pattern.Span);
                    var removed = rest.TryRemove(key, out _);
                    context.CheckExecution(pattern.Span);
                    if (!removed) throw RuntimeErrors.MissingKey(key, pattern.Span);
                }
                captures[pattern.RestSlot] = rest;
                context.CheckExecution(pattern.Span);
                published = true;
                return true;
            }
            finally { if (!published) context.MemoryGovernor.RefundUnpublishedValue(rest); }
        }
        finally { seen?.ReleaseUnpublishedStorage(); }
    }

    private static bool TryMatchRootPattern(LoweredRootPattern pattern, object subject,
        ExecutionContext context, Span<object> storage)
        => TryMatchPattern(pattern.Root, subject, context, storage[..pattern.Bindings.Length],
            storage[pattern.Bindings.Length..]);

    private static bool TryExecuteLoweredPattern(LoweredRootPattern pattern, object subject,
        ExecutionContext context)
    {
        using var admission = context.MemoryGovernor.ReserveTemporary(PyList.EstimateCapacityBytes(pattern.StorageCount), pattern.Root.Span);
        var storage = new object[pattern.StorageCount];
        try
        {
            if (!TryMatchRootPattern(pattern, subject, context, storage)) return false;
            for (var i = 0; i < pattern.Bindings.Length; i++)
                StoreName(pattern.Bindings[i].Name, storage[i], context, pattern.Root.Span);
            return true;
        }
        finally { Array.Clear(storage); }
    }

    private static bool TryMatchClassPattern(
        LoweredClassPattern pattern,
        object subject,
        ExecutionContext context,
        Span<object> captures, Span<object> workspace)
    {
        var classValue = EvaluateLoweredExpression(pattern.Expression, context);
        if (classValue is PyTypedDictType)
            throw new LythonRuntimeException("TypeError", "TypedDict does not support instance or class checks.", pattern.Span);
        if (!IsSupportedTypeSpecifier(classValue))
            throw RuntimeErrors.Type("called match pattern must be a type", pattern.Span);
        if (!IsInstanceAgainstSingleType(subject, classValue, context)) return false;

        var positionalCount = pattern.PositionalPatterns.Length;
        var count = checked(positionalCount + pattern.KeywordPatterns.Length);
        var values = workspace[..count];
        var names = workspace.Slice(count, positionalCount);
        var children = workspace[(count + positionalCount)..];
        PyTuple? matchArgs = null;
        var matchSelf = false;
        long nameBytes = 0;
        var className = classValue switch
        {
            PyType type => type.Name,
            UrllibParseModule.UrlResultType type => type.ShortName,
            INamedRuntimeCallable callable => callable.Name,
            _ => GetBuiltinTypeName(classValue, context) ?? "type"
        };
        if (positionalCount != 0)
        {
            if (TryReadPatternAttribute(classValue, "__match_args__", context, pattern.Span, out var metadata))
            {
                matchArgs = metadata as PyTuple ?? throw RuntimeErrors.Type(
                    $"{className}.__match_args__ must be a tuple (got {RuntimeErrors.OperandTypeName(metadata)})", pattern.Span);
            }
            else
            {
                matchSelf = GetBuiltinTypeName(classValue, context) is "bool" or "bytes" or "dict" or "float" or
                    "int" or "list" or "set" or "str" or "tuple";
            }
            var maximum = matchArgs?.Count ?? (matchSelf ? 1 : 0);
            if (positionalCount > maximum)
                throw RuntimeErrors.Type($"{className}() accepts {maximum} positional sub-pattern{(maximum == 1 ? "" : "s")} ({positionalCount} given)", pattern.Span);
            // Decode only the selected names. Their transient CLR storage stays
            // admitted until matching ends; no full metadata array or cache.
            if (matchArgs is not null)
                for (var i = 0; i < positionalCount; i++)
                {
                    context.CheckExecution(pattern.Span);
                    if (matchArgs[i] is PyString text)
                        nameBytes = checked(nameBytes + 32L + 2L * text.ByteLength);
                }
        }

        using var nameAdmission = context.MemoryGovernor.ReserveTemporary(nameBytes, pattern.Span);
        try
        {
            for (var i = 0; i < positionalCount; i++)
            {
                context.CheckExecution(pattern.Span);
                if (matchSelf) { values[i] = subject; continue; }
                if (matchArgs![i] is not PyString text)
                    throw RuntimeErrors.Type($"__match_args__ elements must be strings (got {RuntimeErrors.OperandTypeName(matchArgs[i])})", pattern.Span);
                var name = text.AsString();
                names[i] = name;
                for (var previous = 0; previous < i; previous++)
                {
                    context.CheckExecution(pattern.Span);
                    if (name == (string)names[previous])
                        throw RuntimeErrors.Type($"{className}() got multiple sub-patterns for attribute '{name}'", pattern.Span);
                }
                if (!TryReadPatternAttribute(subject, name, context, pattern.Span, out var value)) return false;
                values[i] = value;
            }
            for (var i = 0; i < pattern.KeywordPatterns.Length; i++)
            {
                var keyword = pattern.KeywordPatterns[i];
                for (var previous = 0; previous < positionalCount; previous++)
                {
                    context.CheckExecution(pattern.Span);
                    if (keyword.Name == names[previous] as string)
                        throw RuntimeErrors.Type($"{className}() got multiple sub-patterns for attribute '{keyword.Name}'", pattern.Span);
                }
                if (!TryReadPatternAttribute(subject, keyword.Name, context, keyword.Pattern.Span, out var value)) return false;
                values[positionalCount + i] = value;
            }
            // Attribute extraction is complete before any subpattern invokes guest
            // equality/descriptors or mutates the subject. Failed extraction never
            // publishes captures; the enclosing owner retains and clears scratch.
            for (var i = 0; i < positionalCount; i++)
                if (!TryMatchPattern(pattern.PositionalPatterns[i], values[i], context, captures, children)) return false;
            for (var i = 0; i < pattern.KeywordPatterns.Length; i++)
                if (!TryMatchPattern(pattern.KeywordPatterns[i].Pattern, values[positionalCount + i], context, captures, children)) return false;
            return true;
        }
        finally
        {
            names.Clear();
            values.Clear();
        }
    }

    private static bool TryReadPatternAttribute(object subject, string name, ExecutionContext context,
        LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        try
        {
            var found = PyMemberAccess.TryResolve(subject, name, context, span, out value);
            context.CheckExecution(span);
            return found;
        }
        catch (LythonRuntimeException error) when (error.ExceptionType == "AttributeError")
        {
            // AttributeError means mismatch, but cannot suppress a first host
            // refusal or cooperative cancellation hidden by the descriptor.
            context.CheckExecution(span);
            value = null;
            return false;
        }
    }

    private static IReadOnlyList<PyType> ResolveClassBases(object[] baseValues, LythonSourceSpan span, ExecutionContext context)
    {
        if (baseValues.Length == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        var bases = new List<PyType>(baseValues.Length);
        foreach (var baseValue in baseValues)
        {
            if (baseValue is PyTypingAlias { IsClassBaseMarker: true } or PyGenericAlias { Definition.IsClassBaseMarker: true })
            {
                continue;
            }

            if (baseValue is LythonRuntime.JsonEncoderClass or LythonRuntime.JsonDecoderClass)
            {
                continue;
            }

            if ((baseValue is PyGenericAlias generic ? generic.Origin : baseValue) is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "Class bases must be user-defined Lython classes.", span);
            }

            bases.Add(type);
        }

        if (bases.Count == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        return bases;
    }
}
