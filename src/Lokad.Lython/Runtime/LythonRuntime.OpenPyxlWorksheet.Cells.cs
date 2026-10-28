using System.Numerics;
using System.Xml.Linq;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class OpenPyxlWorksheet
    {
        private OpenPyxlCell? ExistingCell(int row, int column)
            => _cells.GetValueOrDefault(new CellAddress(row, column));
        internal object GetCellValue(int row, int column) => ExistingCell(row, column)?.StoredValue ?? PyNone.Instance;
        internal object GetFormulaCachedValue(int row, int column) => ExistingCell(row, column)?.FormulaCachedValue ?? PyNone.Instance;
        internal XElement? GetFormulaXml(int row, int column) => ExistingCell(row, column)?.FormulaXml;
        internal string GetCellDataType(int row, int column) => ExistingCell(row, column)?.DataType ?? CellDataType(GetCellValue(row, column));
        internal string GetCellNumberFormat(int row, int column) => ExistingCell(row, column)?.NumberFormat ?? "General";
        internal OpenPyxlStyleValue? GetAssignedCellStyle(int row, int column, OpenPyxlCellStyleComponent component)
            => ExistingCell(row, column)?.AssignedStyle(component) as OpenPyxlStyleValue;

        internal void SetCellNamedStyle(OpenPyxlCell cell, object value)
        {
            EnsureCanMutate(null);
            OpenPyxlStyleValue? style = value as OpenPyxlStyleValue;
            var name = style?.Kind == OpenPyxlStyleKind.NamedStyle ? NamedStyleName(style, null) : ExpectString(value, "Cell.style", null);
            style ??= Workbook?.FindNamedStyle(name);
            cell.LoadedStyleId = 0;
            cell.NamedStyle = name == "Normal" ? null : name;
            if (style is null) return;
            cell.NumberFormat = NamedStyleNumberFormat(style);
            foreach (var component in Enum.GetValues<OpenPyxlCellStyleComponent>())
                if (NamedStyleComponent(style, component) is { } assigned) cell.AssignStyle(component, assigned);
        }
        internal void SetCellStyle(OpenPyxlCell cell, OpenPyxlCellStyleComponent component, object value)
        {
            EnsureCanMutate(null);
            var expected = ExpectedStyleKind(component);
            if (value is not PyNone && (value is not OpenPyxlStyleValue style || style.Kind != expected))
                throw new LythonRuntimeException("TypeError", "Cell." + CellStyleComponentName(component) + " expects " + OpenPyxlStyleQualifiedName(expected) + ".", null);
            cell.LoadedStyleId = 0;
            cell.AssignStyle(component, value is PyNone ? null : value);
        }
        internal void SetCellHyperlink(OpenPyxlCell cell, object value)
        {
            EnsureCanMutate(null);
            cell.Hyperlink = value is PyNone ? null : value is OpenPyxlHyperlink link ? link.Target : ExpectString(value, "Cell.hyperlink", null);
        }
        internal void SetCellComment(OpenPyxlCell cell, object value)
        {
            EnsureCanMutate(null);
            if (value is not PyNone && value is not OpenPyxlComment)
                throw new LythonRuntimeException("TypeError", "Cell.comment expects openpyxl.comments.Comment or None.", null);
            cell.Comment = value as OpenPyxlComment;
        }
        internal void SetCellNumberFormat(OpenPyxlCell cell, object value)
        {
            EnsureCanMutate(null);
            var format = ExpectString(value, "Cell.number_format", null);
            cell.LoadedStyleId = 0;
            cell.NumberFormat = format == "General" ? null : format;
        }
        internal int GetCellStyleId(OpenPyxlCell cell)
            => Workbook is { } workbook ? OpenPyxlPackage.GetCellStyleId(workbook, cell) : cell.LoadedStyleId;
        internal void SetCellValue(int row, int column, object value, bool advanceRow = true)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var normalized = NormalizeCellValue(value, null);
            SetNormalizedCellValue(GetCellObject(row, column, advanceRow), normalized);
        }
        internal void SetCellValue(OpenPyxlCell cell, object value)
        {
            EnsureCanMutate(null);
            SetNormalizedCellValue(cell, NormalizeCellValue(value, null));
        }
        private static void SetNormalizedCellValue(OpenPyxlCell cell, object normalized)
        {
            cell.StoredValue = normalized;
            cell.FormulaCachedValue = PyNone.Instance;
            cell.FormulaXml = null;
            cell.DataType = PyStringOps.TryAsString(normalized, out var text) && IsCellErrorText(text.AsString()) ? "e"
                : IsDateLikeCellValue(normalized) ? "d" : null;
            if (IsDateLikeCellValue(normalized) && cell.NumberFormat is null)
                cell.NumberFormat = normalized switch
                {
                    PyDate => "yyyy-mm-dd", PyDateTime => "yyyy-mm-dd h:mm:ss", PyTime => "h:mm:ss", PyTimedelta => "[hh]:mm:ss", _ => null,
                };
        }
        internal void SetLoadedCellValue(int row, int column, object value) => GetCellObject(row, column).StoredValue = value;
        internal void SetLoadedFormulaCachedValue(int row, int column, object value) => GetCellObject(row, column).FormulaCachedValue = value;
        internal void SetLoadedFormulaXml(int row, int column, XElement? value) => GetCellObject(row, column).FormulaXml = value is null ? null : new XElement(value);
        internal void SetLoadedCellNumberFormat(int row, int column, string value) => GetCellObject(row, column).NumberFormat = value == "General" ? null : value;
        internal void SetLoadedCellStyleId(int row, int column, int? value) => GetCellObject(row, column).LoadedStyleId = value ?? 0;
        internal void SetLoadedCellStyle(int row, int column, OpenPyxlCellStyleComponent component, OpenPyxlStyleValue? value)
        { if (value is not null) GetCellObject(row, column).AssignStyle(component, value); }
        internal void SetLoadedCellDataType(int row, int column, string? value)
        { if (value is "e" or "s" or "str" or "inlineStr") GetCellObject(row, column).DataType = value == "e" ? "e" : "s"; }
        internal void SetLoadedCellNamedStyle(int row, int column, string? value)
        { if (value is not null && value != "Normal") GetCellObject(row, column).NamedStyle = value; }
        internal void SetLoadedHyperlink(CellRangeAddress range, string value)
        {
            for (var row = range.Start.Row; row <= range.End.Row; row++)
                for (var column = range.Start.Column; column <= range.End.Column; column++) GetCellObject(row, column).Hyperlink = value;
        }
        internal void SetLoadedComment(int row, int column, OpenPyxlComment value) => GetCellObject(row, column).Comment = value;
        internal void SetLoadedCommentsSource(string path) => _commentsSourcePath = path;
        internal void SetLoadedTable(OpenPyxlTable table) => _tables[table.DisplayName] = table;

        internal OpenPyxlWorksheet Copy(string title)
        {
            var copy = new OpenPyxlWorksheet(title);
            if (_memoryGovernor is not null) copy.AttachMemoryGovernor(_memoryGovernor, _allocationSpan, _context);
            copy.ReserveCellCapacity(_cells.Count);
            var otherBytes = (long)MergeSlotBytes * _mergedRanges.Count + RegistrySlotBytes * ((long)_tables.Count + _dataValidations.Count + _conditionalFormattings.Count);
            _memoryGovernor?.Reserve(otherBytes, _allocationSpan);
            _memoryGovernor?.Commit(otherBytes);
            foreach (var pair in _cells)
            {
                _context?.CheckExecution(_allocationSpan);
                var source = pair.Value;
                var cell = new OpenPyxlCell(copy, source.Row, source.Column)
                {
                    StoredValue = source.StoredValue, DataType = source.DataType, NumberFormat = source.NumberFormat,
                    LoadedStyleId = source.LoadedStyleId, Hyperlink = source.Hyperlink, NamedStyle = source.NamedStyle,
                    Comment = source.Comment?.Copy(), FormulaCachedValue = source.FormulaCachedValue,
                    FormulaXml = source.FormulaXml, // private immutable parse data; serialization clones before editing
                    Font = source.Font, Fill = source.Fill, Border = source.Border, Alignment = source.Alignment, Protection = source.Protection,
                };
                copy._cells.Add(pair.Key, cell);
            }
            copy._createdCells = _cells.Count;
            copy._minRow = _minRow; copy._maxRow = _maxRow;
            copy._minColumn = _minColumn; copy._maxColumn = _maxColumn;
            copy._dimensionsDirty = _dimensionsDirty; copy._appendRow = _appendRow;
            copy._commentsSourcePath = _commentsSourcePath;
            copy._hasLoadedCommentsUpdate = _hasLoadedCommentsUpdate;
            foreach (var pair in _tables) copy._tables[pair.Key] = pair.Value.Copy();
            foreach (var validation in _dataValidations) copy._dataValidations.Add(validation.Copy());
            foreach (var formatting in _conditionalFormattings) copy._conditionalFormattings.Add(formatting.Copy());
            foreach (var range in _mergedRanges) { copy._mergedRanges.Add(range); copy._mergedRangeSet.Add(range); }
            copy._protection.CopyFrom(_protection);
            return copy;
        }
    }
}
