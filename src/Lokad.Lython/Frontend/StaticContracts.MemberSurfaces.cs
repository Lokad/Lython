namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly HashSet<string> RegexPatternMembers = new(StringComparer.Ordinal)
    {
        "pattern",
        "flags",
        "groups",
        "groupindex",
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
        "re",
        "string",
        "pos",
        "endpos",
        "lastindex",
        "lastgroup",
        "group",
        "groups",
        "groupdict",
        "expand",
        "start",
        "end",
        "span",
    };

    private static readonly HashSet<string> ArgparseParserMembers = new(StringComparer.Ordinal)
    {
        "add_argument",
        "add_mutually_exclusive_group",
        "parse_args",
        "parse_known_args",
        "format_usage",
        "format_help",
        "print_usage",
        "print_help",
        "error",
        "exit",
        "set_defaults",
        "get_default",
        "add_subparsers",
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

    private static readonly HashSet<string> PkgutilModuleInfoMembers = new(StringComparer.Ordinal)
    {
        "module_finder",
        "name",
        "ispkg",
        "_fields",
        "_asdict",
        "_replace",
        "count",
        "index",
    };

    private static readonly HashSet<string> PkgutilLoaderMembers = new(StringComparer.Ordinal)
    {
        "name",
        "fullname",
        "ispkg",
        "is_package",
        "get_source",
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
        "type",
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
        "_named_styles",
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
        "size",
        "bold",
        "italic",
        "color",
        "underline",
        "u",
        "strike",
        "strikethrough",
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
        "wrapText",
        "text_rotation",
        "textRotation",
        "shrink_to_fit",
        "shrinkToFit",
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

}
