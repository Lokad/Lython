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
    private static void RewriteDistinctCellRanges(
        List<CellRangeAddress> ranges,
        Func<CellRangeAddress, CellRangeAddress?> rewrite)
    {
        if (ranges.Count == 0)
        {
            return;
        }

        var originalCount = ranges.Count;
        var seen = new HashSet<CellRangeAddress>(originalCount);
        var writeIndex = 0;
        for (var readIndex = 0; readIndex < originalCount; readIndex++)
        {
            var target = rewrite(ranges[readIndex]);
            if (target is not { } rewritten || !seen.Add(rewritten))
            {
                continue;
            }

            ranges[writeIndex++] = rewritten;
        }

        if (writeIndex < originalCount)
        {
            ranges.RemoveRange(writeIndex, originalCount - writeIndex);
        }
    }

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

    internal sealed class OpenPyxlTable : IPyMutableDynamicAttributes, IPyRenderableValue
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

    internal sealed class OpenPyxlTableStyleInfo : IPyMutableDynamicAttributes, IPyRenderableValue
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

    internal sealed class OpenPyxlDataValidation : IPyMutableDynamicAttributes, IPyRenderableValue
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
            => RewriteDistinctCellRanges(_ranges, rewrite);

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
            => RewriteDistinctCellRanges(_ranges, rewrite);
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
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.drawing.image.Image path='{ContentPath(PackagePath)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

}
