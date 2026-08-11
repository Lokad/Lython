using System.Linq;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class OpenPyxlWorkbook :
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
        private readonly OpenPyxlWorkbookIndex _index = new();
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
            ExcelDateSystem dateSystem,
            int activeIndex,
            OpenPyxlSaveGuard saveGuard,
            OpenPyxlPackageSnapshot? packageSnapshot,
            bool hasVbaProject)
        {
            _worksheets = worksheets;
            foreach (var worksheet in _worksheets)
            {
                RegisterWorksheet(worksheet);
            }

            _activeIndex = _worksheets.Count == 0 ? 0 : Math.Clamp(activeIndex, 0, _worksheets.Count - 1);
            var normalStyle = CreateNamedStyleValue(PyString.FromString("Normal"));
            _namedStyles.Add(normalStyle);
            _index.AddNamedStyle("Normal", normalStyle);
            _accessMode = accessMode;
            IsoDates = isoDates;
            DateSystem = dateSystem;
            SaveGuard = saveGuard;
            PackageSnapshot = packageSnapshot;
            HasVbaProject = hasVbaProject;
            _security = new OpenPyxlWorkbookSecurity(this);
        }

        public bool ReadOnly => _accessMode == WorkbookAccessMode.ReadOnly;

        public bool WriteOnly => _accessMode == WorkbookAccessMode.WriteOnly;

        public bool IsoDates { get; }

        public ExcelDateSystem DateSystem { get; }

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
                ExcelDateSystem.Windows1900,
                activeIndex: 0,
                OpenPyxlSaveGuard.Safe,
                packageSnapshot: null,
                hasVbaProject: false);

        public static OpenPyxlWorkbook FromWorksheets(List<OpenPyxlWorksheet> worksheets, bool readOnly, ExcelDateSystem dateSystem, int activeIndex, OpenPyxlSaveGuard saveGuard, OpenPyxlPackageSnapshot? packageSnapshot, bool hasVbaProject)
            => new(
                worksheets.Count == 0 ? [new OpenPyxlWorksheet("Sheet")] : worksheets,
                readOnly ? WorkbookAccessMode.ReadOnly : WorkbookAccessMode.Editable,
                isoDates: false,
                dateSystem,
                activeIndex,
                saveGuard,
                packageSnapshot,
                hasVbaProject);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "active" => _worksheets.Count == 0 ? PyNone.Instance : _worksheets[ActiveIndex],
                "worksheets" => new PyList(_worksheets.Cast<object>()),
                "sheetnames" => CreateSheetNames(),
                "read_only" => ReadOnly,
                "write_only" => WriteOnly,
                "iso_dates" => IsoDates,
                "template" => _template,
                "mime_type" => PyString.FromString(WorkbookContentType(this)),
                "epoch" => PyString.FromString(WorkbookBaseDateText(DateSystem)),
                "excel_base_date" => PyString.FromString(WorkbookBaseDateText(DateSystem)),
                "named_styles" => CreateNamedStyleNames(),
                "_named_styles" => new PyList(_namedStyles.Cast<object>()),
                "style_names" => CreateNamedStyleNames(),
                "security" => _security,
                "add_named_style" => BoundCallable.Create(AddNamedStyle, "Workbook.add_named_style", ["style"]),
                "create_sheet" => BoundCallable.Create(CreateSheet, "Workbook.create_sheet", ["title", "index"], requiredCount: 0),
                "remove" => BoundCallable.Create(Remove, "Workbook.remove", ["worksheet"]),
                "remove_sheet" => BoundCallable.Create(Remove, "Workbook.remove_sheet", ["worksheet"]),
                "copy_worksheet" => BoundCallable.Create(CopyWorksheet, "Workbook.copy_worksheet", ["from_worksheet"]),
                "index" => BoundCallable.Create(Index, "Workbook.index", ["worksheet"]),
                "move_sheet" => BoundCallable.Create(MoveSheet, "Workbook.move_sheet", ["sheet", "offset"], requiredCount: 1),
                "get_sheet_names" => BoundCallable.Create(GetSheetNames, "Workbook.get_sheet_names", []),
                "save" => BoundCallable.Create(Save, SaveAsync, "Workbook.save", ["filename"]),
                "close" => BoundCallable.Create(Close, "Workbook.close", []),
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
            if (!_index.TryGetWorksheet(name, out var worksheet))
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
            if (!_index.TryGetWorksheet(name, out var worksheet))
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
                   _index.ContainsWorksheetTitle(name.AsString());
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
            }
            else
            {
                _namedStyles.AddRange(styles);
                if (!_namedStyles.Any(style => string.Equals(NamedStyleName(style, null), "Normal", StringComparison.Ordinal)))
                {
                    _namedStyles.Insert(0, CreateNamedStyleValue(PyString.FromString("Normal")));
                }
            }

            _index.ResetNamedStyles(_namedStyles);
        }

        internal OpenPyxlStyleValue? FindNamedStyle(string name)
            => _index.FindNamedStyle(name);

        private PyList CreateSheetNames()
            => new(_worksheets.Select(sheet => (object)PyString.FromString(sheet.Title)));

        private PyList CreateNamedStyleNames()
            => new(_namedStyles.Select(style => (object)PyString.FromString(NamedStyleName(style, null))));
    }
}
