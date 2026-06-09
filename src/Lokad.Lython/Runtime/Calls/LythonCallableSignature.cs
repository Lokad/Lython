namespace Lokad.Lython.Runtime;

internal readonly record struct LythonCallableSignature(
    string Name,
    string[]? ParameterNames = null,
    int? RequiredCount = null,
    int? MaxPositionalCount = null,
    bool AllowsExtraKeywords = false)
{
    public int MinimumArgumentCount => RequiredCount ?? ParameterNames?.Length ?? 0;

    public int? MaximumArgumentCount => AllowsExtraKeywords
        ? null
        : ParameterNames?.Length;
}

internal static class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature SysExit = new("sys.exit", ["code"], RequiredCount: 0);

    public static readonly LythonCallableSignature PathlibPath = new("pathlib.Path", RequiredCount: 0);

    public static readonly LythonCallableSignature JsonLoads = new("json.loads", ["s"]);
    public static readonly LythonCallableSignature JsonDumps = new("json.dumps", ["obj"]);

    public static readonly LythonCallableSignature CsvReader = new("csv.reader", ["lines", "delimiter"], RequiredCount: 1);
    public static readonly LythonCallableSignature CsvWriter = new("csv.writer", ["delimiter"], RequiredCount: 0);

    public static readonly LythonCallableSignature Decimal = new("decimal.Decimal", ["value"]);

    public static readonly LythonCallableSignature OpenPyxlWorkbook = new("openpyxl.Workbook", ["write_only", "iso_dates"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlLoadWorkbook = new("openpyxl.load_workbook", ["filename", "read_only", "keep_vba", "data_only", "keep_links", "rich_text"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlGetColumnLetter = new("openpyxl.utils.get_column_letter", ["col_idx"]);
    public static readonly LythonCallableSignature OpenPyxlColumnIndexFromString = new("openpyxl.utils.column_index_from_string", ["col"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateFromString = new("openpyxl.utils.coordinate_from_string", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateToTuple = new("openpyxl.utils.coordinate_to_tuple", ["coordinate"]);
    public static readonly LythonCallableSignature OpenPyxlRangeBoundaries = new("openpyxl.utils.range_boundaries", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlGetColumnInterval = new("openpyxl.utils.get_column_interval", ["start", "end"]);
    public static readonly LythonCallableSignature OpenPyxlAbsoluteCoordinate = new("openpyxl.utils.absolute_coordinate", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlQuoteSheetName = new("openpyxl.utils.quote_sheetname", ["sheetname"]);
    public static readonly LythonCallableSignature OpenPyxlRowsFromRange = new("openpyxl.utils.rows_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlColsFromRange = new("openpyxl.utils.cols_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlComment = new("openpyxl.comments.Comment", ["text", "author"]);
    public static readonly LythonCallableSignature OpenPyxlFont = new("openpyxl.styles.Font", ["name", "sz", "bold", "italic", "color", "underline", "b", "i"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlPatternFill = new("openpyxl.styles.PatternFill", ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBorder = new("openpyxl.styles.Border", ["left", "right", "top", "bottom"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlSide = new("openpyxl.styles.Side", ["style", "color", "border_style"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlAlignment = new("openpyxl.styles.Alignment", ["horizontal", "vertical", "wrap_text", "text_rotation"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlProtection = new("openpyxl.styles.Protection", ["locked", "hidden"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlNamedStyle = new("openpyxl.styles.NamedStyle", ["name", "font", "fill", "border", "alignment", "number_format", "protection"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlColor = new("openpyxl.styles.colors.Color", ["rgb", "indexed", "auto", "theme", "tint", "type"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTable = new("openpyxl.worksheet.table.Table", ["displayName", "ref"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTableStyleInfo = new("openpyxl.worksheet.table.TableStyleInfo", ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDataValidation = new("openpyxl.worksheet.datavalidation.DataValidation", ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBarChart = new("openpyxl.chart.BarChart", []);
    public static readonly LythonCallableSignature OpenPyxlLineChart = new("openpyxl.chart.LineChart", []);
    public static readonly LythonCallableSignature OpenPyxlPieChart = new("openpyxl.chart.PieChart", []);
    public static readonly LythonCallableSignature OpenPyxlScatterChart = new("openpyxl.chart.ScatterChart", []);
    public static readonly LythonCallableSignature OpenPyxlChartReference = new("openpyxl.chart.Reference", ["worksheet", "min_col", "min_row", "max_col", "max_row", "range_string"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlChartSeries = new("openpyxl.chart.Series", ["values", "xvalues", "title"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDrawingImage = new("openpyxl.drawing.image.Image", ["img"]);

    public static readonly LythonCallableSignature ReCompile = new("re.compile", ["pattern", "flags"], RequiredCount: 1);
    public static readonly LythonCallableSignature ReSearch = new("re.search", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReMatch = new("re.match", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFullMatch = new("re.fullmatch", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindAll = new("re.findall", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindIter = new("re.finditer", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReSub = new("re.sub", ["pattern", "repl", "string", "count", "flags"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSubn = new("re.subn", ["pattern", "repl", "string", "count", "flags"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSplit = new("re.split", ["pattern", "string", "maxsplit", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReEscape = new("re.escape", ["string"]);

    public static readonly LythonCallableSignature ArgparseArgumentParser = new("argparse.ArgumentParser", ["description"], RequiredCount: 0);

    public static readonly LythonCallableSignature DataclassesIsDataclass = new("dataclasses.is_dataclass", ["value"]);
    public static readonly LythonCallableSignature DataclassesFields = new("dataclasses.fields", ["class_or_instance"]);
    public static readonly LythonCallableSignature DataclassesAsDict = new("dataclasses.asdict", ["obj", "dict_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesAsTuple = new("dataclasses.astuple", ["obj", "tuple_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesReplace = new("dataclasses.replace", ["obj"], RequiredCount: 1, MaxPositionalCount: 1, AllowsExtraKeywords: true);

    private static readonly string[] SubprocessParameters = ["args", "input", "cwd", "timeout", "check", "capture_output", "stdin", "stdout", "stderr", "shell", "text", "encoding", "errors", "env", "universal_newlines"];

    public static readonly LythonCallableSignature SubprocessRun = new("subprocess.run", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCall = new("subprocess.call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckCall = new("subprocess.check_call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckOutput = new("subprocess.check_output", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);

    public static readonly LythonCallableSignature OsListDir = new("os.listdir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsWalk = new("os.walk", ["top", "topdown", "onerror", "followlinks"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsGetCwd = new("os.getcwd", []);
    public static readonly LythonCallableSignature OsFspath = new("os.fspath", ["path"]);
    public static readonly LythonCallableSignature OsStat = new("os.stat", ["path"]);
    public static readonly LythonCallableSignature OsLstat = new("os.lstat", ["path"]);
    public static readonly LythonCallableSignature OsScandir = new("os.scandir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsMkdir = new("os.mkdir", ["path"]);
    public static readonly LythonCallableSignature OsMakedirs = new("os.makedirs", ["path", "exist_ok"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsRemove = new("os.remove", ["path"]);
    public static readonly LythonCallableSignature OsUnlink = new("os.unlink", ["path"]);
    public static readonly LythonCallableSignature OsRename = new("os.rename", ["src", "dst"]);
    public static readonly LythonCallableSignature OsReplace = new("os.replace", ["src", "dst"]);
    public static readonly LythonCallableSignature OsRmdir = new("os.rmdir", ["path"]);
    public static readonly LythonCallableSignature OsRemovedirs = new("os.removedirs", ["path"]);

    public static readonly LythonCallableSignature OsPathJoin = new("os.path.join", RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathSplit = new("os.path.split", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitExt = new("os.path.splitext", ["path"]);
    public static readonly LythonCallableSignature OsPathBasename = new("os.path.basename", ["path"]);
    public static readonly LythonCallableSignature OsPathDirname = new("os.path.dirname", ["path"]);
    public static readonly LythonCallableSignature OsPathIsAbs = new("os.path.isabs", ["path"]);
    public static readonly LythonCallableSignature OsPathNormPath = new("os.path.normpath", ["path"]);
    public static readonly LythonCallableSignature OsPathAbsPath = new("os.path.abspath", ["path"]);
    public static readonly LythonCallableSignature OsPathRelPath = new("os.path.relpath", ["path", "start"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathCommonPath = new("os.path.commonpath", ["paths"]);
    public static readonly LythonCallableSignature OsPathExists = new("os.path.exists", ["path"]);
    public static readonly LythonCallableSignature OsPathLexists = new("os.path.lexists", ["path"]);
    public static readonly LythonCallableSignature OsPathIsFile = new("os.path.isfile", ["path"]);
    public static readonly LythonCallableSignature OsPathIsDir = new("os.path.isdir", ["path"]);
    public static readonly LythonCallableSignature OsPathGetSize = new("os.path.getsize", ["path"]);
    public static readonly LythonCallableSignature OsPathGetMTime = new("os.path.getmtime", ["path"]);
    public static readonly LythonCallableSignature OsPathSameFile = new("os.path.samefile", ["path1", "path2"]);
    public static readonly LythonCallableSignature OsPathRealPath = new("os.path.realpath", ["path"]);

    public static readonly LythonCallableSignature Glob = new("glob.glob", ["pathname", "recursive"], RequiredCount: 1);
    public static readonly LythonCallableSignature IGlob = new("glob.iglob", ["pathname", "recursive"], RequiredCount: 1);
    public static readonly LythonCallableSignature GlobEscape = new("glob.escape", ["pathname"]);

    public static readonly LythonCallableSignature FnMatch = new("fnmatch.fnmatch", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchFilter = new("fnmatch.filter", ["names", "pattern"]);

    public static readonly LythonCallableSignature DifflibUnifiedDiff = new("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibContextDiff = new("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibNdiff = new("difflib.ndiff", ["a", "b"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibRestore = new("difflib.restore", ["delta", "which"]);
    public static readonly LythonCallableSignature DifflibGetCloseMatches = new("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibSequenceMatcher = new("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], RequiredCount: 0);

    public static readonly LythonCallableSignature PkgutilIterModules = new("pkgutil.iter_modules", ["path", "prefix"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilWalkPackages = new("pkgutil.walk_packages", ["path", "prefix", "onerror"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilFindLoader = new("pkgutil.find_loader", ["fullname"]);
    public static readonly LythonCallableSignature PkgutilGetLoader = new("pkgutil.get_loader", ["module_or_name"]);
}
