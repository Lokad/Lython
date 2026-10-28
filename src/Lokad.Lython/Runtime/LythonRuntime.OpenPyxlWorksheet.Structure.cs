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
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.insert_rows(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.insert_rows", "amount", span);
            ValidateCoordinate((long)index + amount - 1, 1, span);
            RewriteCells(address => address.Row >= index
                ? new CellAddress(address.Row + amount, address.Column)
                : address, context, span);
            RewriteAutoFilterRange(range => InsertRowsInRange(range, index, amount));
            RewriteDataValidationRanges(range => InsertRowsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => InsertRowsInRange(range, index, amount));
            RewriteTableRanges(range => InsertRowsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object DeleteRows(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.delete_rows(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.delete_rows", "amount", span);
            var end = (long)index + amount - 1;
            RewriteCells(address =>
                address.Row < index
                    ? address
                    : address.Row > end
                        ? new CellAddress(address.Row - amount, address.Column)
                        : null, context, span);
            RewriteAutoFilterRange(range => DeleteRowsInRange(range, index, amount));
            RewriteDataValidationRanges(range => DeleteRowsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => DeleteRowsInRange(range, index, amount));
            RewriteTableRanges(range => DeleteRowsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object InsertCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.insert_cols(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.insert_cols", "amount", span);
            ValidateCoordinate(1, (long)index + amount - 1, span);
            RewriteCells(address => address.Column >= index
                ? new CellAddress(address.Row, address.Column + amount)
                : address, context, span);
            RewriteAutoFilterRange(range => InsertColumnsInRange(range, index, amount));
            RewriteDataValidationRanges(range => InsertColumnsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => InsertColumnsInRange(range, index, amount));
            RewriteTableRanges(range => InsertColumnsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object DeleteCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureCanMutate(span);
            var index = ExpectPositiveInt(arguments[0], "Worksheet.delete_cols(idx)", span);
            var amount = OptionalPositiveInt(arguments, 1, 1, "Worksheet.delete_cols", "amount", span);
            var end = (long)index + amount - 1;
            RewriteCells(address =>
                address.Column < index
                    ? address
                    : address.Column > end
                        ? new CellAddress(address.Row, address.Column - amount)
                        : null, context, span);
            RewriteAutoFilterRange(range => DeleteColumnsInRange(range, index, amount));
            RewriteDataValidationRanges(range => DeleteColumnsInRange(range, index, amount));
            RewriteConditionalFormattingRanges(range => DeleteColumnsInRange(range, index, amount));
            RewriteTableRanges(range => DeleteColumnsInRange(range, index, amount));
            return PyNone.Instance;
        }
        private object MoveRange(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            EnsureCanMutate(span);
            var range = ParseCellOrRange(ExpectString(arguments[0], "Worksheet.move_range(cell_range)", span), span);
            var rowOffset = OptionalInt(arguments, 1, 0, "Worksheet.move_range", "rows", span);
            var columnOffset = OptionalInt(arguments, 2, 0, "Worksheet.move_range", "cols", span);
            var translate = OptionalBool(arguments, 3, false, "Worksheet.move_range", "translate", span);
            if (translate)
            {
                throw new LythonRuntimeException("NotImplementedError", "Worksheet.move_range(..., translate=True) is not supported by Lython.", span);
            }
            ValidateCoordinate((long)range.Start.Row + rowOffset, (long)range.Start.Column + columnOffset, span);
            ValidateCoordinate((long)range.End.Row + rowOffset, (long)range.End.Column + columnOffset, span);
            MoveCellAddresses(range, rowOffset, columnOffset, context, span);
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
        private static void ValidateCoordinate(long row, long column, LythonSourceSpan span)
        {
            if (row is < 1 or > 1048576 || column is < 1 or > MaxWorksheetColumn)
                throw new LythonRuntimeException("ValueError", "Row or column is outside Excel worksheet bounds.", span);
        }
        private void RewriteCells(Func<CellAddress, CellAddress?> rewrite, ExecutionContext context, LythonSourceSpan span)
        {
            using var storage = context.MemoryGovernor.ReserveTemporary(128L + 128L * _cells.Count, span);
            context.CheckExecution(span);
            var rewritten = new Dictionary<CellAddress, OpenPyxlCell>(_cells.Count);
            foreach (var pair in _cells)
            {
                context.CheckExecution(span);
                if (rewrite(pair.Key) is { } target)
                {
                    ValidateCoordinate(target.Row, target.Column, span);
                    rewritten.Add(target, pair.Value);
                }
            }
            PublishCells(rewritten, context, span);
            _appendRow = _cells.Count == 0 ? 0 : MaxRow;
        }
        private void MoveCellAddresses(CellRangeAddress range, int rowOffset, int columnOffset, ExecutionContext context, LythonSourceSpan span)
        {
            using var storage = context.MemoryGovernor.ReserveTemporary(128L + 128L * _cells.Count, span);
            context.CheckExecution(span);
            var rewritten = new Dictionary<CellAddress, OpenPyxlCell>(_cells.Count);
            foreach (var pair in _cells)
            {
                context.CheckExecution(span);
                if (!Contains(range, pair.Key)) rewritten.Add(pair.Key, pair.Value);
            }
            foreach (var pair in _cells)
            {
                context.CheckExecution(span);
                if (Contains(range, pair.Key)) rewritten[new CellAddress(pair.Key.Row + rowOffset, pair.Key.Column + columnOffset)] = pair.Value;
            }
            PublishCells(rewritten, context, span);
        }
        private void PublishCells(Dictionary<CellAddress, OpenPyxlCell> cells, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            _cells = cells;
            MarkStructuralMutation();
            foreach (var pair in cells)
            {
                context.CheckExecution(span);
                pair.Value.MoveTo(pair.Key);
            }
            _dimensionsDirty = true;
        }
        private void RewriteDataValidationRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            foreach (var validation in _dataValidations)
            {
                _context?.CheckExecution(_allocationSpan);
                validation.RewriteRanges(rewrite);
            }
        }
        private void RewriteConditionalFormattingRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            foreach (var formatting in _conditionalFormattings)
            {
                _context?.CheckExecution(_allocationSpan);
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
                _context?.CheckExecution(_allocationSpan);
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
            var end = (long)index + amount - 1;
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
            var end = (long)index + amount - 1;
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
