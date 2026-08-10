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
    internal sealed partial class OpenPyxlWorksheet :
        IPyMutableDynamicAttributes,
        IMutablePySubscriptableValue,
        IPySliceableValue,
        IPyRenderableValue
    {
        private readonly Dictionary<CellAddress, object> _cells = new();
        private readonly Dictionary<CellAddress, OpenPyxlCell> _cellObjects = new();
        private readonly Dictionary<CellAddress, string> _dataTypes = new();
        private readonly Dictionary<CellAddress, string> _numberFormats = new();
        private readonly Dictionary<CellAddress, int> _loadedStyleIds = new();
        private readonly Dictionary<CellAddress, string> _hyperlinks = new();
        private readonly Dictionary<CellAddress, OpenPyxlComment> _comments = new();
        private readonly Dictionary<CellAddress, object> _formulaCachedValues = new();
        private readonly Dictionary<CellAddress, XElement> _formulaXml = new();
        private readonly Dictionary<(CellAddress Address, string Name), object> _cellStyles = new();
        private readonly Dictionary<CellAddress, string> _cellNamedStyles = new();
        private readonly Dictionary<string, OpenPyxlTable> _tables = new(StringComparer.Ordinal);
        private readonly List<OpenPyxlDataValidation> _dataValidations = new();
        private readonly List<OpenPyxlConditionalFormatting> _conditionalFormattings = new();
        private readonly List<OpenPyxlLoadedDrawing> _drawings = new();
        private readonly List<OpenPyxlLoadedChart> _charts = new();
        private readonly List<OpenPyxlLoadedImage> _images = new();
        private readonly List<CellRangeAddress> _mergedRanges = new();
        private readonly HashSet<CellRangeAddress> _mergedRangeSet = [];
        private readonly Dictionary<int, OpenPyxlColumnDimension> _columnDimensions = new();
        private readonly Dictionary<int, OpenPyxlRowDimension> _rowDimensions = new();
        private int _minRow = 1;
        private int _maxRow = 1;
        private int _minColumn = 1;
        private int _maxColumn = 1;
        private bool _dimensionsDirty;
        private string? _freezePanes;
        private string? _autoFilterRef;
        private bool _showGridLines = true;
        private bool _tabSelected;
        private int _workbookViewId;
        private string _selectionActiveCell = "A1";
        private string _selectionSqref = "A1";
        private string? _selectionPane;
        private readonly OpenPyxlPageMargins _pageMargins;
        private readonly OpenPyxlPageSetup _pageSetup;
        private readonly OpenPyxlSheetProtection _protection;
        private string? _printArea;
        private string? _printTitleRows;
        private string? _printTitleCols;
        private string? _commentsSourcePath;
        private bool _hasPageMargins;
        private bool _hasStructuralMutation;
        private bool _hasLoadedCommentsUpdate;

        public OpenPyxlWorksheet(string title)
        {
            Title = title;
            _pageMargins = new OpenPyxlPageMargins(this);
            _pageSetup = new OpenPyxlPageSetup(this);
            _protection = new OpenPyxlSheetProtection(this);
        }

        public OpenPyxlWorkbook? Workbook { get; set; }

        public string? SourcePath { get; set; }

        public string Title { get; private set; }

        public IReadOnlyDictionary<CellAddress, object> Cells => _cells;

        public IReadOnlyDictionary<CellAddress, string> NumberFormats => _numberFormats;

        public IReadOnlyDictionary<CellAddress, int> LoadedStyleIds => _loadedStyleIds;

        public IReadOnlyDictionary<(CellAddress Address, string Name), object> CellStyles => _cellStyles;

        public IReadOnlyDictionary<CellAddress, string> CellNamedStyles => _cellNamedStyles;

        public IReadOnlyDictionary<CellAddress, string> Hyperlinks => _hyperlinks;

        public IReadOnlyDictionary<CellAddress, OpenPyxlComment> Comments => _comments;

        public string? CommentsSourcePath => _commentsSourcePath;

        public bool HasLoadedCommentsUpdate => _hasLoadedCommentsUpdate;

        public IReadOnlyDictionary<string, OpenPyxlTable> Tables => _tables;

        public IReadOnlyList<OpenPyxlDataValidation> DataValidations => _dataValidations;

        public IReadOnlyList<OpenPyxlConditionalFormatting> ConditionalFormattings => _conditionalFormattings;

        public IReadOnlyList<CellRangeAddress> MergedRanges => _mergedRanges;

        public IReadOnlyDictionary<int, OpenPyxlColumnDimension> ColumnDimensions => _columnDimensions;

        public IReadOnlyDictionary<int, OpenPyxlRowDimension> RowDimensions => _rowDimensions;

        internal bool HasStructuralMutation => _hasStructuralMutation;

        public string? AutoFilterRef
        {
            get => _autoFilterRef;
            set => _autoFilterRef = value;
        }

        public string? FreezePanes => _freezePanes;

        public bool ShowGridLines => _showGridLines;

        public bool TabSelected => _tabSelected;

        public int WorkbookViewId => _workbookViewId;

        public string SelectionActiveCell => _selectionActiveCell;

        public string SelectionSqref => _selectionSqref;

        public string? SelectionPane => _selectionPane;

        public string? PrintArea => _printArea;

        public string? PrintTitleRows => _printTitleRows;

        public string? PrintTitleCols => _printTitleCols;

        public OpenPyxlPageMargins PageMargins => _pageMargins;

        public OpenPyxlPageSetup PageSetup => _pageSetup;

        public OpenPyxlSheetProtection Protection => _protection;

        public bool HasPageMargins => _hasPageMargins;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "title" => PyString.FromString(Title),
                "min_row" => new BigInteger(MinRow),
                "max_row" => new BigInteger(MaxRow),
                "min_column" => new BigInteger(MinColumn),
                "max_column" => new BigInteger(MaxColumn),
                "dimensions" => PyString.FromString(WorksheetDimension(this)),
                "freeze_panes" => _freezePanes is null ? PyNone.Instance : PyString.FromString(_freezePanes),
                "show_gridlines" => _showGridLines,
                "sheet_view" => new OpenPyxlSheetView(this),
                "print_area" => _printArea is null ? PyNone.Instance : PyString.FromString(_printArea),
                "print_title_rows" => _printTitleRows is null ? PyNone.Instance : PyString.FromString(_printTitleRows),
                "print_title_cols" => _printTitleCols is null ? PyNone.Instance : PyString.FromString(_printTitleCols),
                "page_margins" => _pageMargins,
                "page_setup" => _pageSetup,
                "protection" => _protection,
                "set_printer_settings" => new BoundCallable(SetPrinterSettings, "Worksheet.set_printer_settings", ["paper_size", "orientation"], requiredCount: 2),
                "auto_filter" => new OpenPyxlAutoFilter(this),
                "tables" => new OpenPyxlTableCollection(this),
                "data_validations" => new OpenPyxlDataValidationList(this),
                "conditional_formatting" => new OpenPyxlConditionalFormattingCollection(this),
                "_charts" => new PyList(_charts.Cast<object>().ToArray()),
                "_images" => new PyList(_images.Cast<object>().ToArray()),
                "_drawings" => new PyList(_drawings.Cast<object>().ToArray()),
                "drawings" => new PyList(_drawings.Cast<object>().ToArray()),
                "_drawing" => _drawings.Count == 0 ? PyNone.Instance : _drawings[0],
                "add_table" => new BoundCallable(AddTable, "Worksheet.add_table", ["table"]),
                "add_data_validation" => new BoundCallable(AddDataValidation, "Worksheet.add_data_validation", ["data_validation"]),
                "add_chart" => new BoundCallable(AddChart, "Worksheet.add_chart", ["chart", "anchor"], requiredCount: 1),
                "add_image" => new BoundCallable(AddImage, "Worksheet.add_image", ["img", "anchor"], requiredCount: 1),
                "column_dimensions" => new OpenPyxlColumnDimensionCollection(this),
                "row_dimensions" => new OpenPyxlRowDimensionCollection(this),
                "rows" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null),
                "columns" => ColumnsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null),
                "values" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: true, null, null),
                "cell" => new BoundCallable(Cell, "Worksheet.cell", ["row", "column", "value"], requiredCount: 2),
                "append" => new BoundCallable(Append, "Worksheet.append", ["iterable"]),
                "iter_rows" => new BoundCallable(IterRows, "Worksheet.iter_rows", ["min_row", "max_row", "min_col", "max_col", "values_only"], requiredCount: 0),
                "iter_cols" => new BoundCallable(IterCols, "Worksheet.iter_cols", ["min_row", "max_row", "min_col", "max_col", "values_only"], requiredCount: 0),
                "calculate_dimension" => new BoundCallable(CalculateDimension, "Worksheet.calculate_dimension", []),
                "merge_cells" => new BoundCallable(MergeCells, "Worksheet.merge_cells", ["range_string", "start_row", "start_column", "end_row", "end_column"], requiredCount: 0),
                "unmerge_cells" => new BoundCallable(UnmergeCells, "Worksheet.unmerge_cells", ["range_string", "start_row", "start_column", "end_row", "end_column"], requiredCount: 0),
                "insert_rows" => new BoundCallable(InsertRows, "Worksheet.insert_rows", ["idx", "amount"], requiredCount: 1),
                "delete_rows" => new BoundCallable(DeleteRows, "Worksheet.delete_rows", ["idx", "amount"], requiredCount: 1),
                "insert_cols" => new BoundCallable(InsertCols, "Worksheet.insert_cols", ["idx", "amount"], requiredCount: 1),
                "delete_cols" => new BoundCallable(DeleteCols, "Worksheet.delete_cols", ["idx", "amount"], requiredCount: 1),
                "move_range" => new BoundCallable(MoveRange, "Worksheet.move_range", ["cell_range", "rows", "cols", "translate"], requiredCount: 1),
                "merged_cells" => new OpenPyxlMergedCellSet(_mergedRanges),
                "merged_cell_ranges" => MergedRangeList(),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name == "freeze_panes")
            {
                EnsureCanMutate(null);
                _freezePanes = NormalizeOptionalCellReference(value, "Worksheet.freeze_panes", null);
                return true;
            }

            if (name == "show_gridlines")
            {
                SetShowGridLines(ExpectBool(value, "Worksheet.show_gridlines", null), null);
                return true;
            }

            if (name == "print_area")
            {
                SetPrintArea(NormalizeOptionalPrintArea(value, "Worksheet.print_area", null), null);
                return true;
            }

            if (name == "print_title_rows")
            {
                SetPrintTitleRows(NormalizeOptionalPrintTitleRows(value, "Worksheet.print_title_rows", null), null);
                return true;
            }

            if (name == "print_title_cols")
            {
                SetPrintTitleCols(NormalizeOptionalPrintTitleCols(value, "Worksheet.print_title_cols", null), null);
                return true;
            }

            if (name != "title")
            {
                return false;
            }

            EnsureCanMutate(null);
            var title = ExpectString(value, "Worksheet.title", null);
            Title = Workbook is null
                ? ValidateDetachedSheetTitle(title)
                : Workbook.MakeUniqueSheetTitle(title, this, null);
            return true;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            if (PyNumberOps.TryAsInteger(index, out var rowInteger))
            {
                var row = NormalizePositiveInt(rowInteger, "Worksheet row lookup", span);
                ValidateRowColumn(row, 1, span);
                return RowTuple(row, 1, MaxColumn, valuesOnly: false, null, span);
            }

            var text = ExpectString(index, "Worksheet cell lookup", span);
            if (text.Contains(':', StringComparison.Ordinal))
            {
                return GetRangeSubscript(text, span);
            }

            if (TryParseColumnReference(text, span, out var column))
            {
                return ColumnTuple(1, MaxRow, column, valuesOnly: false, null, span);
            }

            var address = ParseCellAddress(text, span);
            return GetCellObject(address.Row, address.Column);
        }

        public object GetSlice(object? start, object? end, object? step, LythonSourceSpan span)
        {
            if (step is not null and not PyNone)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet slicing does not support a step.", span);
            }

            if (TryParseSliceCellRange(start, end, span, out var cellStart, out var cellEnd))
            {
                return RowsTuple(cellStart.Row, cellEnd.Row, cellStart.Column, cellEnd.Column, valuesOnly: false, null, span);
            }

            if (TryParseSliceColumnRange(start, end, span, out var minColumn, out var maxColumn))
            {
                return ColumnsTuple(1, MaxRow, minColumn, maxColumn, valuesOnly: false, null, span);
            }

            if (TryParseSliceRowRange(start, end, span, out var minRow, out var maxRow))
            {
                return RowsTuple(minRow, maxRow, 1, MaxColumn, valuesOnly: false, null, span);
            }

            throw new LythonRuntimeException("TypeError", "Worksheet slice bounds must be row numbers, column letters, or cell coordinates.", span);
        }

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            var text = ExpectString(index, "Worksheet cell assignment", span);
            if (text.Contains(':', StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "Worksheet range assignment is not supported by Lython openpyxl.", span);
            }

            var address = ParseCellAddress(text, span);
            SetCellValue(address.Row, address.Column, value);
        }

        private object GetRangeSubscript(string text, LythonSourceSpan span)
        {
            var parts = text.Split(':', 2);
            if (parts.Length != 2)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell range: {text}", span);
            }

            if (TryParseColumnReference(parts[0], span, out var startColumn) &&
                TryParseColumnReference(parts[1], span, out var endColumn))
            {
                return ColumnsTuple(1, MaxRow, Math.Min(startColumn, endColumn), Math.Max(startColumn, endColumn), valuesOnly: false, null, span);
            }

            if (TryParseRowReference(parts[0], span, out var startRow) &&
                TryParseRowReference(parts[1], span, out var endRow))
            {
                return RowsTuple(Math.Min(startRow, endRow), Math.Max(startRow, endRow), 1, MaxColumn, valuesOnly: false, null, span);
            }

            var (start, end) = ParseRange(text, span);
            return RowsTuple(start.Row, end.Row, start.Column, end.Column, valuesOnly: false, null, span);
        }

        private static bool TryParseSliceCellRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out CellAddress normalizedStart,
            out CellAddress normalizedEnd)
        {
            normalizedStart = default;
            normalizedEnd = default;
            if (!TryGetString(start, out var startText) ||
                !TryGetString(end, out var endText) ||
                !OpenPyxlReferenceFacts.IsCellReference(startText) ||
                !OpenPyxlReferenceFacts.IsCellReference(endText))
            {
                return false;
            }

            var parsedStart = ParseCellAddress(startText, span);
            var parsedEnd = ParseCellAddress(endText, span);
            normalizedStart = new CellAddress(Math.Min(parsedStart.Row, parsedEnd.Row), Math.Min(parsedStart.Column, parsedEnd.Column));
            normalizedEnd = new CellAddress(Math.Max(parsedStart.Row, parsedEnd.Row), Math.Max(parsedStart.Column, parsedEnd.Column));
            return true;
        }

        private static bool TryParseSliceColumnRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out int minColumn,
            out int maxColumn)
        {
            minColumn = 0;
            maxColumn = 0;
            if (!TryGetString(start, out var startText) ||
                !TryGetString(end, out var endText) ||
                !TryParseColumnReference(startText, span, out var startColumn) ||
                !TryParseColumnReference(endText, span, out var endColumn))
            {
                return false;
            }

            minColumn = Math.Min(startColumn, endColumn);
            maxColumn = Math.Max(startColumn, endColumn);
            return true;
        }

        private bool TryParseSliceRowRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out int minRow,
            out int maxRow)
        {
            if (!TryParseOptionalRowBound(start, defaultValue: 1, span, out var startRow) ||
                !TryParseOptionalRowBound(end, defaultValue: MaxRow, span, out var endRow))
            {
                minRow = 0;
                maxRow = 0;
                return false;
            }

            minRow = Math.Min(startRow, endRow);
            maxRow = Math.Max(startRow, endRow);
            return true;
        }

        private static bool TryParseOptionalRowBound(object? value, int defaultValue, LythonSourceSpan span, out int row)
        {
            if (value is null or PyNone)
            {
                row = defaultValue;
                return true;
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                row = NormalizePositiveInt(integer, "Worksheet row slice", span);
                return true;
            }

            row = 0;
            return false;
        }

        private static bool TryGetString(object? value, out string text)
        {
            if (value is not null && PyStringOps.TryAsString(value, out var pyText))
            {
                text = pyText.AsString();
                return true;
            }

            text = string.Empty;
            return false;
        }

        private static bool TryParseColumnReference(string text, LythonSourceSpan span, out int column)
        {
            var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            if (normalized.Length == 0 || normalized.Any(c => !char.IsLetter(c)))
            {
                column = 0;
                return false;
            }

            column = ParseColumnName(normalized, span);
            return true;
        }

        private static bool TryParseRowReference(string text, LythonSourceSpan span, out int row)
        {
            var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            if (normalized.Length == 0 ||
                normalized.Any(c => !char.IsDigit(c)) ||
                !int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
            {
                row = 0;
                return false;
            }

            ValidateRowColumn(row, 1, span);
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<Worksheet \"{Title}\">");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        internal int MinRow
        {
            get
            {
                EnsureDimensions();
                return _minRow;
            }
        }

        internal int MaxRow
        {
            get
            {
                EnsureDimensions();
                return _maxRow;
            }
        }

        internal int MinColumn
        {
            get
            {
                EnsureDimensions();
                return _minColumn;
            }
        }

        internal int MaxColumn
        {
            get
            {
                EnsureDimensions();
                return _maxColumn;
            }
        }

        private void IncludeInDimensions(CellAddress address)
        {
            if (_dimensionsDirty)
            {
                return;
            }

            if (_cells.Count == 1)
            {
                _minRow = address.Row;
                _maxRow = address.Row;
                _minColumn = address.Column;
                _maxColumn = address.Column;
                return;
            }

            _minRow = Math.Min(_minRow, address.Row);
            _maxRow = Math.Max(_maxRow, address.Row);
            _minColumn = Math.Min(_minColumn, address.Column);
            _maxColumn = Math.Max(_maxColumn, address.Column);
        }

        private void InvalidateDimensionsAfterRemoval(CellAddress address)
        {
            if (_cells.Count == 0)
            {
                _minRow = 1;
                _maxRow = 1;
                _minColumn = 1;
                _maxColumn = 1;
                _dimensionsDirty = false;
                return;
            }

            if (address.Row == _minRow ||
                address.Row == _maxRow ||
                address.Column == _minColumn ||
                address.Column == _maxColumn)
            {
                _dimensionsDirty = true;
            }
        }

        private void EnsureDimensions()
        {
            if (!_dimensionsDirty)
            {
                return;
            }

            if (_cells.Count == 0)
            {
                _minRow = 1;
                _maxRow = 1;
                _minColumn = 1;
                _maxColumn = 1;
                _dimensionsDirty = false;
                return;
            }

            var first = true;
            foreach (var address in _cells.Keys)
            {
                if (first)
                {
                    _minRow = address.Row;
                    _maxRow = address.Row;
                    _minColumn = address.Column;
                    _maxColumn = address.Column;
                    first = false;
                    continue;
                }

                _minRow = Math.Min(_minRow, address.Row);
                _maxRow = Math.Max(_maxRow, address.Row);
                _minColumn = Math.Min(_minColumn, address.Column);
                _maxColumn = Math.Max(_maxColumn, address.Column);
            }

            _dimensionsDirty = false;
        }

    }
}
