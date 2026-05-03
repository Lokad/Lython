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
    ];

    private static readonly StaticMemberReturnContract[] MemberReturnContracts = CreateMemberReturnContracts();

    public static bool TryGetMemberReturn(AbstractValue receiver, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (receiver.Kind == AbstractValueKind.ArgparseParser &&
            string.Equals(memberName, "parse_args", StringComparison.Ordinal))
        {
            var parser = (AbstractArgparseParserSummary)receiver.Value;
            value = AbstractValue.ArgparseNamespace(new AbstractArgparseNamespaceSummary(parser.Members, parser.IsSealed), span);
            return true;
        }

        foreach (var contract in MemberReturnContracts)
        {
            if (contract.ReceiverKind == receiver.Kind &&
                contract.MemberName == memberName)
            {
                value = CreateReturnValue(contract.ReturnShape, receiver, span);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static StaticMemberReturnContract[] CreateMemberReturnContracts()
    {
        var contracts = new List<StaticMemberReturnContract>
        {
            new(AbstractValueKind.Path, "read_text", StaticReturnShape.String),
            new(AbstractValueKind.Path, "as_posix", StaticReturnShape.String),
            new(AbstractValueKind.Path, "resolve", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "joinpath", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "relative_to", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "with_suffix", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "with_name", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "rename", StaticReturnShape.Path),
            new(AbstractValueKind.Path, "glob", StaticReturnShape.ListOfPath),
            new(AbstractValueKind.Path, "rglob", StaticReturnShape.ListOfPath),
            new(AbstractValueKind.Path, "exists", StaticReturnShape.Boolean),
            new(AbstractValueKind.Path, "is_file", StaticReturnShape.Boolean),
            new(AbstractValueKind.Path, "is_dir", StaticReturnShape.Boolean),
            new(AbstractValueKind.Path, "unlink", StaticReturnShape.None),
            new(AbstractValueKind.Path, "mkdir", StaticReturnShape.None),
            new(AbstractValueKind.TextFileHandle, "read", StaticReturnShape.String),
            new(AbstractValueKind.TextFileHandle, "readline", StaticReturnShape.String),
            new(AbstractValueKind.TextFileHandle, "readlines", StaticReturnShape.ListOfString),
            new(AbstractValueKind.TextFileHandle, "flush", StaticReturnShape.None),
        };

        foreach (var (memberName, returnShape) in StringMemberReturnShapes)
        {
            contracts.Add(new StaticMemberReturnContract(AbstractValueKind.String, memberName, returnShape));
            contracts.Add(new StaticMemberReturnContract(AbstractValueKind.StringType, memberName, returnShape));
        }

        contracts.AddRange(
        [
            new(AbstractValueKind.List, "append", StaticReturnShape.None),
            new(AbstractValueKind.List, "extend", StaticReturnShape.None),
            new(AbstractValueKind.List, "pop", StaticReturnShape.ListElement),
            new(AbstractValueKind.List, "copy", StaticReturnShape.ListSame),
            new(AbstractValueKind.List, "clear", StaticReturnShape.None),
            new(AbstractValueKind.ListType, "append", StaticReturnShape.None),
            new(AbstractValueKind.ListType, "extend", StaticReturnShape.None),
            new(AbstractValueKind.ListType, "pop", StaticReturnShape.ListElement),
            new(AbstractValueKind.ListType, "copy", StaticReturnShape.ListSame),
            new(AbstractValueKind.ListType, "clear", StaticReturnShape.None),
            new(AbstractValueKind.Dict, "copy", StaticReturnShape.DictSame),
            new(AbstractValueKind.Dict, "clear", StaticReturnShape.None),
            new(AbstractValueKind.Dict, "update", StaticReturnShape.None),
            new(AbstractValueKind.Set, "copy", StaticReturnShape.SetSame),
            new(AbstractValueKind.Set, "clear", StaticReturnShape.None),
            new(AbstractValueKind.Set, "add", StaticReturnShape.None),
            new(AbstractValueKind.Set, "discard", StaticReturnShape.None),
            new(AbstractValueKind.Set, "remove", StaticReturnShape.None),
            new(AbstractValueKind.RegexPattern, "sub", StaticReturnShape.String),
            new(AbstractValueKind.RegexPattern, "split", StaticReturnShape.ListOfString),
            new(AbstractValueKind.RegexPattern, "search", StaticReturnShape.MaybeRegexMatch),
            new(AbstractValueKind.RegexPattern, "match", StaticReturnShape.MaybeRegexMatch),
            new(AbstractValueKind.RegexPattern, "fullmatch", StaticReturnShape.MaybeRegexMatch),
            new(AbstractValueKind.ArgparseParser, "add_mutually_exclusive_group", StaticReturnShape.ArgparseMutuallyExclusiveGroup),
            new(AbstractValueKind.ArgparseParser, "parse_args", StaticReturnShape.ArgparseNamespace),
            new(AbstractValueKind.CsvWriter, "writerow", StaticReturnShape.None),
            new(AbstractValueKind.CsvWriter, "writerows", StaticReturnShape.None),
            new(AbstractValueKind.CsvWriter, "getvalue", StaticReturnShape.String),
        ]);

        return contracts.ToArray();
    }

}
