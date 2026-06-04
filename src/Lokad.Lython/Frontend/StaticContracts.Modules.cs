namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly Dictionary<string, HashSet<string>> ModuleMembers = new(StringComparer.Ordinal)
    {
        ["__future__"] = Members("annotations"),
        ["sys"] = Members("argv", "stdin", "stdout", "stderr", "exit"),
        ["argparse"] = Members("ArgumentParser", "Namespace", "SUPPRESS"),
        ["dataclasses"] = Members("dataclass", "field", "is_dataclass", "fields", "asdict", "astuple", "replace", "MISSING", "KW_ONLY", "InitVar", "FrozenInstanceError"),
        ["typing"] = Members("ClassVar"),
        ["pathlib"] = Members("Path"),
        ["collections"] = Members("defaultdict", "Counter", "deque"),
        ["itertools"] = Members("chain", "islice", "product", "zip_longest"),
        ["os"] = Members("path", "sep", "curdir", "pardir", "listdir", "walk", "getcwd", "mkdir", "makedirs", "remove", "unlink", "rename", "replace", "rmdir", "removedirs"),
        ["os.path"] = Members("join", "split", "splitext", "basename", "dirname", "isabs", "normpath", "abspath", "relpath", "commonpath", "exists", "isfile", "isdir"),
        ["glob"] = Members("glob", "iglob", "escape"),
        ["decimal"] = Members("Decimal", "InvalidOperation", "DivisionByZero", "ROUND_HALF_EVEN", "ROUND_DOWN", "ROUND_UP"),
        ["math"] = Members("pi", "e", "tau", "inf", "nan", "sqrt", "exp", "log", "log10", "log2", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "sinh", "cosh", "tanh", "floor", "ceil", "fabs", "trunc", "degrees", "radians", "isfinite", "isinf", "isnan", "pow", "hypot", "fmod", "copysign", "isclose", "prod", "fsum"),
        ["datetime"] = Members("MINYEAR", "MAXYEAR", "timedelta", "date", "time", "datetime", "timezone"),
        ["statistics"] = Members("StatisticsError", "mean", "fmean", "median", "median_low", "median_high", "mode", "multimode", "pstdev", "stdev", "pvariance", "variance"),
        ["random"] = Members("seed", "random", "randrange", "randint", "choice", "choices", "shuffle", "sample", "getrandbits"),
        ["copy"] = Members("copy", "deepcopy", "Error"),
        ["operator"] = Members("add", "sub", "mul", "truediv", "eq", "ne", "lt", "le", "gt", "ge", "getitem", "setitem", "contains", "itemgetter", "attrgetter", "methodcaller"),
        ["functools"] = Members("update_wrapper", "wraps", "total_ordering", "reduce", "partial", "cmp_to_key"),
        ["re"] = Members("compile", "search", "match", "fullmatch", "findall", "finditer", "sub", "subn", "split", "escape", "Pattern", "Match", "IGNORECASE", "I", "UNICODE", "U", "MULTILINE", "M", "DOTALL", "S", "VERBOSE", "X"),
        ["fnmatch"] = Members("fnmatch", "filter"),
        ["difflib"] = Members("unified_diff", "context_diff", "ndiff", "restore", "get_close_matches", "SequenceMatcher"),
        ["json"] = Members("loads", "dumps"),
        ["csv"] = Members("reader", "writer"),
        ["subprocess"] = Members("run"),
    };

    public static bool IsKnownBuiltinModule(string moduleName)
        => ModuleMembers.ContainsKey(moduleName);

    public static bool TryGetModuleMemberValue(string moduleName, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (!ModuleMembers.TryGetValue(moduleName, out var members) ||
            !members.Contains(memberName))
        {
            value = default;
            return false;
        }

        if (string.Equals(moduleName, "os", StringComparison.Ordinal) &&
            string.Equals(memberName, "path", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("os.path", span);
            return true;
        }

        var targetName = $"{moduleName}.{memberName}";
        if (TryGetKnownCallContract(targetName, out _))
        {
            value = AbstractValue.KnownCallable(targetName, span);
            return true;
        }

        value = TryCreateKnownModuleConstant(moduleName, memberName, span, out var constant)
            ? constant
            : AbstractValue.Unknown(span);
        return true;
    }

    private static bool TryCreateKnownModuleConstant(string moduleName, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        value = (moduleName, memberName) switch
        {
            ("__future__", "annotations") => AbstractValue.None(span),
            ("sys", "argv") => AbstractValue.ListOf(AbstractValue.StringType(span), span),
            ("sys", "stdin") => AbstractValue.TextFileHandle(AbstractTextFileMode.Read, span),
            ("sys", "stdout") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("sys", "stderr") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("argparse", "SUPPRESS") => AbstractValue.String("SUPPRESS", span),
            ("os", "sep") => AbstractValue.String("/", span),
            ("os", "curdir") => AbstractValue.String(".", span),
            ("os", "pardir") => AbstractValue.String("..", span),
            ("math", "pi") or ("math", "e") or ("math", "tau") or ("math", "inf") or ("math", "nan") => AbstractValue.FloatType(span),
            ("datetime", "MINYEAR") or ("datetime", "MAXYEAR") => AbstractValue.IntegerType(span),
            ("decimal", "ROUND_HALF_EVEN") or ("decimal", "ROUND_DOWN") or ("decimal", "ROUND_UP") => AbstractValue.StringType(span),
            ("re", "IGNORECASE") or ("re", "I") or ("re", "UNICODE") or ("re", "U") or ("re", "MULTILINE") or ("re", "M") or ("re", "DOTALL") or ("re", "S") or ("re", "VERBOSE") or ("re", "X") => AbstractValue.IntegerType(span),
            _ => default
        };

        return value.Kind != default;
    }

    private static HashSet<string> Members(params string[] names)
        => new(names, StringComparer.Ordinal);
}
