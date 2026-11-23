namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    internal static class Zlib
    {
        public static readonly LythonCallableSignature Compress =
            LythonCallableSignature.Create("zlib.compress", ["data", "level", "wbits"], requiredCount: 1,
                maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature Decompress =
            LythonCallableSignature.Create("zlib.decompress", ["data", "wbits", "bufsize"], requiredCount: 1,
                maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    }

    internal static class ElementTree
    {
        public static readonly LythonCallableSignature FromString =
            LythonCallableSignature.Create("xml.etree.ElementTree.fromstring", ["text", "parser"], requiredCount: 1);
    }

    internal static class Io
    {
        public static readonly LythonCallableSignature BytesIO = LythonCallableSignature.Create("io.BytesIO", ["initial_bytes"], requiredCount: 0);
        public static readonly LythonCallableSignature StringIO = LythonCallableSignature.Create("io.StringIO", ["initial_value", "newline"], requiredCount: 0);
    }

    internal static class OpenPyxl
    {
        public static readonly LythonCallableSignature Workbook = LythonCallableSignature.Create("openpyxl.Workbook", ["write_only", "iso_dates"], requiredCount: 0);
        public static readonly LythonCallableSignature LoadWorkbook = LythonCallableSignature.Create("openpyxl.load_workbook", ["filename", "read_only", "keep_vba", "data_only", "keep_links", "rich_text"], requiredCount: 1);
        public static readonly LythonCallableSignature GetColumnLetter = LythonCallableSignature.Create("openpyxl.utils.get_column_letter", ["col_idx"]);
        public static readonly LythonCallableSignature ColumnIndexFromString = LythonCallableSignature.Create("openpyxl.utils.column_index_from_string", ["col"]);
        public static readonly LythonCallableSignature CoordinateFromString = LythonCallableSignature.Create("openpyxl.utils.coordinate_from_string", ["coord_string"]);
        public static readonly LythonCallableSignature CoordinateToTuple = LythonCallableSignature.Create("openpyxl.utils.coordinate_to_tuple", ["coordinate"]);
        public static readonly LythonCallableSignature RangeBoundaries = LythonCallableSignature.Create("openpyxl.utils.range_boundaries", ["range_string"]);
        public static readonly LythonCallableSignature GetColumnInterval = LythonCallableSignature.Create("openpyxl.utils.get_column_interval", ["start", "end"]);
        public static readonly LythonCallableSignature AbsoluteCoordinate = LythonCallableSignature.Create("openpyxl.utils.absolute_coordinate", ["coord_string"]);
        public static readonly LythonCallableSignature QuoteSheetName = LythonCallableSignature.Create("openpyxl.utils.quote_sheetname", ["sheetname"]);
        public static readonly LythonCallableSignature RowsFromRange = LythonCallableSignature.Create("openpyxl.utils.rows_from_range", ["range_string"]);
        public static readonly LythonCallableSignature ColsFromRange = LythonCallableSignature.Create("openpyxl.utils.cols_from_range", ["range_string"]);
        public static readonly LythonCallableSignature Comment = LythonCallableSignature.Create("openpyxl.comments.Comment", ["text", "author"]);
        public static readonly LythonCallableSignature Font = LythonCallableSignature.Create("openpyxl.styles.Font", ["name", "sz", "bold", "italic", "color", "underline", "b", "i", "size", "u", "strike", "strikethrough"], requiredCount: 0);
        public static readonly LythonCallableSignature PatternFill = LythonCallableSignature.Create("openpyxl.styles.PatternFill", ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], requiredCount: 0);
        public static readonly LythonCallableSignature Border = LythonCallableSignature.Create("openpyxl.styles.Border", ["left", "right", "top", "bottom"], requiredCount: 0);
        public static readonly LythonCallableSignature Side = LythonCallableSignature.Create("openpyxl.styles.Side", ["style", "color", "border_style"], requiredCount: 0);
        public static readonly LythonCallableSignature Alignment = LythonCallableSignature.Create("openpyxl.styles.Alignment", ["horizontal", "vertical", "wrap_text", "text_rotation", "wrapText", "textRotation", "shrinkToFit", "shrink_to_fit"], requiredCount: 0);
        public static readonly LythonCallableSignature Protection = LythonCallableSignature.Create("openpyxl.styles.Protection", ["locked", "hidden"], requiredCount: 0);
        public static readonly LythonCallableSignature NamedStyle = LythonCallableSignature.Create("openpyxl.styles.NamedStyle", ["name", "font", "fill", "border", "alignment", "number_format", "protection"], requiredCount: 0);
        public static readonly LythonCallableSignature Color = LythonCallableSignature.Create("openpyxl.styles.colors.Color", ["rgb", "indexed", "auto", "theme", "tint", "type"], requiredCount: 0);
        public static readonly LythonCallableSignature Table = LythonCallableSignature.Create("openpyxl.worksheet.table.Table", ["displayName", "ref"], requiredCount: 0);
        public static readonly LythonCallableSignature TableStyleInfo = LythonCallableSignature.Create("openpyxl.worksheet.table.TableStyleInfo", ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], requiredCount: 0);
        public static readonly LythonCallableSignature DataValidation = LythonCallableSignature.Create("openpyxl.worksheet.datavalidation.DataValidation", ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"], requiredCount: 0);
        public static readonly LythonCallableSignature BarChart = LythonCallableSignature.Create("openpyxl.chart.BarChart", []);
        public static readonly LythonCallableSignature LineChart = LythonCallableSignature.Create("openpyxl.chart.LineChart", []);
        public static readonly LythonCallableSignature PieChart = LythonCallableSignature.Create("openpyxl.chart.PieChart", []);
        public static readonly LythonCallableSignature ScatterChart = LythonCallableSignature.Create("openpyxl.chart.ScatterChart", []);
        public static readonly LythonCallableSignature ChartReference = LythonCallableSignature.Create("openpyxl.chart.Reference", ["worksheet", "min_col", "min_row", "max_col", "max_row", "range_string"], requiredCount: 1);
        public static readonly LythonCallableSignature ChartSeries = LythonCallableSignature.Create("openpyxl.chart.Series", ["values", "xvalues", "title"], requiredCount: 0);
        public static readonly LythonCallableSignature DrawingImage = LythonCallableSignature.Create("openpyxl.drawing.image.Image", ["img"]);
    }

    internal static class Regex
    {
        public static readonly LythonCallableSignature Compile = LythonCallableSignature.Create("re.compile", ["pattern", "flags"], requiredCount: 1);
        public static readonly LythonCallableSignature Search = LythonCallableSignature.Create("re.search", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature Match = LythonCallableSignature.Create("re.match", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature FullMatch = LythonCallableSignature.Create("re.fullmatch", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature FindAll = LythonCallableSignature.Create("re.findall", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature FindIter = LythonCallableSignature.Create("re.finditer", ["pattern", "string", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature Sub = LythonCallableSignature.Create("re.sub", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], requiredCount: 3);
        public static readonly LythonCallableSignature Subn = LythonCallableSignature.Create("re.subn", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], requiredCount: 3);
        public static readonly LythonCallableSignature Split = LythonCallableSignature.Create("re.split", ["pattern", "string", "maxsplit", "flags", "pos", "endpos"], requiredCount: 2);
        public static readonly LythonCallableSignature Escape = LythonCallableSignature.Create("re.escape", ["string"]);
        public static readonly LythonCallableSignature Purge = LythonCallableSignature.Create("re.purge", []);
    }

    internal static class Argparse
    {
        public static readonly LythonCallableSignature ArgumentParser = LythonCallableSignature.Create(
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
        public static readonly LythonCallableSignature Namespace = LythonCallableSignature.Create("argparse.Namespace", [], requiredCount: 0, maximumPositionalArgumentCount: 0, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature FileType = LythonCallableSignature.Create("argparse.FileType", ["mode", "bufsize", "encoding", "errors"], requiredCount: 0);
    }

    internal static class Dataclasses
    {
        public static readonly LythonCallableSignature Dataclass = LythonCallableSignature.Create("dataclasses.dataclass", ["cls", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Field = LythonCallableSignature.Create("dataclasses.field", ["default", "default_factory", "init", "repr", "hash", "compare", "metadata", "kw_only"], requiredCount: 0, maximumPositionalArgumentCount: 0);
        public static readonly LythonCallableSignature MakeDataclass = LythonCallableSignature.Create("dataclasses.make_dataclass", ["cls_name", "fields", "bases", "namespace", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature IsDataclass = LythonCallableSignature.Create("dataclasses.is_dataclass", ["value"]);
        public static readonly LythonCallableSignature Fields = LythonCallableSignature.Create("dataclasses.fields", ["class_or_instance"]);
        public static readonly LythonCallableSignature AsDict = LythonCallableSignature.Create("dataclasses.asdict", ["obj", "dict_factory"], requiredCount: 1);
        public static readonly LythonCallableSignature AsTuple = LythonCallableSignature.Create("dataclasses.astuple", ["obj", "tuple_factory"], requiredCount: 1);
        public static readonly LythonCallableSignature Replace = LythonCallableSignature.Create("dataclasses.replace", ["obj"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    }

    internal static class Typing
    {
        public static readonly LythonCallableSignature TypeVar = LythonCallableSignature.Create("typing.TypeVar", ["name"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
        public static readonly LythonCallableSignature NewType = LythonCallableSignature.Create("typing.NewType", ["name", "tp"]);
        public static readonly LythonCallableSignature Cast = LythonCallableSignature.Create("typing.cast", ["typ", "val"]);
        public static readonly LythonCallableSignature GetOrigin = LythonCallableSignature.Create("typing.get_origin", ["tp"]);
        public static readonly LythonCallableSignature GetArgs = LythonCallableSignature.Create("typing.get_args", ["tp"]);
        public static readonly LythonCallableSignature NamedTuple = LythonCallableSignature.Create("typing.NamedTuple", ["typename", "fields"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature TypedDict = LythonCallableSignature.Create("typing.TypedDict", ["typename", "fields"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    }

    internal static class Subprocess
    {
        private static readonly string[] Parameters = ["args", "input", "cwd", "timeout", "check", "capture_output", "stdin", "stdout", "stderr", "shell", "text", "encoding", "errors", "env", "universal_newlines"];
        private static readonly string[] PopenParameters = ["args", "bufsize", "executable", "stdin", "stdout", "stderr", "preexec_fn", "close_fds", "shell", "cwd", "env", "universal_newlines", "startupinfo", "creationflags", "restore_signals", "start_new_session", "pass_fds", "user", "group", "extra_groups", "encoding", "errors", "text", "umask", "pipesize", "process_group"];

        public static readonly LythonCallableSignature Run = LythonCallableSignature.Create("subprocess.run", Parameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
        public static readonly LythonCallableSignature Call = LythonCallableSignature.Create("subprocess.call", Parameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
        public static readonly LythonCallableSignature CheckCall = LythonCallableSignature.Create("subprocess.check_call", Parameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
        public static readonly LythonCallableSignature CheckOutput = LythonCallableSignature.Create("subprocess.check_output", Parameters, requiredCount: 1, maximumPositionalArgumentCount: 6);
        public static readonly LythonCallableSignature Popen = LythonCallableSignature.Create("subprocess.Popen", PopenParameters, requiredCount: 1, maximumPositionalArgumentCount: 17);
        public static readonly LythonCallableSignature CompletedProcess = LythonCallableSignature.Create("subprocess.CompletedProcess", ["args", "returncode", "stdout", "stderr"], requiredCount: 2);
        public static readonly LythonCallableSignature TimeoutExpired = LythonCallableSignature.Create("subprocess.TimeoutExpired", ["cmd", "timeout", "output", "stderr"], requiredCount: 2);
        public static readonly LythonCallableSignature List2Cmdline = LythonCallableSignature.Create("subprocess.list2cmdline", ["seq"]);
        public static readonly LythonCallableSignature Unsupported = LythonCallableSignature.Create("subprocess.unsupported", [], requiredCount: 0, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    }
}
