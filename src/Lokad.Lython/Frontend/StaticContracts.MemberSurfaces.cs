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

    private static readonly HashSet<string> CsvReaderMembers = new(StringComparer.Ordinal)
    {
        "line_num",
    };

    private static readonly HashSet<string> CsvDictReaderMembers = new(StringComparer.Ordinal)
    {
        "fieldnames",
        "line_num",
    };

    private static readonly HashSet<string> CsvWriterMembers = new(StringComparer.Ordinal)
    {
        "writerow",
        "writerows",
        "getvalue",
    };

    private static readonly HashSet<string> CsvDictWriterMembers = new(StringComparer.Ordinal)
    {
        "writeheader",
        "writerow",
        "writerows",
    };

    private static readonly HashSet<string> DifflibDifferMembers = new(StringComparer.Ordinal)
    {
        "linejunk",
        "charjunk",
        "compare",
    };

    private static readonly HashSet<string> DifflibHtmlDiffMembers = new(StringComparer.Ordinal)
    {
        "make_table",
        "make_file",
    };

    private static readonly HashSet<string> DifflibMatchMembers = new(StringComparer.Ordinal)
    {
        "a",
        "b",
        "size",
    };

    private static readonly HashSet<string> DifflibSequenceMatcherMembers = new(StringComparer.Ordinal)
    {
        "a",
        "b",
        "b2j",
        "bjunk",
        "bpopular",
        "set_seqs",
        "set_seq1",
        "set_seq2",
        "find_longest_match",
        "get_matching_blocks",
        "get_opcodes",
        "get_grouped_opcodes",
        "ratio",
        "quick_ratio",
        "real_quick_ratio",
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

    private static readonly HashSet<string> OpenPyxlWorkbookMembers = new(StringComparer.Ordinal)
    {
        "active",
        "worksheets",
        "sheetnames",
        "read_only",
        "write_only",
        "iso_dates",
        "template",
        "mime_type",
        "epoch",
        "excel_base_date",
        "named_styles",
        "style_names",
        "security",
        "add_named_style",
        "create_sheet",
        "remove",
        "remove_sheet",
        "copy_worksheet",
        "index",
        "move_sheet",
        "get_sheet_names",
        "save",
        "close",
    };

    private static readonly HashSet<string> OpenPyxlWorksheetMembers = new(StringComparer.Ordinal)
    {
        "title",
        "min_row",
        "max_row",
        "min_column",
        "max_column",
        "dimensions",
        "freeze_panes",
        "show_gridlines",
        "sheet_view",
        "print_area",
        "print_title_rows",
        "print_title_cols",
        "page_margins",
        "page_setup",
        "protection",
        "set_printer_settings",
        "auto_filter",
        "tables",
        "data_validations",
        "conditional_formatting",
        "_charts",
        "_images",
        "_drawings",
        "drawings",
        "_drawing",
        "add_table",
        "add_data_validation",
        "add_chart",
        "add_image",
        "column_dimensions",
        "row_dimensions",
        "rows",
        "columns",
        "values",
        "cell",
        "append",
        "iter_rows",
        "iter_cols",
        "calculate_dimension",
        "merge_cells",
        "unmerge_cells",
        "insert_rows",
        "delete_rows",
        "insert_cols",
        "delete_cols",
        "move_range",
        "merged_cells",
        "merged_cell_ranges",
    };

    private static readonly HashSet<string> OpenPyxlCellMembers = new(StringComparer.Ordinal)
    {
        "value",
        "row",
        "column",
        "col_idx",
        "column_letter",
        "coordinate",
        "parent",
        "internal_value",
        "is_date",
        "base_date",
        "number_format",
        "hyperlink",
        "comment",
        "font",
        "fill",
        "border",
        "alignment",
        "protection",
        "style",
        "style_id",
        "data_type",
        "offset",
    };

    private static readonly HashSet<string> OpenPyxlHyperlinkMembers = new(StringComparer.Ordinal)
    {
        "ref",
        "target",
        "location",
        "tooltip",
        "display",
    };

    private static readonly HashSet<string> OpenPyxlCommentMembers = new(StringComparer.Ordinal)
    {
        "text",
        "author",
        "width",
        "height",
    };

    private static readonly HashSet<string> OpenPyxlFontMembers = new(StringComparer.Ordinal)
    {
        "name",
        "sz",
        "bold",
        "italic",
        "color",
        "underline",
        "b",
        "i",
    };

    private static readonly HashSet<string> OpenPyxlPatternFillMembers = new(StringComparer.Ordinal)
    {
        "fill_type",
        "start_color",
        "end_color",
        "fgColor",
        "bgColor",
        "patternType",
    };

    private static readonly HashSet<string> OpenPyxlBorderMembers = new(StringComparer.Ordinal)
    {
        "left",
        "right",
        "top",
        "bottom",
    };

    private static readonly HashSet<string> OpenPyxlSideMembers = new(StringComparer.Ordinal)
    {
        "style",
        "color",
        "border_style",
    };

    private static readonly HashSet<string> OpenPyxlAlignmentMembers = new(StringComparer.Ordinal)
    {
        "horizontal",
        "vertical",
        "wrap_text",
        "text_rotation",
    };

    private static readonly HashSet<string> OpenPyxlProtectionMembers = new(StringComparer.Ordinal)
    {
        "locked",
        "hidden",
    };

    private static readonly HashSet<string> OpenPyxlNamedStyleMembers = new(StringComparer.Ordinal)
    {
        "name",
        "number_format",
        "font",
        "fill",
        "border",
        "alignment",
        "protection",
    };

    private static readonly HashSet<string> OpenPyxlColorMembers = new(StringComparer.Ordinal)
    {
        "type",
        "rgb",
        "indexed",
        "theme",
        "tint",
        "auto",
        "index",
        "value",
    };

    private static readonly HashSet<string> OpenPyxlTableMembers = new(StringComparer.Ordinal)
    {
        "displayName",
        "name",
        "ref",
        "tableStyleInfo",
    };

    private static readonly HashSet<string> OpenPyxlTableStyleInfoMembers = new(StringComparer.Ordinal)
    {
        "name",
        "showFirstColumn",
        "showLastColumn",
        "showRowStripes",
        "showColumnStripes",
    };

    private static readonly HashSet<string> OpenPyxlDataValidationMembers = new(StringComparer.Ordinal)
    {
        "type",
        "formula1",
        "formula2",
        "allow_blank",
        "allowBlank",
        "showErrorMessage",
        "showInputMessage",
        "operator",
        "errorTitle",
        "error",
        "promptTitle",
        "prompt",
        "sqref",
        "ranges",
        "add",
    };

    private static readonly HashSet<string> OpenPyxlConditionalFormattingRuleMembers = new(StringComparer.Ordinal)
    {
        "type",
        "operator",
        "priority",
        "formula",
    };

    private static readonly HashSet<string> OpenPyxlAutoFilterMembers = new(StringComparer.Ordinal)
    {
        "ref",
    };

    private static readonly HashSet<string> OpenPyxlSheetProtectionMembers = new(StringComparer.Ordinal)
    {
        "sheet",
        "objects",
        "scenarios",
        "password",
        "algorithmName",
        "hashValue",
        "saltValue",
        "spinCount",
        "enable",
        "disable",
    };

    private static readonly HashSet<string> OpenPyxlWorkbookProtectionMembers = new(StringComparer.Ordinal)
    {
        "lockStructure",
        "lockWindows",
        "lockRevision",
        "workbookPassword",
        "workbookPasswordCharacterSet",
        "revisionsPassword",
        "revisionsPasswordCharacterSet",
        "workbookAlgorithmName",
        "workbookHashValue",
        "workbookSaltValue",
        "workbookSpinCount",
        "revisionsAlgorithmName",
        "revisionsHashValue",
        "revisionsSaltValue",
        "revisionsSpinCount",
        "set_workbook_password",
        "set_revisions_password",
    };

    private static readonly HashSet<string> OpenPyxlDrawingMembers = new(StringComparer.Ordinal)
    {
        "path",
        "_path",
        "package_path",
        "relationship_id",
        "charts",
        "_charts",
        "images",
        "_images",
    };

    private static readonly HashSet<string> OpenPyxlChartMembers = new(StringComparer.Ordinal)
    {
        "type",
        "path",
        "_path",
        "package_path",
        "relationship_id",
        "drawing_path",
        "anchor",
        "width",
        "height",
        "title",
        "style",
        "x_axis",
        "y_axis",
        "series",
        "categories",
        "add_data",
        "set_categories",
        "append",
    };

    private static readonly HashSet<string> OpenPyxlImageMembers = new(StringComparer.Ordinal)
    {
        "path",
        "_path",
        "package_path",
        "relationship_id",
        "drawing_path",
        "ref",
        "format",
        "anchor",
        "width",
        "height",
    };

    private static readonly HashSet<string> OpenPyxlSheetViewMembers = new(StringComparer.Ordinal)
    {
        "showGridLines",
        "show_gridlines",
        "tabSelected",
        "workbookViewId",
        "selection",
    };

    private static readonly HashSet<string> OpenPyxlSelectionMembers = new(StringComparer.Ordinal)
    {
        "activeCell",
        "sqref",
        "pane",
    };

    private static readonly HashSet<string> OpenPyxlPageMarginsMembers = new(StringComparer.Ordinal)
    {
        "left",
        "right",
        "top",
        "bottom",
        "header",
        "footer",
    };

    private static readonly HashSet<string> OpenPyxlPageSetupMembers = new(StringComparer.Ordinal)
    {
        "orientation",
        "paperSize",
        "paper_size",
        "fitToWidth",
        "fit_to_width",
        "fitToHeight",
        "fit_to_height",
        "scale",
    };

    private static readonly HashSet<string> OpenPyxlTableCollectionMembers = new(StringComparer.Ordinal)
    {
        "keys",
        "values",
        "items",
    };

    private static readonly HashSet<string> OpenPyxlDataValidationListMembers = new(StringComparer.Ordinal)
    {
        "dataValidation",
        "count",
        "append",
    };

    private static readonly HashSet<string> OpenPyxlConditionalFormattingCollectionMembers = new(StringComparer.Ordinal)
    {
        "ranges",
        "items",
        "add",
    };

    private static readonly HashSet<string> OpenPyxlColumnDimensionMembers = new(StringComparer.Ordinal)
    {
        "index",
        "width",
        "hidden",
        "style",
    };

    private static readonly HashSet<string> OpenPyxlRowDimensionMembers = new(StringComparer.Ordinal)
    {
        "index",
        "height",
        "hidden",
        "style",
    };

    private static readonly HashSet<string> OpenPyxlMergedCellSetMembers = new(StringComparer.Ordinal)
    {
        "ranges",
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
        "index",
        "count",
        "insert",
        "remove",
        "pop",
        "reverse",
        "sort",
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
            AbstractValueKind.CsvReader or
            AbstractValueKind.CsvDictReader or
            AbstractValueKind.CsvWriter or
            AbstractValueKind.CsvDictWriter or
            AbstractValueKind.DifflibDiffer or
            AbstractValueKind.DifflibHtmlDiff or
            AbstractValueKind.DifflibMatch or
            AbstractValueKind.DifflibSequenceMatcher or
            AbstractValueKind.SubprocessCompletedProcess or
            AbstractValueKind.DataclassField or
            AbstractValueKind.OpenPyxlWorkbook or
            AbstractValueKind.OpenPyxlWorksheet or
            AbstractValueKind.OpenPyxlCell or
            AbstractValueKind.OpenPyxlHyperlink or
            AbstractValueKind.OpenPyxlComment or
            AbstractValueKind.OpenPyxlFont or
            AbstractValueKind.OpenPyxlPatternFill or
            AbstractValueKind.OpenPyxlBorder or
            AbstractValueKind.OpenPyxlSide or
            AbstractValueKind.OpenPyxlAlignment or
            AbstractValueKind.OpenPyxlProtection or
            AbstractValueKind.OpenPyxlNamedStyle or
            AbstractValueKind.OpenPyxlColor or
            AbstractValueKind.OpenPyxlTable or
            AbstractValueKind.OpenPyxlTableStyleInfo or
            AbstractValueKind.OpenPyxlDataValidation or
            AbstractValueKind.OpenPyxlConditionalFormattingRule or
            AbstractValueKind.OpenPyxlAutoFilter or
            AbstractValueKind.OpenPyxlSheetProtection or
            AbstractValueKind.OpenPyxlWorkbookProtection or
            AbstractValueKind.OpenPyxlDrawing or
            AbstractValueKind.OpenPyxlChart or
            AbstractValueKind.OpenPyxlImage or
            AbstractValueKind.OpenPyxlSheetView or
            AbstractValueKind.OpenPyxlSelection or
            AbstractValueKind.OpenPyxlPageMargins or
            AbstractValueKind.OpenPyxlPageSetup or
            AbstractValueKind.OpenPyxlTableCollection or
            AbstractValueKind.OpenPyxlDataValidationList or
            AbstractValueKind.OpenPyxlConditionalFormattingCollection or
            AbstractValueKind.OpenPyxlColumnDimension or
            AbstractValueKind.OpenPyxlRowDimension or
            AbstractValueKind.OpenPyxlMergedCellSet ||
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
            AbstractValueKind.CsvReader => CsvReaderMembers.Contains(memberName),
            AbstractValueKind.CsvDictReader => CsvDictReaderMembers.Contains(memberName),
            AbstractValueKind.CsvWriter => CsvWriterMembers.Contains(memberName),
            AbstractValueKind.CsvDictWriter => CsvDictWriterMembers.Contains(memberName),
            AbstractValueKind.DifflibDiffer => DifflibDifferMembers.Contains(memberName),
            AbstractValueKind.DifflibHtmlDiff => DifflibHtmlDiffMembers.Contains(memberName),
            AbstractValueKind.DifflibMatch => DifflibMatchMembers.Contains(memberName),
            AbstractValueKind.DifflibSequenceMatcher => DifflibSequenceMatcherMembers.Contains(memberName),
            AbstractValueKind.SubprocessCompletedProcess => CompletedProcessMembers.Contains(memberName),
            AbstractValueKind.DataclassField => DataclassFieldMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorkbook => OpenPyxlWorkbookMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorksheet => OpenPyxlWorksheetMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlCell => OpenPyxlCellMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlHyperlink => OpenPyxlHyperlinkMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlComment => OpenPyxlCommentMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlFont => OpenPyxlFontMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPatternFill => OpenPyxlPatternFillMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlBorder => OpenPyxlBorderMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSide => OpenPyxlSideMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlAlignment => OpenPyxlAlignmentMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlProtection => OpenPyxlProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlNamedStyle => OpenPyxlNamedStyleMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlColor => OpenPyxlColorMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTable => OpenPyxlTableMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTableStyleInfo => OpenPyxlTableStyleInfoMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDataValidation => OpenPyxlDataValidationMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlConditionalFormattingRule => OpenPyxlConditionalFormattingRuleMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlAutoFilter => OpenPyxlAutoFilterMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSheetProtection => OpenPyxlSheetProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlWorkbookProtection => OpenPyxlWorkbookProtectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDrawing => OpenPyxlDrawingMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlChart => OpenPyxlChartMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlImage => OpenPyxlImageMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSheetView => OpenPyxlSheetViewMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlSelection => OpenPyxlSelectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPageMargins => OpenPyxlPageMarginsMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlPageSetup => OpenPyxlPageSetupMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlTableCollection => OpenPyxlTableCollectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlDataValidationList => OpenPyxlDataValidationListMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlConditionalFormattingCollection => OpenPyxlConditionalFormattingCollectionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlColumnDimension => OpenPyxlColumnDimensionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlRowDimension => OpenPyxlRowDimensionMembers.Contains(memberName),
            AbstractValueKind.OpenPyxlMergedCellSet => OpenPyxlMergedCellSetMembers.Contains(memberName),
            AbstractValueKind.UserInstance => HasKnownDataclassInstanceMember((AbstractInstanceSummary)value.Value, memberName),
            _ => false
        };
    }

    private static bool HasKnownDataclassInstanceMember(AbstractInstanceSummary instance, string memberName)
        => instance.Class.IsDataclass &&
           (instance.Class.Fields.Any(field => field.StoreOnInstance && string.Equals(field.Name, memberName, StringComparison.Ordinal)) ||
            instance.Class.Methods.ContainsKey(memberName));
}
