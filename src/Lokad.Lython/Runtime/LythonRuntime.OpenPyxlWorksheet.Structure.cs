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
        private object InsertRows(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.insert_rows(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.insert_rows", "amount", span);
            ValidateRowColumn(checked(index + amount - 1), 1, span);
            MarkStructuralMutation();
            RewriteCells(address => address.Row >= index
                ? new CellAddress(address.Row + amount, address.Column)
                : address);
            RewriteAutoFilterRange(range => InsertRowsInRange(range, index, amount));
            RewriteDataValidationRanges(range => InsertRowsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => InsertRowsInRange(range, index, amount));
            RewriteTableRanges(range => InsertRowsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object DeleteRows(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.delete_rows(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.delete_rows", "amount", span);
            var end = checked(index + amount - 1);
            MarkStructuralMutation();
            RewriteCells(address =>
                address.Row < index
                    ? address
                    : address.Row > end
                        ? new CellAddress(address.Row - amount, address.Column)
                        : null);
            RewriteAutoFilterRange(range => DeleteRowsInRange(range, index, amount));
            RewriteDataValidationRanges(range => DeleteRowsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => DeleteRowsInRange(range, index, amount));
            RewriteTableRanges(range => DeleteRowsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object InsertCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.insert_cols(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.insert_cols", "amount", span);
            ValidateRowColumn(1, checked(index + amount - 1), span);
            MarkStructuralMutation();
            RewriteCells(address => address.Column >= index
                ? new CellAddress(address.Row, address.Column + amount)
                : address);
            RewriteAutoFilterRange(range => InsertColumnsInRange(range, index, amount));
            RewriteDataValidationRanges(range => InsertColumnsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => InsertColumnsInRange(range, index, amount));
            RewriteTableRanges(range => InsertColumnsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object DeleteCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.delete_cols(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.delete_cols", "amount", span);
            var end = checked(index + amount - 1);
            MarkStructuralMutation();
            RewriteCells(address =>
                address.Column < index
                    ? address
                    : address.Column > end
                        ? new CellAddress(address.Row, address.Column - amount)
                        : null);
            RewriteAutoFilterRange(range => DeleteColumnsInRange(range, index, amount));
            RewriteDataValidationRanges(range => DeleteColumnsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => DeleteColumnsInRange(range, index, amount));
            RewriteTableRanges(range => DeleteColumnsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object MoveRange(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var range = ParseCellRange(ExpectString(arguments[0], "Worksheet.move_range(cell_range)", span), span);
            var rowOffset = OptionalInt(arguments, 1, 0, "Worksheet.move_range", "rows", span);
            var columnOffset = OptionalInt(arguments, 2, 0, "Worksheet.move_range", "cols", span);
            var translate = OptionalBool(arguments, 3, false, "Worksheet.move_range", "translate", span);
            if (translate)
            {
                throw new LythonRuntimeException("NotImplementedError", "Worksheet.move_range(..., translate=True) is not supported by Lython.", span);
            }
            ValidateRowColumn(range.Start.Row + rowOffset, range.Start.Column + columnOffset, span);
            ValidateRowColumn(range.End.Row + rowOffset, range.End.Column + columnOffset, span);
            if (rowOffset != 0 || columnOffset != 0)
            {
                MarkStructuralMutation();
            }
            MoveCellAddresses(range, rowOffset, columnOffset);
            RewriteAutoFilterRange(filterRange => Contains(range, filterRange)
                ? ShiftRange(filterRange, rowOffset, columnOffset)
                : filterRange);
            RewriteDataValidationRanges(validationRange => Contains(range, validationRange)
                ? ShiftRange(validationRange, rowOffset, columnOffset)
                : validationRange);
            RewriteConditionalFormattingRanges(formattingRange => Contains(range, formattingRange)
                ? ShiftRange(formattingRange, rowOffset, columnOffset)
                : formattingRange);
            RewriteTableRanges(tableRange => Contains(range, tableRange)
                ? ShiftRange(tableRange, rowOffset, columnOffset)
                : tableRange);
            return PyNone.Instance;
        }
        private void MarkStructuralMutation()
        {
            _hasStructuralMutation = true;
            if (_commentsSourcePath is not null)
            {
                _hasLoadedCommentsUpdate = true;
            }
        }
        private void RewriteCells(Func<CellAddress, CellAddress?> rewrite)
        {
            RewriteCellAddressedMap(_cells, rewrite);
            RewriteCellAddressedMap(_dataTypes, rewrite);
            RewriteCellAddressedMap(_numberFormats, rewrite);
            RewriteCellAddressedMap(_loadedStyleIds, rewrite);
            RewriteCellAddressedMap(_hyperlinks, rewrite);
            RewriteCellAddressedMap(_comments, rewrite);
            RewriteCellAddressedMap(_formulaCachedValues, rewrite);
            RewriteCellAddressedMap(_formulaXml, rewrite);
            RewriteCellAddressedMap(_cellNamedStyles, rewrite);
            RewriteCellStyleMap(_cellStyles, rewrite);
            RewriteCellObjectMap(_cellObjects, rewrite);
            _dimensionsDirty = true;
        }
        private void MoveCellAddresses(CellRangeAddress range, int rowOffset, int columnOffset)
        {
            MoveRangeEntries(_cells, range, rowOffset, columnOffset);
            MoveRangeEntries(_dataTypes, range, rowOffset, columnOffset);
            MoveRangeEntries(_numberFormats, range, rowOffset, columnOffset);
            MoveRangeEntries(_loadedStyleIds, range, rowOffset, columnOffset);
            MoveRangeEntries(_hyperlinks, range, rowOffset, columnOffset);
            MoveRangeEntries(_comments, range, rowOffset, columnOffset);
            MoveRangeEntries(_formulaCachedValues, range, rowOffset, columnOffset);
            MoveRangeEntries(_formulaXml, range, rowOffset, columnOffset);
            MoveRangeEntries(_cellNamedStyles, range, rowOffset, columnOffset);
            MoveRangeStyleEntries(_cellStyles, range, rowOffset, columnOffset);
            var movingCellObjects = _cellObjects
                .Where(pair => Contains(range, pair.Key))
                .ToArray();
            foreach (var pair in movingCellObjects)
            {
                _cellObjects.Remove(pair.Key);
            }
            foreach (var pair in movingCellObjects)
            {
                var target = new CellAddress(
                    pair.Key.Row + rowOffset,
                    pair.Key.Column + columnOffset);
                pair.Value.MoveTo(target);
                _cellObjects[target] = pair.Value;
            }
            _dimensionsDirty = true;
        }
        private void RewriteDataValidationRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            foreach (var validation in _dataValidations)
            {
                validation.RewriteRanges(rewrite);
            }
        }
        private void RewriteConditionalFormattingRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            foreach (var formatting in _conditionalFormattings)
            {
                formatting.RewriteRanges(rewrite);
            }
        }
        private void RewriteAutoFilterRange(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            if (_autoFilterRef is null)
            {
                return;
            }
            var target = rewrite(ParseCellOrRange(_autoFilterRef, null));
            _autoFilterRef = target?.CellOrRangeReference;
        }
        private void RewriteTableRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            var removed = new List<string>();
            foreach (var pair in _tables)
            {
                if (!pair.Value.RewriteReference(rewrite))
                {
                    removed.Add(pair.Key);
                }
            }
            foreach (var name in removed)
            {
                _tables.Remove(name);
            }
        }
        private static void RewriteCellAddressedMap<T>(Dictionary<CellAddress, T> map, Func<CellAddress, CellAddress?> rewrite)
        {
            if (map.Count == 0)
            {
                return;
            }
            var rewritten = new Dictionary<CellAddress, T>();
            foreach (var pair in map)
            {
                var target = rewrite(pair.Key);
                if (target is not null)
                {
                    rewritten[target.Value] = pair.Value;
                }
            }
            map.Clear();
            foreach (var pair in rewritten)
            {
                map[pair.Key] = pair.Value;
            }
        }
        private static void RewriteCellStyleMap(
            Dictionary<(CellAddress Address, OpenPyxlCellStyleComponent Component), object> map,
            Func<CellAddress, CellAddress?> rewrite)
        {
            if (map.Count == 0)
            {
                return;
            }
            var rewritten = new Dictionary<(CellAddress Address, OpenPyxlCellStyleComponent Component), object>();
            foreach (var pair in map)
            {
                var target = rewrite(pair.Key.Address);
                if (target is not null)
                {
                    rewritten[(target.Value, pair.Key.Component)] = pair.Value;
                }
            }
            map.Clear();
            foreach (var pair in rewritten)
            {
                map[pair.Key] = pair.Value;
            }
        }
        private static void RewriteCellObjectMap(
            Dictionary<CellAddress, OpenPyxlCell> map,
            Func<CellAddress, CellAddress?> rewrite)
        {
            if (map.Count == 0)
            {
                return;
            }
            var rewritten = new Dictionary<CellAddress, OpenPyxlCell>();
            foreach (var pair in map)
            {
                var target = rewrite(pair.Key);
                if (target is not null)
                {
                    pair.Value.MoveTo(target.Value);
                    rewritten[target.Value] = pair.Value;
                }
            }
            map.Clear();
            foreach (var pair in rewritten)
            {
                map[pair.Key] = pair.Value;
            }
        }
        private static void MoveRangeEntries<T>(Dictionary<CellAddress, T> map, CellRangeAddress range, int rowOffset, int columnOffset)
        {
            var moving = map
                .Where(pair => Contains(range, pair.Key))
                .ToArray();
            foreach (var pair in moving)
            {
                map.Remove(pair.Key);
            }
            foreach (var pair in moving)
            {
                var target = new CellAddress(pair.Key.Row + rowOffset, pair.Key.Column + columnOffset);
                map[target] = pair.Value;
            }
        }
        private static void MoveRangeStyleEntries(
            Dictionary<(CellAddress Address, OpenPyxlCellStyleComponent Component), object> map,
            CellRangeAddress range,
            int rowOffset,
            int columnOffset)
        {
            var moving = map
                .Where(pair => Contains(range, pair.Key.Address))
                .ToArray();
            foreach (var pair in moving)
            {
                map.Remove(pair.Key);
            }
            foreach (var pair in moving)
            {
                var target = new CellAddress(
                    pair.Key.Address.Row + rowOffset,
                    pair.Key.Address.Column + columnOffset);
                map[(target, pair.Key.Component)] = pair.Value;
            }
        }
        private static bool Contains(CellRangeAddress range, CellAddress address)
            => address.Row >= range.Start.Row &&
               address.Row <= range.End.Row &&
               address.Column >= range.Start.Column &&
               address.Column <= range.End.Column;
        private static bool Contains(CellRangeAddress outer, CellRangeAddress inner)
            => Contains(outer, inner.Start) && Contains(outer, inner.End);
        private static CellRangeAddress ShiftRange(CellRangeAddress range, int rowOffset, int columnOffset)
            => new(
                new CellAddress(range.Start.Row + rowOffset, range.Start.Column + columnOffset),
                new CellAddress(range.End.Row + rowOffset, range.End.Column + columnOffset));
        private static CellRangeAddress? InsertRowsInRange(CellRangeAddress range, int index, int amount)
        {
            if (range.End.Row < index)
            {
                return range;
            }
            if (range.Start.Row >= index)
            {
                return ShiftRange(range, amount, 0);
            }
            return new CellRangeAddress(range.Start, new CellAddress(range.End.Row + amount, range.End.Column));
        }
        private static CellRangeAddress? DeleteRowsInRange(CellRangeAddress range, int index, int amount)
        {
            var end = index + amount - 1;
            if (range.End.Row < index)
            {
                return range;
            }
            if (range.Start.Row > end)
            {
                return ShiftRange(range, -amount, 0);
            }
            if (range.Start.Row >= index && range.End.Row <= end)
            {
                return null;
            }
            if (range.Start.Row < index && range.End.Row > end)
            {
                return new CellRangeAddress(range.Start, new CellAddress(range.End.Row - amount, range.End.Column));
            }
            if (range.Start.Row < index)
            {
                return new CellRangeAddress(range.Start, new CellAddress(index - 1, range.End.Column));
            }
            return new CellRangeAddress(
                new CellAddress(index, range.Start.Column),
                new CellAddress(range.End.Row - amount, range.End.Column));
        }
        private static CellRangeAddress? InsertColumnsInRange(CellRangeAddress range, int index, int amount)
        {
            if (range.End.Column < index)
            {
                return range;
            }
            if (range.Start.Column >= index)
            {
                return ShiftRange(range, 0, amount);
            }
            return new CellRangeAddress(range.Start, new CellAddress(range.End.Row, range.End.Column + amount));
        }
        private static CellRangeAddress? DeleteColumnsInRange(CellRangeAddress range, int index, int amount)
        {
            var end = index + amount - 1;
            if (range.End.Column < index)
            {
                return range;
            }
            if (range.Start.Column > end)
            {
                return ShiftRange(range, 0, -amount);
            }
            if (range.Start.Column >= index && range.End.Column <= end)
            {
                return null;
            }
            if (range.Start.Column < index && range.End.Column > end)
            {
                return new CellRangeAddress(range.Start, new CellAddress(range.End.Row, range.End.Column - amount));
            }
            if (range.Start.Column < index)
            {
                return new CellRangeAddress(range.Start, new CellAddress(range.End.Row, index - 1));
            }
            return new CellRangeAddress(
                new CellAddress(range.Start.Row, index),
                new CellAddress(range.End.Row, range.End.Column - amount));
        }
    }
}
