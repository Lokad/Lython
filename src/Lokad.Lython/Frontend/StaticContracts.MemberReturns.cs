namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly (string MemberName, StaticReturnShape ReturnShape)[] StringMemberReturnShapes =
    [
        ("lower", StaticReturnShape.String),
        ("capitalize", StaticReturnShape.String),
        ("upper", StaticReturnShape.String),
        ("swapcase", StaticReturnShape.String),
        ("title", StaticReturnShape.String),
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

    private static readonly StaticMemberReturnContract[] MemberReturnContracts = CreateMemberReturnContracts();
    private static readonly IReadOnlyDictionary<StaticMemberContractKey, StaticMemberReturnContract> MemberReturnContractsByMember =
        MemberReturnContracts.ToDictionary(static contract => new StaticMemberContractKey(contract.ReceiverKind, contract.MemberName));

    public static bool TryGetMemberReturn(AbstractValue receiver, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (receiver.Kind == AbstractValueKind.ArgparseParser &&
            string.Equals(memberName, "parse_args", StringComparison.Ordinal))
        {
            var parser = receiver.RequirePayload<AbstractArgparseParserSummary>();
            value = AbstractValue.ArgparseNamespace(new AbstractArgparseNamespaceSummary(parser.Members, parser.IsSealed), span);
            return true;
        }

        if (MemberReturnContractsByMember.TryGetValue(
            new StaticMemberContractKey(receiver.Kind, memberName),
            out var contract))
        {
            value = CreateReturnValue(contract.ReturnShape, receiver, span);
            return true;
        }

        value = default;
        return false;
    }

    private static StaticMemberReturnContract[] CreateMemberReturnContracts()
    {
        var contracts = new List<StaticMemberReturnContract>(
            CoreMemberReturnContractCatalog.Contracts.Length +
            ExtendedMemberReturnContractCatalog.Contracts.Length +
            (StringMemberReturnShapes.Length * 2));
        contracts.AddRange(CoreMemberReturnContractCatalog.Contracts);

        foreach (var (memberName, returnShape) in StringMemberReturnShapes)
        {
            contracts.Add(new StaticMemberReturnContract(AbstractValueKind.String, memberName, returnShape));
            contracts.Add(new StaticMemberReturnContract(AbstractValueKind.StringType, memberName, returnShape));
        }

        contracts.AddRange(ExtendedMemberReturnContractCatalog.Contracts);

        return contracts.ToArray();
    }

}
