using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class OpenPyxlWorksheet
    {
        private object AddTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlTable table)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_table(table) expects openpyxl.worksheet.table.Table.", span);
            }

            if (_tables.ContainsKey(table.DisplayName))
            {
                throw new LythonRuntimeException("ValueError", "Table with name " + table.DisplayName + " already exists.", span);
            }

            ReserveRegistrySlot();
            _tables[table.DisplayName] = table;
            return PyNone.Instance;
        }

        internal void AddDataValidation(OpenPyxlDataValidation validation)
            => _dataValidations.Add(validation);

        internal void AddLoadedConditionalFormatting(string sqref, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules, LythonSourceSpan span)
            => _conditionalFormattings.Add(new OpenPyxlConditionalFormatting(sqref, rules, span));

        internal void AddLoadedDrawing(OpenPyxlLoadedDrawing drawing)
        {
            _drawings.Add(drawing);
            _charts.AddRange(drawing.Charts);
            _images.AddRange(drawing.Images);
        }

        private object AddDataValidation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlDataValidation validation)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_data_validation(data_validation) expects openpyxl.worksheet.datavalidation.DataValidation.", span);
            }

            ReserveRegistrySlot();
            AddDataValidation(validation);
            return PyNone.Instance;
        }

        private object AddChart(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2 || arguments[0] is not OpenPyxlChartStub chart)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_chart(chart[, anchor]) expects an openpyxl chart.", span);
            }

            EnsureCanMutate(span);
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                chart.Anchor = NormalizeCellReference(arguments[1], "Worksheet.add_chart(anchor)", span);
            }

            throw new LythonRuntimeException("NotImplementedError", "Worksheet.add_chart(...) cannot serialize charts.", span);
        }

        private object AddImage(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2 || arguments[0] is not OpenPyxlImageStub image)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_image(img[, anchor]) expects an openpyxl image.", span);
            }

            EnsureCanMutate(span);
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                image.Anchor = NormalizeCellReference(arguments[1], "Worksheet.add_image(anchor)", span);
            }

            throw new LythonRuntimeException("NotImplementedError", "Worksheet.add_image(...) cannot serialize images.", span);
        }

        private object Cell(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var row = ExpectPositiveInt(arguments[0], "Worksheet.cell(row=...)", span);
            var column = ExpectPositiveInt(arguments[1], "Worksheet.cell(column=...)", span);
            ValidateRowColumn(row, column, span);
            if (arguments.Length >= 3)
            {
                SetCellValue(row, column, arguments[2]);
            }

            return GetCellObject(row, column);
        }

        private object Append(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.append(iterable) expects one iterable argument.", span);
            }

            EnsureCanMutate(span);
            var row = _cells.Count == 0 ? 1 : MaxRow + 1;
            if (arguments[0] is PyDict dict)
            {
                foreach (var pair in dict)
                {
                    context.CheckExecutionBudget(span);
                    SetCellValue(row, ColumnFromAppendKey(pair.Key, span), pair.Value);
                }

                return PyNone.Instance;
            }

            var column = 1;
            foreach (var item in ToSequence(arguments[0], span, context))
            {
                context.CheckExecutionBudget(span);
                SetCellValue(row, column, item);
                column++;
            }

            return PyNone.Instance;
        }

        private static int ColumnFromAppendKey(object key, LythonSourceSpan span)
        {
            if (PyNumberOps.TryAsInteger(key, out var columnInteger))
            {
                var column = NormalizePositiveInt(columnInteger, "Worksheet.append(...) dictionary key", span);
                ValidateRowColumn(1, column, span);
                return column;
            }

            if (PyStringOps.TryAsString(key, out var columnText))
            {
                return ParseColumnName(columnText.AsString(), MaxWorksheetColumn, span);
            }

            throw new LythonRuntimeException("TypeError", "Worksheet.append(...) dictionary keys must be column letters or one-based column indices.", span);
        }

        private object IterRows(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bounds = ParseIterationBounds(arguments, span);
            return RowsTuple(bounds.MinRow, bounds.MaxRow, bounds.MinColumn, bounds.MaxColumn, bounds.ValuesOnly, context, span);
        }

        private object IterCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (Workbook?.ReadOnly == true)
            {
                throw new LythonRuntimeException("NotImplementedError", "Worksheet.iter_cols(...) is not available for read-only workbooks in Lython.", span);
            }

            var bounds = ParseIterationBounds(arguments, span);
            return ColumnsTuple(bounds.MinRow, bounds.MaxRow, bounds.MinColumn, bounds.MaxColumn, bounds.ValuesOnly, context, span);
        }

        private object CalculateDimension(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.calculate_dimension() expects no arguments.", span);
            }

            return PyString.FromString(WorksheetDimension(this));
        }

        private object SetPrinterSettings(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            _pageSetup.PaperSize = ExpectPositiveInt(arguments[0], "Worksheet.set_printer_settings(paper_size)", span);
            _pageSetup.Orientation = NormalizePageOrientation(arguments[1], "Worksheet.set_printer_settings(orientation)", span);
            return PyNone.Instance;
        }

        private object MergeCells(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var range = ParseCellRangeArguments(arguments, "Worksheet.merge_cells", span);
            if (_mergedRangeSet.Add(range))
            {
                if (_memoryGovernor is not null)
                {
                    _memoryGovernor.Reserve(MergeSlotBytes, _allocationSpan);
                    _memoryGovernor.Commit(MergeSlotBytes);
                    _committedMergeBytes += MergeSlotBytes;
                }

                _mergedRanges.Add(range);
            }

            return PyNone.Instance;
        }

        private object UnmergeCells(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var range = ParseCellRangeArguments(arguments, "Worksheet.unmerge_cells", span);
            if (!_mergedRangeSet.Remove(range))
            {
                throw new LythonRuntimeException("ValueError", $"Cell range {range.Reference} is not merged.", span);
            }

            if (_memoryGovernor is not null && _committedMergeBytes >= MergeSlotBytes)
            {
                _memoryGovernor.Release(MergeSlotBytes);
                _committedMergeBytes -= MergeSlotBytes;
            }

            _mergedRanges.Remove(range);

            return PyNone.Instance;
        }

        private PyList MergedRangeList()
            => new(_mergedRanges.Select(range => (object)PyString.FromString(range.Reference)).ToArray());

        internal void AddLoadedMergedRange(CellRangeAddress range)
        {
            if (_mergedRangeSet.Add(range))
            {
                _mergedRanges.Add(range);
            }
        }

        internal void SetLoadedFreezePanes(string? reference)
        {
            _freezePanes = reference;
        }

        internal void SetLoadedAutoFilter(string? reference)
        {
            _autoFilterRef = reference;
        }

        internal void SetLoadedSheetView(bool showGridLines, bool tabSelected, int workbookViewId)
        {
            _showGridLines = showGridLines;
            _tabSelected = tabSelected;
            _workbookViewId = workbookViewId;
        }

        internal void SetLoadedSelection(string? activeCell, string? sqref, string? pane)
        {
            if (activeCell is not null)
            {
                _selectionActiveCell = activeCell;
            }

            if (sqref is not null)
            {
                _selectionSqref = sqref;
            }

            _selectionPane = pane;
        }

        internal void SetShowGridLines(bool value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _showGridLines = value;
        }

        internal void SetTabSelected(bool value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _tabSelected = value;
        }

        internal void SetWorkbookViewId(int value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _workbookViewId = value;
        }

        internal void SetSelectionActiveCell(string value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionActiveCell = value;
        }

        internal void SetSelectionSqref(string value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionSqref = value;
        }

        internal void SetSelectionPane(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionPane = value;
        }

        internal void SetPrintArea(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printArea = value;
        }

        internal void SetPrintTitleRows(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printTitleRows = value;
        }

        internal void SetPrintTitleCols(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printTitleCols = value;
        }

        internal void SetLoadedPrintArea(string? value)
        {
            _printArea = value;
        }

        internal void SetLoadedPrintTitles(string? rows, string? columns)
        {
            _printTitleRows = rows;
            _printTitleCols = columns;
        }

        internal void MarkPageMargins()
        {
            _hasPageMargins = true;
        }

        internal void SetLoadedPageMargins(double left, double right, double top, double bottom, double header, double footer)
        {
            _pageMargins.SetLoaded(left, right, top, bottom, header, footer);
            _hasPageMargins = true;
        }

        internal OpenPyxlColumnDimension GetOrCreateColumnDimension(int column)
        {
            if (_columnDimensions.TryGetValue(column, out var dimension))
            {
                return dimension;
            }

            if (_memoryGovernor is not null)
            {
                _memoryGovernor.Reserve(DimensionSlotBytes, _allocationSpan);
                _memoryGovernor.Commit(DimensionSlotBytes);
            }

            dimension = new OpenPyxlColumnDimension(this, column);
            _columnDimensions[column] = dimension;
            return dimension;
        }

        internal OpenPyxlRowDimension GetOrCreateRowDimension(int row)
        {
            if (_rowDimensions.TryGetValue(row, out var dimension))
            {
                return dimension;
            }

            if (_memoryGovernor is not null)
            {
                _memoryGovernor.Reserve(DimensionSlotBytes, _allocationSpan);
                _memoryGovernor.Commit(DimensionSlotBytes);
            }

            dimension = new OpenPyxlRowDimension(this, row);
            _rowDimensions[row] = dimension;
            return dimension;
        }

        internal OpenPyxlColumnDimension GetColumnDimension(int column)
        {
            if (!_columnDimensions.TryGetValue(column, out var dimension))
            {
                dimension = new OpenPyxlColumnDimension(this, column);
                _columnDimensions[column] = dimension;
            }

            return dimension;
        }

        internal OpenPyxlRowDimension GetRowDimension(int row)
        {
            if (!_rowDimensions.TryGetValue(row, out var dimension))
            {
                dimension = new OpenPyxlRowDimension(this, row);
                _rowDimensions[row] = dimension;
            }

            return dimension;
        }

        private PyTuple ColumnsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var columns = new List<object>();
            for (var column = minColumn; column <= maxColumn; column++)
            {
                context?.CheckExecutionBudget(span);
                columns.Add(ColumnTuple(minRow, maxRow, column, valuesOnly, context, span));
            }

            context?.ObserveCollectionCount(columns.Count, span);
            return context is null ? new PyTuple(columns) : new PyTuple(columns, context.MemoryGovernor, span);
        }

        private PyTuple RowsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var rows = new List<object>();
            for (var row = minRow; row <= maxRow; row++)
            {
                context?.CheckExecutionBudget(span);
                rows.Add(RowTuple(row, minColumn, maxColumn, valuesOnly, context, span));
            }

            context?.ObserveCollectionCount(rows.Count, span);
            return context is null ? new PyTuple(rows) : new PyTuple(rows, context.MemoryGovernor, span);
        }

        private PyTuple RowTuple(int row, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var items = new List<object>();
            for (var column = minColumn; column <= maxColumn; column++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : GetCellObject(row, column));
            }

            return context is null ? new PyTuple(items) : new PyTuple(items, context.MemoryGovernor, span);
        }

        private PyTuple ColumnTuple(int minRow, int maxRow, int column, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var items = new List<object>();
            for (var row = minRow; row <= maxRow; row++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : GetCellObject(row, column));
            }

            return context is null ? new PyTuple(items) : new PyTuple(items, context.MemoryGovernor, span);
        }

        private IterationBounds ParseIterationBounds(object[] arguments, LythonSourceSpan span)
        {
            var minRow = OptionalPositiveInt(arguments, 0, 1, "Worksheet.iter_rows", "min_row", span);
            var maxRow = OptionalPositiveInt(arguments, 1, MaxRow, "Worksheet.iter_rows", "max_row", span);
            var minColumn = OptionalPositiveInt(arguments, 2, 1, "Worksheet.iter_rows", "min_col", span);
            var maxColumn = OptionalPositiveInt(arguments, 3, MaxColumn, "Worksheet.iter_rows", "max_col", span);
            var valuesOnly = OptionalBool(arguments, 4, false, "Worksheet.iter_rows", "values_only", span);

            if (maxRow < minRow || maxColumn < minColumn)
            {
                return new IterationBounds(1, 0, 1, 0, valuesOnly);
            }

            ValidateRowColumn(maxRow, maxColumn, span);
            return new IterationBounds(minRow, maxRow, minColumn, maxColumn, valuesOnly);
        }

        private readonly record struct IterationBounds(int MinRow, int MaxRow, int MinColumn, int MaxColumn, bool ValuesOnly);

        internal OpenPyxlCell GetCellObject(int row, int column)
        {
            var address = new CellAddress(row, column);
            if (!_cellObjects.TryGetValue(address, out var cell))
            {
                // Wrapper caches stay uncharged with the other model maps;
                // only the value table below is owned here.
                cell = new OpenPyxlCell(this, row, column);
                _cellObjects[address] = cell;
            }

            return cell;
        }

        internal void EnsureCanMutate(LythonSourceSpan? span)
        {
            if (Workbook?.ReadOnly == true)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet belongs to a read-only workbook.", span);
            }
        }
    }
}
