using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal enum DataclassFieldKind
{
    Normal,
    InitVar,
    ClassVar,
}

internal enum DataclassHashMode
{
    Identity,
    Generated,
    Unhashable,
}

internal sealed record DataclassFieldSpec(
    string Name,
    object Annotation,
    DataclassFieldKind Kind,
    bool HasDefault,
    object DefaultValue,
    bool HasDefaultFactory,
    object DefaultFactory,
    bool Init,
    bool Repr,
    bool Compare,
    bool? Hash,
    bool KwOnly,
    PyDataclassFieldObject Field,
    bool Store);

internal sealed class PyDataclassFieldObject : IPyRenderableValue
{
    internal DataclassFieldKind Kind { get; set; }
    public string? Name { get; set; }
    public object Annotation { get; set; } = PyNone.Instance;
    public object Default { get; set; } = PyDataclassMissing.Instance;
    public object DefaultFactory { get; init; } = PyDataclassMissing.Instance;
    public bool HasDefault => !ReferenceEquals(Default, PyDataclassMissing.Instance);
    public bool HasDefaultFactory => !ReferenceEquals(DefaultFactory, PyDataclassMissing.Instance);
    public object Init { get; init; } = true;
    public object Repr { get; init; } = true;
    public object Compare { get; init; } = true;
    public object Hash { get; init; } = PyNone.Instance;
    public object KwOnly { get; set; } = PyDataclassMissing.Instance;
    public required object Metadata { get; init; }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        switch (name)
        {
            case "name":
                value = Name is null ? PyNone.Instance : PyString.FromString(Name);
                return true;
            case "type":
                value = Annotation;
                return true;
            case "default":
                value = Default;
                return true;
            case "default_factory":
                value = DefaultFactory;
                return true;
            case "init":
                value = Init;
                return true;
            case "repr":
                value = Repr;
                return true;
            case "compare":
                value = Compare;
                return true;
            case "hash":
                value = Hash;
                return true;
            case "kw_only":
                value = KwOnly;
                return true;
            case "metadata":
                value = Metadata;
                return true;
            default:
                value = PyNone.Instance;
                return false;
        }
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString($"Field(name='{Name}')");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassParamsObject : IPyRenderableValue
{
    public PyDataclassParamsObject(DataclassDecoratorSyntax options, IReadOnlyDictionary<string, object>? raw = null)
    {
        Init = raw is not null && raw.TryGetValue("init", out var rawInit) ? rawInit : options.Init;
        Repr = raw is not null && raw.TryGetValue("repr", out var rawRepr) ? rawRepr : options.Repr;
        Eq = raw is not null && raw.TryGetValue("eq", out var rawEq) ? rawEq : options.Eq;
        Order = raw is not null && raw.TryGetValue("order", out var rawOrder) ? rawOrder : options.Order;
        UnsafeHash = raw is not null && raw.TryGetValue("unsafe_hash", out var rawUnsafeHash) ? rawUnsafeHash : options.UnsafeHash;
        Frozen = raw is not null && raw.TryGetValue("frozen", out var rawFrozen) ? rawFrozen : options.Frozen;
        KwOnly = raw is not null && raw.TryGetValue("kw_only", out var rawKwOnly) ? rawKwOnly : options.KwOnly;
        MatchArgs = raw is not null && raw.TryGetValue("match_args", out var rawMatchArgs) ? rawMatchArgs : options.MatchArgs;
    }

    public object Init { get; }

    public object Repr { get; }

    public object Eq { get; }

    public object Order { get; }

    public object UnsafeHash { get; }

    public object Frozen { get; }

    public object KwOnly { get; }

    public object MatchArgs { get; }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name switch
        {
            "init" => Init,
            "repr" => Repr,
            "eq" => Eq,
            "order" => Order,
            "unsafe_hash" => UnsafeHash,
            "frozen" => Frozen,
            "kw_only" => KwOnly,
            "match_args" => MatchArgs,
            _ => PyNone.Instance
        };
        return name is "init" or "repr" or "eq" or "order" or "unsafe_hash" or "frozen" or "kw_only" or "match_args";
    }

    public PyString RenderPython(PyRenderingContext context)
        => PyString.FromString($"_DataclassParams(init={Init}, repr={Repr}, eq={Eq}, order={Order}, unsafe_hash={UnsafeHash}, frozen={Frozen}, kw_only={KwOnly}, match_args={MatchArgs})");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassMissing : IPyRenderableValue
{
    public static readonly PyDataclassMissing Instance = new();

    private PyDataclassMissing()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("MISSING");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassKwOnlyMarker : IPyRenderableValue
{
    public static readonly PyDataclassKwOnlyMarker Instance = new();

    private PyDataclassKwOnlyMarker()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("KW_ONLY");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}

internal sealed class PyDataclassInitVarMarker : IPyRenderableValue, IPyDynamicAttributes,
    IPyGovernedValue
{
    public static readonly PyDataclassInitVarMarker Instance = new();

    private readonly object? _type;

    public MemoryGovernor? OwnerMemoryGovernor { get; private init; }
    public LythonSourceSpan? AllocationSpan { get; private init; }

    private PyDataclassInitVarMarker()
    {
    }

    private PyDataclassInitVarMarker(object type) => _type = type;

    public object GetSubscript(object type, LythonSourceSpan span, LythonRuntime.ExecutionContext? context)
    {
        if (_type is not null) throw RuntimeErrors.NotSubscriptable(span);
        context?.CheckExecution(span);
        context?.MemoryGovernor.Reserve(64L, span);
        context?.MemoryGovernor.Commit(64L);
        return new PyDataclassInitVarMarker(type) { OwnerMemoryGovernor = context?.MemoryGovernor, AllocationSpan = span };
    }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        value = name == "type" && _type is not null ? _type : MissingMemberValue.Instance;
        return !ReferenceEquals(value, MissingMemberValue.Instance);
    }

    public PyString RenderPython(PyRenderingContext context)
    {
        if (_type is null) return PyString.FromString("<class 'dataclasses.InitVar'>");
        var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, AllocationSpan);
        try
        {
            builder.AppendAscii("dataclasses.InitVar[");
            switch (_type)
            {
                case PyBuiltinRuntimeType type: builder.AppendString(type.Name); break;
                case PyType type: builder.AppendString(type.Name); break;
                case INamedRuntimeCallable callable: builder.AppendString(callable.Name); break;
                default: builder.Append(PyRendering.ToReprPyString(_type, context)); break;
            }
            builder.AppendAscii("]");
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
