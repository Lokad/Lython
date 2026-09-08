using System.Collections.Frozen;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly Dictionary<string, BuiltinModuleSurface> ModuleSurfaces = new(StringComparer.Ordinal)
    {
        ["__future__"] = Members("annotations"),
        ["builtins"] = new BuiltinModuleSurface(ExecutionState.BuiltinNames.Concat(["__debug__", "__name__", "False", "None", "True"])),
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
        ["importlib"] = Members("import_module", "invalidate_caches", "util"),
        ["importlib.util"] = Members("find_spec", "resolve_name", "module_from_spec", "spec_from_file_location", "spec_from_loader"),
        ["filecmp"] = Members("cmp", "clear_cache", "dircmp"),
        ["hashlib"] = Members("md5", "sha1", "sha256", "sha384", "sha512", "new", "algorithms_available", "algorithms_guaranteed", "file_digest"),
        ["gzip"] = Members("open", "compress", "decompress", "BadGzipFile"),
        ["shlex"] = Members("shlex", "quote", "join", "split"),
        ["time"] = Members("time", "time_ns", "monotonic", "monotonic_ns", "perf_counter", "perf_counter_ns", "sleep", "get_clock_info", "process_time", "process_time_ns", "thread_time", "thread_time_ns", "clock_gettime", "clock_gettime_ns", "clock_getres", "clock_settime", "clock_settime_ns", "pthread_getcpuclockid", "gmtime", "localtime", "ctime", "mktime", "asctime", "strftime", "strptime", "struct_time", "timezone", "altzone", "daylight", "tzname", "tzset"),
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
        ["shutil"] = Members("Error", "SameFileError", "copyfile", "copy", "copy2", "move", "copyfileobj"),
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
        ["copy"] = Members("copy", "deepcopy", "replace", "Error", "error", "dispatch_table"),
        ["operator"] = Members(
            "truth",
            "not_",
            "is_",
            "is_not",
            "abs",
            "neg",
            "pos",
            "invert",
            "index",
            "add",
            "sub",
            "mul",
            "truediv",
            "floordiv",
            "mod",
            "pow",
            "matmul",
            "lshift",
            "rshift",
            "and_",
            "or_",
            "xor",
            "concat",
            "eq",
            "ne",
            "lt",
            "le",
            "gt",
            "ge",
            "getitem",
            "setitem",
            "delitem",
            "contains",
            "length_hint",
            "countOf",
            "indexOf",
            "iadd",
            "isub",
            "imul",
            "itruediv",
            "ifloordiv",
            "imod",
            "ipow",
            "ilshift",
            "irshift",
            "iand",
            "ior",
            "ixor",
            "iconcat",
            "call",
            "itemgetter",
            "attrgetter",
            "methodcaller"),
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
        ["functools"] = Members(
            "WRAPPER_ASSIGNMENTS",
            "WRAPPER_UPDATES",
            "Placeholder",
            "update_wrapper",
            "wraps",
            "total_ordering",
            "reduce",
            "partial",
            "partialmethod",
            "cmp_to_key",
            "lru_cache",
            "cache",
            "cached_property",
            "singledispatch",
            "singledispatchmethod",
            "recursive_repr"),
        ["re"] = Members("compile", "search", "match", "fullmatch", "findall", "finditer", "sub", "subn", "split", "escape", "purge", "error", "PatternError", "RegexFlag", "Pattern", "Match", "NOFLAG", "IGNORECASE", "I", "UNICODE", "U", "MULTILINE", "M", "DOTALL", "S", "VERBOSE", "X", "ASCII", "A", "LOCALE", "L", "DEBUG"),
        ["fnmatch"] = Members("fnmatch", "fnmatchcase", "filter", "translate"),
        ["difflib"] = Members("IS_LINE_JUNK", "IS_CHARACTER_JUNK", "unified_diff", "context_diff", "ndiff", "restore", "get_close_matches", "diff_bytes", "Differ", "HtmlDiff", "SequenceMatcher"),
        ["json"] = Members("load", "loads", "dump", "dumps", "JSONDecodeError", "JSONEncoder", "JSONDecoder"),
        ["csv"] = Members("reader", "writer", "DictReader", "DictWriter", "Error", "QUOTE_MINIMAL", "QUOTE_ALL", "QUOTE_NONE", "QUOTE_NONNUMERIC"),
        ["subprocess"] = Members("run", "call", "check_call", "check_output", "CompletedProcess", "CalledProcessError", "SubprocessError", "TimeoutExpired", "Popen", "list2cmdline", "getoutput", "getstatusoutput", "PIPE", "STDOUT", "DEVNULL"),
        ["zipfile"] = Members("BadZipFile", "BadZipfile", "LargeZipFile", "error", "ZIP_STORED", "ZIP_DEFLATED", "ZIP_BZIP2", "ZIP_LZMA", "is_zipfile", "ZipInfo", "ZipFile"),
    };

    private static readonly FrozenDictionary<ModuleMemberName, string> ModuleMemberModules =
        new Dictionary<ModuleMemberName, string>
        {
            [new("os", "path")] = "os.path",
            [new("importlib", "util")] = "importlib.util",
            [new("openpyxl", "utils")] = "openpyxl.utils",
            [new("openpyxl", "workbook")] = "openpyxl.workbook",
            [new("openpyxl", "reader")] = "openpyxl.reader",
            [new("openpyxl", "styles")] = "openpyxl.styles",
            [new("openpyxl", "comments")] = "openpyxl.comments",
            [new("openpyxl", "chart")] = "openpyxl.chart",
            [new("openpyxl", "cell")] = "openpyxl.cell",
            [new("openpyxl", "worksheet")] = "openpyxl.worksheet",
            [new("openpyxl", "drawing")] = "openpyxl.drawing",
            [new("openpyxl.reader", "excel")] = "openpyxl.reader.excel",
            [new("openpyxl.cell", "cell")] = "openpyxl.cell.cell",
            [new("openpyxl.utils", "cell")] = "openpyxl.utils.cell",
            [new("openpyxl.utils", "exceptions")] = "openpyxl.utils.exceptions",
            [new("openpyxl.styles", "colors")] = "openpyxl.styles.colors",
            [new("openpyxl.worksheet", "table")] = "openpyxl.worksheet.table",
            [new("openpyxl.worksheet", "datavalidation")] = "openpyxl.worksheet.datavalidation",
            [new("openpyxl.worksheet", "worksheet")] = "openpyxl.worksheet.worksheet",
            [new("openpyxl.drawing", "image")] = "openpyxl.drawing.image",
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ModuleMemberName, string> ModuleMemberCallableAliases =
        new Dictionary<ModuleMemberName, string>
        {
            [new("openpyxl.workbook", "Workbook")] = "openpyxl.Workbook",
            [new("openpyxl.reader.excel", "load_workbook")] = "openpyxl.load_workbook",
            [new("openpyxl.utils.cell", "get_column_letter")] = "openpyxl.utils.get_column_letter",
            [new("openpyxl.utils.cell", "column_index_from_string")] = "openpyxl.utils.column_index_from_string",
            [new("openpyxl.utils.cell", "coordinate_from_string")] = "openpyxl.utils.coordinate_from_string",
            [new("openpyxl.utils.cell", "coordinate_to_tuple")] = "openpyxl.utils.coordinate_to_tuple",
            [new("openpyxl.utils.cell", "range_boundaries")] = "openpyxl.utils.range_boundaries",
            [new("openpyxl.utils.cell", "get_column_interval")] = "openpyxl.utils.get_column_interval",
            [new("openpyxl.utils.cell", "absolute_coordinate")] = "openpyxl.utils.absolute_coordinate",
            [new("openpyxl.utils.cell", "quote_sheetname")] = "openpyxl.utils.quote_sheetname",
            [new("openpyxl.utils.cell", "rows_from_range")] = "openpyxl.utils.rows_from_range",
            [new("openpyxl.utils.cell", "cols_from_range")] = "openpyxl.utils.cols_from_range",
            [new("openpyxl.styles", "Font")] = "openpyxl.styles.Font",
            [new("openpyxl.styles", "PatternFill")] = "openpyxl.styles.PatternFill",
            [new("openpyxl.styles", "Border")] = "openpyxl.styles.Border",
            [new("openpyxl.styles", "Side")] = "openpyxl.styles.Side",
            [new("openpyxl.styles", "Alignment")] = "openpyxl.styles.Alignment",
            [new("openpyxl.styles", "Protection")] = "openpyxl.styles.Protection",
            [new("openpyxl.styles", "NamedStyle")] = "openpyxl.styles.NamedStyle",
            [new("openpyxl.comments", "Comment")] = "openpyxl.comments.Comment",
            [new("openpyxl.worksheet.table", "Table")] = "openpyxl.worksheet.table.Table",
            [new("openpyxl.worksheet.table", "TableStyleInfo")] = "openpyxl.worksheet.table.TableStyleInfo",
            [new("openpyxl.worksheet.datavalidation", "DataValidation")] = "openpyxl.worksheet.datavalidation.DataValidation",
        }.ToFrozenDictionary();

    private static readonly string[] KnownBuiltinModuleNames = [.. LythonRuntime.GetKnownBuiltinModuleNames()];

    static StaticContracts()
    {
        var declaredSurfaces = new HashSet<string>(ModuleSurfaces.Keys, StringComparer.Ordinal);
        if (!declaredSurfaces.SetEquals(KnownBuiltinModuleNames))
        {
            var missingSurfaces = KnownBuiltinModuleNames.Where(name => !declaredSurfaces.Contains(name));
            var missingRuntimeModules = declaredSurfaces.Where(name => !KnownBuiltinModuleNames.Contains(name, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Builtin module catalog mismatch. Missing surfaces: {string.Join(", ", missingSurfaces)}. " +
                $"Missing runtime registrations: {string.Join(", ", missingRuntimeModules)}.");
        }

        foreach (var (member, targetModuleName) in ModuleMemberModules)
        {
            if (!IsKnownBuiltinModuleMember(member.ModuleName, member.MemberName) ||
                !IsKnownBuiltinModule(targetModuleName))
            {
                throw new InvalidOperationException(
                    $"Invalid builtin submodule mapping '{member.ModuleName}.{member.MemberName}' -> '{targetModuleName}'.");
            }
        }

        foreach (var (member, targetName) in ModuleMemberCallableAliases)
        {
            if (!IsKnownBuiltinModuleMember(member.ModuleName, member.MemberName) ||
                !TryGetKnownCallContract(targetName, out _))
            {
                throw new InvalidOperationException(
                    $"Invalid builtin callable alias '{member.ModuleName}.{member.MemberName}' -> '{targetName}'.");
            }
        }
    }

    public static bool IsKnownBuiltinModule(string moduleName)
        => ModuleSurfaces.ContainsKey(moduleName);

    public static bool IsKnownBuiltinModuleMember(string moduleName, string memberName)
        => ModuleSurfaces.TryGetValue(moduleName, out var surface) && surface.Contains(memberName);

    public static IReadOnlyList<string> GetKnownBuiltinModuleNames()
        => KnownBuiltinModuleNames;

    public static IReadOnlyList<string> GetModuleMemberNames(string moduleName)
        => ModuleSurfaces.TryGetValue(moduleName, out var surface)
            ? surface.MemberNames
            : [];

    public static IReadOnlyList<string> GetModuleExportedMemberNames(string moduleName)
        => ModuleSurfaces.TryGetValue(moduleName, out var surface)
            ? surface.ExportedMemberNames
            : [];

    public static bool TryGetModuleMemberValue(string moduleName, string memberName, LythonSourceSpan span, out AbstractValue value)
    {
        if (!ModuleSurfaces.TryGetValue(moduleName, out var surface) ||
            !surface.Contains(memberName))
        {
            value = default;
            return false;
        }

        var member = new ModuleMemberName(moduleName, memberName);
        if (ModuleMemberModules.TryGetValue(member, out var targetModuleName))
        {
            value = AbstractValue.Module(targetModuleName, span);
            return true;
        }

        var targetName = ModuleMemberCallableAliases.GetValueOrDefault(member) ?? $"{moduleName}.{memberName}";
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
            ("builtins", "__name__") => AbstractValue.String("builtins", span),
            ("builtins", "__debug__") or ("builtins", "True") => AbstractValue.Boolean(true, span),
            ("builtins", "False") => AbstractValue.Boolean(false, span),
            ("builtins", "None") => AbstractValue.None(span),
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
            ("re", "NOFLAG") or ("re", "IGNORECASE") or ("re", "I") or ("re", "UNICODE") or ("re", "U") or ("re", "MULTILINE") or ("re", "M") or ("re", "DOTALL") or ("re", "S") or ("re", "VERBOSE") or ("re", "X") or ("re", "ASCII") or ("re", "A") or ("re", "LOCALE") or ("re", "L") or ("re", "DEBUG") => AbstractValue.IntegerType(span),
            ("csv", "QUOTE_MINIMAL") or ("csv", "QUOTE_ALL") or ("csv", "QUOTE_NONE") or ("csv", "QUOTE_NONNUMERIC") => AbstractValue.IntegerType(span),
            ("subprocess", "PIPE") or ("subprocess", "STDOUT") or ("subprocess", "DEVNULL") => AbstractValue.IntegerType(span),
            _ => default
        };

        return value.Kind != default;
    }

    private static BuiltinModuleSurface Members(params string[] names)
        => new(names);


    private sealed class BuiltinModuleSurface
    {
        private readonly FrozenSet<string> _memberLookup;

        public BuiltinModuleSurface(IEnumerable<string> members)
        {
            _memberLookup = members.ToFrozenSet(StringComparer.Ordinal);
            MemberNames = _memberLookup.Order(StringComparer.Ordinal).ToArray();
            ExportedMemberNames = MemberNames
                .Where(static name => !name.StartsWith("_", StringComparison.Ordinal))
                .ToArray();
        }

        public IReadOnlyList<string> MemberNames { get; }

        public IReadOnlyList<string> ExportedMemberNames { get; }

        public bool Contains(string memberName) => _memberLookup.Contains(memberName);
    }

    private readonly record struct ModuleMemberName(string ModuleName, string MemberName);
}
