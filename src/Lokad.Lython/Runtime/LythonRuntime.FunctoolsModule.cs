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

    private static bool TryReadFunctoolsMetadata(PyDict? metadata, string name, MemoryGovernor owner,
        LythonSourceSpan? span, ExecutionContext? context, [MaybeNullWhen(false)] out object value)
    {
        if (metadata is null) { value = PyNone.Instance; return false; }
        var governor = context?.MemoryGovernor ?? owner;
        var key = PyString.FromString(name, governor, span);
        var mayEscape = metadata.HasProtocolKeys;
        try
        {
            return context is null ? metadata.TryGetValue(key, out value)
                : metadata.TryGetValue(key, out value, context, span ?? new LythonSourceSpan(0, 0, 0, 0));
        }
        finally
        {
            // Only guest key callbacks can retain an unexposed lookup string.
            if (!mayEscape) governor.RefundUnpublishedValue(key);
        }
    }

    private static bool SetFunctoolsMetadata(ref PyDict? metadata, string name, object value,
        MemoryGovernor governor, LythonSourceSpan? span)
    {
        if (name == "__dict__")
        {
            if (value is not PyDict dictionary)
                throw new LythonRuntimeException("TypeError", "__dict__ must be set to a dictionary", span);
            // The old dictionary can have live aliases. Its owner keeps it funded.
            metadata = dictionary;
            return true;
        }
        metadata ??= new PyDict(governor, span);
        StoreFunctoolsStringKey(metadata, name, value, governor, span);
        return true;
    }

    private static void StoreFunctoolsStringKey(PyDict dictionary, string name, object value,
        MemoryGovernor governor, LythonSourceSpan? span)
    {
        var key = PyString.FromString(name, governor, span);
        var mayEscape = dictionary.HasProtocolKeys;
        var before = dictionary.Count;
        try { dictionary.SetItem(key, value); }
        finally
        {
            // Native replacement retains the original key. Adapter publication
            // before an exception still retains the newly inserted key.
            if (!mayEscape && dictionary.Count == before) governor.RefundUnpublishedValue(key);
        }
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
                "total_ordering" => BuiltinCallable.Create(LythonKnownCallableSignatures.FunctoolsTotalOrdering, TotalOrdering, TotalOrderingAsync),
                "reduce" => BuiltinCallable.Create(LythonKnownCallableSignatures.FunctoolsReduce, Reduce, ReduceAsync),
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
