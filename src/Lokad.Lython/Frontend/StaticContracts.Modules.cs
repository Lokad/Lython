namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly Dictionary<string, HashSet<string>> ModuleMembers = new(StringComparer.Ordinal)
    {
        ["__future__"] = Members("annotations"),
        ["sys"] = Members(
            "argv",
            "stdin",
            "stdout",
            "stderr",
            "version",
            "version_info",
            "hexversion",
            "implementation",
            "platform",
            "maxsize",
            "byteorder",
            "prefix",
            "base_prefix",
            "executable",
            "path",
            "modules",
            "builtin_module_names",
            "stdlib_module_names",
            "exit",
            "getdefaultencoding",
            "exc_info",
            "getsizeof",
            "settrace",
            "setprofile",
            "setrecursionlimit",
            "addaudithook",
            "audit"),
        ["argparse"] = Members(
            "ArgumentParser",
            "Namespace",
            "FileType",
            "ArgumentError",
            "ArgumentTypeError",
            "HelpFormatter",
            "RawDescriptionHelpFormatter",
            "RawTextHelpFormatter",
            "ArgumentDefaultsHelpFormatter",
            "SUPPRESS",
            "OPTIONAL",
            "ZERO_OR_MORE",
            "ONE_OR_MORE",
            "PARSER",
            "REMAINDER"),
        ["dataclasses"] = Members("dataclass", "Field", "field", "make_dataclass", "is_dataclass", "fields", "asdict", "astuple", "replace", "MISSING", "KW_ONLY", "InitVar", "FrozenInstanceError"),
        ["typing"] = Members(
            "Any",
            "Optional",
            "Union",
            "List",
            "Dict",
            "Tuple",
            "Set",
            "FrozenSet",
            "Sequence",
            "Iterable",
            "Iterator",
            "Mapping",
            "MutableMapping",
            "Callable",
            "Type",
            "ClassVar",
            "Final",
            "Literal",
            "Annotated",
            "TYPE_CHECKING",
            "TypeVar",
            "NewType",
            "Generic",
            "Protocol",
            "NamedTuple",
            "TypedDict",
            "cast",
            "get_origin",
            "get_args"),
        ["pathlib"] = Members("Path", "PurePath", "PurePosixPath", "PosixPath", "PureWindowsPath", "WindowsPath"),
        ["pkgutil"] = Members(
            "ModuleInfo",
            "iter_modules",
            "walk_packages",
            "find_loader",
            "get_loader",
            "extend_path",
            "resolve_name",
            "get_importer",
            "iter_importers",
            "iter_importer_modules",
            "iter_zipimport_modules",
            "get_data",
            "read_code"),
        ["collections"] = Members("defaultdict", "Counter", "deque", "namedtuple", "OrderedDict", "ChainMap", "UserDict", "UserList", "UserString", "abc"),
        ["collections.abc"] = Members("Iterable", "Iterator", "Sequence", "MutableSequence", "Mapping", "MutableMapping", "Set", "MutableSet", "Callable"),
        ["itertools"] = Members(
            "chain",
            "count",
            "repeat",
            "cycle",
            "islice",
            "product",
            "zip_longest",
            "combinations",
            "combinations_with_replacement",
            "permutations",
            "accumulate",
            "compress",
            "filterfalse",
            "dropwhile",
            "takewhile",
            "starmap",
            "pairwise",
            "groupby",
            "tee",
            "batched"),
        ["os"] = Members(
            "path",
            "name",
            "sep",
            "curdir",
            "pardir",
            "linesep",
            "pathsep",
            "altsep",
            "extsep",
            "devnull",
            "F_OK",
            "R_OK",
            "W_OK",
            "X_OK",
            "environ",
            "listdir",
            "walk",
            "getcwd",
            "fspath",
            "fsencode",
            "fsdecode",
            "getenv",
            "putenv",
            "unsetenv",
            "get_exec_path",
            "stat",
            "lstat",
            "scandir",
            "mkdir",
            "makedirs",
            "remove",
            "unlink",
            "rename",
            "replace",
            "rmdir",
            "removedirs",
            "access",
            "chdir",
            "system",
            "popen",
            "open",
            "read",
            "write",
            "close",
            "dup",
            "fork",
            "execv",
            "execve",
            "spawnv",
            "spawnve",
            "chmod",
            "chown",
            "symlink",
            "link",
            "getpid",
            "kill"),
        ["os.path"] = Members(
            "join",
            "split",
            "splitext",
            "basename",
            "dirname",
            "isabs",
            "normpath",
            "normcase",
            "abspath",
            "relpath",
            "commonpath",
            "commonprefix",
            "exists",
            "lexists",
            "isfile",
            "isdir",
            "getsize",
            "getmtime",
            "getatime",
            "getctime",
            "samefile",
            "realpath",
            "expandvars",
            "expanduser",
            "splitdrive",
            "splitroot",
            "ismount",
            "islink",
            "supports_unicode_filenames"),
        ["glob"] = Members("glob", "iglob", "escape", "has_magic", "translate", "glob0", "glob1"),
        ["decimal"] = Members(
            "Decimal",
            "DecimalTuple",
            "Context",
            "getcontext",
            "setcontext",
            "localcontext",
            "DefaultContext",
            "BasicContext",
            "ExtendedContext",
            "DecimalException",
            "InvalidOperation",
            "DivisionByZero",
            "Inexact",
            "Rounded",
            "Overflow",
            "Underflow",
            "Subnormal",
            "Clamped",
            "FloatOperation",
            "ROUND_CEILING",
            "ROUND_FLOOR",
            "ROUND_HALF_UP",
            "ROUND_HALF_DOWN",
            "ROUND_HALF_EVEN",
            "ROUND_DOWN",
            "ROUND_UP",
            "ROUND_05UP"),
        ["math"] = Members(
            "pi",
            "e",
            "tau",
            "inf",
            "nan",
            "sqrt",
            "exp",
            "log",
            "log10",
            "log2",
            "sin",
            "cos",
            "tan",
            "asin",
            "acos",
            "atan",
            "atan2",
            "sinh",
            "cosh",
            "tanh",
            "floor",
            "ceil",
            "fabs",
            "trunc",
            "degrees",
            "radians",
            "isfinite",
            "isinf",
            "isnan",
            "pow",
            "hypot",
            "fmod",
            "copysign",
            "isclose",
            "prod",
            "fsum",
            "factorial",
            "gcd",
            "lcm",
            "comb",
            "perm",
            "isqrt",
            "dist",
            "frexp",
            "ldexp",
            "modf",
            "remainder",
            "nextafter",
            "ulp",
            "exp2",
            "expm1",
            "log1p",
            "cbrt",
            "erf",
            "erfc",
            "gamma",
            "lgamma",
            "fma",
            "sumprod"),
        ["datetime"] = Members("MINYEAR", "MAXYEAR", "UTC", "timedelta", "date", "time", "datetime", "timezone", "tzinfo"),
        ["statistics"] = Members(
            "StatisticsError",
            "mean",
            "fmean",
            "geometric_mean",
            "harmonic_mean",
            "median",
            "median_low",
            "median_high",
            "median_grouped",
            "mode",
            "multimode",
            "pstdev",
            "stdev",
            "pvariance",
            "variance",
            "quantiles",
            "covariance",
            "correlation",
            "linear_regression",
            "LinearRegression",
            "NormalDist",
            "kde",
            "kde_random"),
        ["random"] = Members(
            "Random",
            "SystemRandom",
            "BPF",
            "RECIP_BPF",
            "seed",
            "random",
            "getstate",
            "setstate",
            "randrange",
            "randint",
            "choice",
            "choices",
            "shuffle",
            "sample",
            "getrandbits",
            "randbytes",
            "uniform",
            "triangular",
            "betavariate",
            "expovariate",
            "gammavariate",
            "gauss",
            "normalvariate",
            "lognormvariate",
            "paretovariate",
            "vonmisesvariate",
            "weibullvariate"),
        ["copy"] = Members("copy", "deepcopy", "Error"),
        ["operator"] = Members("add", "sub", "mul", "truediv", "eq", "ne", "lt", "le", "gt", "ge", "getitem", "setitem", "contains", "itemgetter", "attrgetter", "methodcaller"),
        ["openpyxl"] = Members("Workbook", "load_workbook", "__version__", "utils", "workbook", "reader", "styles", "comments", "chart", "cell", "worksheet", "drawing"),
        ["openpyxl.workbook"] = Members("Workbook"),
        ["openpyxl.cell"] = Members("cell"),
        ["openpyxl.cell.cell"] = Members("Cell"),
        ["openpyxl.reader"] = Members("excel"),
        ["openpyxl.reader.excel"] = Members("load_workbook"),
        ["openpyxl.utils"] = Members("get_column_letter", "column_index_from_string", "coordinate_from_string", "coordinate_to_tuple", "range_boundaries", "get_column_interval", "absolute_coordinate", "quote_sheetname", "rows_from_range", "cols_from_range", "cell", "exceptions"),
        ["openpyxl.utils.cell"] = Members("get_column_letter", "column_index_from_string", "coordinate_from_string", "coordinate_to_tuple", "range_boundaries", "get_column_interval", "absolute_coordinate", "quote_sheetname", "rows_from_range", "cols_from_range"),
        ["openpyxl.utils.exceptions"] = Members("CellCoordinatesException", "IllegalCharacterError", "InvalidFileException", "NamedRangeException", "ReadOnlyWorkbookException", "SheetTitleException", "WorkbookAlreadySaved"),
        ["openpyxl.styles"] = Members("Font", "PatternFill", "GradientFill", "Alignment", "Border", "Side", "Protection", "NamedStyle", "colors"),
        ["openpyxl.styles.colors"] = Members("Color", "BLACK", "WHITE", "BLUE"),
        ["openpyxl.comments"] = Members("Comment"),
        ["openpyxl.chart"] = Members("BarChart", "LineChart", "PieChart", "ScatterChart", "Reference", "Series"),
        ["openpyxl.worksheet"] = Members("table", "datavalidation", "worksheet"),
        ["openpyxl.worksheet.worksheet"] = Members("Worksheet"),
        ["openpyxl.worksheet.table"] = Members("Table", "TableStyleInfo"),
        ["openpyxl.worksheet.datavalidation"] = Members("DataValidation"),
        ["openpyxl.drawing"] = Members("image"),
        ["openpyxl.drawing.image"] = Members("Image"),
        ["functools"] = Members("update_wrapper", "wraps", "total_ordering", "reduce", "partial", "cmp_to_key"),
        ["re"] = Members("compile", "search", "match", "fullmatch", "findall", "finditer", "sub", "subn", "split", "escape", "Pattern", "Match", "IGNORECASE", "I", "UNICODE", "U", "MULTILINE", "M", "DOTALL", "S", "VERBOSE", "X"),
        ["fnmatch"] = Members("fnmatch", "filter"),
        ["difflib"] = Members("IS_LINE_JUNK", "IS_CHARACTER_JUNK", "unified_diff", "context_diff", "ndiff", "restore", "get_close_matches", "diff_bytes", "Differ", "HtmlDiff", "SequenceMatcher"),
        ["json"] = Members("loads", "dumps"),
        ["csv"] = Members("reader", "writer", "DictReader", "DictWriter", "Error", "QUOTE_MINIMAL", "QUOTE_ALL", "QUOTE_NONE", "QUOTE_NONNUMERIC"),
        ["subprocess"] = Members("run", "call", "check_call", "check_output", "PIPE", "STDOUT", "DEVNULL"),
    };

    public static bool IsKnownBuiltinModule(string moduleName)
        => ModuleMembers.ContainsKey(moduleName);

    public static IReadOnlyList<string> GetModuleExportedMemberNames(string moduleName)
        => ModuleMembers.TryGetValue(moduleName, out var members)
            ? members.Where(static name => !name.StartsWith("_", StringComparison.Ordinal)).ToArray()
            : [];

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

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "utils", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.utils", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "workbook", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.workbook", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "reader", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.reader", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "styles", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.styles", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "comments", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.comments", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "chart", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.chart", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "cell", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.cell", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "worksheet", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.worksheet", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl", StringComparison.Ordinal) &&
            string.Equals(memberName, "drawing", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.drawing", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.reader", StringComparison.Ordinal) &&
            string.Equals(memberName, "excel", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.reader.excel", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.cell", StringComparison.Ordinal) &&
            string.Equals(memberName, "cell", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.cell.cell", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.utils", StringComparison.Ordinal) &&
            string.Equals(memberName, "cell", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.utils.cell", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.utils", StringComparison.Ordinal) &&
            string.Equals(memberName, "exceptions", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.utils.exceptions", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.styles", StringComparison.Ordinal) &&
            string.Equals(memberName, "colors", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.styles.colors", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.worksheet", StringComparison.Ordinal) &&
            string.Equals(memberName, "table", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.worksheet.table", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.worksheet", StringComparison.Ordinal) &&
            string.Equals(memberName, "datavalidation", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.worksheet.datavalidation", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.worksheet", StringComparison.Ordinal) &&
            string.Equals(memberName, "worksheet", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.worksheet.worksheet", span);
            return true;
        }

        if (string.Equals(moduleName, "openpyxl.drawing", StringComparison.Ordinal) &&
            string.Equals(memberName, "image", StringComparison.Ordinal))
        {
            value = AbstractValue.Module("openpyxl.drawing.image", span);
            return true;
        }

        var targetName = TryMapOpenPyxlCallableAlias(moduleName, memberName, out var openPyxlTargetName)
            ? openPyxlTargetName
            : $"{moduleName}.{memberName}";
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

    private static bool TryMapOpenPyxlCallableAlias(string moduleName, string memberName, out string targetName)
    {
        targetName = (moduleName, memberName) switch
        {
            ("openpyxl.workbook", "Workbook") => "openpyxl.Workbook",
            ("openpyxl.reader.excel", "load_workbook") => "openpyxl.load_workbook",
            ("openpyxl.utils.cell", "get_column_letter") => "openpyxl.utils.get_column_letter",
            ("openpyxl.utils.cell", "column_index_from_string") => "openpyxl.utils.column_index_from_string",
            ("openpyxl.utils.cell", "coordinate_from_string") => "openpyxl.utils.coordinate_from_string",
            ("openpyxl.utils.cell", "coordinate_to_tuple") => "openpyxl.utils.coordinate_to_tuple",
            ("openpyxl.utils.cell", "range_boundaries") => "openpyxl.utils.range_boundaries",
            ("openpyxl.utils.cell", "get_column_interval") => "openpyxl.utils.get_column_interval",
            ("openpyxl.utils.cell", "absolute_coordinate") => "openpyxl.utils.absolute_coordinate",
            ("openpyxl.utils.cell", "quote_sheetname") => "openpyxl.utils.quote_sheetname",
            ("openpyxl.utils.cell", "rows_from_range") => "openpyxl.utils.rows_from_range",
            ("openpyxl.utils.cell", "cols_from_range") => "openpyxl.utils.cols_from_range",
            ("openpyxl.styles", "Font") => "openpyxl.styles.Font",
            ("openpyxl.styles", "PatternFill") => "openpyxl.styles.PatternFill",
            ("openpyxl.styles", "Border") => "openpyxl.styles.Border",
            ("openpyxl.styles", "Side") => "openpyxl.styles.Side",
            ("openpyxl.styles", "Alignment") => "openpyxl.styles.Alignment",
            ("openpyxl.styles", "Protection") => "openpyxl.styles.Protection",
            ("openpyxl.styles", "NamedStyle") => "openpyxl.styles.NamedStyle",
            ("openpyxl.comments", "Comment") => "openpyxl.comments.Comment",
            ("openpyxl.worksheet.table", "Table") => "openpyxl.worksheet.table.Table",
            ("openpyxl.worksheet.table", "TableStyleInfo") => "openpyxl.worksheet.table.TableStyleInfo",
            ("openpyxl.worksheet.datavalidation", "DataValidation") => "openpyxl.worksheet.datavalidation.DataValidation",
            _ => string.Empty
        };

        return targetName.Length != 0;
    }

    private static bool TryCreateKnownModuleConstant(string moduleName, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        value = (moduleName, memberName) switch
        {
            ("__future__", "annotations") => AbstractValue.None(span),
            ("typing", "TYPE_CHECKING") => AbstractValue.Boolean(false, span),
            ("sys", "argv") => AbstractValue.ListOf(AbstractValue.StringType(span), span),
            ("sys", "stdin") => AbstractValue.TextFileHandle(AbstractTextFileMode.Read, span),
            ("sys", "stdout") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("sys", "stderr") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("sys", "version") => AbstractValue.StringType(span),
            ("sys", "version_info") => AbstractValue.Unknown(span),
            ("sys", "hexversion") => AbstractValue.IntegerType(span),
            ("sys", "implementation") => AbstractValue.Unknown(span),
            ("sys", "platform") => AbstractValue.StringType(span),
            ("sys", "maxsize") => AbstractValue.IntegerType(span),
            ("sys", "byteorder") => AbstractValue.StringType(span),
            ("sys", "prefix") => AbstractValue.StringType(span),
            ("sys", "base_prefix") => AbstractValue.StringType(span),
            ("sys", "executable") => AbstractValue.StringType(span),
            ("sys", "path") => AbstractValue.ListOf(AbstractValue.StringType(span), span),
            ("sys", "modules") => AbstractValue.Unknown(span),
            ("sys", "builtin_module_names") => AbstractValue.Unknown(span),
            ("sys", "stdlib_module_names") => AbstractValue.Unknown(span),
            ("argparse", "SUPPRESS") => AbstractValue.String("SUPPRESS", span),
            ("argparse", "OPTIONAL") => AbstractValue.String("?", span),
            ("argparse", "ZERO_OR_MORE") => AbstractValue.String("*", span),
            ("argparse", "ONE_OR_MORE") => AbstractValue.String("+", span),
            ("argparse", "PARSER") => AbstractValue.String("A...", span),
            ("argparse", "REMAINDER") => AbstractValue.String("...", span),
            ("os", "name") => AbstractValue.String("posix", span),
            ("os", "sep") => AbstractValue.String("/", span),
            ("os", "curdir") => AbstractValue.String(".", span),
            ("os", "pardir") => AbstractValue.String("..", span),
            ("os", "linesep") => AbstractValue.String("\n", span),
            ("os", "pathsep") => AbstractValue.String(":", span),
            ("os", "altsep") => AbstractValue.None(span),
            ("os", "extsep") => AbstractValue.String(".", span),
            ("os", "devnull") => AbstractValue.String("/dev/null", span),
            ("os", "F_OK") or ("os", "R_OK") or ("os", "W_OK") or ("os", "X_OK") => AbstractValue.IntegerType(span),
            ("os", "environ") => AbstractValue.Dict([], span),
            ("os.path", "supports_unicode_filenames") => AbstractValue.Boolean(true, span),
            ("math", "pi") or ("math", "e") or ("math", "tau") or ("math", "inf") or ("math", "nan") => AbstractValue.FloatType(span),
            ("random", "BPF") => AbstractValue.IntegerType(span),
            ("random", "RECIP_BPF") => AbstractValue.FloatType(span),
            ("openpyxl", "__version__") => AbstractValue.StringType(span),
            ("datetime", "MINYEAR") or ("datetime", "MAXYEAR") => AbstractValue.IntegerType(span),
            ("datetime", "UTC") => AbstractValue.DateTimeTimezone(span),
            ("decimal", "DefaultContext") or ("decimal", "BasicContext") or ("decimal", "ExtendedContext") => AbstractValue.DecimalContext(span),
            ("decimal", "ROUND_CEILING") or
            ("decimal", "ROUND_FLOOR") or
            ("decimal", "ROUND_HALF_UP") or
            ("decimal", "ROUND_HALF_DOWN") or
            ("decimal", "ROUND_HALF_EVEN") or
            ("decimal", "ROUND_DOWN") or
            ("decimal", "ROUND_UP") or
            ("decimal", "ROUND_05UP") => AbstractValue.StringType(span),
            ("re", "IGNORECASE") or ("re", "I") or ("re", "UNICODE") or ("re", "U") or ("re", "MULTILINE") or ("re", "M") or ("re", "DOTALL") or ("re", "S") or ("re", "VERBOSE") or ("re", "X") => AbstractValue.IntegerType(span),
            ("csv", "QUOTE_MINIMAL") or ("csv", "QUOTE_ALL") or ("csv", "QUOTE_NONE") or ("csv", "QUOTE_NONNUMERIC") => AbstractValue.IntegerType(span),
            ("subprocess", "PIPE") or ("subprocess", "STDOUT") or ("subprocess", "DEVNULL") => AbstractValue.IntegerType(span),
            _ => default
        };

        return value.Kind != default;
    }

    private static HashSet<string> Members(params string[] names)
        => new(names, StringComparer.Ordinal);
}
