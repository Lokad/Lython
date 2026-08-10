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
        internal object GetCellValue(int row, int column)
            => _cells.TryGetValue(new CellAddress(row, column), out var value) ? value : PyNone.Instance;
        internal object GetFormulaCachedValue(int row, int column)
            => _formulaCachedValues.TryGetValue(new CellAddress(row, column), out var value) ? value : PyNone.Instance;
        internal XElement? GetFormulaXml(int row, int column)
            => _formulaXml.TryGetValue(new CellAddress(row, column), out var formula) ? formula : null;
        internal string GetCellDataType(int row, int column)
        {
            var address = new CellAddress(row, column);
            return _dataTypes.TryGetValue(address, out var dataType)
                ? dataType
                : CellDataType(GetCellValue(row, column));
        }
        internal string GetCellNumberFormat(int row, int column)
            => _numberFormats.TryGetValue(new CellAddress(row, column), out var format) ? format : "General";
        internal object GetCellHyperlink(int row, int column)
            => _hyperlinks.TryGetValue(new CellAddress(row, column), out var target)
                ? new OpenPyxlHyperlink(CellReference(row, column), target)
                : PyNone.Instance;
        internal object GetCellComment(int row, int column)
            => _comments.TryGetValue(new CellAddress(row, column), out var comment)
                ? comment
                : PyNone.Instance;
        internal object GetCellStyle(int row, int column, string name)
            => _cellStyles.TryGetValue((new CellAddress(row, column), name), out var style)
                ? style
                : DefaultCellStyle(name);
        internal OpenPyxlStyleValue? GetAssignedCellStyle(int row, int column, string name)
            => _cellStyles.TryGetValue((new CellAddress(row, column), name), out var style) &&
                style is OpenPyxlStyleValue styleValue
                ? styleValue
                : null;
        internal object GetCellNamedStyle(int row, int column)
            => PyString.FromString(_cellNamedStyles.TryGetValue(new CellAddress(row, column), out var name) ? name : "Normal");
        internal void SetCellNamedStyle(int row, int column, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var address = new CellAddress(row, column);
            OpenPyxlStyleValue? namedStyle = null;
            var name = value is OpenPyxlStyleValue style && style.Kind == OpenPyxlStyleKind.NamedStyle
                ? NamedStyleName(namedStyle = style, null)
                : ExpectString(value, "Cell.style", null);
            namedStyle ??= Workbook?.FindNamedStyle(name);
            if (name == "Normal")
            {
                _cellNamedStyles.Remove(address);
                ApplyNamedStyle(address, namedStyle);
                return;
            }
            _cellNamedStyles[address] = name;
            ApplyNamedStyle(address, namedStyle);
        }
        internal void SetLoadedCellNamedStyle(int row, int column, string? name)
        {
            if (name is not null && name != "Normal")
            {
                _cellNamedStyles[new CellAddress(row, column)] = name;
            }
        }
        private void ApplyNamedStyle(CellAddress address, OpenPyxlStyleValue? style)
        {
            if (style is null)
            {
                return;
            }
            if (NamedStyleNumberFormat(style) is { } numberFormat)
            {
                if (numberFormat == "General")
                {
                    _numberFormats.Remove(address);
                }
                else
                {
                    _numberFormats[address] = numberFormat;
                }
            }
            ApplyNamedStyleComponent(address, style, "font");
            ApplyNamedStyleComponent(address, style, "fill");
            ApplyNamedStyleComponent(address, style, "border");
            ApplyNamedStyleComponent(address, style, "alignment");
            ApplyNamedStyleComponent(address, style, "protection");
        }
        private void ApplyNamedStyleComponent(CellAddress address, OpenPyxlStyleValue style, string name)
        {
            if (NamedStyleComponent(style, name) is { } component)
            {
                _cellStyles[(address, name)] = component;
            }
        }
        internal void SetCellStyle(int row, int column, string name, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var key = (new CellAddress(row, column), name);
            if (value is PyNone)
            {
                _cellStyles.Remove(key);
                return;
            }
            var expected = ExpectedStyleKind(name);
            if (value is not OpenPyxlStyleValue style || style.Kind != expected)
            {
                throw new LythonRuntimeException("TypeError", "Cell." + name + " expects " + OpenPyxlStyleQualifiedName(expected) + ".", null);
            }
            _cellStyles[key] = value;
        }
        internal void SetCellHyperlink(int row, int column, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var address = new CellAddress(row, column);
            if (value is PyNone)
            {
                _hyperlinks.Remove(address);
                return;
            }
            var target = value is OpenPyxlHyperlink hyperlink
                ? hyperlink.Target
                : ExpectString(value, "Cell.hyperlink", null);
            _hyperlinks[address] = target;
        }
        internal void SetCellComment(int row, int column, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var address = new CellAddress(row, column);
            if (value is PyNone)
            {
                _comments.Remove(address);
                return;
            }
            if (value is not OpenPyxlComment comment)
            {
                throw new LythonRuntimeException("TypeError", "Cell.comment expects openpyxl.comments.Comment or None.", null);
            }
            _comments[address] = comment;
        }
        internal bool IsDateCell(int row, int column)
            => IsDateLikeCellValue(GetCellValue(row, column)) || IsDateNumberFormat(GetCellNumberFormat(row, column));
        internal int GetCellStyleId(int row, int column)
        {
            if (_loadedStyleIds.TryGetValue(new CellAddress(row, column), out var loadedStyleId))
            {
                return loadedStyleId;
            }
            var format = GetCellNumberFormat(row, column);
            if (format == "General")
            {
                return 0;
            }
            var workbook = Workbook;
            if (workbook is null)
            {
                return 1;
            }
            var styleMap = OpenPyxlPackage.CreateNumberFormatStyleMap(workbook);
            return styleMap.TryGetValue(format, out var styleId) ? styleId : 0;
        }
        internal void SetCellNumberFormat(int row, int column, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var format = ExpectString(value, "Cell.number_format", null);
            var address = new CellAddress(row, column);
            if (format == "General")
            {
                _numberFormats.Remove(address);
                return;
            }
            _numberFormats[address] = format;
        }
        internal void SetCellValue(int row, int column, object value)
        {
            EnsureCanMutate(null);
            ValidateRowColumn(row, column, null);
            var normalized = NormalizeCellValue(value, null);
            var address = new CellAddress(row, column);
            if (normalized is PyNone)
            {
                if (_cells.Remove(address))
                {
                    InvalidateDimensionsAfterRemoval(address);
                }
                _dataTypes.Remove(address);
                _formulaCachedValues.Remove(address);
                _formulaXml.Remove(address);
                return;
            }
            var added = !_cells.ContainsKey(address);
            _cells[address] = normalized;
            if (added)
            {
                IncludeInDimensions(address);
            }
            _formulaCachedValues.Remove(address);
            _formulaXml.Remove(address);
            if (PyStringOps.TryAsString(normalized, out var text) && IsCellErrorText(text.AsString()))
            {
                _dataTypes[address] = "e";
            }
            else if (IsDateLikeCellValue(normalized))
            {
                _dataTypes[address] = "d";
                _numberFormats.TryAdd(address, DefaultDateNumberFormat(normalized));
            }
            else
            {
                _dataTypes.Remove(address);
            }
        }
        internal void SetLoadedCellValue(int row, int column, object value)
        {
            if (value is not PyNone)
            {
                var address = new CellAddress(row, column);
                var added = !_cells.ContainsKey(address);
                _cells[address] = value;
                if (added)
                {
                    IncludeInDimensions(address);
                }
            }
        }
        internal void SetLoadedFormulaCachedValue(int row, int column, object value)
        {
            if (value is not PyNone)
            {
                _formulaCachedValues[new CellAddress(row, column)] = value;
            }
        }
        internal void SetLoadedFormulaXml(int row, int column, XElement? formula)
        {
            if (formula is not null)
            {
                _formulaXml[new CellAddress(row, column)] = new XElement(formula);
            }
        }
        internal void SetLoadedCellNumberFormat(int row, int column, string format)
        {
            if (format != "General")
            {
                _numberFormats[new CellAddress(row, column)] = format;
            }
        }
        internal void SetLoadedCellStyleId(int row, int column, int? styleId)
        {
            if (styleId is not null && styleId.Value > 0)
            {
                _loadedStyleIds[new CellAddress(row, column)] = styleId.Value;
            }
        }
        internal void SetLoadedCellStyle(int row, int column, string name, OpenPyxlStyleValue? style)
        {
            if (style is not null)
            {
                _cellStyles[(new CellAddress(row, column), name)] = style;
            }
        }
        internal void SetLoadedCellDataType(int row, int column, string? dataType)
        {
            if (dataType == "e")
            {
                _dataTypes[new CellAddress(row, column)] = dataType;
                return;
            }
            if (dataType is "s" or "str" or "inlineStr")
            {
                _dataTypes[new CellAddress(row, column)] = "s";
            }
        }
        internal void SetLoadedHyperlink(CellRangeAddress range, string target)
        {
            for (var row = range.Start.Row; row <= range.End.Row; row++)
            {
                for (var column = range.Start.Column; column <= range.End.Column; column++)
                {
                    _hyperlinks[new CellAddress(row, column)] = target;
                }
            }
        }
        internal void SetLoadedComment(int row, int column, OpenPyxlComment comment)
            => _comments[new CellAddress(row, column)] = comment;
        internal void SetLoadedCommentsSource(string path)
            => _commentsSourcePath = path;
        internal void SetLoadedTable(OpenPyxlTable table)
            => _tables[table.DisplayName] = table;
        internal OpenPyxlWorksheet Copy(string title)
        {
            var copy = new OpenPyxlWorksheet(title);
            foreach (var pair in _cells)
            {
                copy._cells[pair.Key] = pair.Value;
            }
            copy._minRow = _minRow;
            copy._maxRow = _maxRow;
            copy._minColumn = _minColumn;
            copy._maxColumn = _maxColumn;
            copy._dimensionsDirty = _dimensionsDirty;
            foreach (var pair in _dataTypes)
            {
                copy._dataTypes[pair.Key] = pair.Value;
            }
            foreach (var pair in _numberFormats)
            {
                copy._numberFormats[pair.Key] = pair.Value;
            }
            foreach (var pair in _loadedStyleIds)
            {
                copy._loadedStyleIds[pair.Key] = pair.Value;
            }
            foreach (var pair in _hyperlinks)
            {
                copy._hyperlinks[pair.Key] = pair.Value;
            }
            foreach (var pair in _comments)
            {
                copy._comments[pair.Key] = pair.Value.Copy();
            }
            copy._commentsSourcePath = _commentsSourcePath;
            copy._hasLoadedCommentsUpdate = _hasLoadedCommentsUpdate;
            foreach (var pair in _formulaCachedValues)
            {
                copy._formulaCachedValues[pair.Key] = pair.Value;
            }
            foreach (var pair in _formulaXml)
            {
                copy._formulaXml[pair.Key] = new XElement(pair.Value);
            }
            foreach (var pair in _cellStyles)
            {
                copy._cellStyles[pair.Key] = pair.Value;
            }
            foreach (var pair in _cellNamedStyles)
            {
                copy._cellNamedStyles[pair.Key] = pair.Value;
            }
            foreach (var pair in _tables)
            {
                copy._tables[pair.Key] = pair.Value.Copy();
            }
            foreach (var validation in _dataValidations)
            {
                copy._dataValidations.Add(validation.Copy());
            }
            foreach (var formatting in _conditionalFormattings)
            {
                copy._conditionalFormattings.Add(formatting.Copy());
            }
            foreach (var range in _mergedRanges)
            {
                copy._mergedRanges.Add(range);
                copy._mergedRangeSet.Add(range);
            }
            copy._protection.CopyFrom(_protection);
            return copy;
        }
    }
}
