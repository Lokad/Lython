using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static class OpenPyxlCallableContractCatalog
    {
        public static readonly StaticCallableContract[] Contracts =
        [
        new(AbstractValueKind.OpenPyxlWorkbook, "add_named_style", 1, 1, "LA3161", "Workbook.add_named_style(style) expects one argument.", StaticMutationKind.MutatesReceiver, ["style"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "create_sheet", 0, 2, "LA3161", "Workbook.create_sheet([title][, index]) expects zero to two arguments.", StaticMutationKind.MutatesReceiver, ["title", "index"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "remove", 1, 1, "LA3161", "Workbook.remove(worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["worksheet"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "remove_sheet", 1, 1, "LA3161", "Workbook.remove_sheet(worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["worksheet"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "copy_worksheet", 1, 1, "LA3161", "Workbook.copy_worksheet(from_worksheet) expects one worksheet argument.", StaticMutationKind.MutatesReceiver, ["from_worksheet"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "index", 1, 1, "LA3161", "Workbook.index(worksheet) expects one worksheet argument.", parameterNames: ["worksheet"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "move_sheet", 1, 2, "LA3161", "Workbook.move_sheet(sheet[, offset]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["sheet", "offset"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "get_sheet_names", 0, 0, "LA3161", "Workbook.get_sheet_names() expects no arguments."),
        new(AbstractValueKind.OpenPyxlWorkbook, "save", 1, 1, "LA3161", "Workbook.save(filename) expects one argument.", StaticMutationKind.MutatesReceiver, ["filename"]),
        new(AbstractValueKind.OpenPyxlWorkbook, "close", 0, 0, "LA3161", "Workbook.close() expects no arguments."),
        new(AbstractValueKind.OpenPyxlWorksheet, "set_printer_settings", 2, 2, "LA3161", "Worksheet.set_printer_settings(paper_size, orientation) expects two arguments.", StaticMutationKind.MutatesReceiver, ["paper_size", "orientation"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "add_table", 1, 1, "LA3161", "Worksheet.add_table(table) expects one table argument.", StaticMutationKind.MutatesReceiver, ["table"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "add_data_validation", 1, 1, "LA3161", "Worksheet.add_data_validation(data_validation) expects one data validation argument.", StaticMutationKind.MutatesReceiver, ["data_validation"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "add_chart", 1, 2, "LA3161", "Worksheet.add_chart(chart[, anchor]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["chart", "anchor"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "add_image", 1, 2, "LA3161", "Worksheet.add_image(img[, anchor]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["img", "anchor"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "cell", 2, 3, "LA3161", "Worksheet.cell(row, column[, value]) expects two or three arguments.", StaticMutationKind.MutatesReceiver, ["row", "column", "value"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "append", 1, 1, "LA3161", "Worksheet.append(iterable) expects one argument.", StaticMutationKind.MutatesReceiver, ["iterable"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "iter_rows", 0, 5, "LA3161", "Worksheet.iter_rows(...) expects up to five supported arguments.", parameterNames: ["min_row", "max_row", "min_col", "max_col", "values_only"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "iter_cols", 0, 5, "LA3161", "Worksheet.iter_cols(...) expects up to five supported arguments.", parameterNames: ["min_row", "max_row", "min_col", "max_col", "values_only"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "calculate_dimension", 0, 0, "LA3161", "Worksheet.calculate_dimension() expects no arguments."),
        new(AbstractValueKind.OpenPyxlWorksheet, "merge_cells", 0, 5, "LA3161", "Worksheet.merge_cells(...) expects supported range arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "start_row", "start_column", "end_row", "end_column"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "unmerge_cells", 0, 5, "LA3161", "Worksheet.unmerge_cells(...) expects supported range arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "start_row", "start_column", "end_row", "end_column"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "insert_rows", 1, 2, "LA3161", "Worksheet.insert_rows(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "delete_rows", 1, 2, "LA3161", "Worksheet.delete_rows(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "insert_cols", 1, 2, "LA3161", "Worksheet.insert_cols(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "delete_cols", 1, 2, "LA3161", "Worksheet.delete_cols(idx[, amount]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["idx", "amount"]),
        new(AbstractValueKind.OpenPyxlWorksheet, "move_range", 1, 4, "LA3161", "Worksheet.move_range(cell_range[, rows][, cols][, translate]) expects one to four arguments.", StaticMutationKind.MutatesReceiver, ["cell_range", "rows", "cols", "translate"]),
        new(AbstractValueKind.OpenPyxlCell, "offset", 0, 2, "LA3161", "Cell.offset([row][, column]) expects zero to two arguments.", parameterNames: ["row", "column"]),
        new(AbstractValueKind.OpenPyxlTableCollection, "keys", 0, 0, "LA3161", "TableList.keys() expects no arguments."),
        new(AbstractValueKind.OpenPyxlTableCollection, "values", 0, 0, "LA3161", "TableList.values() expects no arguments."),
        new(AbstractValueKind.OpenPyxlTableCollection, "items", 0, 0, "LA3161", "TableList.items() expects no arguments."),
        new(AbstractValueKind.OpenPyxlDataValidation, "add", 1, 1, "LA3161", "DataValidation.add(cell_range) expects one argument.", StaticMutationKind.MutatesReceiver, ["cell_range"]),
        new(AbstractValueKind.OpenPyxlDataValidationList, "append", 1, 1, "LA3161", "DataValidationList.append(data_validation) expects one data validation argument.", StaticMutationKind.MutatesReceiver, ["data_validation"]),
        new(AbstractValueKind.OpenPyxlConditionalFormattingCollection, "items", 0, 0, "LA3161", "ConditionalFormattingList.items() expects no arguments."),
        new(AbstractValueKind.OpenPyxlConditionalFormattingCollection, "add", 2, 2, "LA3161", "ConditionalFormattingList.add(range_string, rule) expects two arguments.", StaticMutationKind.MutatesReceiver, ["range_string", "rule"]),
        new(AbstractValueKind.OpenPyxlChart, "add_data", 1, 3, "LA3161", "Chart.add_data(data[, titles_from_data][, from_rows]) expects one to three arguments.", StaticMutationKind.MutatesReceiver, ["data", "titles_from_data", "from_rows"]),
        new(AbstractValueKind.OpenPyxlChart, "set_categories", 1, 1, "LA3161", "Chart.set_categories(labels) expects one argument.", StaticMutationKind.MutatesReceiver, ["labels"]),
        new(AbstractValueKind.OpenPyxlChart, "append", 1, 1, "LA3161", "Chart.append(value) expects one argument.", StaticMutationKind.MutatesReceiver, ["value"]),
        new(AbstractValueKind.OpenPyxlSheetProtection, "enable", 0, 0, "LA3161", "SheetProtection.enable() expects no arguments.", StaticMutationKind.MutatesReceiver),
        new(AbstractValueKind.OpenPyxlSheetProtection, "disable", 0, 0, "LA3161", "SheetProtection.disable() expects no arguments.", StaticMutationKind.MutatesReceiver),
        new(AbstractValueKind.OpenPyxlWorkbookProtection, "set_workbook_password", 1, 2, "LA3161", "WorkbookProtection.set_workbook_password(value[, already_hashed]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["value", "already_hashed"]),
        new(AbstractValueKind.OpenPyxlWorkbookProtection, "set_revisions_password", 1, 2, "LA3161", "WorkbookProtection.set_revisions_password(value[, already_hashed]) expects one or two arguments.", StaticMutationKind.MutatesReceiver, ["value", "already_hashed"]),
        ];
    }
}
