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
    internal readonly struct OpenPyxlSaveGuard
    {
        private readonly string? _unsafeReason;

        private OpenPyxlSaveGuard(string unsafeReason)
        {
            _unsafeReason = unsafeReason;
        }

        public static OpenPyxlSaveGuard Safe => default;

        public static OpenPyxlSaveGuard Unsafe(string reason) => new(reason);

        public bool TryGetUnsafeReason([MaybeNullWhen(false)] out string reason)
        {
            reason = _unsafeReason;
            return reason is not null;
        }
    }

    internal readonly record struct OpenPyxlPackageSnapshot(IReadOnlyDictionary<string, byte[]> Parts);

    internal readonly record struct CellAddress(int Row, int Column);

    internal readonly record struct CellRangeAddress(CellAddress Start, CellAddress End)
    {
        public string Reference => CellReference(Start.Row, Start.Column) + ":" + CellReference(End.Row, End.Column);

        public string CellOrRangeReference => Start.Equals(End) ? CellReference(Start.Row, Start.Column) : Reference;
    }

    private readonly record struct PrintTitleParts(string? Rows, string? Columns);

    private static string WorkbookContentType(OpenPyxlWorkbook workbook)
    {
        if (workbook.HasVbaProject)
        {
            return workbook.Template
                ? "application/vnd.ms-excel.template.macroEnabled.main+xml"
                : "application/vnd.ms-excel.sheet.macroEnabled.main+xml";
        }

        return workbook.Template
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    }

    private static string WorkbookBaseDateText(bool date1904)
        => date1904 ? "1904-01-01 00:00:00" : "1899-12-30 00:00:00";

    private static bool IsFormulaValue(object value)
        => PyStringOps.TryAsString(value, out var text) && IsFormulaText(text.AsString());

    private static bool IsFormulaText(string text)
        => text.StartsWith("=", StringComparison.Ordinal);

    private static bool IsCellErrorText(string text)
        => text is "#NULL!" or "#DIV/0!" or "#VALUE!" or "#REF!" or "#NAME?" or "#NUM!" or "#N/A" or "#GETTING_DATA";

    private static bool IsDateLikeCellValue(object value)
        => value is PyDate or PyDateTime or PyTime or PyTimedelta;

    private static string DefaultDateNumberFormat(object value)
        => value switch
        {
            PyDate => "yyyy-mm-dd",
            PyDateTime => "yyyy-mm-dd h:mm:ss",
            PyTime => "h:mm:ss",
            PyTimedelta => "[hh]:mm:ss",
            _ => "General",
        };

    private static double ExcelSerialFromDate(PyDate date, bool date1904)
        => ExcelSerialFromDateTime(date.Value.ToDateTime(TimeOnly.MinValue), date1904);

    private static double ExcelSerialFromDateTime(PyDateTime dateTime, bool date1904)
        => ExcelSerialFromDateTime(dateTime.Value, date1904);

    private static double ExcelSerialFromDateTime(DateTime dateTime, bool date1904)
        => date1904
            ? (dateTime - new DateTime(1904, 1, 1)).TotalDays
            : dateTime.ToOADate();

    private static double ExcelSerialFromTime(PyTime time)
        => time.Value.ToTimeSpan().TotalDays;

    private static double ExcelSerialFromTimedelta(PyTimedelta delta)
        => delta.TotalSeconds() / 86_400.0;

    private static object DateValueFromExcelSerial(double serial, string numberFormat, bool date1904)
    {
        var normalizedFormat = NormalizeNumberFormatForDetection(numberFormat);
        if (ContainsElapsedTimeToken(normalizedFormat))
        {
            return new PyTimedelta(TimeSpan.FromDays(serial));
        }

        var hasDate = normalizedFormat.Contains('y') || normalizedFormat.Contains('d');
        var hasTime = normalizedFormat.Contains('h') || normalizedFormat.Contains('s');
        var dateTime = date1904
            ? new DateTime(1904, 1, 1).AddDays(serial)
            : DateTime.FromOADate(serial);
        if (hasDate && hasTime)
        {
            return new PyDateTime(dateTime);
        }

        if (hasDate)
        {
            return new PyDate(DateOnly.FromDateTime(dateTime));
        }

        if (hasTime)
        {
            var fraction = serial - Math.Floor(serial);
            if (fraction < 0)
            {
                fraction += 1;
            }

            return new PyTime(TimeOnly.FromTimeSpan(TimeSpan.FromDays(fraction)));
        }

        return serial;
    }

    private static bool IsDateNumberFormat(string numberFormat)
    {
        var normalizedFormat = NormalizeNumberFormatForDetection(numberFormat);
        return normalizedFormat.Contains('y') ||
               normalizedFormat.Contains('d') ||
               normalizedFormat.Contains('h') ||
               normalizedFormat.Contains('s') ||
               ContainsElapsedTimeToken(normalizedFormat);
    }

    private static bool ContainsElapsedTimeToken(string normalizedFormat)
        => normalizedFormat.Contains("[h", StringComparison.Ordinal) ||
           normalizedFormat.Contains("[m", StringComparison.Ordinal) ||
           normalizedFormat.Contains("[s", StringComparison.Ordinal);

    private static string NormalizeNumberFormatForDetection(string numberFormat)
    {
        var builder = new StringBuilder(numberFormat.Length);
        var inQuote = false;
        for (var index = 0; index < numberFormat.Length; index++)
        {
            var ch = numberFormat[index];
            if (ch == '"')
            {
                inQuote = !inQuote;
                continue;
            }

            if (inQuote)
            {
                continue;
            }

            if (ch == '\\' || ch == '_')
            {
                index++;
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static string NormalizeWorkbookPath(object value, ExecutionContext context, LythonSourceSpan span)
    {
        var rawPath = value switch
        {
            PyPath path => path.Value.AsString(),
            _ when PyStringOps.TryAsString(value, out var text) => text.AsString(),
            _ => throw new LythonRuntimeException("TypeError", "openpyxl workbook filename must be a string or pathlib.Path.", span)
        };

        return PathOps.Normalize(rawPath, context.Host.Cwd);
    }

    private static bool OptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan? span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return defaultValue;
        }

        return arguments[index] is bool value
            ? value
            : throw new LythonRuntimeException("TypeError", $"{owner} expects {parameterName} to be a bool.", span);
    }

    private static int OptionalPositiveInt(object[] arguments, int index, int defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return defaultValue;
        }

        if (!PyNumberOps.TryAsInteger(arguments[index], out var integer) || integer < BigInteger.One || integer > new BigInteger(int.MaxValue))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects {parameterName} to be a positive integer.", span);
        }

        return (int)integer;
    }

    private static int OptionalInt(object[] arguments, int index, int defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return defaultValue;
        }

        if (!PyNumberOps.TryAsInteger(arguments[index], out var integer) ||
            integer < new BigInteger(int.MinValue) ||
            integer > new BigInteger(int.MaxValue))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects {parameterName} to be an integer.", span);
        }

        return (int)integer;
    }

    private static int ExpectPositiveInt(object value, string owner, LythonSourceSpan? span)
    {
        if (!PyNumberOps.TryAsInteger(value, out var integer) || integer < BigInteger.One || integer > new BigInteger(int.MaxValue))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a positive integer.", span);
        }

        return (int)integer;
    }

    private static int NormalizePositiveInt(BigInteger integer, string owner, LythonSourceSpan span)
    {
        if (integer < BigInteger.One || integer > new BigInteger(int.MaxValue))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a positive integer.", span);
        }

        return (int)integer;
    }

    private static int ExpectNonNegativeInt(object value, string owner, LythonSourceSpan? span)
    {
        if (!PyNumberOps.TryAsInteger(value, out var integer) || integer < BigInteger.Zero || integer > new BigInteger(int.MaxValue))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a non-negative integer.", span);
        }

        return (int)integer;
    }

    private static int? NormalizeOptionalPositiveInt(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        return ExpectPositiveInt(value, owner, span);
    }

    private static int? NormalizeOptionalNonNegativeInt(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        return ExpectNonNegativeInt(value, owner, span);
    }

    private static double? NormalizeOptionalNonNegativeDouble(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        var number = value switch
        {
            double floating => floating,
            BigInteger integer => (double)integer,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects a non-negative number or None.", span)
        };

        if (!double.IsFinite(number) || number < 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a non-negative finite number.", span);
        }

        return number;
    }

    private static bool ExpectBool(object value, string owner, LythonSourceSpan? span)
        => value is bool boolean
            ? boolean
            : throw new LythonRuntimeException("TypeError", $"{owner} expects a bool.", span);

    private static string NormalizePageOrientation(object value, string owner, LythonSourceSpan? span)
    {
        var orientation = ExpectString(value, owner, span).Trim().ToLowerInvariant();
        return orientation switch
        {
            "portrait" or "landscape" => orientation,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} expects 'portrait' or 'landscape'.", span)
        };
    }

    private static string? NormalizeOptionalPageOrientation(object value, string owner, LythonSourceSpan? span)
        => value is PyNone ? null : NormalizePageOrientation(value, owner, span);

    private static string ExpectString(object value, string owner, LythonSourceSpan? span)
    {
        return PyStringOps.TryAsString(value, out var text)
            ? text.AsString()
            : throw new LythonRuntimeException("TypeError", $"{owner} expects a string.", span);
    }

    private static object NormalizeCellValue(object value, LythonSourceSpan? span)
    {
        if (value is PyNone or bool or BigInteger or double or PyDecimal or PyDate or PyDateTime or PyTime or PyTimedelta or PyString)
        {
            return value;
        }

        throw new LythonRuntimeException("TypeError", "openpyxl cell values must be None, str, int, float, Decimal, bool, date, time, datetime, or timedelta.", span);
    }

    private static void ValidateRowColumn(int row, int column, LythonSourceSpan? span)
    {
        if (row is < 1 or > 1048576 || column is < 1 or > 16384)
        {
            throw new LythonRuntimeException("ValueError", "Row or column is outside Excel worksheet bounds.", span);
        }
    }

    private static void ValidateSheetTitle(string title, LythonSourceSpan? span)
    {
        if (title.Length == 0 || title.Length > 31 || title.IndexOfAny(['[', ']', ':', '*', '?', '/', '\\']) >= 0)
        {
            throw new LythonRuntimeException("ValueError", "Invalid worksheet title.", span);
        }
    }

    private static string ValidateDetachedSheetTitle(string title)
    {
        ValidateSheetTitle(title, null);
        return title;
    }

    private static CellAddress ParseCellAddress(string reference, LythonSourceSpan? span)
    {
        var text = reference.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var index = 0;
        while (index < text.Length && char.IsLetter(text[index]))
        {
            index++;
        }

        if (index == 0 || index == text.Length)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid cell coordinate: {reference}", span);
        }

        var column = ParseColumnName(text[..index], span);
        if (!int.TryParse(text[index..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
        {
            throw new LythonRuntimeException("ValueError", $"Invalid cell coordinate: {reference}", span);
        }

        ValidateRowColumn(row, column, span);
        return new CellAddress(row, column);
    }

    private static CellRangeAddress ParseRange(string reference, LythonSourceSpan? span)
    {
        var parts = reference.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid cell range: {reference}", span);
        }

        var start = ParseCellAddress(parts[0], span);
        var end = ParseCellAddress(parts[1], span);
        return new CellRangeAddress(
            new CellAddress(Math.Min(start.Row, end.Row), Math.Min(start.Column, end.Column)),
            new CellAddress(Math.Max(start.Row, end.Row), Math.Max(start.Column, end.Column)));
    }

    private static CellRangeAddress ParseCellRange(string reference, LythonSourceSpan? span)
    {
        var (start, end) = ParseRange(reference, span);
        return new CellRangeAddress(start, end);
    }

    private static CellRangeAddress ParseCellOrRange(string reference, LythonSourceSpan? span)
    {
        if (reference.Contains(':', StringComparison.Ordinal))
        {
            return ParseCellRange(reference, span);
        }

        var address = ParseCellAddress(reference, span);
        return new CellRangeAddress(address, address);
    }

    private static string? NormalizeOptionalPrintArea(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        if (PyStringOps.TryAsString(value, out var text))
        {
            return NormalizePrintAreaText(text.AsString(), owner, span);
        }

        var ranges = new List<string>();
        foreach (var item in ToSequence(value, span.RequireNotNull()))
        {
            ranges.Add(NormalizeCellOrRangeReference(ExpectString(item, owner, span), span));
        }

        if (ranges.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects at least one cell range.", span);
        }

        return string.Join(",", ranges);
    }

    private static string NormalizePrintAreaText(string text, string owner, LythonSourceSpan? span)
    {
        var ranges = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (ranges.Length == 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects at least one cell range.", span);
        }

        return string.Join(",", ranges.Select(range => NormalizeCellOrRangeReference(StripDefinedNameSheetPrefix(range), span)));
    }

    private static string NormalizeCellOrRangeReference(string reference, LythonSourceSpan? span)
    {
        var text = reference.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        return text.Contains(':', StringComparison.Ordinal)
            ? ParseCellRange(text, span).Reference
            : NormalizeCellReference(PyString.FromString(text), "cell reference", span);
    }

    private static string? NormalizeOptionalPrintTitleRows(object value, string owner, LythonSourceSpan? span)
        => value is PyNone ? null : NormalizePrintTitleRowsText(ExpectString(value, owner, span), owner, span);

    private static string NormalizePrintTitleRowsText(string text, string owner, LythonSourceSpan? span)
    {
        var normalized = StripDefinedNameSheetPrefix(text).Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var parts = normalized.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            parts = [parts[0], parts[0]];
        }

        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a row range such as '1:3'.", span);
        }

        ValidateRowColumn(start, 1, span);
        ValidateRowColumn(end, 1, span);
        return Math.Min(start, end).ToString(CultureInfo.InvariantCulture) + ":" + Math.Max(start, end).ToString(CultureInfo.InvariantCulture);
    }

    private static string? NormalizeOptionalPrintTitleCols(object value, string owner, LythonSourceSpan? span)
        => value is PyNone ? null : NormalizePrintTitleColsText(ExpectString(value, owner, span), owner, span);

    private static string NormalizePrintTitleColsText(string text, string owner, LythonSourceSpan? span)
    {
        var normalized = StripDefinedNameSheetPrefix(text).Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var parts = normalized.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            parts = [parts[0], parts[0]];
        }

        if (parts.Length != 2)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a column range such as 'A:C'.", span);
        }

        var start = ParseColumnName(parts[0], span);
        var end = ParseColumnName(parts[1], span);
        return ColumnName(Math.Min(start, end)) + ":" + ColumnName(Math.Max(start, end));
    }

    private static PrintTitleParts NormalizePrintTitlesText(string text, LythonSourceSpan? span)
    {
        string? rows = null;
        string? columns = null;
        foreach (var rawPart in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var part = StripDefinedNameSheetPrefix(rawPart).Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            var compact = part.Replace(":", string.Empty, StringComparison.Ordinal);
            if (compact.All(char.IsDigit))
            {
                rows = NormalizePrintTitleRowsText(part, "Print_Titles", span);
                continue;
            }

            if (compact.All(char.IsLetter))
            {
                columns = NormalizePrintTitleColsText(part, "Print_Titles", span);
            }
        }

        return new PrintTitleParts(rows, columns);
    }

    private static string StripDefinedNameSheetPrefix(string reference)
    {
        var bang = reference.LastIndexOf('!');
        return (bang >= 0 ? reference[(bang + 1)..] : reference).Trim();
    }

    private static string? NormalizeOptionalCellReference(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        var normalized = NormalizeCellReference(value, owner, span);
        var address = ParseCellAddress(normalized, span);
        if (address.Row == 1 && address.Column == 1)
        {
            return null;
        }

        return normalized;
    }

    private static string NormalizeCellReference(object value, string owner, LythonSourceSpan? span)
    {
        var address = ParseCellAddress(ExpectString(value, owner, span), span);
        return CellReference(address.Row, address.Column);
    }

    private static string? NormalizeOptionalRangeReference(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        return ParseCellRange(ExpectString(value, owner, span), span).Reference;
    }

    private static string NormalizeSelectionReference(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return "A1";
        }

        var text = ExpectString(value, owner, span);
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner} expects a cell coordinate or range reference.", span);
        }

        var normalized = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            normalized[i] = parts[i].Contains(':', StringComparison.Ordinal)
                ? ParseCellRange(parts[i], span).Reference
                : NormalizeCellReference(PyString.FromString(parts[i]), owner, span);
        }

        return string.Join(" ", normalized);
    }

    private static string? NormalizeOptionalPane(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        var pane = ExpectString(value, owner, span);
        return pane switch
        {
            "topLeft" or "topRight" or "bottomLeft" or "bottomRight" => pane,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} expects a worksheet pane name.", span)
        };
    }

    private static CellRangeAddress ParseCellRangeArguments(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length >= 1 && arguments[0] is not PyNone)
        {
            return ParseCellRange(ExpectString(arguments[0], owner + "(range_string)", span), span);
        }

        if (arguments.Length < 5 ||
            arguments[1] is PyNone ||
            arguments[2] is PyNone ||
            arguments[3] is PyNone ||
            arguments[4] is PyNone)
        {
            throw new LythonRuntimeException("TypeError", owner + "(...) expects range_string or start_row, start_column, end_row, and end_column.", span);
        }

        var startRow = ExpectPositiveInt(arguments[1], owner + "(start_row=...)", span);
        var startColumn = ExpectPositiveInt(arguments[2], owner + "(start_column=...)", span);
        var endRow = ExpectPositiveInt(arguments[3], owner + "(end_row=...)", span);
        var endColumn = ExpectPositiveInt(arguments[4], owner + "(end_column=...)", span);
        ValidateRowColumn(startRow, startColumn, span);
        ValidateRowColumn(endRow, endColumn, span);
        return new CellRangeAddress(
            new CellAddress(Math.Min(startRow, endRow), Math.Min(startColumn, endColumn)),
            new CellAddress(Math.Max(startRow, endRow), Math.Max(startColumn, endColumn)));
    }

    private static int ParseColumnName(string text, LythonSourceSpan? span)
    {
        if (text.Length == 0 || text.Length > 3)
        {
            throw new LythonRuntimeException("ValueError", "Invalid column name.", span);
        }

        var value = 0;
        foreach (var raw in text)
        {
            var c = char.ToUpperInvariant(raw);
            if (c is < 'A' or > 'Z')
            {
                throw new LythonRuntimeException("ValueError", "Invalid column name.", span);
            }

            value = checked((value * 26) + (c - 'A' + 1));
        }

        if (value is < 1 or > 16384)
        {
            throw new LythonRuntimeException("ValueError", "Invalid column name.", span);
        }

        return value;
    }

    private static string ColumnName(int column)
    {
        var value = column;
        Span<char> buffer = stackalloc char[4];
        var index = buffer.Length;
        while (value > 0)
        {
            value--;
            buffer[--index] = (char)('A' + (value % 26));
            value /= 26;
        }

        return new string(buffer[index..]);
    }

    private static string CellReference(int row, int column) => ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);

    private static string WorksheetDimension(OpenPyxlWorksheet worksheet)
        => worksheet.Cells.Count == 0
            ? "A1:A1"
            : $"{CellReference(worksheet.MinRow, worksheet.MinColumn)}:{CellReference(worksheet.MaxRow, worksheet.MaxColumn)}";

    private static string NormalizePackagePartName(string name)
        => name.Replace('\\', '/').TrimStart('/');

    private static string ContentPath(string packagePath)
        => "/" + NormalizePackagePartName(packagePath);

    private static string CellDataType(object value)
    {
        if (value is PyNone)
        {
            return "n";
        }

        if (value is bool)
        {
            return "b";
        }

        if (PyStringOps.TryAsString(value, out var text))
        {
            var rawText = text.AsString();
            if (IsCellErrorText(rawText))
            {
                return "e";
            }

            return rawText.StartsWith("=", StringComparison.Ordinal) ? "f" : "s";
        }

        if (IsDateLikeCellValue(value))
        {
            return "d";
        }

        return "n";
    }
}
