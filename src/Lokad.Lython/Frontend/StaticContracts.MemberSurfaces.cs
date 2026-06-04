namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly HashSet<string> RegexPatternMembers = new(StringComparer.Ordinal)
    {
        "search",
        "match",
        "fullmatch",
        "findall",
        "finditer",
        "sub",
        "subn",
        "split",
    };

    private static readonly HashSet<string> RegexMatchMembers = new(StringComparer.Ordinal)
    {
        "group",
        "start",
        "end",
        "span",
    };

    private static readonly HashSet<string> ArgparseParserMembers = new(StringComparer.Ordinal)
    {
        "add_argument",
        "add_mutually_exclusive_group",
        "parse_args",
    };

    private static readonly HashSet<string> ArgparseGroupMembers = new(StringComparer.Ordinal)
    {
        "add_argument",
    };

    private static readonly HashSet<string> CsvWriterMembers = new(StringComparer.Ordinal)
    {
        "writerow",
        "writerows",
        "getvalue",
    };

    private static readonly HashSet<string> CompletedProcessMembers = new(StringComparer.Ordinal)
    {
        "args",
        "returncode",
        "stdout",
        "stderr",
        "check_returncode",
    };

    private static readonly HashSet<string> DataclassFieldMembers = new(StringComparer.Ordinal)
    {
        "name",
        "default",
        "default_factory",
        "init",
        "repr",
        "hash",
        "compare",
        "metadata",
        "kw_only",
    };

    private static readonly HashSet<string> StringMembers = new(StringComparer.Ordinal)
    {
        "replace",
        "startswith",
        "endswith",
        "lower",
        "capitalize",
        "islower",
        "upper",
        "swapcase",
        "title",
        "isupper",
        "isalpha",
        "isdigit",
        "isalnum",
        "isspace",
        "split",
        "rsplit",
        "splitlines",
        "expandtabs",
        "strip",
        "lstrip",
        "rstrip",
        "join",
        "center",
        "ljust",
        "rjust",
        "zfill",
        "find",
        "index",
        "rfind",
        "rindex",
        "count",
        "removeprefix",
        "removesuffix",
        "partition",
        "rpartition",
        "format",
        "format_map",
    };

    private static readonly HashSet<string> PathMembers = new(StringComparer.Ordinal)
    {
        "name",
        "suffix",
        "suffixes",
        "stem",
        "parent",
        "parents",
        "parts",
        "drive",
        "root",
        "anchor",
        "is_absolute",
        "joinpath",
        "match",
        "as_posix",
        "resolve",
        "absolute",
        "relative_to",
        "is_relative_to",
        "with_suffix",
        "with_name",
        "with_stem",
        "exists",
        "is_file",
        "is_dir",
        "is_symlink",
        "samefile",
        "stat",
        "unlink",
        "rmdir",
        "rename",
        "replace",
        "mkdir",
        "touch",
        "open",
        "iterdir",
        "glob",
        "read_text",
        "write_text",
        "rglob",
        "read_bytes",
        "write_bytes",
    };

    private static readonly HashSet<string> TextFileHandleMembers = new(StringComparer.Ordinal)
    {
        "__enter__",
        "__exit__",
        "close",
        "read",
        "readline",
        "readlines",
        "write",
        "writelines",
        "flush",
    };

    private static readonly HashSet<string> ListMembers = new(StringComparer.Ordinal)
    {
        "append",
        "extend",
        "pop",
        "copy",
        "clear",
    };

    private static readonly HashSet<string> DictMembers = new(StringComparer.Ordinal)
    {
        "get",
        "keys",
        "values",
        "items",
        "update",
        "pop",
        "copy",
        "clear",
        "setdefault",
    };

    private static readonly HashSet<string> SetMembers = new(StringComparer.Ordinal)
    {
        "add",
        "discard",
        "remove",
        "copy",
        "clear",
    };

    public static bool IsKnownSealedMemberSurface(AbstractValue value)
        => value.Kind is AbstractValueKind.Module && ModuleMembers.ContainsKey((string)value.Value) ||
           value.Kind is AbstractValueKind.Path or
            AbstractValueKind.String or
            AbstractValueKind.StringType or
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None or
            AbstractValueKind.TextFileHandle or
            AbstractValueKind.List or
            AbstractValueKind.ListType or
            AbstractValueKind.Tuple or
            AbstractValueKind.Dict or
            AbstractValueKind.Set or
            AbstractValueKind.RegexPattern or
            AbstractValueKind.RegexMatch or
            AbstractValueKind.ArgparseParser or
            AbstractValueKind.ArgparseMutuallyExclusiveGroup or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField ||
           value.Kind == AbstractValueKind.ArgparseNamespace &&
            ((AbstractArgparseNamespaceSummary)value.Value).IsSealed ||
           value.Kind == AbstractValueKind.UserInstance &&
            ((AbstractInstanceSummary)value.Value).Class.IsDataclass;

    public static bool HasKnownMember(AbstractValue value, string memberName)
    {
        return value.Kind switch
        {
            AbstractValueKind.Module => ModuleMembers.TryGetValue((string)value.Value, out var members) && members.Contains(memberName),
            AbstractValueKind.String or AbstractValueKind.StringType => StringMembers.Contains(memberName),
            AbstractValueKind.Bytes or
            AbstractValueKind.BytesType or
            AbstractValueKind.Integer or
            AbstractValueKind.IntegerType or
            AbstractValueKind.Float or
            AbstractValueKind.FloatType or
            AbstractValueKind.Boolean or
            AbstractValueKind.BooleanType or
            AbstractValueKind.None => false,
            AbstractValueKind.Path => PathMembers.Contains(memberName),
            AbstractValueKind.TextFileHandle => TextFileHandleMembers.Contains(memberName),
            AbstractValueKind.List or AbstractValueKind.ListType => ListMembers.Contains(memberName),
            AbstractValueKind.Tuple => false,
            AbstractValueKind.Dict => DictMembers.Contains(memberName),
            AbstractValueKind.Set => SetMembers.Contains(memberName),
            AbstractValueKind.RegexPattern => RegexPatternMembers.Contains(memberName),
            AbstractValueKind.RegexMatch => RegexMatchMembers.Contains(memberName),
            AbstractValueKind.ArgparseParser => ArgparseParserMembers.Contains(memberName),
            AbstractValueKind.ArgparseMutuallyExclusiveGroup => ArgparseGroupMembers.Contains(memberName),
            AbstractValueKind.ArgparseNamespace => ((AbstractArgparseNamespaceSummary)value.Value).Members.ContainsKey(memberName),
            AbstractValueKind.CsvWriter => CsvWriterMembers.Contains(memberName),
            AbstractValueKind.SubprocessCompletedProcess => CompletedProcessMembers.Contains(memberName),
            AbstractValueKind.DataclassField => DataclassFieldMembers.Contains(memberName),
            AbstractValueKind.UserInstance => HasKnownDataclassInstanceMember((AbstractInstanceSummary)value.Value, memberName),
            _ => false
        };
    }

    private static bool HasKnownDataclassInstanceMember(AbstractInstanceSummary instance, string memberName)
        => instance.Class.IsDataclass &&
           (instance.Class.Fields.Any(field => field.StoreOnInstance && string.Equals(field.Name, memberName, StringComparison.Ordinal)) ||
            instance.Class.Methods.ContainsKey(memberName));
}
