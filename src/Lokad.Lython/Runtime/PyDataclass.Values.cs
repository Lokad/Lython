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
    object Metadata,
    bool Store);

internal sealed class PyDataclassFieldDefinition
{
    public required bool HasDefault { get; init; }

    public required object DefaultValue { get; init; }

    public required bool HasDefaultFactory { get; init; }

    public required object DefaultFactory { get; init; }

    public required bool Init { get; init; }

    public required bool Repr { get; init; }

    public required bool Compare { get; init; }

    public required bool? Hash { get; init; }

    public required bool? KwOnly { get; init; }

    public required object Metadata { get; init; }
}

internal sealed class PyDataclassFieldObject : IPyRenderableValue
{
    public PyDataclassFieldObject(DataclassFieldSpec field)
    {
        Name = field.Name;
        Annotation = field.Annotation;
        Default = field.HasDefault ? field.DefaultValue : PyDataclassMissing.Instance;
        DefaultFactory = field.HasDefaultFactory ? field.DefaultFactory : PyDataclassMissing.Instance;
        Init = field.Init;
        Repr = field.Repr;
        Compare = field.Compare;
        Hash = field.Hash is null ? PyNone.Instance : field.Hash.Value;
        KwOnly = field.KwOnly;
        Metadata = field.Metadata;
    }

    public string Name { get; }

    public object Annotation { get; }

    public object Default { get; }

    public object DefaultFactory { get; }

    public bool Init { get; }

    public bool Repr { get; }

    public bool Compare { get; }

    public object Hash { get; }

    public bool KwOnly { get; }

    public object Metadata { get; }

    public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
    {
        switch (name)
        {
            case "name":
                value = PyString.FromString(Name);
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

internal sealed class PyDataclassAnnotationValue(ExpressionSyntax expression) : IPyRenderableValue
{
    public ExpressionSyntax Expression { get; } = expression;

    public PyString RenderPython(PyRenderingContext context)
    {
        _ = context;
        return PyString.FromString(Format(Expression));
    }

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    public override string ToString() => Format(Expression);

    private static string Format(ExpressionSyntax expression)
        => expression switch
        {
            IdentifierExpressionSyntax identifier => identifier.Name,
            MemberExpressionSyntax member => $"{Format(member.Target)}.{member.MemberName}",
            SubscriptExpressionSyntax subscript => $"{Format(subscript.Target)}[{Format(subscript.Index)}]",
            TupleLiteralExpressionSyntax tuple => string.Join(", ", tuple.Items.Select(static item => Format(item.Expression))),
            StringLiteralExpressionSyntax text => $"'{text.Value}'",
            IntegerLiteralExpressionSyntax integer => integer.ValueText,
            FloatLiteralExpressionSyntax floating => floating.ValueText,
            BooleanLiteralExpressionSyntax boolean => boolean.Value ? "True" : "False",
            NoneLiteralExpressionSyntax => "None",
            ParenthesizedExpressionSyntax parenthesized => $"({Format(parenthesized.Inner)})",
            _ => "<annotation>"
        };
}

internal sealed class PyDataclassParamsObject : IPyRenderableValue
{
    public PyDataclassParamsObject(DataclassDecoratorSyntax options)
    {
        Init = options.Init;
        Repr = options.Repr;
        Eq = options.Eq;
        Order = options.Order;
        UnsafeHash = options.UnsafeHash;
        Frozen = options.Frozen;
        KwOnly = options.KwOnly;
        MatchArgs = options.MatchArgs;
    }

    public bool Init { get; }

    public bool Repr { get; }

    public bool Eq { get; }

    public bool Order { get; }

    public bool UnsafeHash { get; }

    public bool Frozen { get; }

    public bool KwOnly { get; }

    public bool MatchArgs { get; }

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
        return !ReferenceEquals(value, PyNone.Instance);
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

internal sealed class PyDataclassInitVarMarker : IPyRenderableValue
{
    public static readonly PyDataclassInitVarMarker Instance = new();

    private PyDataclassInitVarMarker()
    {
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("InitVar");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
