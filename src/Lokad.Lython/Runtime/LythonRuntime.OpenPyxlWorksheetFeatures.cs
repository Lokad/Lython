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
    private sealed class OpenPyxlWorksheetModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlWorksheetModule Instance = new();

        private OpenPyxlWorksheetModule()
            : base("openpyxl.worksheet", new Dictionary<string, object>
            {
                ["table"] = OpenPyxlWorksheetTableModule.Instance,
                ["datavalidation"] = OpenPyxlWorksheetDataValidationModule.Instance,
                ["worksheet"] = OpenPyxlWorksheetWorksheetModule.Instance,
            })
        {
        }
    }

    private sealed class OpenPyxlWorksheetWorksheetModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlWorksheetWorksheetModule Instance = new();

        private OpenPyxlWorksheetWorksheetModule()
            : base("openpyxl.worksheet.worksheet", new Dictionary<string, object>
            {
                ["Worksheet"] = UnsupportedOpenPyxlCallable("openpyxl.worksheet.worksheet.Worksheet"),
            })
        {
        }
    }

    private sealed class OpenPyxlWorksheetTableModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlWorksheetTableModule Instance = new();

        private OpenPyxlWorksheetTableModule()
            : base("openpyxl.worksheet.table", new Dictionary<string, object>
            {
                ["Table"] = new BuiltinCallable("openpyxl.worksheet.table.Table", CreateTable, ["displayName", "ref"], requiredCount: 0),
                ["TableStyleInfo"] = new BuiltinCallable("openpyxl.worksheet.table.TableStyleInfo", CreateTableStyleInfo, ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], requiredCount: 0),
            })
        {
        }
    }

    internal sealed class OpenPyxlTable : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlTable(string displayName, string reference)
        {
            DisplayName = displayName;
            Reference = reference;
            TableStyleInfo = PyNone.Instance;
        }

        public string DisplayName { get; private set; }

        public string Reference { get; private set; }

        public string? SourcePath { get; private set; }

        private string? LoadedReference { get; set; }

        public object TableStyleInfo { get; internal set; }

        public bool HasLoadedPartUpdate =>
            SourcePath is not null &&
            !string.Equals(LoadedReference, Reference, StringComparison.Ordinal);

        public void SetLoadedSource(string sourcePath)
        {
            SourcePath = sourcePath;
            LoadedReference = Reference;
        }

        public OpenPyxlTable Copy()
        {
            var copy = new OpenPyxlTable(DisplayName, Reference);
            copy.TableStyleInfo = TableStyleInfo is OpenPyxlTableStyleInfo style ? style.Copy() : TableStyleInfo;
            if (SourcePath is not null)
            {
                copy.SourcePath = SourcePath;
                copy.LoadedReference = LoadedReference;
            }

            return copy;
        }

        public bool RewriteReference(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            var rewritten = rewrite(ParseCellRange(Reference, null));
            if (rewritten is null)
            {
                return false;
            }

            Reference = rewritten.Value.Reference;
            return true;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "displayName" => PyString.FromString(DisplayName),
                "name" => PyString.FromString(DisplayName),
                "ref" => PyString.FromString(Reference),
                "tableStyleInfo" => TableStyleInfo,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "displayName":
                case "name":
                    DisplayName = ExpectString(value, "Table.displayName", null);
                    return true;
                case "ref":
                    Reference = ExpectString(value, "Table.ref", null);
                    return true;
                case "tableStyleInfo":
                    if (value is not PyNone && value is not OpenPyxlTableStyleInfo)
                    {
                        throw new LythonRuntimeException("TypeError", "Table.tableStyleInfo expects openpyxl.worksheet.table.TableStyleInfo or None.", null);
                    }

                    TableStyleInfo = value;
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.table.Table>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlTableStyleInfo : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlTableStyleInfo(string name, bool showFirstColumn, bool showLastColumn, bool showRowStripes, bool showColumnStripes)
        {
            Name = name;
            ShowFirstColumn = showFirstColumn;
            ShowLastColumn = showLastColumn;
            ShowRowStripes = showRowStripes;
            ShowColumnStripes = showColumnStripes;
        }

        public string Name { get; private set; }

        public bool ShowFirstColumn { get; private set; }

        public bool ShowLastColumn { get; private set; }

        public bool ShowRowStripes { get; private set; }

        public bool ShowColumnStripes { get; private set; }

        public OpenPyxlTableStyleInfo Copy() => new(Name, ShowFirstColumn, ShowLastColumn, ShowRowStripes, ShowColumnStripes);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(Name),
                "showFirstColumn" => ShowFirstColumn,
                "showLastColumn" => ShowLastColumn,
                "showRowStripes" => ShowRowStripes,
                "showColumnStripes" => ShowColumnStripes,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "name":
                    Name = ExpectString(value, "TableStyleInfo.name", null);
                    return true;
                case "showFirstColumn":
                    ShowFirstColumn = ExpectBool(value, "TableStyleInfo.showFirstColumn", null);
                    return true;
                case "showLastColumn":
                    ShowLastColumn = ExpectBool(value, "TableStyleInfo.showLastColumn", null);
                    return true;
                case "showRowStripes":
                    ShowRowStripes = ExpectBool(value, "TableStyleInfo.showRowStripes", null);
                    return true;
                case "showColumnStripes":
                    ShowColumnStripes = ExpectBool(value, "TableStyleInfo.showColumnStripes", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.table.TableStyleInfo>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlWorksheetDataValidationModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlWorksheetDataValidationModule Instance = new();

        private OpenPyxlWorksheetDataValidationModule()
            : base("openpyxl.worksheet.datavalidation", new Dictionary<string, object>
            {
                ["DataValidation"] = new BuiltinCallable(
                    "openpyxl.worksheet.datavalidation.DataValidation",
                    CreateDataValidation,
                    ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"],
                    requiredCount: 0),
            })
        {
        }
    }

    internal sealed class OpenPyxlDataValidation : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly List<CellRangeAddress> _ranges = new();

        public OpenPyxlDataValidation(
            string? type,
            string? formula1,
            string? formula2,
            bool allowBlank,
            bool showErrorMessage,
            bool showInputMessage,
            string? operatorValue,
            string? errorTitle,
            string? error,
            string? promptTitle,
            string? prompt)
        {
            Type = type;
            Formula1 = formula1;
            Formula2 = formula2;
            AllowBlank = allowBlank;
            ShowErrorMessage = showErrorMessage;
            ShowInputMessage = showInputMessage;
            Operator = operatorValue;
            ErrorTitle = errorTitle;
            Error = error;
            PromptTitle = promptTitle;
            Prompt = prompt;
        }

        public string? Type { get; private set; }

        public string? Formula1 { get; private set; }

        public string? Formula2 { get; private set; }

        public bool AllowBlank { get; private set; }

        public bool ShowErrorMessage { get; private set; }

        public bool ShowInputMessage { get; private set; }

        public string? Operator { get; private set; }

        public string? ErrorTitle { get; private set; }

        public string? Error { get; private set; }

        public string? PromptTitle { get; private set; }

        public string? Prompt { get; private set; }

        public IReadOnlyList<CellRangeAddress> Ranges => _ranges;

        public string Sqref => string.Join(" ", _ranges.Select(range => range.Reference));

        public OpenPyxlDataValidation Copy()
        {
            var copy = new OpenPyxlDataValidation(
                Type,
                Formula1,
                Formula2,
                AllowBlank,
                ShowErrorMessage,
                ShowInputMessage,
                Operator,
                ErrorTitle,
                Error,
                PromptTitle,
                Prompt);
            foreach (var range in _ranges)
            {
                copy._ranges.Add(range);
            }

            return copy;
        }

        public void AddRange(string reference, LythonSourceSpan span)
            => _ranges.Add(ParseCellOrRange(reference, span));

        internal void RewriteRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            if (_ranges.Count == 0)
            {
                return;
            }

            var rewritten = new List<CellRangeAddress>();
            foreach (var range in _ranges)
            {
                var target = rewrite(range);
                if (target is not null && !rewritten.Contains(target.Value))
                {
                    rewritten.Add(target.Value);
                }
            }

            _ranges.Clear();
            _ranges.AddRange(rewritten);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => OptionalStringValue(Type),
                "formula1" => OptionalStringValue(Formula1),
                "formula2" => OptionalStringValue(Formula2),
                "allow_blank" => AllowBlank,
                "allowBlank" => AllowBlank,
                "showErrorMessage" => ShowErrorMessage,
                "showInputMessage" => ShowInputMessage,
                "operator" => OptionalStringValue(Operator),
                "errorTitle" => OptionalStringValue(ErrorTitle),
                "error" => OptionalStringValue(Error),
                "promptTitle" => OptionalStringValue(PromptTitle),
                "prompt" => OptionalStringValue(Prompt),
                "sqref" => PyString.FromString(Sqref),
                "ranges" => new PyList(_ranges.Select(range => (object)PyString.FromString(range.Reference)).ToArray()),
                "add" => new BoundCallable(Add, "DataValidation.add", ["cell_range"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "type":
                    Type = NullableStringValue(value, "DataValidation.type");
                    return true;
                case "formula1":
                    Formula1 = NullableStringValue(value, "DataValidation.formula1");
                    return true;
                case "formula2":
                    Formula2 = NullableStringValue(value, "DataValidation.formula2");
                    return true;
                case "allow_blank":
                case "allowBlank":
                    AllowBlank = ExpectBool(value, "DataValidation.allow_blank", null);
                    return true;
                case "showErrorMessage":
                    ShowErrorMessage = ExpectBool(value, "DataValidation.showErrorMessage", null);
                    return true;
                case "showInputMessage":
                    ShowInputMessage = ExpectBool(value, "DataValidation.showInputMessage", null);
                    return true;
                case "operator":
                    Operator = NullableStringValue(value, "DataValidation.operator");
                    return true;
                case "errorTitle":
                    ErrorTitle = NullableStringValue(value, "DataValidation.errorTitle");
                    return true;
                case "error":
                    Error = NullableStringValue(value, "DataValidation.error");
                    return true;
                case "promptTitle":
                    PromptTitle = NullableStringValue(value, "DataValidation.promptTitle");
                    return true;
                case "prompt":
                    Prompt = NullableStringValue(value, "DataValidation.prompt");
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.datavalidation.DataValidation>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Add(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "DataValidation.add(cell_range) expects one argument.", span);
            }

            AddRange(ExpectString(arguments[0], "DataValidation.add(cell_range)", span), span);
            return PyNone.Instance;
        }

        private static object OptionalStringValue(string? value)
            => value is null ? PyNone.Instance : PyString.FromString(value);

        private static string? NullableStringValue(object value, string owner)
            => value is PyNone ? null : ExpectString(value, owner, null);
    }

    private sealed class OpenPyxlDataValidationList :
        IPyDynamicAttributes,
        IPyIterableValue,
        IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlDataValidationList(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "dataValidation" => new PyList(_worksheet.DataValidations.Select(validation => (object)validation).ToArray()),
                "count" => new BigInteger(_worksheet.DataValidations.Count),
                "append" => new BoundCallable(Append, "DataValidationList.append", ["data_validation"]),
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

        public IEnumerable<object> Iterate() => _worksheet.DataValidations;

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.worksheet.datavalidation.DataValidationList>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Append(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1 || arguments[0] is not OpenPyxlDataValidation validation)
            {
                throw new LythonRuntimeException("TypeError", "DataValidationList.append(data_validation) expects openpyxl.worksheet.datavalidation.DataValidation.", span);
            }

            _worksheet.AddDataValidation(validation);
            return PyNone.Instance;
        }
    }

    internal sealed class OpenPyxlConditionalFormattingRule : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlConditionalFormattingRule(string? type, string? operatorValue, int? priority, IReadOnlyList<string> formulas) : this(type, operatorValue, priority, formulas, null) { }

        public OpenPyxlConditionalFormattingRule(string? type, string? operatorValue, int? priority, IReadOnlyList<string> formulas, XElement? sourceXml)
        {
            Type = type;
            Operator = operatorValue;
            Priority = priority;
            Formulas = formulas;
            SourceXml = sourceXml;
        }

        public string? Type { get; }

        public string? Operator { get; }

        public int? Priority { get; }

        public IReadOnlyList<string> Formulas { get; }

        internal XElement? SourceXml { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => Type is null ? PyNone.Instance : PyString.FromString(Type),
                "operator" => Operator is null ? PyNone.Instance : PyString.FromString(Operator),
                "priority" => Priority is null ? PyNone.Instance : new BigInteger(Priority.Value),
                "formula" => new PyList(Formulas.Select(formula => (object)PyString.FromString(formula)).ToArray()),
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
            return PyString.FromString("<openpyxl.formatting.rule.Rule>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlConditionalFormatting
    {
        private readonly List<CellRangeAddress> _ranges = new();

        public OpenPyxlConditionalFormatting(string sqref, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules, LythonSourceSpan span)
        {
            foreach (var reference in sqref.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _ranges.Add(ParseCellOrRange(reference, span));
            }

            Rules = rules;
        }

        private OpenPyxlConditionalFormatting(IEnumerable<CellRangeAddress> ranges, IReadOnlyList<OpenPyxlConditionalFormattingRule> rules)
        {
            _ranges.AddRange(ranges);
            Rules = rules;
        }

        public IReadOnlyList<CellRangeAddress> Ranges => _ranges;

        public string Sqref => string.Join(" ", _ranges.Select(range => range.CellOrRangeReference));

        public IReadOnlyList<OpenPyxlConditionalFormattingRule> Rules { get; }

        public OpenPyxlConditionalFormatting Copy()
            => new(_ranges, Rules);

        internal void RewriteRanges(Func<CellRangeAddress, CellRangeAddress?> rewrite)
        {
            if (_ranges.Count == 0)
            {
                return;
            }

            var rewritten = new List<CellRangeAddress>();
            foreach (var range in _ranges)
            {
                var target = rewrite(range);
                if (target is not null && !rewritten.Contains(target.Value))
                {
                    rewritten.Add(target.Value);
                }
            }

            _ranges.Clear();
            _ranges.AddRange(rewritten);
        }
    }

    private sealed class OpenPyxlConditionalFormattingCollection :
        IPyDynamicAttributes,
        IPySubscriptableValue,
        IPyIterableValue,
        IPyRenderableValue
    {
        private readonly OpenPyxlWorksheet _worksheet;

        public OpenPyxlConditionalFormattingCollection(OpenPyxlWorksheet worksheet)
        {
            _worksheet = worksheet;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ranges" => new PyList(_worksheet.ConditionalFormattings.Select(formatting => (object)PyString.FromString(formatting.Sqref)).ToArray()),
                "items" => new BoundCallable(Items, "ConditionalFormattingList.items", []),
                "add" => new BoundCallable(Add, "ConditionalFormattingList.add", ["range_string", "rule"], requiredCount: 2),
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
            var reference = NormalizeCellOrRangeReference(ExpectString(index, "Worksheet.conditional_formatting[...] key", span), span);
            var formatting = _worksheet.ConditionalFormattings.FirstOrDefault(item => string.Equals(item.Sqref, reference, StringComparison.Ordinal));
            return formatting is null
                ? new PyList(Array.Empty<object>())
                : new PyList(formatting.Rules.Cast<object>().ToArray());
        }

        public IEnumerable<object> Iterate()
            => _worksheet.ConditionalFormattings.Select(formatting => (object)PyString.FromString(formatting.Sqref));

        public IEnumerator<object> GetEnumerator() => Iterate().GetEnumerator();

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.formatting.formatting.ConditionalFormattingList>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Items(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "ConditionalFormattingList.items() expects no arguments.", span);
            }

            return new PyList(_worksheet.ConditionalFormattings
                .Select(formatting => (object)new PyTuple(new object[]
                {
                    PyString.FromString(formatting.Sqref),
                    new PyList(formatting.Rules.Cast<object>().ToArray()),
                }))
                .ToArray());
        }

        private static object Add(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", "ConditionalFormattingList.add(...) is not supported by Lython.", span);
        }
    }

    internal sealed class OpenPyxlLoadedDrawing : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly List<OpenPyxlLoadedChart> _charts = new();
        private readonly List<OpenPyxlLoadedImage> _images = new();

        public OpenPyxlLoadedDrawing(string packagePath, string relationshipId)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public IReadOnlyList<OpenPyxlLoadedChart> Charts => _charts;

        public IReadOnlyList<OpenPyxlLoadedImage> Images => _images;

        public void AddChart(OpenPyxlLoadedChart chart)
            => _charts.Add(chart);

        public void AddImage(OpenPyxlLoadedImage image)
            => _images.Add(image);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "charts" => new PyList(_charts.Cast<object>().ToArray()),
                "_charts" => new PyList(_charts.Cast<object>().ToArray()),
                "images" => new PyList(_images.Cast<object>().ToArray()),
                "_images" => new PyList(_images.Cast<object>().ToArray()),
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
            return PyString.FromString($"<openpyxl.drawing.spreadsheet_drawing.SpreadsheetDrawing path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlLoadedChart : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlLoadedChart(string packagePath, string relationshipId, string drawingPath)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
            DrawingPath = drawingPath;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public string DrawingPath { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "drawing_path" => PyString.FromString(ContentPath(DrawingPath)),
                "anchor" => PyNone.Instance,
                "title" => PyNone.Instance,
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
            return PyString.FromString($"<openpyxl.chart._chart.Chart path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlLoadedImage : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlLoadedImage(string packagePath, string relationshipId, string drawingPath)
        {
            PackagePath = packagePath;
            RelationshipId = relationshipId;
            DrawingPath = drawingPath;
        }

        public string PackagePath { get; }

        public string RelationshipId { get; }

        public string DrawingPath { get; }

        public string Format => Path.GetExtension(PackagePath).TrimStart('.').ToLowerInvariant();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "path" => PyString.FromString(ContentPath(PackagePath)),
                "_path" => PyString.FromString(ContentPath(PackagePath)),
                "package_path" => PyString.FromString(PackagePath),
                "relationship_id" => PyString.FromString(RelationshipId),
                "drawing_path" => PyString.FromString(ContentPath(DrawingPath)),
                "format" => PyString.FromString(Format),
                "anchor" => PyNone.Instance,
                "width" => PyNone.Instance,
                "height" => PyNone.Instance,
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
            return PyString.FromString($"<openpyxl.drawing.image.Image path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

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
                "get_column_letter" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlGetColumnLetter, GetColumnLetter),
                "column_index_from_string" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlColumnIndexFromString, ColumnIndexFromString),
                "coordinate_from_string" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlCoordinateFromString, CoordinateFromString),
                "coordinate_to_tuple" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlCoordinateToTuple, CoordinateToTuple),
                "range_boundaries" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlRangeBoundaries, RangeBoundaries),
                "get_column_interval" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlGetColumnInterval, GetColumnInterval),
                "absolute_coordinate" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlAbsoluteCoordinate, AbsoluteCoordinate),
                "quote_sheetname" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlQuoteSheetName, QuoteSheetName),
                "rows_from_range" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlRowsFromRange, RowsFromRange),
                "cols_from_range" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlColsFromRange, ColsFromRange),
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

            return new BigInteger(ParseUtilityColumnName(text.AsString(), span));
        }

        private static object CoordinateFromString(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "coordinate_from_string(coord_string)", span);
            var part = ParseUtilityReferencePart(reference, allowCell: true, allowColumn: false, allowRow: false, span);
            if (part.Row is null or 0)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({reference})", span);
            }

            return new PyTuple(
                [PyString.FromString(ColumnName(part.Column.RequireNotNull())), new BigInteger(part.Row.Value)],
                context.MemoryGovernor,
                span);
        }

        private static object CoordinateToTuple(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var reference = ExpectSingleStringArgument(arguments, "coordinate_to_tuple(coordinate)", span);
            var part = ParseUtilityReferencePart(reference, allowCell: true, allowColumn: false, allowRow: false, span);
            if (part.Row is null or 0)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({reference})", span);
            }

            return new PyTuple(
                [new BigInteger(part.Row.Value), new BigInteger(part.Column.RequireNotNull())],
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
            for (var row = bounds.MinRow.RequireNotNull(); row <= bounds.MaxRow.RequireNotNull(); row++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxColumn.RequireNotNull() - bounds.MinColumn.RequireNotNull() + 1];
                for (var column = bounds.MinColumn.RequireNotNull(); column <= bounds.MaxColumn.RequireNotNull(); column++)
                {
                    cells[column - bounds.MinColumn.RequireNotNull()] = PyString.FromString(CellReference(row, column));
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
            for (var column = bounds.MinColumn.RequireNotNull(); column <= bounds.MaxColumn.RequireNotNull(); column++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxRow.RequireNotNull() - bounds.MinRow.RequireNotNull() + 1];
                for (var row = bounds.MinRow.RequireNotNull(); row <= bounds.MaxRow.RequireNotNull(); row++)
                {
                    cells[row - bounds.MinRow.RequireNotNull()] = PyString.FromString(CellReference(row, column));
                }

                columns.Add(new PyTuple(cells, context.MemoryGovernor, span));
            }

            return new PyList(columns, context.MemoryGovernor, span);
        }

        private const int MaxUtilityColumn = 18278;

        private enum UtilityReferenceKind
        {
            Cell,
            Column,
            Row,
        }

        private readonly record struct UtilityReferencePart(UtilityReferenceKind Kind, int? Column, int? Row);

        private readonly record struct UtilityRangeBoundaries(int? MinColumn, int? MinRow, int? MaxColumn, int? MaxRow);

        private static string ExpectSingleStringArgument(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects one argument.", span);
            }

            return ExpectString(arguments[0], owner, span);
        }

        private static UtilityRangeBoundaries ParseBoundedUtilityRange(string reference, string owner, LythonSourceSpan span)
        {
            var bounds = ParseUtilityRangeBoundaries(reference, span);
            if (bounds.MinColumn is null || bounds.MinRow is null || bounds.MaxColumn is null || bounds.MaxRow is null)
            {
                throw new LythonRuntimeException("TypeError", owner + " expects a bounded cell range.", span);
            }

            return bounds;
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
            if (start.Kind != end.Kind)
            {
                throw new LythonRuntimeException("ValueError", $"{reference} is not a valid coordinate or range", span);
            }

            return start.Kind switch
            {
                UtilityReferenceKind.Cell => new UtilityRangeBoundaries(
                    Math.Min(start.Column.RequireNotNull(), end.Column.RequireNotNull()),
                    Math.Min(start.Row.RequireNotNull(), end.Row.RequireNotNull()),
                    Math.Max(start.Column.RequireNotNull(), end.Column.RequireNotNull()),
                    Math.Max(start.Row.RequireNotNull(), end.Row.RequireNotNull())),
                UtilityReferenceKind.Column => new UtilityRangeBoundaries(
                    Math.Min(start.Column.RequireNotNull(), end.Column.RequireNotNull()),
                    null,
                    Math.Max(start.Column.RequireNotNull(), end.Column.RequireNotNull()),
                    null),
                UtilityReferenceKind.Row => new UtilityRangeBoundaries(
                    null,
                    Math.Min(start.Row.RequireNotNull(), end.Row.RequireNotNull()),
                    null,
                    Math.Max(start.Row.RequireNotNull(), end.Row.RequireNotNull())),
                _ => throw new InvalidOperationException("Unsupported reference kind."),
            };
        }

        private static string AbsoluteUtilityReference(string part, string original, LythonSourceSpan span)
        {
            var reference = ParseUtilityReferencePart(part, allowCell: true, allowColumn: true, allowRow: true, span);
            return reference.Kind switch
            {
                UtilityReferenceKind.Cell => "$" + ColumnName(reference.Column.RequireNotNull()) + "$" + reference.Row.RequireNotNull().ToString(CultureInfo.InvariantCulture),
                UtilityReferenceKind.Column => "$" + ColumnName(reference.Column.RequireNotNull()),
                UtilityReferenceKind.Row => "$" + reference.Row.RequireNotNull().ToString(CultureInfo.InvariantCulture),
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

                return new UtilityReferencePart(UtilityReferenceKind.Column, ParseUtilityColumnName(text, span), null);
            }

            if (letters == 0)
            {
                if (!allowRow || !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                {
                    throw new LythonRuntimeException("ValueError", $"{raw} is not a valid coordinate or range", span);
                }

                return new UtilityReferencePart(UtilityReferenceKind.Row, null, row);
            }

            if (!allowCell || !int.TryParse(text[letters..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var cellRow))
            {
                throw new LythonRuntimeException("ValueError", $"Invalid cell coordinates ({raw})", span);
            }

            return new UtilityReferencePart(UtilityReferenceKind.Cell, ParseUtilityColumnName(text[..letters], span), cellRow);
        }

        private static int ExpectUtilityColumnIndex(object value, string owner, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out var text))
            {
                return ParseUtilityColumnName(text.AsString(), span);
            }

            if (!PyNumberOps.TryAsInteger(value, out var integer) ||
                integer < BigInteger.One ||
                integer > new BigInteger(MaxUtilityColumn))
            {
                throw new LythonRuntimeException("ValueError", owner + " expects a column index or name.", span);
            }

            return (int)integer;
        }

        private static int ParseUtilityColumnName(string text, LythonSourceSpan span)
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

            if (value is < 1 or > MaxUtilityColumn)
            {
                throw new LythonRuntimeException("ValueError", "Invalid column name.", span);
            }

            return value;
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
                ["CellCoordinatesException"] = new ExceptionTypeValue("CellCoordinatesException"),
                ["IllegalCharacterError"] = new ExceptionTypeValue("IllegalCharacterError"),
                ["InvalidFileException"] = new ExceptionTypeValue("InvalidFileException"),
                ["NamedRangeException"] = new ExceptionTypeValue("NamedRangeException"),
                ["ReadOnlyWorkbookException"] = new ExceptionTypeValue("ReadOnlyWorkbookException"),
                ["SheetTitleException"] = new ExceptionTypeValue("SheetTitleException"),
                ["WorkbookAlreadySaved"] = new ExceptionTypeValue("WorkbookAlreadySaved"),
            })
        {
        }
    }

}
