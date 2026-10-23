using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal static partial class PyDataclass
{
    public static object ReplaceInstance(
        PyInstance instance,
        IReadOnlyList<CallArgumentValue> changeArguments,
        LythonSourceSpan span,
        LythonRuntime.ExecutionContext context,
        string owner)
    {
        if (instance.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(obj, **changes) expects a dataclass instance.", span);
        }

        var changes = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var argument in changeArguments)
        {
            if (argument.IsPositional)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(obj, **changes) only accepts keyword changes after the dataclass instance.", span);
            }

            if (!changes.TryAdd(argument.KeywordName, argument.Value))
            {
                throw CallErrors.MultipleValues(PythonCallableKind.Builtin, owner, argument.KeywordName, span);
            }
        }

        foreach (var change in changes.Keys)
        {
            if (!instance.Type.DataclassFieldsByName.RequireNotNull().TryGetValue(change, out var field))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}() got an unexpected field '{change}'.", span);
            }

            if (!field.Init)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}() cannot override init=False field '{change}'.", span);
            }
        }

        var callArguments = new List<CallArgumentValue>(instance.Type.DataclassFields.Count);
        foreach (var field in instance.Type.DataclassFields)
        {
            if (field.Kind == DataclassFieldKind.ClassVar || !field.Init)
            {
                continue;
            }

            if (changes.TryGetValue(field.Name, out var changedValue))
            {
                callArguments.Add(CallArgumentValue.Keyword(field.Name, changedValue));
                continue;
            }

            if (field.Kind == DataclassFieldKind.InitVar)
            {
                if (field.HasDefaultFactory)
                {
                    callArguments.Add(CallArgumentValue.Keyword(field.Name, DataclassInitMethod.InvokeDefaultFactory(field, span, context)));
                    continue;
                }

                if (field.HasDefault)
                {
                    callArguments.Add(CallArgumentValue.Keyword(field.Name, field.DefaultValue));
                    continue;
                }

                throw new LythonRuntimeException("ValueError", $"InitVar '{field.Name}' must be specified with {owner}().", span);
            }

            _ = instance.TryGetOwnAttribute(field.Name, out var existingValue);
            callArguments.Add(CallArgumentValue.Keyword(field.Name, existingValue ?? PyNone.Instance));
        }

        return instance.Type.Invoke(callArguments.ToArray(), span, context);
    }

    public static object IsDataclass(object[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => arguments[0] switch { PyType type => HasFieldMap(type), PyInstance instance => HasFieldMap(instance.Type), _ => false };

    private static bool HasFieldMap(PyType type) => type.TryGetMember("__dataclass_fields__",out _);

    private static IEnumerable<object> VisibleFields(PyType type, LythonSourceSpan span)
    {
        if (!type.TryGetMember("__dataclass_fields__",out var raw) || raw is not PyDict map)
            throw RuntimeErrors.Type("dataclass fields require a dataclass type or instance",span);
        foreach(var pair in map)
        {
            if(pair.Value is not PyDataclassFieldObject field) throw RuntimeErrors.Type("invalid dataclass field metadata",span);
            if(field.Kind==DataclassFieldKind.Normal)yield return field;
        }
    }

    public static object Fields(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context)
    {
        var type=arguments[0] switch {PyType cls=>cls,PyInstance instance=>instance.Type,_=>throw RuntimeErrors.Type("fields() requires a dataclass type or instance",span)};
        using var fields=PyIteration.DrainLeased(VisibleFields(type,span),span,context);
        return new PyTuple(fields.Items,context.MemoryGovernor,span);
    }

    public static object AsDict(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments,span,context,false,false).GetAwaiter().GetResult();
    public static ValueTask<object> AsDictAsync(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments,span,context,false,true);
    public static object AsTuple(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments,span,context,true,false).GetAwaiter().GetResult();
    public static ValueTask<object> AsTupleAsync(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context)
        => ConvertRoot(arguments,span,context,true,true);

    private static ValueTask<object> ConvertRoot(object[] arguments,LythonSourceSpan span,LythonRuntime.ExecutionContext context,bool tuple,bool asynchronous)
    {
        if(arguments[0] is not PyInstance instance || !HasFieldMap(instance.Type))
            throw RuntimeErrors.Type(tuple?"astuple() should be called on a dataclass instance":"asdict() should be called on a dataclass instance",span);
        return ConvertValue(instance,arguments.Length>1?arguments[1]:null,tuple,asynchronous,context,span);
    }

    internal static object ReadField(PyInstance instance,string name,LythonRuntime.ExecutionContext context,LythonSourceSpan span)
        => instance.TryGetAttribute(name,context,span,out var value)?value:throw new LythonRuntimeException("AttributeError",$"'{instance.Type.Name}' object has no attribute '{name}'",span);
    internal static async ValueTask<object> ReadFieldAsync(PyInstance instance,string name,LythonRuntime.ExecutionContext context,LythonSourceSpan span)
    {
        var result=await PyAttributeLookup.TryResolveInstanceMemberAsync(instance,name,context,span).ConfigureAwait(false);
        return result.Found?result.Value:throw new LythonRuntimeException("AttributeError",$"'{instance.Type.Name}' object has no attribute '{name}'",span);
    }

    public static int CompareOrderedInstances(PyInstance left, PyInstance right, LythonSourceSpan span)
    {
        if (!ReferenceEquals(left.Type, right.Type) || left.Type.DataclassFields is null)
        {
            throw new LythonRuntimeException("TypeError", "Values are not comparable.", span);
        }

        using (PyStructuralGuard.EnterPair(left, right, span))
        {
            foreach (var field in left.Type.DataclassComparableFields.RequireNotNull())
            {
                PyStructuralGuard.NoteWork();
                _ = left.TryGetOwnAttribute(field.Name, out var leftValue);
                _ = right.TryGetOwnAttribute(field.Name, out var rightValue);
                var result = PyComparison.Compare(leftValue ?? PyNone.Instance, rightValue ?? PyNone.Instance, span);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }
    }

}
