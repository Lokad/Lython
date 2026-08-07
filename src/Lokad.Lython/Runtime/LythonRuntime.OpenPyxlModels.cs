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
        IPyDynamicAttributes,
        IPyIterableValue,
        IEnumerable<object>,
        IPyContainsValue,
        IMutablePySubscriptableValue,
        IDeletablePySubscriptableValue,
        IPyRenderableValue
    {
        private readonly List<OpenPyxlWorksheet> _worksheets;
        private readonly List<OpenPyxlStyleValue> _namedStyles = new();
        private readonly OpenPyxlWorkbookSecurity _security;
        private int _activeIndex;
        private bool _template;
        private bool _saved;

        private OpenPyxlWorkbook(
            List<OpenPyxlWorksheet> worksheets,
            bool readOnly,
            bool writeOnly,
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
            ReadOnly = readOnly;
            WriteOnly = writeOnly;
            IsoDates = isoDates;
            Date1904 = date1904;
            SaveGuard = saveGuard;
            PackageSnapshot = packageSnapshot;
            HasVbaProject = hasVbaProject;
            _security = new OpenPyxlWorkbookSecurity(this);
        }

        public bool ReadOnly { get; }

        public bool WriteOnly { get; }

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
            => new([new OpenPyxlWorksheet("Sheet")], readOnly: false, writeOnly, isoDates, date1904: false, activeIndex: 0, OpenPyxlSaveGuard.Safe, packageSnapshot: null, hasVbaProject: false);

        public static OpenPyxlWorkbook FromWorksheets(List<OpenPyxlWorksheet> worksheets, bool readOnly, bool date1904, int activeIndex, OpenPyxlSaveGuard saveGuard, OpenPyxlPackageSnapshot? packageSnapshot, bool hasVbaProject)
            => new(worksheets.Count == 0 ? [new OpenPyxlWorksheet("Sheet")] : worksheets, readOnly, writeOnly: false, isoDates: false, date1904, activeIndex, saveGuard, packageSnapshot, hasVbaProject);

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
            if (!SaveGuard.CanSave)
            {
                throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Workbook.save() would discard unsupported openpyxl workbook content: " + SaveGuard.Reason,
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

    internal sealed class OpenPyxlWorksheet :
        IPyDynamicAttributes,
        IMutablePySubscriptableValue,
        IPySliceableValue,
        IPyRenderableValue
    {
        private readonly Dictionary<CellAddress, object> _cells = new();
        private readonly Dictionary<CellAddress, OpenPyxlCell> _cellObjects = new();
        private readonly Dictionary<CellAddress, string> _dataTypes = new();
        private readonly Dictionary<CellAddress, string> _numberFormats = new();
        private readonly Dictionary<CellAddress, int> _loadedStyleIds = new();
        private readonly Dictionary<CellAddress, string> _hyperlinks = new();
        private readonly Dictionary<CellAddress, OpenPyxlComment> _comments = new();
        private readonly Dictionary<CellAddress, object> _formulaCachedValues = new();
        private readonly Dictionary<CellAddress, XElement> _formulaXml = new();
        private readonly Dictionary<(CellAddress Address, string Name), object> _cellStyles = new();
        private readonly Dictionary<CellAddress, string> _cellNamedStyles = new();
        private readonly Dictionary<string, OpenPyxlTable> _tables = new(StringComparer.Ordinal);
        private readonly List<OpenPyxlDataValidation> _dataValidations = new();
        private readonly List<OpenPyxlConditionalFormatting> _conditionalFormattings = new();
        private readonly List<OpenPyxlLoadedDrawing> _drawings = new();
        private readonly List<OpenPyxlLoadedChart> _charts = new();
        private readonly List<OpenPyxlLoadedImage> _images = new();
        private readonly List<CellRangeAddress> _mergedRanges = new();
        private readonly HashSet<CellRangeAddress> _mergedRangeSet = [];
        private readonly Dictionary<int, OpenPyxlColumnDimension> _columnDimensions = new();
        private readonly Dictionary<int, OpenPyxlRowDimension> _rowDimensions = new();
        private int _minRow = 1;
        private int _maxRow = 1;
        private int _minColumn = 1;
        private int _maxColumn = 1;
        private bool _dimensionsDirty;
        private string? _freezePanes;
        private string? _autoFilterRef;
        private bool _showGridLines = true;
        private bool _tabSelected;
        private int _workbookViewId;
        private string _selectionActiveCell = "A1";
        private string _selectionSqref = "A1";
        private string? _selectionPane;
        private readonly OpenPyxlPageMargins _pageMargins;
        private readonly OpenPyxlPageSetup _pageSetup;
        private readonly OpenPyxlSheetProtection _protection;
        private string? _printArea;
        private string? _printTitleRows;
        private string? _printTitleCols;
        private string? _commentsSourcePath;
        private bool _hasPageMargins;
        private bool _hasStructuralMutation;
        private bool _hasLoadedCommentsUpdate;

        public OpenPyxlWorksheet(string title)
        {
            Title = title;
            _pageMargins = new OpenPyxlPageMargins(this);
            _pageSetup = new OpenPyxlPageSetup(this);
            _protection = new OpenPyxlSheetProtection(this);
        }

        public OpenPyxlWorkbook? Workbook { get; set; }

        public string? SourcePath { get; set; }

        public string Title { get; private set; }

        public IReadOnlyDictionary<CellAddress, object> Cells => _cells;

        public IReadOnlyDictionary<CellAddress, string> NumberFormats => _numberFormats;

        public IReadOnlyDictionary<CellAddress, int> LoadedStyleIds => _loadedStyleIds;

        public IReadOnlyDictionary<(CellAddress Address, string Name), object> CellStyles => _cellStyles;

        public IReadOnlyDictionary<CellAddress, string> CellNamedStyles => _cellNamedStyles;

        public IReadOnlyDictionary<CellAddress, string> Hyperlinks => _hyperlinks;

        public IReadOnlyDictionary<CellAddress, OpenPyxlComment> Comments => _comments;

        public string? CommentsSourcePath => _commentsSourcePath;

        public bool HasLoadedCommentsUpdate => _hasLoadedCommentsUpdate;

        public IReadOnlyDictionary<string, OpenPyxlTable> Tables => _tables;

        public IReadOnlyList<OpenPyxlDataValidation> DataValidations => _dataValidations;

        public IReadOnlyList<OpenPyxlConditionalFormatting> ConditionalFormattings => _conditionalFormattings;

        public IReadOnlyList<OpenPyxlLoadedDrawing> Drawings => _drawings;

        public IReadOnlyList<OpenPyxlLoadedChart> Charts => _charts;

        public IReadOnlyList<OpenPyxlLoadedImage> Images => _images;

        public IReadOnlyList<CellRangeAddress> MergedRanges => _mergedRanges;

        public IReadOnlyDictionary<int, OpenPyxlColumnDimension> ColumnDimensions => _columnDimensions;

        public IReadOnlyDictionary<int, OpenPyxlRowDimension> RowDimensions => _rowDimensions;

        internal bool HasStructuralMutation => _hasStructuralMutation;

        public string? AutoFilterRef
        {
            get => _autoFilterRef;
            set => _autoFilterRef = value;
        }

        public string? FreezePanes => _freezePanes;

        public bool ShowGridLines => _showGridLines;

        public bool TabSelected => _tabSelected;

        public int WorkbookViewId => _workbookViewId;

        public string SelectionActiveCell => _selectionActiveCell;

        public string SelectionSqref => _selectionSqref;

        public string? SelectionPane => _selectionPane;

        public string? PrintArea => _printArea;

        public string? PrintTitleRows => _printTitleRows;

        public string? PrintTitleCols => _printTitleCols;

        public OpenPyxlPageMargins PageMargins => _pageMargins;

        public OpenPyxlPageSetup PageSetup => _pageSetup;

        public OpenPyxlSheetProtection Protection => _protection;

        public bool HasPageMargins => _hasPageMargins;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "title" => PyString.FromString(Title),
                "min_row" => new BigInteger(MinRow),
                "max_row" => new BigInteger(MaxRow),
                "min_column" => new BigInteger(MinColumn),
                "max_column" => new BigInteger(MaxColumn),
                "dimensions" => PyString.FromString(WorksheetDimension(this)),
                "freeze_panes" => _freezePanes is null ? PyNone.Instance : PyString.FromString(_freezePanes),
                "show_gridlines" => _showGridLines,
                "sheet_view" => new OpenPyxlSheetView(this),
                "print_area" => _printArea is null ? PyNone.Instance : PyString.FromString(_printArea),
                "print_title_rows" => _printTitleRows is null ? PyNone.Instance : PyString.FromString(_printTitleRows),
                "print_title_cols" => _printTitleCols is null ? PyNone.Instance : PyString.FromString(_printTitleCols),
                "page_margins" => _pageMargins,
                "page_setup" => _pageSetup,
                "protection" => _protection,
                "set_printer_settings" => new BoundCallable(SetPrinterSettings, "Worksheet.set_printer_settings", ["paper_size", "orientation"], requiredCount: 2),
                "auto_filter" => new OpenPyxlAutoFilter(this),
                "tables" => new OpenPyxlTableCollection(this),
                "data_validations" => new OpenPyxlDataValidationList(this),
                "conditional_formatting" => new OpenPyxlConditionalFormattingCollection(this),
                "_charts" => new PyList(_charts.Cast<object>().ToArray()),
                "_images" => new PyList(_images.Cast<object>().ToArray()),
                "_drawings" => new PyList(_drawings.Cast<object>().ToArray()),
                "drawings" => new PyList(_drawings.Cast<object>().ToArray()),
                "_drawing" => _drawings.Count == 0 ? PyNone.Instance : _drawings[0],
                "add_table" => new BoundCallable(AddTable, "Worksheet.add_table", ["table"]),
                "add_data_validation" => new BoundCallable(AddDataValidation, "Worksheet.add_data_validation", ["data_validation"]),
                "add_chart" => new BoundCallable(AddChart, "Worksheet.add_chart", ["chart", "anchor"], requiredCount: 1),
                "add_image" => new BoundCallable(AddImage, "Worksheet.add_image", ["img", "anchor"], requiredCount: 1),
                "column_dimensions" => new OpenPyxlColumnDimensionCollection(this),
                "row_dimensions" => new OpenPyxlRowDimensionCollection(this),
                "rows" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null),
                "columns" => ColumnsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null),
                "values" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: true, null, null),
                "cell" => new BoundCallable(Cell, "Worksheet.cell", ["row", "column", "value"], requiredCount: 2),
                "append" => new BoundCallable(Append, "Worksheet.append", ["iterable"]),
                "iter_rows" => new BoundCallable(IterRows, "Worksheet.iter_rows", ["min_row", "max_row", "min_col", "max_col", "values_only"], requiredCount: 0),
                "iter_cols" => new BoundCallable(IterCols, "Worksheet.iter_cols", ["min_row", "max_row", "min_col", "max_col", "values_only"], requiredCount: 0),
                "calculate_dimension" => new BoundCallable(CalculateDimension, "Worksheet.calculate_dimension", []),
                "merge_cells" => new BoundCallable(MergeCells, "Worksheet.merge_cells", ["range_string", "start_row", "start_column", "end_row", "end_column"], requiredCount: 0),
                "unmerge_cells" => new BoundCallable(UnmergeCells, "Worksheet.unmerge_cells", ["range_string", "start_row", "start_column", "end_row", "end_column"], requiredCount: 0),
                "insert_rows" => new BoundCallable(InsertRows, "Worksheet.insert_rows", ["idx", "amount"], requiredCount: 1),
                "delete_rows" => new BoundCallable(DeleteRows, "Worksheet.delete_rows", ["idx", "amount"], requiredCount: 1),
                "insert_cols" => new BoundCallable(InsertCols, "Worksheet.insert_cols", ["idx", "amount"], requiredCount: 1),
                "delete_cols" => new BoundCallable(DeleteCols, "Worksheet.delete_cols", ["idx", "amount"], requiredCount: 1),
                "move_range" => new BoundCallable(MoveRange, "Worksheet.move_range", ["cell_range", "rows", "cols", "translate"], requiredCount: 1),
                "merged_cells" => new OpenPyxlMergedCellSet(_mergedRanges),
                "merged_cell_ranges" => MergedRangeList(),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name == "freeze_panes")
            {
                EnsureCanMutate(null);
                _freezePanes = NormalizeOptionalCellReference(value, "Worksheet.freeze_panes", null);
                return true;
            }

            if (name == "show_gridlines")
            {
                SetShowGridLines(ExpectBool(value, "Worksheet.show_gridlines", null), null);
                return true;
            }

            if (name == "print_area")
            {
                SetPrintArea(NormalizeOptionalPrintArea(value, "Worksheet.print_area", null), null);
                return true;
            }

            if (name == "print_title_rows")
            {
                SetPrintTitleRows(NormalizeOptionalPrintTitleRows(value, "Worksheet.print_title_rows", null), null);
                return true;
            }

            if (name == "print_title_cols")
            {
                SetPrintTitleCols(NormalizeOptionalPrintTitleCols(value, "Worksheet.print_title_cols", null), null);
                return true;
            }

            if (name != "title")
            {
                return false;
            }

            EnsureCanMutate(null);
            var title = ExpectString(value, "Worksheet.title", null);
            Title = Workbook is null
                ? ValidateDetachedSheetTitle(title)
                : Workbook.MakeUniqueSheetTitle(title, this, null);
            return true;
        }

        public object GetSubscript(object index, LythonSourceSpan span)
        {
            if (PyNumberOps.TryAsInteger(index, out var rowInteger))
            {
                var row = NormalizePositiveInt(rowInteger, "Worksheet row lookup", span);
                ValidateRowColumn(row, 1, span);
                return RowTuple(row, 1, MaxColumn, valuesOnly: false, null, span);
            }

            var text = ExpectString(index, "Worksheet cell lookup", span);
            if (text.Contains(':', StringComparison.Ordinal))
            {
                return GetRangeSubscript(text, span);
            }

            if (TryParseColumnReference(text, span, out var column))
            {
                return ColumnTuple(1, MaxRow, column, valuesOnly: false, null, span);
            }

            var address = ParseCellAddress(text, span);
            return GetCellObject(address.Row, address.Column);
        }

        public object GetSlice(object? start, object? end, object? step, LythonSourceSpan span)
        {
            if (step is not null and not PyNone)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet slicing does not support a step.", span);
            }

            if (TryParseSliceCellRange(start, end, span, out var cellStart, out var cellEnd))
            {
                return RowsTuple(cellStart.Row, cellEnd.Row, cellStart.Column, cellEnd.Column, valuesOnly: false, null, span);
            }

            if (TryParseSliceColumnRange(start, end, span, out var minColumn, out var maxColumn))
            {
                return ColumnsTuple(1, MaxRow, minColumn, maxColumn, valuesOnly: false, null, span);
            }

            if (TryParseSliceRowRange(start, end, span, out var minRow, out var maxRow))
            {
                return RowsTuple(minRow, maxRow, 1, MaxColumn, valuesOnly: false, null, span);
            }

            throw new LythonRuntimeException("TypeError", "Worksheet slice bounds must be row numbers, column letters, or cell coordinates.", span);
        }

        public void SetSubscript(object index, object value, LythonSourceSpan span)
        {
            var text = ExpectString(index, "Worksheet cell assignment", span);
            if (text.Contains(':', StringComparison.Ordinal))
            {
                throw new LythonRuntimeException("TypeError", "Worksheet range assignment is not supported by Lython openpyxl.", span);
            }

            var address = ParseCellAddress(text, span);
            SetCellValue(address.Row, address.Column, value);
        }

        private object GetRangeSubscript(string text, LythonSourceSpan span)
        {
            var parts = text.Split(':', 2);
            if (parts.Length != 2)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell range: {text}", span);
            }

            if (TryParseColumnReference(parts[0], span, out var startColumn) &&
                TryParseColumnReference(parts[1], span, out var endColumn))
            {
                return ColumnsTuple(1, MaxRow, Math.Min(startColumn, endColumn), Math.Max(startColumn, endColumn), valuesOnly: false, null, span);
            }

            if (TryParseRowReference(parts[0], span, out var startRow) &&
                TryParseRowReference(parts[1], span, out var endRow))
            {
                return RowsTuple(Math.Min(startRow, endRow), Math.Max(startRow, endRow), 1, MaxColumn, valuesOnly: false, null, span);
            }

            var (start, end) = ParseRange(text, span);
            return RowsTuple(start.Row, end.Row, start.Column, end.Column, valuesOnly: false, null, span);
        }

        private static bool TryParseSliceCellRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out CellAddress normalizedStart,
            out CellAddress normalizedEnd)
        {
            normalizedStart = default;
            normalizedEnd = default;
            if (!TryGetString(start, out var startText) ||
                !TryGetString(end, out var endText) ||
                !LooksLikeCellReference(startText) ||
                !LooksLikeCellReference(endText))
            {
                return false;
            }

            var parsedStart = ParseCellAddress(startText, span);
            var parsedEnd = ParseCellAddress(endText, span);
            normalizedStart = new CellAddress(Math.Min(parsedStart.Row, parsedEnd.Row), Math.Min(parsedStart.Column, parsedEnd.Column));
            normalizedEnd = new CellAddress(Math.Max(parsedStart.Row, parsedEnd.Row), Math.Max(parsedStart.Column, parsedEnd.Column));
            return true;
        }

        private static bool TryParseSliceColumnRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out int minColumn,
            out int maxColumn)
        {
            minColumn = 0;
            maxColumn = 0;
            if (!TryGetString(start, out var startText) ||
                !TryGetString(end, out var endText) ||
                !TryParseColumnReference(startText, span, out var startColumn) ||
                !TryParseColumnReference(endText, span, out var endColumn))
            {
                return false;
            }

            minColumn = Math.Min(startColumn, endColumn);
            maxColumn = Math.Max(startColumn, endColumn);
            return true;
        }

        private bool TryParseSliceRowRange(
            object? start,
            object? end,
            LythonSourceSpan span,
            out int minRow,
            out int maxRow)
        {
            if (!TryParseOptionalRowBound(start, defaultValue: 1, span, out var startRow) ||
                !TryParseOptionalRowBound(end, defaultValue: MaxRow, span, out var endRow))
            {
                minRow = 0;
                maxRow = 0;
                return false;
            }

            minRow = Math.Min(startRow, endRow);
            maxRow = Math.Max(startRow, endRow);
            return true;
        }

        private static bool TryParseOptionalRowBound(object? value, int defaultValue, LythonSourceSpan span, out int row)
        {
            if (value is null or PyNone)
            {
                row = defaultValue;
                return true;
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                row = NormalizePositiveInt(integer, "Worksheet row slice", span);
                return true;
            }

            row = 0;
            return false;
        }

        private static bool TryGetString(object? value, out string text)
        {
            if (value is not null && PyStringOps.TryAsString(value, out var pyText))
            {
                text = pyText.AsString();
                return true;
            }

            text = string.Empty;
            return false;
        }

        private static bool TryParseColumnReference(string text, LythonSourceSpan span, out int column)
        {
            var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            if (normalized.Length == 0 || normalized.Any(c => !char.IsLetter(c)))
            {
                column = 0;
                return false;
            }

            column = ParseColumnName(normalized, span);
            return true;
        }

        private static bool TryParseRowReference(string text, LythonSourceSpan span, out int row)
        {
            var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            if (normalized.Length == 0 ||
                normalized.Any(c => !char.IsDigit(c)) ||
                !int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out row))
            {
                row = 0;
                return false;
            }

            ValidateRowColumn(row, 1, span);
            return true;
        }

        private static bool LooksLikeCellReference(string text)
        {
            var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
            var index = 0;
            while (index < normalized.Length && char.IsLetter(normalized[index]))
            {
                index++;
            }

            return index > 0 &&
                   index < normalized.Length &&
                   normalized.Skip(index).All(char.IsDigit);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<Worksheet \"{Title}\">");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        internal int MinRow
        {
            get
            {
                EnsureDimensions();
                return _minRow;
            }
        }

        internal int MaxRow
        {
            get
            {
                EnsureDimensions();
                return _maxRow;
            }
        }

        internal int MinColumn
        {
            get
            {
                EnsureDimensions();
                return _minColumn;
            }
        }

        internal int MaxColumn
        {
            get
            {
                EnsureDimensions();
                return _maxColumn;
            }
        }

        private void IncludeInDimensions(CellAddress address)
        {
            if (_dimensionsDirty)
            {
                return;
            }

            if (_cells.Count == 1)
            {
                _minRow = address.Row;
                _maxRow = address.Row;
                _minColumn = address.Column;
                _maxColumn = address.Column;
                return;
            }

            _minRow = Math.Min(_minRow, address.Row);
            _maxRow = Math.Max(_maxRow, address.Row);
            _minColumn = Math.Min(_minColumn, address.Column);
            _maxColumn = Math.Max(_maxColumn, address.Column);
        }

        private void InvalidateDimensionsAfterRemoval(CellAddress address)
        {
            if (_cells.Count == 0)
            {
                _minRow = 1;
                _maxRow = 1;
                _minColumn = 1;
                _maxColumn = 1;
                _dimensionsDirty = false;
                return;
            }

            if (address.Row == _minRow ||
                address.Row == _maxRow ||
                address.Column == _minColumn ||
                address.Column == _maxColumn)
            {
                _dimensionsDirty = true;
            }
        }

        private void EnsureDimensions()
        {
            if (!_dimensionsDirty)
            {
                return;
            }

            if (_cells.Count == 0)
            {
                _minRow = 1;
                _maxRow = 1;
                _minColumn = 1;
                _maxColumn = 1;
                _dimensionsDirty = false;
                return;
            }

            var first = true;
            foreach (var address in _cells.Keys)
            {
                if (first)
                {
                    _minRow = address.Row;
                    _maxRow = address.Row;
                    _minColumn = address.Column;
                    _maxColumn = address.Column;
                    first = false;
                    continue;
                }

                _minRow = Math.Min(_minRow, address.Row);
                _maxRow = Math.Max(_maxRow, address.Row);
                _minColumn = Math.Min(_minColumn, address.Column);
                _maxColumn = Math.Max(_maxColumn, address.Column);
            }

            _dimensionsDirty = false;
        }

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
            var name = value is OpenPyxlStyleValue style && style.QualifiedName == "openpyxl.styles.NamedStyle"
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

            var expected = ExpectedStyleType(name);
            if (value is not OpenPyxlStyleValue style || style.QualifiedName != expected)
            {
                throw new LythonRuntimeException("TypeError", "Cell." + name + " expects " + expected + ".", null);
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

        private object AddTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlTable table)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_table(table) expects openpyxl.worksheet.table.Table.", span);
            }

            if (_tables.ContainsKey(table.DisplayName))
            {
                throw new LythonRuntimeException("ValueError", "Table with name " + table.DisplayName + " already exists.", span);
            }

            _tables[table.DisplayName] = table;
            return PyNone.Instance;
        }

        internal void AddDataValidation(OpenPyxlDataValidation validation)
            => _dataValidations.Add(validation);

        internal void AddLoadedConditionalFormatting(string sqref, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules, LythonSourceSpan span)
            => _conditionalFormattings.Add(new OpenPyxlConditionalFormatting(sqref, rules, span));

        internal void AddLoadedDrawing(OpenPyxlLoadedDrawing drawing)
        {
            _drawings.Add(drawing);
            _charts.AddRange(drawing.Charts);
            _images.AddRange(drawing.Images);
        }

        private object AddDataValidation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlDataValidation validation)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_data_validation(data_validation) expects openpyxl.worksheet.datavalidation.DataValidation.", span);
            }

            AddDataValidation(validation);
            return PyNone.Instance;
        }

        private object AddChart(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2 || arguments[0] is not OpenPyxlChartStub chart)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_chart(chart[, anchor]) expects an openpyxl chart.", span);
            }

            EnsureCanMutate(span);
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                chart.Anchor = NormalizeCellReference(arguments[1], "Worksheet.add_chart(anchor)", span);
            }

            throw new LythonRuntimeException("NotImplementedError", "Worksheet.add_chart(...) cannot serialize charts.", span);
        }

        private object AddImage(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 2 || arguments[0] is not OpenPyxlImageStub image)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.add_image(img[, anchor]) expects an openpyxl image.", span);
            }

            EnsureCanMutate(span);
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                image.Anchor = NormalizeCellReference(arguments[1], "Worksheet.add_image(anchor)", span);
            }

            throw new LythonRuntimeException("NotImplementedError", "Worksheet.add_image(...) cannot serialize images.", span);
        }

        private object Cell(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var row = ExpectPositiveInt(arguments[0], "Worksheet.cell(row=...)", span);
            var column = ExpectPositiveInt(arguments[1], "Worksheet.cell(column=...)", span);
            ValidateRowColumn(row, column, span);
            if (arguments.Length >= 3)
            {
                SetCellValue(row, column, arguments[2]);
            }

            return GetCellObject(row, column);
        }

        private object Append(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.append(iterable) expects one iterable argument.", span);
            }

            EnsureCanMutate(span);
            var row = _cells.Count == 0 ? 1 : MaxRow + 1;
            if (arguments[0] is PyDict dict)
            {
                foreach (var pair in dict)
                {
                    context.CheckExecutionBudget(span);
                    SetCellValue(row, ColumnFromAppendKey(pair.Key, span), pair.Value);
                }

                return PyNone.Instance;
            }

            var column = 1;
            foreach (var item in ToSequence(arguments[0], span))
            {
                context.CheckExecutionBudget(span);
                SetCellValue(row, column, item);
                column++;
            }

            return PyNone.Instance;
        }

        private static int ColumnFromAppendKey(object key, LythonSourceSpan span)
        {
            if (PyNumberOps.TryAsInteger(key, out var columnInteger))
            {
                var column = NormalizePositiveInt(columnInteger, "Worksheet.append(...) dictionary key", span);
                ValidateRowColumn(1, column, span);
                return column;
            }

            if (PyStringOps.TryAsString(key, out var columnText))
            {
                return ParseColumnName(columnText.AsString(), span);
            }

            throw new LythonRuntimeException("TypeError", "Worksheet.append(...) dictionary keys must be column letters or one-based column indices.", span);
        }

        private object IterRows(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var bounds = ParseIterationBounds(arguments, span);
            return RowsTuple(bounds.MinRow, bounds.MaxRow, bounds.MinColumn, bounds.MaxColumn, bounds.ValuesOnly, context, span);
        }

        private object IterCols(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (Workbook?.ReadOnly == true)
            {
                throw new LythonRuntimeException("NotImplementedError", "Worksheet.iter_cols(...) is not available for read-only workbooks in Lython.", span);
            }

            var bounds = ParseIterationBounds(arguments, span);
            return ColumnsTuple(bounds.MinRow, bounds.MaxRow, bounds.MinColumn, bounds.MaxColumn, bounds.ValuesOnly, context, span);
        }

        private object CalculateDimension(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet.calculate_dimension() expects no arguments.", span);
            }

            return PyString.FromString(WorksheetDimension(this));
        }

        private object SetPrinterSettings(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            _pageSetup.PaperSize = ExpectPositiveInt(arguments[0], "Worksheet.set_printer_settings(paper_size)", span);
            _pageSetup.Orientation = NormalizePageOrientation(arguments[1], "Worksheet.set_printer_settings(orientation)", span);
            return PyNone.Instance;
        }

        private object MergeCells(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var range = ParseCellRangeArguments(arguments, "Worksheet.merge_cells", span);
            if (_mergedRangeSet.Add(range))
            {
                _mergedRanges.Add(range);
            }

            return PyNone.Instance;
        }

        private object UnmergeCells(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            EnsureCanMutate(span);
            var range = ParseCellRangeArguments(arguments, "Worksheet.unmerge_cells", span);
            if (!_mergedRangeSet.Remove(range))
            {
                throw new LythonRuntimeException("ValueError", $"Cell range {range.Reference} is not merged.", span);
            }

            _mergedRanges.Remove(range);

            return PyNone.Instance;
        }

        private PyList MergedRangeList()
            => new(_mergedRanges.Select(range => (object)PyString.FromString(range.Reference)).ToArray());

        internal void AddLoadedMergedRange(CellRangeAddress range)
        {
            if (_mergedRangeSet.Add(range))
            {
                _mergedRanges.Add(range);
            }
        }

        internal void SetLoadedFreezePanes(string? reference)
        {
            _freezePanes = reference;
        }

        internal void SetLoadedAutoFilter(string? reference)
        {
            _autoFilterRef = reference;
        }

        internal void SetLoadedSheetView(bool showGridLines, bool tabSelected, int workbookViewId)
        {
            _showGridLines = showGridLines;
            _tabSelected = tabSelected;
            _workbookViewId = workbookViewId;
        }

        internal void SetLoadedSelection(string? activeCell, string? sqref, string? pane)
        {
            if (activeCell is not null)
            {
                _selectionActiveCell = activeCell;
            }

            if (sqref is not null)
            {
                _selectionSqref = sqref;
            }

            _selectionPane = pane;
        }

        internal void SetShowGridLines(bool value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _showGridLines = value;
        }

        internal void SetTabSelected(bool value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _tabSelected = value;
        }

        internal void SetWorkbookViewId(int value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _workbookViewId = value;
        }

        internal void SetSelectionActiveCell(string value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionActiveCell = value;
        }

        internal void SetSelectionSqref(string value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionSqref = value;
        }

        internal void SetSelectionPane(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _selectionPane = value;
        }

        internal void SetPrintArea(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printArea = value;
        }

        internal void SetPrintTitleRows(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printTitleRows = value;
        }

        internal void SetPrintTitleCols(string? value, LythonSourceSpan? span)
        {
            EnsureCanMutate(span);
            _printTitleCols = value;
        }

        internal void SetLoadedPrintArea(string? value)
        {
            _printArea = value;
        }

        internal void SetLoadedPrintTitles(string? rows, string? columns)
        {
            _printTitleRows = rows;
            _printTitleCols = columns;
        }

        internal void MarkPageMargins()
        {
            _hasPageMargins = true;
        }

        internal void SetLoadedPageMargins(double left, double right, double top, double bottom, double header, double footer)
        {
            _pageMargins.SetLoaded(left, right, top, bottom, header, footer);
            _hasPageMargins = true;
        }

        internal OpenPyxlColumnDimension GetColumnDimension(int column)
        {
            if (!_columnDimensions.TryGetValue(column, out var dimension))
            {
                dimension = new OpenPyxlColumnDimension(this, column);
                _columnDimensions[column] = dimension;
            }

            return dimension;
        }

        internal OpenPyxlRowDimension GetRowDimension(int row)
        {
            if (!_rowDimensions.TryGetValue(row, out var dimension))
            {
                dimension = new OpenPyxlRowDimension(this, row);
                _rowDimensions[row] = dimension;
            }

            return dimension;
        }

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
            Dictionary<(CellAddress Address, string Name), object> map,
            Func<CellAddress, CellAddress?> rewrite)
        {
            if (map.Count == 0)
            {
                return;
            }

            var rewritten = new Dictionary<(CellAddress Address, string Name), object>();
            foreach (var pair in map)
            {
                var target = rewrite(pair.Key.Address);
                if (target is not null)
                {
                    rewritten[(target.Value, pair.Key.Name)] = pair.Value;
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
            Dictionary<(CellAddress Address, string Name), object> map,
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
                map[(target, pair.Key.Name)] = pair.Value;
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

        private PyTuple ColumnsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var columns = new List<object>();
            for (var column = minColumn; column <= maxColumn; column++)
            {
                context?.CheckExecutionBudget(span);
                columns.Add(ColumnTuple(minRow, maxRow, column, valuesOnly, context, span));
            }

            context?.ObserveCollectionCount(columns.Count, span);
            return context is null ? new PyTuple(columns) : new PyTuple(columns, context.MemoryGovernor, span);
        }

        private PyTuple RowsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var rows = new List<object>();
            for (var row = minRow; row <= maxRow; row++)
            {
                context?.CheckExecutionBudget(span);
                rows.Add(RowTuple(row, minColumn, maxColumn, valuesOnly, context, span));
            }

            context?.ObserveCollectionCount(rows.Count, span);
            return context is null ? new PyTuple(rows) : new PyTuple(rows, context.MemoryGovernor, span);
        }

        private PyTuple RowTuple(int row, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var items = new List<object>();
            for (var column = minColumn; column <= maxColumn; column++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : GetCellObject(row, column));
            }

            return context is null ? new PyTuple(items) : new PyTuple(items, context.MemoryGovernor, span);
        }

        private PyTuple ColumnTuple(int minRow, int maxRow, int column, bool valuesOnly, ExecutionContext? context, LythonSourceSpan? span)
        {
            var items = new List<object>();
            for (var row = minRow; row <= maxRow; row++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : GetCellObject(row, column));
            }

            return context is null ? new PyTuple(items) : new PyTuple(items, context.MemoryGovernor, span);
        }

        private IterationBounds ParseIterationBounds(object[] arguments, LythonSourceSpan span)
        {
            var minRow = OptionalPositiveInt(arguments, 0, 1, "Worksheet.iter_rows", "min_row", span);
            var maxRow = OptionalPositiveInt(arguments, 1, MaxRow, "Worksheet.iter_rows", "max_row", span);
            var minColumn = OptionalPositiveInt(arguments, 2, 1, "Worksheet.iter_rows", "min_col", span);
            var maxColumn = OptionalPositiveInt(arguments, 3, MaxColumn, "Worksheet.iter_rows", "max_col", span);
            var valuesOnly = OptionalBool(arguments, 4, false, "Worksheet.iter_rows", "values_only", span);

            if (maxRow < minRow || maxColumn < minColumn)
            {
                return new IterationBounds(1, 0, 1, 0, valuesOnly);
            }

            ValidateRowColumn(maxRow, maxColumn, span);
            return new IterationBounds(minRow, maxRow, minColumn, maxColumn, valuesOnly);
        }

        private readonly record struct IterationBounds(int MinRow, int MaxRow, int MinColumn, int MaxColumn, bool ValuesOnly);

        internal OpenPyxlCell GetCellObject(int row, int column)
        {
            var address = new CellAddress(row, column);
            if (!_cellObjects.TryGetValue(address, out var cell))
            {
                cell = new OpenPyxlCell(this, row, column);
                _cellObjects[address] = cell;
            }

            return cell;
        }

        internal void EnsureCanMutate(LythonSourceSpan? span)
        {
            if (Workbook?.ReadOnly == true)
            {
                throw new LythonRuntimeException("TypeError", "Worksheet belongs to a read-only workbook.", span);
            }
        }
    }

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
