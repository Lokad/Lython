using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    public static object ReplaceInstance(PyInstance instance, IReadOnlyList<CallArgumentValue> changes, LythonSourceSpan span, LythonRuntime.ExecutionContext context, string owner)
        => ReplaceInstanceCore(instance, changes, span, context, owner, false).GetAwaiter().GetResult();
    public static ValueTask<object> ReplaceInstanceAsync(PyInstance instance, IReadOnlyList<CallArgumentValue> changes, LythonSourceSpan span, LythonRuntime.ExecutionContext context, string owner)
        => ReplaceInstanceCore(instance, changes, span, context, owner, true);
    private static async ValueTask<object> ReplaceInstanceCore(PyInstance instance, IReadOnlyList<CallArgumentValue> changeArguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, string owner, bool asynchronous)
    {
        if (!instance.Type.TryGetMember("__dataclass_fields__", out var raw) || raw is not PyDict map) throw RuntimeErrors.Type("replace() requires a dataclass instance", span);
        using var working = context.MemoryGovernor.ReserveTemporary(checked(128L + (map.Count + (long)changeArguments.Count) * 128L), span);
        var changes = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var argument in changeArguments)
        {
            if (argument.IsPositional) throw RuntimeErrors.Type("replace() accepts only keyword changes", span);
            if (!changes.TryAdd(argument.KeywordName, argument.Value)) throw CallErrors.MultipleValues(PythonCallableKind.Builtin, owner, argument.KeywordName, span);
        }
        foreach (var pair in map)
        {
            context.CheckExecution(span);
            if (pair.Value is not PyDataclassFieldObject field) throw RuntimeErrors.Type("invalid dataclass field metadata", span);
            if (field.Kind == DataclassFieldKind.ClassVar) continue;
            var name = field.Name.RequireNotNull();
            var init = asynchronous ? await LythonRuntime.IsTruthyAsync(field.Init, context, span).ConfigureAwait(false) : LythonRuntime.IsTruthy(field.Init, context, span);
            if (!init)
            { if (changes.ContainsKey(name)) throw new LythonRuntimeException("ValueError", $"cannot override init=False field '{name}'", span); continue; }
            if (changes.ContainsKey(name)) continue;
            if (field.Kind == DataclassFieldKind.InitVar && !field.HasDefault) throw new LythonRuntimeException("ValueError", $"InitVar '{name}' must be specified with replace()", span);
            changes[name] = asynchronous ? await ReadFieldAsync(instance, name, context, span).ConfigureAwait(false) : ReadField(instance, name, context, span);
        }
        var args = changes.Select(p => CallArgumentValue.Keyword(p.Key, p.Value)).ToArray();
        return asynchronous ? await instance.Type.InvokeAsync(args, span, context).ConfigureAwait(false) : instance.Type.Invoke(args, span, context);
    }

    public static object IsDataclass(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => arguments[0] switch { PyType type => HasFieldMap(type), PyInstance instance => HasFieldMap(instance.Type), _ => false };

    private static bool HasFieldMap(PyType type) => type.TryGetMember("__dataclass_fields__", out _);

    private static IEnumerable<object> VisibleFields(PyType type, LythonSourceSpan span)
    {
        if (!type.TryGetMember("__dataclass_fields__", out var raw) || raw is not PyDict map)
            throw RuntimeErrors.Type("dataclass fields require a dataclass type or instance", span);
        foreach (var pair in map)
        {
            if (pair.Value is not PyDataclassFieldObject field) throw RuntimeErrors.Type("invalid dataclass field metadata", span);
            if (field.Kind == DataclassFieldKind.Normal) yield return field;
        }
    }

    public static object Fields(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
    {
        var type = arguments[0] switch { PyType cls => cls, PyInstance instance => instance.Type, _ => throw RuntimeErrors.Type("fields() requires a dataclass type or instance", span) };
        using var fields = PyIteration.DrainLeased(VisibleFields(type, span), span, context);
        return new PyTuple(fields.Items, context.MemoryGovernor, span);
    }

    public static object AsDict(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments, span, context, false, false).GetAwaiter().GetResult();
    public static ValueTask<object> AsDictAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments, span, context, false, true);
    public static object AsTuple(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments, span, context, true, false).GetAwaiter().GetResult();
    public static ValueTask<object> AsTupleAsync(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments, span, context, true, true);

    private static ValueTask<object> ConvertRoot(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context, bool tuple, bool asynchronous)
    {
        if (arguments[0] is not PyInstance instance || !HasFieldMap(instance.Type))
            throw RuntimeErrors.Type(tuple ? "astuple() should be called on a dataclass instance" : "asdict() should be called on a dataclass instance", span);
        return ConvertValue(instance, arguments.Length > 1 ? arguments[1] : null, tuple, asynchronous, context, span);
    }

    internal static object ReadField(PyInstance instance, string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => instance.TryGetAttribute(name, context, span, out var value) ? value : throw new LythonRuntimeException("AttributeError", $"'{instance.Type.Name}' object has no attribute '{name}'", span);
    internal static async ValueTask<object> ReadFieldAsync(PyInstance instance, string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var result = await PyAttributeLookup.TryResolveInstanceMemberAsync(instance, name, context, span).ConfigureAwait(false);
        return result.Found ? result.Value : throw new LythonRuntimeException("AttributeError", $"'{instance.Type.Name}' object has no attribute '{name}'", span);
    }

    public static int CompareOrderedInstances(PyInstance left, PyInstance right, LythonSourceSpan span, LythonRuntime.ExecutionContext? context = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        if (!ReferenceEquals(left.Type, right.Type) || left.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", "Values are not comparable.", span);
        }

        using (PyStructuralGuard.EnterPair(left, right, span))
        {
            foreach (var field in left.Type.DataclassComparableFields.RequireNotNull())
            {
                PyStructuralGuard.NoteWork();
                var leftValue = ReadStructuralField(left, field.Name, span, context);
                var rightValue = ReadStructuralField(right, field.Name, span, context);
                var result = PyComparison.Compare(leftValue, rightValue, span);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }
    }

    internal static object ReadStructuralField(PyInstance instance, string name, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        if (context is not null && span is not null) return ReadField(instance, name, context, span);
        if (instance.TryGetOwnAttribute(name, out var value) || instance.Type.TryGetMember(name, out value)) return value;
        throw new LythonRuntimeException("AttributeError", $"missing dataclass field '{name}'", span);
    }

}
