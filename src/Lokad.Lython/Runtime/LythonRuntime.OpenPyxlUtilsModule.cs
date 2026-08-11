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
    private sealed class OpenPyxlWorkbookModule : PyModule
    {
        public static readonly OpenPyxlWorkbookModule Instance = new();

        private OpenPyxlWorkbookModule() : base("openpyxl.workbook")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "Workbook")
            {
                return OpenPyxlModule.Instance.TryGetMember("Workbook", out value);
            }

            value = null;
            return false;
        }
    }

    private sealed class OpenPyxlReaderModule : PyModule
    {
        public static readonly OpenPyxlReaderModule Instance = new();

        private OpenPyxlReaderModule() : base("openpyxl.reader")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "excel" => OpenPyxlReaderExcelModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private sealed class OpenPyxlReaderExcelModule : PyModule
    {
        public static readonly OpenPyxlReaderExcelModule Instance = new();

        private OpenPyxlReaderExcelModule() : base("openpyxl.reader.excel")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "load_workbook")
            {
                return OpenPyxlModule.Instance.TryGetMember("load_workbook", out value);
            }

            value = null;
            return false;
        }
    }

    private sealed class OpenPyxlUtilsModule : PyModule
    {
        public static readonly OpenPyxlUtilsModule Instance = new();

        private OpenPyxlUtilsModule() : base("openpyxl.utils")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "get_column_letter" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlGetColumnLetter, GetColumnLetter),
                "column_index_from_string" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlColumnIndexFromString, ColumnIndexFromString),
                "coordinate_from_string" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlCoordinateFromString, CoordinateFromString),
                "coordinate_to_tuple" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlCoordinateToTuple, CoordinateToTuple),
                "range_boundaries" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlRangeBoundaries, RangeBoundaries),
                "get_column_interval" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlGetColumnInterval, GetColumnInterval),
                "absolute_coordinate" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlAbsoluteCoordinate, AbsoluteCoordinate),
                "quote_sheetname" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlQuoteSheetName, QuoteSheetName),
                "rows_from_range" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlRowsFromRange, RowsFromRange),
                "cols_from_range" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlColsFromRange, ColsFromRange),
                "cell" => OpenPyxlUtilsCellModule.Instance,
                "exceptions" => OpenPyxlUtilsExceptionsModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object GetColumnLetter(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || !PyNumberOps.TryAsInteger(arguments[0], out var index))
            {
                throw new LythonRuntimeException("TypeError", "get_column_letter(col_idx) expects one integer argument.", span);
            }

            if (index < BigInteger.One || index > new BigInteger(MaxUtilityColumn))
            {
                throw new LythonRuntimeException("ValueError", "Invalid column index.", span);
            }

            return PyString.FromString(ColumnName((int)index));
        }

        private static object ColumnIndexFromString(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "column_index_from_string(col) expects one string argument.", span);
            }

            return new BigInteger(ParseColumnName(text.AsString(), MaxUtilityColumn, span));
        }

        private static object CoordinateFromString(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "coordinate_from_string(coord_string)", span);
            if (ParseUtilityReferencePart(reference, allowCell: true, allowColumn: false, allowRow: false, span) is not UtilityCellReference part ||
                part.Row == 0)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({reference})", span);
            }

            return PyTuple.FromOwnedArray(
                [PyString.FromString(ColumnName(part.Column)), new BigInteger(part.Row)],
                context.MemoryGovernor,
                span);
        }

        private static object CoordinateToTuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "coordinate_to_tuple(coordinate)", span);
            if (ParseUtilityReferencePart(reference, allowCell: true, allowColumn: false, allowRow: false, span) is not UtilityCellReference part ||
                part.Row == 0)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({reference})", span);
            }

            return PyTuple.FromOwnedArray(
                [new BigInteger(part.Row), new BigInteger(part.Column)],
                context.MemoryGovernor,
                span);
        }

        private static object RangeBoundaries(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "range_boundaries(range_string)", span);
            var bounds = ParseUtilityRangeBoundaries(reference, span);
            return new PyTuple(
                [
                    bounds.MinColumn is null ? PyNone.Instance : new BigInteger(bounds.MinColumn.Value),
                    bounds.MinRow is null ? PyNone.Instance : new BigInteger(bounds.MinRow.Value),
                    bounds.MaxColumn is null ? PyNone.Instance : new BigInteger(bounds.MaxColumn.Value),
                    bounds.MaxRow is null ? PyNone.Instance : new BigInteger(bounds.MaxRow.Value),
                ],
                context.MemoryGovernor,
                span);
        }

        private static object GetColumnInterval(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2)
            {
                throw new LythonRuntimeException("TypeError", "get_column_interval(start, end) expects two arguments.", span);
            }

            var start = ExpectUtilityColumnIndex(arguments[0], "get_column_interval(start)", span);
            var end = ExpectUtilityColumnIndex(arguments[1], "get_column_interval(end)", span);
            var columns = new List<object>();
            for (var column = start; column <= end; column++)
            {
                context.CheckExecutionBudget(span);
                columns.Add(PyString.FromString(ColumnName(column)));
            }

            return new PyList(columns, context.MemoryGovernor, span);
        }

        private static object AbsoluteCoordinate(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var reference = ExpectSingleStringArgument(arguments, "absolute_coordinate(coord_string)", span);
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(part => part.Length == 0))
            {
                throw new LythonRuntimeException("ValueError", $"{reference} is not a valid coordinate range", span);
            }

            return PyString.FromString(string.Join(":", parts.Select(part => AbsoluteUtilityReference(part, reference, span))));
        }

        private static object QuoteSheetName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var sheetName = ExpectSingleStringArgument(arguments, "quote_sheetname(sheetname)", span);
            return PyString.FromString("'" + sheetName.Replace("'", "''", StringComparison.Ordinal) + "'");
        }

        private static object RowsFromRange(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "rows_from_range(range_string)", span);
            var bounds = ParseBoundedUtilityRange(reference, "rows_from_range(range_string)", span);
            var rows = new List<object>();
            for (var row = bounds.MinRow; row <= bounds.MaxRow; row++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxColumn - bounds.MinColumn + 1];
                for (var column = bounds.MinColumn; column <= bounds.MaxColumn; column++)
                {
                    cells[column - bounds.MinColumn] = PyString.FromString(CellReference(row, column));
                }

                rows.Add(new PyTuple(cells, context.MemoryGovernor, span));
            }

            return new PyList(rows, context.MemoryGovernor, span);
        }

        private static object ColsFromRange(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "cols_from_range(range_string)", span);
            var bounds = ParseBoundedUtilityRange(reference, "cols_from_range(range_string)", span);
            var columns = new List<object>();
            for (var column = bounds.MinColumn; column <= bounds.MaxColumn; column++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxRow - bounds.MinRow + 1];
                for (var row = bounds.MinRow; row <= bounds.MaxRow; row++)
                {
                    cells[row - bounds.MinRow] = PyString.FromString(CellReference(row, column));
                }

                columns.Add(new PyTuple(cells, context.MemoryGovernor, span));
            }

            return new PyList(columns, context.MemoryGovernor, span);
        }

        private const int MaxUtilityColumn = 18278;

        private abstract record UtilityReferencePart
        {
            private protected UtilityReferencePart() { }
        }

        private sealed record UtilityCellReference(int Column, int Row) : UtilityReferencePart;

        private sealed record UtilityColumnReference(int Column) : UtilityReferencePart;

        private sealed record UtilityRowReference(int Row) : UtilityReferencePart;

        private readonly record struct UtilityRangeBoundaries(int? MinColumn, int? MinRow, int? MaxColumn, int? MaxRow);

        private readonly record struct BoundedUtilityRange(int MinColumn, int MinRow, int MaxColumn, int MaxRow);

        private static string ExpectSingleStringArgument(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects one argument.", span);
            }

            return ExpectString(arguments[0], owner, span);
        }

        private static BoundedUtilityRange ParseBoundedUtilityRange(string reference, string owner, LythonSourceSpan span)
        {
            var bounds = ParseUtilityRangeBoundaries(reference, span);
            if (bounds is not
                {
                    MinColumn: { } minColumn,
                    MinRow: { } minRow,
                    MaxColumn: { } maxColumn,
                    MaxRow: { } maxRow,
                })
            {
                throw new LythonRuntimeException("TypeError", owner + " expects a bounded cell range.", span);
            }

            return new BoundedUtilityRange(minColumn, minRow, maxColumn, maxRow);
        }

        private static UtilityRangeBoundaries ParseUtilityRangeBoundaries(string reference, LythonSourceSpan span)
        {
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || parts.Any(part => part.Length == 0))
            {
                throw new LythonRuntimeException("ValueError", $"{reference} is not a valid coordinate or range", span);
            }

            var start = ParseUtilityReferencePart(parts[0], allowCell: true, allowColumn: true, allowRow: true, span);
            var end = parts.Length == 1
                ? start
                : ParseUtilityReferencePart(parts[1], allowCell: true, allowColumn: true, allowRow: true, span);
            return (start, end) switch
            {
                (UtilityCellReference startCell, UtilityCellReference endCell) => new UtilityRangeBoundaries(
                    Math.Min(startCell.Column, endCell.Column),
                    Math.Min(startCell.Row, endCell.Row),
                    Math.Max(startCell.Column, endCell.Column),
                    Math.Max(startCell.Row, endCell.Row)),
                (UtilityColumnReference startColumn, UtilityColumnReference endColumn) => new UtilityRangeBoundaries(
                    Math.Min(startColumn.Column, endColumn.Column),
                    null,
                    Math.Max(startColumn.Column, endColumn.Column),
                    null),
                (UtilityRowReference startRow, UtilityRowReference endRow) => new UtilityRangeBoundaries(
                    null,
                    Math.Min(startRow.Row, endRow.Row),
                    null,
                    Math.Max(startRow.Row, endRow.Row)),
                _ => throw new LythonRuntimeException("ValueError", $"{reference} is not a valid coordinate or range", span),
            };
        }

        private static string AbsoluteUtilityReference(string part, string original, LythonSourceSpan span)
        {
            var reference = ParseUtilityReferencePart(part, allowCell: true, allowColumn: true, allowRow: true, span);
            return reference switch
            {
                UtilityCellReference cell => "$" + ColumnName(cell.Column) + "$" + cell.Row.ToString(CultureInfo.InvariantCulture),
                UtilityColumnReference column => "$" + ColumnName(column.Column),
                UtilityRowReference row => "$" + row.Row.ToString(CultureInfo.InvariantCulture),
                _ => throw new LythonRuntimeException("ValueError", $"{original} is not a valid coordinate range", span),
            };
        }

        private static UtilityReferencePart ParseUtilityReferencePart(
            string raw,
            bool allowCell,
            bool allowColumn,
            bool allowRow,
            LythonSourceSpan span)
        {
            var text = raw.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            var letters = 0;
            while (letters < text.Length && char.IsLetter(text[letters]))
            {
                letters++;
            }

            var digits = letters;
            while (digits < text.Length && char.IsDigit(text[digits]))
            {
                digits++;
            }

            if (text.Length == 0 || digits != text.Length)
            {
                throw new LythonRuntimeException("ValueError", $"{raw} is not a valid coordinate or range", span);
            }

            if (letters == text.Length)
            {
                if (!allowColumn)
                {
                    throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({raw})", span);
                }

                return new UtilityColumnReference(ParseColumnName(text, MaxUtilityColumn, span));
            }

            if (letters == 0)
            {
                if (!allowRow || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                {
                    throw new LythonRuntimeException("ValueError", $"{raw} is not a valid coordinate or range", span);
                }

                return new UtilityRowReference(row);
            }

            if (!allowCell || !int.TryParse(text[letters..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cellRow))
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({raw})", span);
            }

            return new UtilityCellReference(ParseColumnName(text[..letters], MaxUtilityColumn, span), cellRow);
        }

        private static int ExpectUtilityColumnIndex(object value, string owner, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out var text))
            {
                return ParseColumnName(text.AsString(), MaxUtilityColumn, span);
            }

            if (!PyNumberOps.TryAsInteger(value, out var integer) ||
                integer < BigInteger.One ||
                integer > new BigInteger(MaxUtilityColumn))
            {
                throw new LythonRuntimeException("ValueError", owner + " expects a column index or name.", span);
            }

            return (int)integer;
        }

    }

    private sealed class OpenPyxlUtilsCellModule : PyModule
    {
        public static readonly OpenPyxlUtilsCellModule Instance = new();

        private OpenPyxlUtilsCellModule() : base("openpyxl.utils.cell")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is
                "get_column_letter" or
                "column_index_from_string" or
                "coordinate_from_string" or
                "coordinate_to_tuple" or
                "range_boundaries" or
                "get_column_interval" or
                "absolute_coordinate" or
                "quote_sheetname" or
                "rows_from_range" or
                "cols_from_range")
            {
                return OpenPyxlUtilsModule.Instance.TryGetMember(name, out value);
            }

            value = null;
            return false;
        }
    }

    private sealed class OpenPyxlUtilsExceptionsModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlUtilsExceptionsModule Instance = new();

        private OpenPyxlUtilsExceptionsModule()
            : base("openpyxl.utils.exceptions", new Dictionary<string, object>
            {
                ["CellCoordinatesException"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "CellCoordinatesException")),
                ["IllegalCharacterError"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "IllegalCharacterError")),
                ["InvalidFileException"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "InvalidFileException")),
                ["NamedRangeException"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "NamedRangeException")),
                ["ReadOnlyWorkbookException"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "ReadOnlyWorkbookException")),
                ["SheetTitleException"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "SheetTitleException")),
                ["WorkbookAlreadySaved"] = new ExceptionTypeValue(ModuleException("openpyxl.utils.exceptions", "WorkbookAlreadySaved")),
            })
        {
        }
    }

}
