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
    internal sealed class OpenPyxlWorkbook :
        IPyMutableDynamicAttributes,
        IPyIterableValue,
        IEnumerable<object>,
        IPyContainsValue,
        IMutablePySubscriptableValue,
        IDeletablePySubscriptableValue,
        IPyRenderableValue
    {
        private enum WorkbookAccessMode
        {
            Editable,
            ReadOnly,
            WriteOnly,
        }

        private readonly List<OpenPyxlWorksheet> _worksheets;
        private readonly List<OpenPyxlStyleValue> _namedStyles = new();
        private readonly OpenPyxlWorkbookSecurity _security;
        private readonly WorkbookAccessMode _accessMode;
        private int _activeIndex;
        private bool _template;
        private bool _saved;

        private OpenPyxlWorkbook(
            List<OpenPyxlWorksheet> worksheets,
            WorkbookAccessMode accessMode,
            bool isoDates,
            bool date1904,
            int activeIndex,
            OpenPyxlSaveGuard saveGuard,
            OpenPyxlPackageSnapshot? packageSnapshot,
            bool hasVbaProject)
        {
            _worksheets = worksheets;
            foreach (var worksheet in _worksheets)
            {
                worksheet.Workbook = this;
            }

            _activeIndex = _worksheets.Count == 0 ? 0 : Math.Clamp(activeIndex, 0, _worksheets.Count - 1);
            _namedStyles.Add(CreateNamedStyleValue(PyString.FromString("Normal")));
            _accessMode = accessMode;
            IsoDates = isoDates;
            Date1904 = date1904;
            SaveGuard = saveGuard;
            PackageSnapshot = packageSnapshot;
            HasVbaProject = hasVbaProject;
            _security = new OpenPyxlWorkbookSecurity(this);
        }

        public bool ReadOnly => _accessMode == WorkbookAccessMode.ReadOnly;

        public bool WriteOnly => _accessMode == WorkbookAccessMode.WriteOnly;

        public bool IsoDates { get; }

        public bool Date1904 { get; }

        public bool Template => _template;

        public OpenPyxlSaveGuard SaveGuard { get; }

        public OpenPyxlPackageSnapshot? PackageSnapshot { get; }

        public IReadOnlyList<OpenPyxlWorksheet> Worksheets => _worksheets;

        public bool HasVbaProject { get; }

        public OpenPyxlWorkbookSecurity Security => _security;

        public int ActiveIndex => _worksheets.Count == 0 ? 0 : Math.Clamp(_activeIndex, 0, _worksheets.Count - 1);

        public bool HasFormulaCells => _worksheets.Any(worksheet => worksheet.Cells.Values.Any(IsFormulaValue));

        public static OpenPyxlWorkbook CreateNew(bool writeOnly, bool isoDates)
            => new(
                [new OpenPyxlWorksheet("Sheet")],
                writeOnly ? WorkbookAccessMode.WriteOnly : WorkbookAccessMode.Editable,
                isoDates,
                date1904: false,
                activeIndex: 0,
                OpenPyxlSaveGuard.Safe,
                packageSnapshot: null,
                hasVbaProject: false);

        public static OpenPyxlWorkbook FromWorksheets(List<OpenPyxlWorksheet> worksheets, bool readOnly, bool date1904, int activeIndex, OpenPyxlSaveGuard saveGuard, OpenPyxlPackageSnapshot? packageSnapshot, bool hasVbaProject)
            => new(
                worksheets.Count == 0 ? [new OpenPyxlWorksheet("Sheet")] : worksheets,
                readOnly ? WorkbookAccessMode.ReadOnly : WorkbookAccessMode.Editable,
                isoDates: false,
                date1904,
                activeIndex,
                saveGuard,
                packageSnapshot,
                hasVbaProject);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "active" => _worksheets.Count == 0 ? PyNone.Instance : _worksheets[ActiveIndex],
                "worksheets" => new PyList(_worksheets.Cast<object>().ToArray()),
                "sheetnames" => new PyList(_worksheets.Select(sheet => (object)PyString.FromString(sheet.Title)).ToArray()),
                "read_only" => ReadOnly,
                "write_only" => WriteOnly,
                "iso_dates" => IsoDates,
                "template" => _template,
                "mime_type" => PyString.FromString(WorkbookContentType(this)),
                "epoch" => PyString.FromString(WorkbookBaseDateText(Date1904)),
                "excel_base_date" => PyString.FromString(WorkbookBaseDateText(Date1904)),
                "named_styles" => new PyList(_namedStyles.Select(style => (object)PyString.FromString(NamedStyleName(style, null))).ToArray()),
                "_named_styles" => new PyList(_namedStyles.Cast<object>().ToArray()),
                "style_names" => new PyList(_namedStyles.Select(style => (object)PyString.FromString(NamedStyleName(style, null))).ToArray()),
                "security" => _security,
                "add_named_style" => new BoundCallable(AddNamedStyle, "Workbook.add_named_style", ["style"]),
                "create_sheet" => new BoundCallable(CreateSheet, "Workbook.create_sheet", ["title", "index"], requiredCount: 0),
                "remove" => new BoundCallable(Remove, "Workbook.remove", ["worksheet"]),
                "remove_sheet" => new BoundCallable(Remove, "Workbook.remove_sheet", ["worksheet"]),
                "copy_worksheet" => new BoundCallable(CopyWorksheet, "Workbook.copy_worksheet", ["from_worksheet"]),
                "index" => new BoundCallable(Index, "Workbook.index", ["worksheet"]),
                "move_sheet" => new BoundCallable(MoveSheet, "Workbook.move_sheet", ["sheet", "offset"], requiredCount: 1),
                "get_sheet_names" => new BoundCallable(GetSheetNames, "Workbook.get_sheet_names", []),
                "save" => new BoundCallable(Save, SaveAsync, "Workbook.save", ["filename"]),
                "close" => new BoundCallable(Close, "Workbook.close", []),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name == "template")
            {
                EnsureCanMutate(null);
                _template = ExpectBool(value, "Workbook.template", null);
                return true;
            }

            if (name != "active")
            {
                return false;
            }

            SetActive(value, null);
            return true;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            var name = ExpectString(index, "Workbook sheet lookup", span);
            var worksheet = _worksheets.FirstOrDefault(sheet => string.Equals(sheet.Title, name, StringComparison.Ordinal));
            if (worksheet is null)
            {
                throw new LythonRuntimeException("KeyError", $"Worksheet {name} does not exist.", span);
            }

            return worksheet;
        }

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            _ = index;
            _ = value;
            throw new LythonRuntimeException("TypeError", "Workbook does not support item assignment.", span);
        }

        public void DeleteSubscript(object index, LythonSourceSpan span)
        {
            EnsureCanMutate(span);
            var name = ExpectString(index, "Workbook sheet deletion", span);
            var worksheet = _worksheets.FirstOrDefault(sheet => string.Equals(sheet.Title, name, StringComparison.Ordinal));
            if (worksheet is null)
            {
                throw new LythonRuntimeException("KeyError", $"Worksheet {name} does not exist.", span);
            }

            RemoveWorksheet(worksheet, span);
        }

        public IEnumerable<object> Iterate() => _worksheets;

        public bool Contains(object candidate, LythonSourceSpan span)
        {
            _ = span;
            return PyStringOps.TryAsString(candidate, out var name) &&
                   _worksheets.Any(sheet => string.Equals(sheet.Title, name.AsString(), StringComparison.Ordinal));
        }

        public IEnumerator<object> GetEnumerator() => _worksheets.Cast<object>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.Workbook>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        internal void SetLoadedNamedStyles(IReadOnlyList<OpenPyxlStyleValue> styles)
        {
            _namedStyles.Clear();
            if (styles.Count == 0)
            {
                _namedStyles.Add(CreateNamedStyleValue(PyString.FromString("Normal")));
                return;
            }

            _namedStyles.AddRange(styles);
            if (!_namedStyles.Any(style => string.Equals(NamedStyleName(style, null), "Normal", StringComparison.Ordinal)))
            {
                _namedStyles.Insert(0, CreateNamedStyleValue(PyString.FromString("Normal")));
            }
        }

        internal OpenPyxlStyleValue? FindNamedStyle(string name)
            => _namedStyles.FirstOrDefault(style => string.Equals(NamedStyleName(style, null), name, StringComparison.Ordinal));

        private object CreateSheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var title = arguments.Length >= 1 && arguments[0] is not PyNone
                ? ExpectString(arguments[0], "Workbook.create_sheet(title)", span)
                : NextSheetName();
            title = MakeUniqueSheetTitle(title, current: null, span);

            var worksheet = new OpenPyxlWorksheet(title) { Workbook = this };
            var active = _worksheets.Count == 0 ? null : _worksheets[ActiveIndex];
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                if (!PyNumberOps.TryAsInteger(arguments[1], out var indexInteger) ||
                    indexInteger < new BigInteger(int.MinValue) ||
                    indexInteger > new BigInteger(int.MaxValue))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.create_sheet(..., index=...) expects a valid worksheet index.", span);
                }

                _worksheets.Insert(NormalizeInsertIndex((int)indexInteger), worksheet);
                if (active is not null)
                {
                    _activeIndex = _worksheets.IndexOf(active);
                }
            }
            else
            {
                _worksheets.Add(worksheet);
            }

            return worksheet;
        }

        private object Remove(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlWorksheet worksheet || !ReferenceEquals(worksheet.Workbook, this))
            {
                throw new LythonRuntimeException("TypeError", "Workbook.remove(worksheet) expects a worksheet from this workbook.", span);
            }

            RemoveWorksheet(worksheet, span);
            return PyNone.Instance;
        }

        private void SetActive(object value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                if (integer < BigInteger.Zero || integer >= new BigInteger(_worksheets.Count))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.active expects a valid worksheet index.", span);
                }

                _activeIndex = (int)integer;
                return;
            }

            var worksheet = ResolveOwnedWorksheet(value, "Workbook.active", span);
            _activeIndex = _worksheets.IndexOf(worksheet);
        }

        private void RemoveWorksheet(OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            if (_worksheets.Count == 1)
            {
                throw new LythonRuntimeException("ValueError", "Cannot remove the only worksheet.", span);
            }

            var removedIndex = _worksheets.IndexOf(worksheet);
            _worksheets.Remove(worksheet);
            worksheet.Workbook = null;
            if (_activeIndex == removedIndex)
            {
                _activeIndex = Math.Min(removedIndex, _worksheets.Count - 1);
            }
            else if (removedIndex >= 0 && removedIndex < _activeIndex)
            {
                _activeIndex--;
            }
        }

        private object CopyWorksheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var source = ExpectOwnedWorksheet(arguments, "Workbook.copy_worksheet(from_worksheet)", span);
            var copyTitle = MakeUniqueSheetTitle(MakeCopyTitle(source.Title), current: null, span);
            var copy = source.Copy(copyTitle);
            copy.Workbook = this;
            _worksheets.Add(copy);
            return copy;
        }

        private object Index(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var worksheet = ExpectOwnedWorksheet(arguments, "Workbook.index(worksheet)", span);
            return new BigInteger(_worksheets.IndexOf(worksheet));
        }

        private object MoveSheet(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var worksheet = ResolveWorksheetArgument(arguments[0], "Workbook.move_sheet(sheet)", span);
            var offset = 0;
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                if (!PyNumberOps.TryAsInteger(arguments[1], out var offsetInteger) ||
                    offsetInteger < new BigInteger(int.MinValue) ||
                    offsetInteger > new BigInteger(int.MaxValue))
                {
                    throw new LythonRuntimeException("ValueError", "Workbook.move_sheet(..., offset=...) expects an integer offset.", span);
                }

                offset = (int)offsetInteger;
            }

            var oldIndex = _worksheets.IndexOf(worksheet);
            var newIndex = Math.Clamp(oldIndex + offset, 0, _worksheets.Count - 1);
            if (newIndex != oldIndex)
            {
                var active = _worksheets[ActiveIndex];
                _worksheets.RemoveAt(oldIndex);
                _worksheets.Insert(newIndex, worksheet);
                _activeIndex = _worksheets.IndexOf(active);
            }

            return PyNone.Instance;
        }

        private object AddNamedStyle(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlStyleValue style || style.QualifiedName != "openpyxl.styles.NamedStyle")
            {
                throw new LythonRuntimeException("TypeError", "Workbook.add_named_style(style) expects an openpyxl.styles.NamedStyle.", span);
            }

            var name = NamedStyleName(style, span);
            if (_namedStyles.Any(existing => string.Equals(NamedStyleName(existing, span), name, StringComparison.Ordinal)))
            {
                throw new LythonRuntimeException("ValueError", "Style " + name + " exists already.", span);
            }

            _namedStyles.Add(style);
            return PyNone.Instance;
        }

        private object GetSheetNames(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.get_sheet_names() expects no arguments.", span);
            }

            return new PyList(_worksheets.Select(sheet => (object)PyString.FromString(sheet.Title)).ToArray());
        }

        private object Save(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.save(filename) expects one filename argument.", span);
            }

            EnsureCanSave(span);
            var path = NormalizeWorkbookPath(arguments[0], context, span);
            var payload = OpenPyxlPackage.Save(this, context, span);
            context.RegisterHostCall(span);
            context.WriteHostBytes(path, payload, span);
            _saved = true;
            return PyNone.Instance;
        }

        private async ValueTask<object> SaveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.save(filename) expects one filename argument.", span);
            }

            EnsureCanSave(span);
            var path = NormalizeWorkbookPath(arguments[0], context, span);
            var payload = OpenPyxlPackage.Save(this, context, span);
            context.RegisterHostCall(span);
            await context.WriteHostBytesAsync(path, payload, span).ConfigureAwait(false);
            _saved = true;
            return PyNone.Instance;
        }

        private void EnsureCanSave(LythonSourceSpan span)
        {
            EnsureCanMutate(span);
            if (SaveGuard.TryGetUnsafeReason(out var unsafeReason))
            {
                throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Workbook.save() would discard unsupported openpyxl workbook content: " + unsafeReason,
                    span);
            }

            var stalePreservedFeatureReason = StructurallyStalePreservedFeatureReason();
            if (stalePreservedFeatureReason is not null)
            {
                throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Workbook.save() would leave stale preserved openpyxl worksheet metadata after structural edits: " + stalePreservedFeatureReason,
                    span);
            }

            if (WriteOnly && _saved)
            {
                throw new LythonRuntimeException("WorkbookAlreadySaved", "Workbook has already been saved and cannot be saved again.", span);
            }
        }

        private string? StructurallyStalePreservedFeatureReason()
        {
            foreach (var worksheet in _worksheets)
            {
                var reason = OpenPyxlPackage.StructuralMutationPreservedFeatureReason(worksheet);
                if (reason is not null)
                {
                    return reason;
                }
            }

            return null;
        }

        internal void EnsureCanMutate(LythonSourceSpan? span)
        {
            if (ReadOnly)
            {
                throw new LythonRuntimeException("TypeError", "Workbook is read-only.", span);
            }
        }

        private static object Close(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.close() expects no arguments.", span);
            }

            return PyNone.Instance;
        }

        private string NextSheetName()
        {
            var index = _worksheets.Count + 1;
            while (true)
            {
                var candidate = "Sheet" + index.ToString(CultureInfo.InvariantCulture);
                if (_worksheets.All(sheet => !string.Equals(sheet.Title, candidate, StringComparison.Ordinal)))
                {
                    return candidate;
                }

                index++;
            }
        }

        internal string MakeUniqueSheetTitle(string title, OpenPyxlWorksheet? current, LythonSourceSpan? span)
        {
            ValidateSheetTitle(title, span);
            if (!ContainsSheetTitle(title, current))
            {
                return title;
            }

            var suffix = 1;
            while (true)
            {
                var suffixText = suffix.ToString(CultureInfo.InvariantCulture);
                var prefixLength = Math.Min(title.Length, 31 - suffixText.Length);
                var candidate = title[..prefixLength] + suffixText;
                if (!ContainsSheetTitle(candidate, current))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private bool ContainsSheetTitle(string title, OpenPyxlWorksheet? current)
            => _worksheets.Any(sheet =>
                !ReferenceEquals(sheet, current) &&
                string.Equals(sheet.Title, title, StringComparison.OrdinalIgnoreCase));

        private int NormalizeInsertIndex(int index)
        {
            if (index < 0)
            {
                return Math.Max(0, _worksheets.Count + index);
            }

            return Math.Min(index, _worksheets.Count);
        }

        private static string MakeCopyTitle(string sourceTitle)
        {
            const string suffix = " Copy";
            var prefixLength = Math.Min(sourceTitle.Length, 31 - suffix.Length);
            return sourceTitle[..prefixLength] + suffix;
        }

        private OpenPyxlWorksheet ExpectOwnedWorksheet(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects a worksheet from this workbook.", span);
            }

            return ResolveOwnedWorksheet(arguments[0], owner, span);
        }

        private OpenPyxlWorksheet ResolveWorksheetArgument(object value, string owner, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out var title))
            {
                var worksheet = _worksheets.FirstOrDefault(sheet => string.Equals(sheet.Title, title.AsString(), StringComparison.Ordinal));
                if (worksheet is null)
                {
                    throw new LythonRuntimeException("KeyError", $"Worksheet {title.AsString()} does not exist.", span);
                }

                return worksheet;
            }

            return ResolveOwnedWorksheet(value, owner, span);
        }

        private OpenPyxlWorksheet ResolveOwnedWorksheet(object value, string owner, LythonSourceSpan? span)
        {
            if (value is OpenPyxlWorksheet worksheet &&
                ReferenceEquals(worksheet.Workbook, this) &&
                _worksheets.Contains(worksheet))
            {
                return worksheet;
            }

            throw new LythonRuntimeException("TypeError", owner + " expects a worksheet from this workbook.", span);
        }
    }

}
