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
    private sealed class OpenPyxlSheetView : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlSheetView(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "showGridLines" or "show_gridlines" => _worksheet.ShowGridLines,
                "tabSelected" => _worksheet.TabSelected,
                "workbookViewId" => new BigInteger(_worksheet.WorkbookViewId),
                "selection" => new PyList([new OpenPyxlSelection(_worksheet)]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "showGridLines":
                case "show_gridlines":
                    _worksheet.SetShowGridLines(ExpectBool(value, "SheetView.showGridLines", null), null);
                    return true;
                case "tabSelected":
                    _worksheet.SetTabSelected(ExpectBool(value, "SheetView.tabSelected", null), null);
                    return true;
                case "workbookViewId":
                    _worksheet.SetWorkbookViewId(ExpectNonNegativeInt(value, "SheetView.workbookViewId", null), null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.views.SheetView>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlSelection : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlSelection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "activeCell" => PyString.FromString(_worksheet.SelectionActiveCell),
                "sqref" => PyString.FromString(_worksheet.SelectionSqref),
                "pane" => _worksheet.SelectionPane is null ? PyNone.Instance : PyString.FromString(_worksheet.SelectionPane),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "activeCell":
                    _worksheet.SetSelectionActiveCell(NormalizeCellReference(value, "Selection.activeCell", null), null);
                    return true;
                case "sqref":
                    _worksheet.SetSelectionSqref(NormalizeSelectionReference(value, "Selection.sqref", null), null);
                    return true;
                case "pane":
                    _worksheet.SetSelectionPane(NormalizeOptionalPane(value, "Selection.pane", null), null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.views.Selection>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlPageMargins : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlPageMargins(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public double Left { get; set; } = 0.75;

        public double Right { get; set; } = 0.75;

        public double Top { get; set; } = 1.0;

        public double Bottom { get; set; } = 1.0;

        public double Header { get; set; } = 0.5;

        public double Footer { get; set; } = 0.5;

        public void SetLoaded(double left, double right, double top, double bottom, double header, double footer)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
            Header = header;
            Footer = footer;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "left" => Left,
                "right" => Right,
                "top" => Top,
                "bottom" => Bottom,
                "header" => Header,
                "footer" => Footer,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "left":
                case "right":
                case "top":
                case "bottom":
                case "header":
                case "footer":
                    break;
                default:
                    return false;
            }

            var normalized = NormalizeOptionalNonNegativeDouble(value, "PageMargins." + name, null);
            if (normalized is null)
            {
                throw new LythonRuntimeException("TypeError", "PageMargins." + name + " expects a non-negative number.", null);
            }

            _worksheet.EnsureCanMutate(null);
            switch (name)
            {
                case "left":
                    Left = normalized.Value;
                    break;
                case "right":
                    Right = normalized.Value;
                    break;
                case "top":
                    Top = normalized.Value;
                    break;
                case "bottom":
                    Bottom = normalized.Value;
                    break;
                case "header":
                    Header = normalized.Value;
                    break;
                case "footer":
                    Footer = normalized.Value;
                    break;
            }

            _worksheet.MarkPageMargins();
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.page.PageMargins>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlPageSetup : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlPageSetup(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public string? Orientation { get; set; }

        public int? PaperSize { get; set; }

        public int? FitToWidth { get; set; }

        public int? FitToHeight { get; set; }

        public int? Scale { get; set; }

        public bool HasSettings =>
            Orientation is not null ||
            PaperSize is not null ||
            FitToWidth is not null ||
            FitToHeight is not null ||
            Scale is not null;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "orientation" => Orientation is null ? PyNone.Instance : PyString.FromString(Orientation),
                "paperSize" or "paper_size" => PaperSize is null ? PyNone.Instance : new BigInteger(PaperSize.Value),
                "fitToWidth" or "fit_to_width" => FitToWidth is null ? PyNone.Instance : new BigInteger(FitToWidth.Value),
                "fitToHeight" or "fit_to_height" => FitToHeight is null ? PyNone.Instance : new BigInteger(FitToHeight.Value),
                "scale" => Scale is null ? PyNone.Instance : new BigInteger(Scale.Value),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "orientation":
                    _worksheet.EnsureCanMutate(null);
                    Orientation = NormalizeOptionalPageOrientation(value, "PageSetup.orientation", null);
                    return true;
                case "paperSize":
                case "paper_size":
                    _worksheet.EnsureCanMutate(null);
                    PaperSize = NormalizeOptionalPositiveInt(value, "PageSetup.paperSize", null);
                    return true;
                case "fitToWidth":
                case "fit_to_width":
                    _worksheet.EnsureCanMutate(null);
                    FitToWidth = NormalizeOptionalNonNegativeInt(value, "PageSetup.fitToWidth", null);
                    return true;
                case "fitToHeight":
                case "fit_to_height":
                    _worksheet.EnsureCanMutate(null);
                    FitToHeight = NormalizeOptionalNonNegativeInt(value, "PageSetup.fitToHeight", null);
                    return true;
                case "scale":
                    _worksheet.EnsureCanMutate(null);
                    Scale = NormalizeOptionalPositiveInt(value, "PageSetup.scale", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.page.PrintPageSetup>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlColumnDimensionCollection : IPySubscriptableValue, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlColumnDimensionCollection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var column = ParseColumnName(ExpectString(index, "Worksheet.column_dimensions[...] key", span), MaxWorksheetColumn, span);
            return _worksheet.GetOrCreateColumnDimension(column);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<DimensionHolder>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlRowDimensionCollection : IPySubscriptableValue, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlRowDimensionCollection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var row = ExpectPositiveInt(index, "Worksheet.row_dimensions[...] key", span);
            ValidateRowColumn(row, 1, span);
            return _worksheet.GetOrCreateRowDimension(row);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<DimensionHolder>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlTableCollection :
        IPyDynamicAttributes,
        IPySubscriptableValue,
        IPyIterableValue,
        IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;
        private readonly MemoryGovernor _governor;
        private readonly LythonSourceSpan? _allocationSpan;

        public OpenPyxlTableCollection(OpenPyxlWorksheet worksheet)
            : this(worksheet, governor: null, allocationSpan: null)
        {
        }

        public OpenPyxlTableCollection(OpenPyxlWorksheet worksheet, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
        {
            _worksheet = worksheet;
            _governor = governor;
            _allocationSpan = allocationSpan;
            if (governor is not null)
            {
                governor.Reserve(64L, allocationSpan);
                governor.Commit(64L);
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "keys" => BoundCallable.Create(Keys, "TableList.keys", []),
                "values" => BoundCallable.Create(Values, "TableList.values", []),
                "items" => BoundCallable.Create(Items, "TableList.items", []),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var name = ExpectString(index, "Worksheet.tables[...] key", span);
            if (!_worksheet.Tables.TryGetValue(name, out var table))
            {
                throw new LythonRuntimeException("KeyError", name, span);
            }

            return table;
        }

        public IEnumerable<object> Iterate()
            => _worksheet.Tables.Keys.Order(StringComparer.Ordinal).Select(name => _governor is null
                ? (object)PyString.FromString(name)
                : PyString.FromString(name, _governor, _allocationSpan));

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.table.TableList>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Keys(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.keys() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables.Keys.Order(StringComparer.Ordinal).Select(name => (object)PyString.FromString(name, context.MemoryGovernor, span)), context.MemoryGovernor, span);
        }

        private object Values(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.values() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (object)pair.Value), context.MemoryGovernor, span);
        }

        private object Items(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.items() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (object)PyTuple.FromOwnedArray([PyString.FromString(pair.Key, context.MemoryGovernor, span), pair.Value], context.MemoryGovernor, span))
                .ToArray(), context.MemoryGovernor, span);
        }
    }

    internal sealed class OpenPyxlColumnDimension : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlColumnDimension(OpenPyxlWorksheet worksheet, int column)
        {
            _worksheet = worksheet;
            Column = column;
        }

        public int Column { get; }

        public double? Width { get; set; }

        public bool Hidden { get; set; }

        public object Style { get; set; } = PyNone.Instance;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "index" => PyString.FromString(ColumnName(Column)),
                "width" => Width is null ? PyNone.Instance : Width.Value,
                "hidden" => Hidden,
                "style" => Style,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "width":
                    _worksheet.EnsureCanMutate(null);
                    Width = NormalizeOptionalNonNegativeDouble(value, "ColumnDimension.width", null);
                    return true;
                case "hidden":
                    _worksheet.EnsureCanMutate(null);
                    Hidden = ExpectBool(value, "ColumnDimension.hidden", null);
                    return true;
                case "style":
                    _worksheet.EnsureCanMutate(null);
                    Style = NormalizeDimensionStyle(value, "ColumnDimension.style", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<ColumnDimension {ColumnName(Column)}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlRowDimension : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlRowDimension(OpenPyxlWorksheet worksheet, int row)
        {
            _worksheet = worksheet;
            Row = row;
        }

        public int Row { get; }

        public double? Height { get; set; }

        public bool Hidden { get; set; }

        public object Style { get; set; } = PyNone.Instance;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "index" => new BigInteger(Row),
                "height" => Height is null ? PyNone.Instance : Height.Value,
                "hidden" => Hidden,
                "style" => Style,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "height":
                    _worksheet.EnsureCanMutate(null);
                    Height = NormalizeOptionalNonNegativeDouble(value, "RowDimension.height", null);
                    return true;
                case "hidden":
                    _worksheet.EnsureCanMutate(null);
                    Hidden = ExpectBool(value, "RowDimension.hidden", null);
                    return true;
                case "style":
                    _worksheet.EnsureCanMutate(null);
                    Style = NormalizeDimensionStyle(value, "RowDimension.style", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<RowDimension {Row}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

}
