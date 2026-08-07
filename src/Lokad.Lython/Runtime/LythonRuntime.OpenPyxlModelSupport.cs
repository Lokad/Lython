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
    internal sealed class OpenPyxlCell : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlCell(OpenPyxlWorksheet worksheet, int row, int column)
        {
            _worksheet = worksheet;
            Row = row;
            Column = column;
        }

        public int Row { get; private set; }

        public int Column { get; private set; }

        internal void MoveTo(CellAddress address)
        {
            Row = address.Row;
            Column = address.Column;
        }

        public object Value
        {
            get => _worksheet.GetCellValue(Row, Column);
            set => _worksheet.SetCellValue(Row, Column, value);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "value" => Value,
                "row" => new BigInteger(Row),
                "column" => new BigInteger(Column),
                "col_idx" => new BigInteger(Column),
                "column_letter" => PyString.FromString(ColumnName(Column)),
                "coordinate" => PyString.FromString(CellReference(Row, Column)),
                "parent" => _worksheet,
                "internal_value" => Value,
                "is_date" => _worksheet.IsDateCell(Row, Column),
                "base_date" => PyString.FromString(WorkbookBaseDateText(_worksheet.Workbook?.Date1904 ?? false)),
                "number_format" => PyString.FromString(_worksheet.GetCellNumberFormat(Row, Column)),
                "hyperlink" => _worksheet.GetCellHyperlink(Row, Column),
                "comment" => _worksheet.GetCellComment(Row, Column),
                "font" => _worksheet.GetCellStyle(Row, Column, "font"),
                "fill" => _worksheet.GetCellStyle(Row, Column, "fill"),
                "border" => _worksheet.GetCellStyle(Row, Column, "border"),
                "alignment" => _worksheet.GetCellStyle(Row, Column, "alignment"),
                "protection" => _worksheet.GetCellStyle(Row, Column, "protection"),
                "style" => _worksheet.GetCellNamedStyle(Row, Column),
                "style_id" => new BigInteger(_worksheet.GetCellStyleId(Row, Column)),
                "data_type" => PyString.FromString(_worksheet.GetCellDataType(Row, Column)),
                "offset" => new BoundCallable(Offset, "Cell.offset", ["row", "column"], requiredCount: 0),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name == "number_format")
            {
                _worksheet.SetCellNumberFormat(Row, Column, value);
                return true;
            }

            if (name == "hyperlink")
            {
                _worksheet.SetCellHyperlink(Row, Column, value);
                return true;
            }

            if (name == "comment")
            {
                _worksheet.SetCellComment(Row, Column, value);
                return true;
            }

            if (name is "font" or "fill" or "border" or "alignment" or "protection")
            {
                _worksheet.SetCellStyle(Row, Column, name, value);
                return true;
            }

            if (name == "style")
            {
                _worksheet.SetCellNamedStyle(Row, Column, value);
                return true;
            }

            if (name != "value")
            {
                return false;
            }

            Value = value;
            return true;
        }

        private object Offset(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var rowOffset = OptionalInt(arguments, 0, 0, "Cell.offset", "row", span);
            var columnOffset = OptionalInt(arguments, 1, 0, "Cell.offset", "column", span);
            var row = (long)Row + rowOffset;
            var column = (long)Column + columnOffset;
            if (row is < 1 or > 1048576 || column is < 1 or > 16384)
            {
                throw new LythonRuntimeException("ValueError", "Row or column is outside Excel worksheet bounds.", span);
            }

            return _worksheet.GetCellObject((int)row, (int)column);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            var title = QuotePythonString(_worksheet.Title);
            return PyString.FromString($"<Cell {title}.{CellReference(Row, Column)}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static string QuotePythonString(string value)
    {
        var quote = value.Contains('\'') && !value.Contains('"') ? '"' : '\'';
        var builder = new StringBuilder(value.Length + 2);
        builder.Append(quote);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character == quote)
                    {
                        builder.Append('\\');
                    }

                    builder.Append(character);
                    break;
            }
        }

        builder.Append(quote);
        return builder.ToString();
    }

    private sealed class OpenPyxlHyperlink : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlHyperlink(string reference, string target)
        {
            Reference = reference;
            Target = target;
        }

        public string Reference { get; }

        public string Target { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ref" => PyString.FromString(Reference),
                "target" => PyString.FromString(Target),
                "location" => PyNone.Instance,
                "tooltip" => PyNone.Instance,
                "display" => PyString.FromString(Target),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.worksheet.hyperlink.Hyperlink ref='{Reference}' target='{Target}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => PyString.FromString(Target);
    }

    private sealed class OpenPyxlMergedCellSet : IPyDynamicAttributes, IPyIterableValue, IPyRenderableValue
    {
        private readonly IReadOnlyList<CellRangeAddress> _ranges;

        public OpenPyxlMergedCellSet(IReadOnlyList<CellRangeAddress> ranges)
        {
            _ranges = ranges;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ranges" => RangeList(),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public IEnumerable<object> Iterate()
            => _ranges.Select(range => (object)PyString.FromString(range.Reference));

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<MultiCellRange>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private PyList RangeList()
            => new(_ranges.Select(range => (object)PyString.FromString(range.Reference)).ToArray());
    }

    private sealed class OpenPyxlAutoFilter : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlAutoFilter(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ref" => _worksheet.AutoFilterRef is null ? PyNone.Instance : PyString.FromString(_worksheet.AutoFilterRef),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name != "ref")
            {
                return false;
            }

            _worksheet.EnsureCanMutate(null);
            _worksheet.AutoFilterRef = NormalizeOptionalRangeReference(value, "AutoFilter.ref", null);
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.filters.AutoFilter>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlSheetProtection : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlSheetProtection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool Sheet { get; private set; }

        public bool Objects { get; private set; }

        public bool Scenarios { get; private set; }

        public string? Password { get; private set; }

        public string? AlgorithmName { get; private set; }

        public string? HashValue { get; private set; }

        public string? SaltValue { get; private set; }

        public int? SpinCount { get; private set; }

        public bool HasSettings =>
            Sheet ||
            Objects ||
            Scenarios ||
            Password is not null ||
            AlgorithmName is not null ||
            HashValue is not null ||
            SaltValue is not null ||
            SpinCount is not null;

        public void SetLoaded(
            bool sheet,
            bool objects,
            bool scenarios,
            string? password,
            string? algorithmName,
            string? hashValue,
            string? saltValue,
            int? spinCount)
        {
            Sheet = sheet;
            Objects = objects;
            Scenarios = scenarios;
            Password = password;
            AlgorithmName = algorithmName;
            HashValue = hashValue;
            SaltValue = saltValue;
            SpinCount = spinCount;
        }

        public void CopyFrom(OpenPyxlSheetProtection other)
            => SetLoaded(other.Sheet, other.Objects, other.Scenarios, other.Password, other.AlgorithmName, other.HashValue, other.SaltValue, other.SpinCount);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "sheet" => Sheet,
                "objects" => Objects,
                "scenarios" => Scenarios,
                "password" => OptionalStringValue(Password),
                "algorithmName" => OptionalStringValue(AlgorithmName),
                "hashValue" => OptionalStringValue(HashValue),
                "saltValue" => OptionalStringValue(SaltValue),
                "spinCount" => SpinCount is null ? PyNone.Instance : new BigInteger(SpinCount.Value),
                "enable" => new BoundCallable(Enable, "SheetProtection.enable", []),
                "disable" => new BoundCallable(Disable, "SheetProtection.disable", []),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _worksheet.EnsureCanMutate(null);
            switch (name)
            {
                case "sheet":
                    Sheet = ExpectBool(value, "SheetProtection.sheet", null);
                    return true;
                case "objects":
                    Objects = ExpectBool(value, "SheetProtection.objects", null);
                    return true;
                case "scenarios":
                    Scenarios = ExpectBool(value, "SheetProtection.scenarios", null);
                    return true;
                case "password":
                    Password = NullableStringValue(value, "SheetProtection.password");
                    return true;
                case "algorithmName":
                    AlgorithmName = NullableStringValue(value, "SheetProtection.algorithmName");
                    return true;
                case "hashValue":
                    HashValue = NullableStringValue(value, "SheetProtection.hashValue");
                    return true;
                case "saltValue":
                    SaltValue = NullableStringValue(value, "SheetProtection.saltValue");
                    return true;
                case "spinCount":
                    SpinCount = value is PyNone ? null : ExpectNonNegativeInt(value, "SheetProtection.spinCount", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.protection.SheetProtection>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Enable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "SheetProtection.enable() expects no arguments.", span);
            }

            _worksheet.EnsureCanMutate(span);
            Sheet = true;
            return PyNone.Instance;
        }

        private object Disable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "SheetProtection.disable() expects no arguments.", span);
            }

            _worksheet.EnsureCanMutate(span);
            Sheet = false;
            return PyNone.Instance;
        }

        private static object OptionalStringValue(string? value)
            => value is null ? PyNone.Instance : PyString.FromString(value);

        private static string? NullableStringValue(object value, string owner)
            => value is PyNone ? null : ExpectString(value, owner, null);
    }

    internal sealed class OpenPyxlWorkbookSecurity : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly OpenPyxlWorkbook _workbook;

        public OpenPyxlWorkbookSecurity(OpenPyxlWorkbook workbook)
        {
            _workbook = workbook;
        }

        public bool? LockStructure { get; private set; }

        public bool? LockWindows { get; private set; }

        public bool? LockRevision { get; private set; }

        public string? WorkbookPassword { get; private set; }

        public string? WorkbookPasswordCharacterSet { get; private set; }

        public string? RevisionsPassword { get; private set; }

        public string? RevisionsPasswordCharacterSet { get; private set; }

        public string? WorkbookAlgorithmName { get; private set; }

        public string? WorkbookHashValue { get; private set; }

        public string? WorkbookSaltValue { get; private set; }

        public int? WorkbookSpinCount { get; private set; }

        public string? RevisionsAlgorithmName { get; private set; }

        public string? RevisionsHashValue { get; private set; }

        public string? RevisionsSaltValue { get; private set; }

        public int? RevisionsSpinCount { get; private set; }

        public bool HasSettings =>
            LockStructure is not null ||
            LockWindows is not null ||
            LockRevision is not null ||
            WorkbookPassword is not null ||
            WorkbookPasswordCharacterSet is not null ||
            RevisionsPassword is not null ||
            RevisionsPasswordCharacterSet is not null ||
            WorkbookAlgorithmName is not null ||
            WorkbookHashValue is not null ||
            WorkbookSaltValue is not null ||
            WorkbookSpinCount is not null ||
            RevisionsAlgorithmName is not null ||
            RevisionsHashValue is not null ||
            RevisionsSaltValue is not null ||
            RevisionsSpinCount is not null;

        public void SetLoaded(
            bool? lockStructure,
            bool? lockWindows,
            bool? lockRevision,
            string? workbookPassword,
            string? workbookPasswordCharacterSet,
            string? revisionsPassword,
            string? revisionsPasswordCharacterSet,
            string? workbookAlgorithmName,
            string? workbookHashValue,
            string? workbookSaltValue,
            int? workbookSpinCount,
            string? revisionsAlgorithmName,
            string? revisionsHashValue,
            string? revisionsSaltValue,
            int? revisionsSpinCount)
        {
            LockStructure = lockStructure;
            LockWindows = lockWindows;
            LockRevision = lockRevision;
            WorkbookPassword = workbookPassword;
            WorkbookPasswordCharacterSet = workbookPasswordCharacterSet;
            RevisionsPassword = revisionsPassword;
            RevisionsPasswordCharacterSet = revisionsPasswordCharacterSet;
            WorkbookAlgorithmName = workbookAlgorithmName;
            WorkbookHashValue = workbookHashValue;
            WorkbookSaltValue = workbookSaltValue;
            WorkbookSpinCount = workbookSpinCount;
            RevisionsAlgorithmName = revisionsAlgorithmName;
            RevisionsHashValue = revisionsHashValue;
            RevisionsSaltValue = revisionsSaltValue;
            RevisionsSpinCount = revisionsSpinCount;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "lockStructure" => OptionalBoolValue(LockStructure),
                "lockWindows" => OptionalBoolValue(LockWindows),
                "lockRevision" => OptionalBoolValue(LockRevision),
                "workbookPassword" => OptionalStringValue(WorkbookPassword),
                "workbookPasswordCharacterSet" => OptionalStringValue(WorkbookPasswordCharacterSet),
                "revisionsPassword" => OptionalStringValue(RevisionsPassword),
                "revisionsPasswordCharacterSet" => OptionalStringValue(RevisionsPasswordCharacterSet),
                "workbookAlgorithmName" => OptionalStringValue(WorkbookAlgorithmName),
                "workbookHashValue" => OptionalStringValue(WorkbookHashValue),
                "workbookSaltValue" => OptionalStringValue(WorkbookSaltValue),
                "workbookSpinCount" => WorkbookSpinCount is null ? PyNone.Instance : new BigInteger(WorkbookSpinCount.Value),
                "revisionsAlgorithmName" => OptionalStringValue(RevisionsAlgorithmName),
                "revisionsHashValue" => OptionalStringValue(RevisionsHashValue),
                "revisionsSaltValue" => OptionalStringValue(RevisionsSaltValue),
                "revisionsSpinCount" => RevisionsSpinCount is null ? PyNone.Instance : new BigInteger(RevisionsSpinCount.Value),
                "set_workbook_password" => new BoundCallable(SetWorkbookPassword, "WorkbookProtection.set_workbook_password", ["value", "already_hashed"], requiredCount: 1),
                "set_revisions_password" => new BoundCallable(SetRevisionsPassword, "WorkbookProtection.set_revisions_password", ["value", "already_hashed"], requiredCount: 1),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _workbook.EnsureCanMutate(null);
            switch (name)
            {
                case "lockStructure":
                    LockStructure = NormalizeOptionalBool(value, "WorkbookProtection.lockStructure");
                    return true;
                case "lockWindows":
                    LockWindows = NormalizeOptionalBool(value, "WorkbookProtection.lockWindows");
                    return true;
                case "lockRevision":
                    LockRevision = NormalizeOptionalBool(value, "WorkbookProtection.lockRevision");
                    return true;
                case "workbookPassword":
                    WorkbookPassword = NullableStringValue(value, "WorkbookProtection.workbookPassword");
                    return true;
                case "workbookPasswordCharacterSet":
                    WorkbookPasswordCharacterSet = NullableStringValue(value, "WorkbookProtection.workbookPasswordCharacterSet");
                    return true;
                case "revisionsPassword":
                    RevisionsPassword = NullableStringValue(value, "WorkbookProtection.revisionsPassword");
                    return true;
                case "revisionsPasswordCharacterSet":
                    RevisionsPasswordCharacterSet = NullableStringValue(value, "WorkbookProtection.revisionsPasswordCharacterSet");
                    return true;
                case "workbookAlgorithmName":
                    WorkbookAlgorithmName = NullableStringValue(value, "WorkbookProtection.workbookAlgorithmName");
                    return true;
                case "workbookHashValue":
                    WorkbookHashValue = NullableStringValue(value, "WorkbookProtection.workbookHashValue");
                    return true;
                case "workbookSaltValue":
                    WorkbookSaltValue = NullableStringValue(value, "WorkbookProtection.workbookSaltValue");
                    return true;
                case "workbookSpinCount":
                    WorkbookSpinCount = value is PyNone ? null : ExpectNonNegativeInt(value, "WorkbookProtection.workbookSpinCount", null);
                    return true;
                case "revisionsAlgorithmName":
                    RevisionsAlgorithmName = NullableStringValue(value, "WorkbookProtection.revisionsAlgorithmName");
                    return true;
                case "revisionsHashValue":
                    RevisionsHashValue = NullableStringValue(value, "WorkbookProtection.revisionsHashValue");
                    return true;
                case "revisionsSaltValue":
                    RevisionsSaltValue = NullableStringValue(value, "WorkbookProtection.revisionsSaltValue");
                    return true;
                case "revisionsSpinCount":
                    RevisionsSpinCount = value is PyNone ? null : ExpectNonNegativeInt(value, "WorkbookProtection.revisionsSpinCount", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.workbook.protection.WorkbookProtection>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object SetWorkbookPassword(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "WorkbookProtection.set_workbook_password(value[, already_hashed]) expects one or two arguments.", span);
            }

            _workbook.EnsureCanMutate(span);
            var password = ExpectString(arguments[0], "WorkbookProtection.set_workbook_password(value)", span);
            var alreadyHashed = OptionalBool(arguments, 1, false, "WorkbookProtection.set_workbook_password", "already_hashed", span);
            WorkbookPassword = alreadyHashed ? password : HashOpenXmlPassword(password);
            return PyNone.Instance;
        }

        private object SetRevisionsPassword(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "WorkbookProtection.set_revisions_password(value[, already_hashed]) expects one or two arguments.", span);
            }

            _workbook.EnsureCanMutate(span);
            var password = ExpectString(arguments[0], "WorkbookProtection.set_revisions_password(value)", span);
            var alreadyHashed = OptionalBool(arguments, 1, false, "WorkbookProtection.set_revisions_password", "already_hashed", span);
            RevisionsPassword = alreadyHashed ? password : HashOpenXmlPassword(password);
            return PyNone.Instance;
        }

        private static object OptionalBoolValue(bool? value)
            => value is null ? PyNone.Instance : value.Value;

        private static object OptionalStringValue(string? value)
            => value is null ? PyNone.Instance : PyString.FromString(value);

        private static bool? NormalizeOptionalBool(object value, string owner)
            => value is PyNone ? null : ExpectBool(value, owner, null);

        private static string? NullableStringValue(object value, string owner)
            => value is PyNone ? null : ExpectString(value, owner, null);

        private static string HashOpenXmlPassword(string password)
        {
            var hash = 0;
            for (var i = 0; i < password.Length; i++)
            {
                var value = password[i] << (i + 1);
                var rotatedBits = value >> 15;
                value &= 0x7fff;
                hash ^= value | rotatedBits;
            }

            hash ^= password.Length;
            hash ^= 0xCE4B;
            return hash.ToString("X", CultureInfo.InvariantCulture);
        }
    }

    private sealed class OpenPyxlSheetView : IPyDynamicAttributes, IPyRenderableValue
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

    private sealed class OpenPyxlSelection : IPyDynamicAttributes, IPyRenderableValue
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

    internal sealed class OpenPyxlPageMargins : IPyDynamicAttributes, IPyRenderableValue
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

    internal sealed class OpenPyxlPageSetup : IPyDynamicAttributes, IPyRenderableValue
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
            var column = ParseColumnName(ExpectString(index, "Worksheet.column_dimensions[...] key", span), span);
            return _worksheet.GetColumnDimension(column);
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
            return _worksheet.GetRowDimension(row);
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

        public OpenPyxlTableCollection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "keys" => new BoundCallable(Keys, "TableList.keys", []),
                "values" => new BoundCallable(Values, "TableList.values", []),
                "items" => new BoundCallable(Items, "TableList.items", []),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
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
            => _worksheet.Tables.Keys.Order(StringComparer.Ordinal).Select(name => (object)PyString.FromString(name));

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.table.TableList>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Keys(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = span;
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.keys() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables.Keys.Order(StringComparer.Ordinal).Select(name => (object)PyString.FromString(name)).ToArray());
        }

        private object Values(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = span;
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.values() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => (object)pair.Value).ToArray());
        }

        private object Items(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = span;
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "TableList.items() expects no arguments.", span);
            }

            return new PyList(_worksheet.Tables
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (object)new PyTuple(new object[] { PyString.FromString(pair.Key), pair.Value }))
                .ToArray());
        }
    }

    internal sealed class OpenPyxlColumnDimension : IPyDynamicAttributes, IPyRenderableValue
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

    internal sealed class OpenPyxlRowDimension : IPyDynamicAttributes, IPyRenderableValue
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
