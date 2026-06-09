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
        ["pkgutil"] = Members("iter_modules", "walk_packages", "find_loader", "get_loader"),
        ["collections"] = Members("defaultdict", "Counter", "deque"),
        ["itertools"] = Members("chain", "islice", "product", "zip_longest"),
        ["os"] = Members("path", "sep", "curdir", "pardir", "listdir", "walk", "getcwd", "fspath", "stat", "lstat", "scandir", "mkdir", "makedirs", "remove", "unlink", "rename", "replace", "rmdir", "removedirs"),
        ["os.path"] = Members("join", "split", "splitext", "basename", "dirname", "isabs", "normpath", "abspath", "relpath", "commonpath", "exists", "lexists", "isfile", "isdir", "getsize", "getmtime", "samefile", "realpath"),
        ["glob"] = Members("glob", "iglob", "escape"),
        ["decimal"] = Members("Decimal", "InvalidOperation", "DivisionByZero", "ROUND_HALF_EVEN", "ROUND_DOWN", "ROUND_UP"),
        ["math"] = Members("pi", "e", "tau", "inf", "nan", "sqrt", "exp", "log", "log10", "log2", "sin", "cos", "tan", "asin", "acos", "atan", "atan2", "sinh", "cosh", "tanh", "floor", "ceil", "fabs", "trunc", "degrees", "radians", "isfinite", "isinf", "isnan", "pow", "hypot", "fmod", "copysign", "isclose", "prod", "fsum"),
        ["datetime"] = Members("MINYEAR", "MAXYEAR", "timedelta", "date", "time", "datetime", "timezone"),
        ["statistics"] = Members("StatisticsError", "mean", "fmean", "median", "median_low", "median_high", "mode", "multimode", "pstdev", "stdev", "pvariance", "variance"),
        ["random"] = Members("seed", "random", "randrange", "randint", "choice", "choices", "shuffle", "sample", "getrandbits"),
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
        ["difflib"] = Members("unified_diff", "context_diff", "ndiff", "restore", "get_close_matches", "SequenceMatcher"),
        ["json"] = Members("loads", "dumps"),
        ["csv"] = Members("reader", "writer"),
        ["subprocess"] = Members("run", "call", "check_call", "check_output", "PIPE", "STDOUT", "DEVNULL"),
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
            ("sys", "argv") => AbstractValue.ListOf(AbstractValue.StringType(span), span),
            ("sys", "stdin") => AbstractValue.TextFileHandle(AbstractTextFileMode.Read, span),
            ("sys", "stdout") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("sys", "stderr") => AbstractValue.TextFileHandle(AbstractTextFileMode.Write, span),
            ("argparse", "SUPPRESS") => AbstractValue.String("SUPPRESS", span),
            ("os", "sep") => AbstractValue.String("/", span),
            ("os", "curdir") => AbstractValue.String(".", span),
            ("os", "pardir") => AbstractValue.String("..", span),
            ("math", "pi") or ("math", "e") or ("math", "tau") or ("math", "inf") or ("math", "nan") => AbstractValue.FloatType(span),
            ("openpyxl", "__version__") => AbstractValue.StringType(span),
            ("datetime", "MINYEAR") or ("datetime", "MAXYEAR") => AbstractValue.IntegerType(span),
            ("decimal", "ROUND_HALF_EVEN") or ("decimal", "ROUND_DOWN") or ("decimal", "ROUND_UP") => AbstractValue.StringType(span),
            ("re", "IGNORECASE") or ("re", "I") or ("re", "UNICODE") or ("re", "U") or ("re", "MULTILINE") or ("re", "M") or ("re", "DOTALL") or ("re", "S") or ("re", "VERBOSE") or ("re", "X") => AbstractValue.IntegerType(span),
            ("subprocess", "PIPE") or ("subprocess", "STDOUT") or ("subprocess", "DEVNULL") => AbstractValue.IntegerType(span),
            _ => default
        };

        return value.Kind != default;
    }

    private static HashSet<string> Members(params string[] names)
        => new(names, StringComparer.Ordinal);
}
