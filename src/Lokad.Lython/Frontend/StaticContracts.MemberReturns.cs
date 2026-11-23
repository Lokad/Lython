namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static (string MemberName, StaticReturnShape ReturnShape)[] GetStringMemberReturnShapes() =>
    [
        ("lower", StaticReturnShape.String),
        ("capitalize", StaticReturnShape.String),
        ("upper", StaticReturnShape.String),
        ("swapcase", StaticReturnShape.String),
        ("title", StaticReturnShape.String),
        ("casefold", StaticReturnShape.String),
        ("strip", StaticReturnShape.String),
        ("lstrip", StaticReturnShape.String),
        ("rstrip", StaticReturnShape.String),
        ("center", StaticReturnShape.String),
        ("ljust", StaticReturnShape.String),
        ("rjust", StaticReturnShape.String),
        ("zfill", StaticReturnShape.String),
        ("expandtabs", StaticReturnShape.String),
        ("replace", StaticReturnShape.String),
        ("removeprefix", StaticReturnShape.String),
        ("removesuffix", StaticReturnShape.String),
        ("split", StaticReturnShape.ListOfString),
        ("rsplit", StaticReturnShape.ListOfString),
        ("splitlines", StaticReturnShape.ListOfString),
        ("startswith", StaticReturnShape.Boolean),
        ("endswith", StaticReturnShape.Boolean),
        ("islower", StaticReturnShape.Boolean),
        ("isupper", StaticReturnShape.Boolean),
        ("istitle", StaticReturnShape.Boolean),
        ("isascii", StaticReturnShape.Boolean),
        ("isdecimal", StaticReturnShape.Boolean),
        ("isidentifier", StaticReturnShape.Boolean),
        ("isnumeric", StaticReturnShape.Boolean),
        ("isprintable", StaticReturnShape.Boolean),
        ("isalpha", StaticReturnShape.Boolean),
        ("isdigit", StaticReturnShape.Boolean),
        ("isalnum", StaticReturnShape.Boolean),
        ("isspace", StaticReturnShape.Boolean),
        ("find", StaticReturnShape.Integer),
        ("index", StaticReturnShape.Integer),
        ("rfind", StaticReturnShape.Integer),
        ("rindex", StaticReturnShape.Integer),
        ("count", StaticReturnShape.Integer),
        ("join", StaticReturnShape.String),
        ("format", StaticReturnShape.String),
        ("format_map", StaticReturnShape.String),
        ("encode", StaticReturnShape.Bytes),
    ];

    private static class MemberReturnCatalog
    {
        internal static readonly Dictionary<StaticMemberContractKey, StaticReturnShape> ByMember = CreateMemberReturnShapes();
    }

    public static bool TryGetMemberReturn(AbstractValue receiver, string memberName, LythonSourceSpan span,
        StaticAnalysisContext? context, out AbstractValue value)
    {
        if (TryGetMemberReturnShape(receiver.Kind, memberName, out var shape))
        {
            value = CreateReturnValue(shape, receiver, span, context);
            return true;
        }

        value = default;
        return false;
    }

    internal static bool TryGetMemberReturnShape(AbstractValueKind receiverKind, string memberName, out StaticReturnShape shape)
        => MemberReturnCatalog.ByMember.TryGetValue(new(receiverKind, memberName), out shape);

    private static Dictionary<StaticMemberContractKey, StaticReturnShape> CreateMemberReturnShapes()
    {
        var core = CreateCoreMemberReturnContracts();
        var extended = CreateExtendedMemberReturnContracts();
        var strings = GetStringMemberReturnShapes();
        var shapes = new Dictionary<StaticMemberContractKey, StaticReturnShape>(core.Length + extended.Length + strings.Length * 2);

        foreach (var contract in core)
            shapes.Add(new(contract.ReceiverKind, contract.MemberName), contract.ReturnShape);
        foreach (var (memberName, shape) in strings)
        {
            shapes.Add(new(AbstractValueKind.String, memberName), shape);
            shapes.Add(new(AbstractValueKind.StringType, memberName), shape);
        }
        foreach (var contract in extended)
            shapes.Add(new(contract.ReceiverKind, contract.MemberName), contract.ReturnShape);

        return shapes;
    }
}
