using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly string[] FunctoolsWrapperAssignmentNames =
    [
        "__module__",
        "__name__",
        "__qualname__",
        "__doc__",
        "__annotations__"
    ];

    private static readonly string[] FunctoolsWrapperUpdateNames = ["__dict__"];

    private static readonly OrderingMethod[] OrderingMethods =
        [OrderingMethod.Lt, OrderingMethod.Le, OrderingMethod.Gt, OrderingMethod.Ge];

    private static readonly PyNamedTupleType FunctoolsCacheInfoType =
        new("CacheInfo", ["hits", "misses", "maxsize", "currsize"]);

    private static PyDict BuildFunctoolsMetadataDictionary(IReadOnlyDictionary<string, object> metadata)
    {
        var dictionary = new PyDict();
        foreach (var pair in metadata)
        {
            dictionary.SetItem(PyString.FromString(pair.Key), pair.Value);
        }

        return dictionary;
    }

    private static PyDict BuildFunctoolsMetadataDictionary(
        IReadOnlyDictionary<string, object> metadata,
        MemoryGovernor governor,
        LythonSourceSpan span)
    {
        var dictionary = new PyDict(governor, span);
        foreach (var pair in metadata)
        {
            dictionary.SetItem(PyString.FromString(pair.Key), pair.Value);
        }

        return dictionary;
    }

    private sealed class FunctoolsModule : PyModule
    {
        public static readonly FunctoolsModule Instance = new();

        private FunctoolsModule() : base("functools")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "WRAPPER_ASSIGNMENTS" => CreateStringTuple(FunctoolsWrapperAssignmentNames),
                "WRAPPER_UPDATES" => CreateStringTuple(FunctoolsWrapperUpdateNames),
                "Placeholder" => UnsupportedPartialPlaceholder.Instance,
                "update_wrapper" => UpdateWrapperCallable.Instance,
                "wraps" => WrapsCallable.Instance,
                "total_ordering" => BuiltinCallable.Create(LythonKnownCallableSignatures.FunctoolsTotalOrdering, TotalOrdering),
                "reduce" => BuiltinCallable.Create(LythonKnownCallableSignatures.FunctoolsReduce, Reduce),
                "partial" => PartialFactory.Instance,
                "partialmethod" => PartialMethodFactory.Instance,
                "cmp_to_key" => BuiltinCallable.Create(LythonKnownCallableSignatures.FunctoolsCmpToKey, CmpToKey),
                "lru_cache" => LruCacheFactory.Instance,
                "cache" => CacheFactory.Instance,
                "cached_property" => CachedPropertyFactory.Instance,
                "singledispatch" => SingleDispatchFactory.Instance,
                "singledispatchmethod" => SingleDispatchMethodFactory.Instance,
                "recursive_repr" => RecursiveReprFactory.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class UnsupportedPartialPlaceholder : IPyRenderableValue, IPyHashableValue
    {
        public static readonly UnsupportedPartialPlaceholder Instance = new();

        public int GetPyHashCode() => 0x5F3759DF;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("functools.Placeholder");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static PyTuple CreateStringTuple(IReadOnlyList<string> values)
        => new(values.Select(PyString.FromString).Cast<object>());

}
