namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature OpenPyxlWorkbook = LythonCallableSignature.Create("openpyxl.Workbook", ["write_only", "iso_dates"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlLoadWorkbook = LythonCallableSignature.Create("openpyxl.load_workbook", ["filename", "read_only", "keep_vba", "data_only", "keep_links", "rich_text"], requiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlGetColumnLetter = LythonCallableSignature.Create("openpyxl.utils.get_column_letter", ["col_idx"]);
    public static readonly LythonCallableSignature OpenPyxlColumnIndexFromString = LythonCallableSignature.Create("openpyxl.utils.column_index_from_string", ["col"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateFromString = LythonCallableSignature.Create("openpyxl.utils.coordinate_from_string", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateToTuple = LythonCallableSignature.Create("openpyxl.utils.coordinate_to_tuple", ["coordinate"]);
    public static readonly LythonCallableSignature OpenPyxlRangeBoundaries = LythonCallableSignature.Create("openpyxl.utils.range_boundaries", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlGetColumnInterval = LythonCallableSignature.Create("openpyxl.utils.get_column_interval", ["start", "end"]);
    public static readonly LythonCallableSignature OpenPyxlAbsoluteCoordinate = LythonCallableSignature.Create("openpyxl.utils.absolute_coordinate", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlQuoteSheetName = LythonCallableSignature.Create("openpyxl.utils.quote_sheetname", ["sheetname"]);
    public static readonly LythonCallableSignature OpenPyxlRowsFromRange = LythonCallableSignature.Create("openpyxl.utils.rows_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlColsFromRange = LythonCallableSignature.Create("openpyxl.utils.cols_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlComment = LythonCallableSignature.Create("openpyxl.comments.Comment", ["text", "author"]);
    public static readonly LythonCallableSignature OpenPyxlFont = LythonCallableSignature.Create("openpyxl.styles.Font", ["name", "sz", "bold", "italic", "color", "underline", "b", "i", "size", "u", "strike", "strikethrough"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlPatternFill = LythonCallableSignature.Create("openpyxl.styles.PatternFill", ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBorder = LythonCallableSignature.Create("openpyxl.styles.Border", ["left", "right", "top", "bottom"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlSide = LythonCallableSignature.Create("openpyxl.styles.Side", ["style", "color", "border_style"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlAlignment = LythonCallableSignature.Create("openpyxl.styles.Alignment", ["horizontal", "vertical", "wrap_text", "text_rotation", "wrapText", "textRotation", "shrinkToFit", "shrink_to_fit"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlProtection = LythonCallableSignature.Create("openpyxl.styles.Protection", ["locked", "hidden"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlNamedStyle = LythonCallableSignature.Create("openpyxl.styles.NamedStyle", ["name", "font", "fill", "border", "alignment", "number_format", "protection"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlColor = LythonCallableSignature.Create("openpyxl.styles.colors.Color", ["rgb", "indexed", "auto", "theme", "tint", "type"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTable = LythonCallableSignature.Create("openpyxl.worksheet.table.Table", ["displayName", "ref"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTableStyleInfo = LythonCallableSignature.Create("openpyxl.worksheet.table.TableStyleInfo", ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDataValidation = LythonCallableSignature.Create("openpyxl.worksheet.datavalidation.DataValidation", ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBarChart = LythonCallableSignature.Create("openpyxl.chart.BarChart", []);
    public static readonly LythonCallableSignature OpenPyxlLineChart = LythonCallableSignature.Create("openpyxl.chart.LineChart", []);
    public static readonly LythonCallableSignature OpenPyxlPieChart = LythonCallableSignature.Create("openpyxl.chart.PieChart", []);
    public static readonly LythonCallableSignature OpenPyxlScatterChart = LythonCallableSignature.Create("openpyxl.chart.ScatterChart", []);
    public static readonly LythonCallableSignature OpenPyxlChartReference = LythonCallableSignature.Create("openpyxl.chart.Reference", ["worksheet", "min_col", "min_row", "max_col", "max_row", "range_string"], requiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlChartSeries = LythonCallableSignature.Create("openpyxl.chart.Series", ["values", "xvalues", "title"], requiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDrawingImage = LythonCallableSignature.Create("openpyxl.drawing.image.Image", ["img"]);

    public static readonly LythonCallableSignature ReCompile = LythonCallableSignature.Create("re.compile", ["pattern", "flags"], requiredCount: 1);
    public static readonly LythonCallableSignature ReSearch = LythonCallableSignature.Create("re.search", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReMatch = LythonCallableSignature.Create("re.match", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReFullMatch = LythonCallableSignature.Create("re.fullmatch", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReFindAll = LythonCallableSignature.Create("re.findall", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReFindIter = LythonCallableSignature.Create("re.finditer", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReSub = LythonCallableSignature.Create("re.sub", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], requiredCount: 3);
    public static readonly LythonCallableSignature ReSubn = LythonCallableSignature.Create("re.subn", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], requiredCount: 3);
    public static readonly LythonCallableSignature ReSplit = LythonCallableSignature.Create("re.split", ["pattern", "string", "maxsplit", "flags", "pos", "endpos"], requiredCount: 2);
    public static readonly LythonCallableSignature ReEscape = LythonCallableSignature.Create("re.escape", ["string"]);
    public static readonly LythonCallableSignature RePurge = LythonCallableSignature.Create("re.purge", []);

    public static readonly LythonCallableSignature ArgparseArgumentParser = LythonCallableSignature.Create(
        "argparse.ArgumentParser",
        [
            "prog",
            "usage",
            "description",
            "epilog",
            "formatter_class",
            "add_help",
            "allow_abbrev",
            "exit_on_error",
            "fromfile_prefix_chars",
            "parents",
            "conflict_handler",
            "prefix_chars",
            "argument_default",
        ],
        requiredCount: 0,
        maximumPositionalArgumentCount: 8);
    public static readonly LythonCallableSignature ArgparseNamespace = LythonCallableSignature.Create("argparse.Namespace", [], requiredCount: 0, maximumPositionalArgumentCount: 0, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature ArgparseFileType = LythonCallableSignature.Create("argparse.FileType", ["mode", "bufsize", "encoding", "errors"], requiredCount: 0);

    public static readonly LythonCallableSignature DataclassesDataclass = LythonCallableSignature.Create("dataclasses.dataclass", ["cls", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature DataclassesField = LythonCallableSignature.Create("dataclasses.field", ["default", "default_factory", "init", "repr", "hash", "compare", "metadata", "kw_only"], requiredCount: 0, maximumPositionalArgumentCount: 0);
    public static readonly LythonCallableSignature DataclassesMakeDataclass = LythonCallableSignature.Create("dataclasses.make_dataclass", ["cls_name", "fields", "bases", "namespace", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature DataclassesIsDataclass = LythonCallableSignature.Create("dataclasses.is_dataclass", ["value"]);
    public static readonly LythonCallableSignature DataclassesFields = LythonCallableSignature.Create("dataclasses.fields", ["class_or_instance"]);
    public static readonly LythonCallableSignature DataclassesAsDict = LythonCallableSignature.Create("dataclasses.asdict", ["obj", "dict_factory"], requiredCount: 1);
    public static readonly LythonCallableSignature DataclassesAsTuple = LythonCallableSignature.Create("dataclasses.astuple", ["obj", "tuple_factory"], requiredCount: 1);
    public static readonly LythonCallableSignature DataclassesReplace = LythonCallableSignature.Create("dataclasses.replace", ["obj"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature TypingTypeVar = LythonCallableSignature.Create("typing.TypeVar", ["name"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature TypingNewType = LythonCallableSignature.Create("typing.NewType", ["name", "tp"]);
    public static readonly LythonCallableSignature TypingCast = LythonCallableSignature.Create("typing.cast", ["typ", "val"]);
    public static readonly LythonCallableSignature TypingGetOrigin = LythonCallableSignature.Create("typing.get_origin", ["tp"]);
    public static readonly LythonCallableSignature TypingGetArgs = LythonCallableSignature.Create("typing.get_args", ["tp"]);
    public static readonly LythonCallableSignature TypingNamedTuple = LythonCallableSignature.Create("typing.NamedTuple", ["typename", "fields"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature TypingTypedDict = LythonCallableSignature.Create("typing.TypedDict", ["typename", "fields"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);

    private static readonly string[] SubprocessParameters = ["args", "input", "cwd", "timeout", "check", "capture_output", "stdin", "stdout", "stderr", "shell", "text", "encoding", "errors", "env", "universal_newlines"];
    private static readonly string[] SubprocessPopenParameters = ["args", "bufsize", "executable", "stdin", "stdout", "stderr", "preexec_fn", "close_fds", "shell", "cwd", "env", "universal_newlines", "startupinfo", "creationflags", "restore_signals", "start_new_session", "pass_fds", "user", "group", "extra_groups", "encoding", "errors", "text", "umask", "pipesize", "process_group"];

    public static readonly LythonCallableSignature SubprocessRun = LythonCallableSignature.Create("subprocess.run", SubprocessParameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
    public static readonly LythonCallableSignature SubprocessCall = LythonCallableSignature.Create("subprocess.call", SubprocessParameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckCall = LythonCallableSignature.Create("subprocess.check_call", SubprocessParameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckOutput = LythonCallableSignature.Create("subprocess.check_output", SubprocessParameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
    public static readonly LythonCallableSignature SubprocessPopen = LythonCallableSignature.Create("subprocess.Popen", SubprocessPopenParameters, requiredCount: 1, maximumPositionalArgumentCount: 17);
    public static readonly LythonCallableSignature SubprocessCompletedProcess = LythonCallableSignature.Create("subprocess.CompletedProcess", ["args", "returncode", "stdout", "stderr"], requiredCount: 2);
    public static readonly LythonCallableSignature SubprocessTimeoutExpired = LythonCallableSignature.Create("subprocess.TimeoutExpired", ["cmd", "timeout", "output", "stderr"], requiredCount: 2);
    public static readonly LythonCallableSignature SubprocessList2Cmdline = LythonCallableSignature.Create("subprocess.list2cmdline", ["seq"]);
    public static readonly LythonCallableSignature SubprocessUnsupported = LythonCallableSignature.Create("subprocess.unsupported", [], requiredCount: 0, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);

}
