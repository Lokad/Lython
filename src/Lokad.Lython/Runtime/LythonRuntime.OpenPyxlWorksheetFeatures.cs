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
    internal enum OpenPyxlDataValidationType
    {
        Whole,
        Decimal,
        List,
        Date,
        Time,
        TextLength,
        Custom,
    }

    internal enum OpenPyxlDataValidationOperator
    {
        Between,
        NotBetween,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

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
                ["Table"] = BuiltinCallable.Create("openpyxl.worksheet.table.Table", CreateTable, ["displayName", "ref"], requiredCount: 0),
                ["TableStyleInfo"] = BuiltinCallable.Create("openpyxl.worksheet.table.TableStyleInfo", CreateTableStyleInfo, ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], requiredCount: 0),
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
                ["DataValidation"] = BuiltinCallable.Create(
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
            OpenPyxlDataValidationType? type,
            string? formula1,
            string? formula2,
            bool allowBlank,
            bool showErrorMessage,
            bool showInputMessage,
            OpenPyxlDataValidationOperator? operatorValue,
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

        public OpenPyxlDataValidationType? Type { get; private set; }

        public string? Formula1 { get; private set; }

        public string? Formula2 { get; private set; }

        public bool AllowBlank { get; private set; }

        public bool ShowErrorMessage { get; private set; }

        public bool ShowInputMessage { get; private set; }

        public OpenPyxlDataValidationOperator? Operator { get; private set; }

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
                "type" => OptionalStringValue(FormatDataValidationType(Type)),
                "formula1" => OptionalStringValue(Formula1),
                "formula2" => OptionalStringValue(Formula2),
                "allow_blank" => AllowBlank,
                "allowBlank" => AllowBlank,
                "showErrorMessage" => ShowErrorMessage,
                "showInputMessage" => ShowInputMessage,
                "operator" => OptionalStringValue(FormatDataValidationOperator(Operator)),
                "errorTitle" => OptionalStringValue(ErrorTitle),
                "error" => OptionalStringValue(Error),
                "promptTitle" => OptionalStringValue(PromptTitle),
                "prompt" => OptionalStringValue(Prompt),
                "sqref" => PyString.FromString(Sqref),
                "ranges" => new PyList(_ranges.Select(range => (object)PyString.FromString(range.Reference))),
                "add" => BoundCallable.Create(Add, "DataValidation.add", ["cell_range"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "type":
                    Type = ParseDataValidationType(NullableStringValue(value, "DataValidation.type"), "DataValidation.type", null);
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
                    Operator = ParseDataValidationOperator(NullableStringValue(value, "DataValidation.operator"), "DataValidation.operator", null);
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

    }

    private static OpenPyxlDataValidationType? ParseDataValidationType(string? value, string owner, LythonSourceSpan? span)
    {
        return value switch
        {
            null => null,
            "whole" => OpenPyxlDataValidationType.Whole,
            "decimal" => OpenPyxlDataValidationType.Decimal,
            "list" => OpenPyxlDataValidationType.List,
            "date" => OpenPyxlDataValidationType.Date,
            "time" => OpenPyxlDataValidationType.Time,
            "textLength" => OpenPyxlDataValidationType.TextLength,
            "custom" => OpenPyxlDataValidationType.Custom,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} does not support value '{value}'.", span),
        };
    }

    private static string? FormatDataValidationType(OpenPyxlDataValidationType? value)
    {
        return value switch
        {
            null => null,
            OpenPyxlDataValidationType.Whole => "whole",
            OpenPyxlDataValidationType.Decimal => "decimal",
            OpenPyxlDataValidationType.List => "list",
            OpenPyxlDataValidationType.Date => "date",
            OpenPyxlDataValidationType.Time => "time",
            OpenPyxlDataValidationType.TextLength => "textLength",
            OpenPyxlDataValidationType.Custom => "custom",
            _ => throw new InvalidOperationException($"Unknown data-validation type '{value}'."),
        };
    }

    private static OpenPyxlDataValidationOperator? ParseDataValidationOperator(string? value, string owner, LythonSourceSpan? span)
    {
        return value switch
        {
            null => null,
            "between" => OpenPyxlDataValidationOperator.Between,
            "notBetween" => OpenPyxlDataValidationOperator.NotBetween,
            "equal" => OpenPyxlDataValidationOperator.Equal,
            "notEqual" => OpenPyxlDataValidationOperator.NotEqual,
            "lessThan" => OpenPyxlDataValidationOperator.LessThan,
            "lessThanOrEqual" => OpenPyxlDataValidationOperator.LessThanOrEqual,
            "greaterThan" => OpenPyxlDataValidationOperator.GreaterThan,
            "greaterThanOrEqual" => OpenPyxlDataValidationOperator.GreaterThanOrEqual,
            _ => throw new LythonRuntimeException("ValueError", $"{owner} does not support value '{value}'.", span),
        };
    }

    private static string? FormatDataValidationOperator(OpenPyxlDataValidationOperator? value)
    {
        return value switch
        {
            null => null,
            OpenPyxlDataValidationOperator.Between => "between",
            OpenPyxlDataValidationOperator.NotBetween => "notBetween",
            OpenPyxlDataValidationOperator.Equal => "equal",
            OpenPyxlDataValidationOperator.NotEqual => "notEqual",
            OpenPyxlDataValidationOperator.LessThan => "lessThan",
            OpenPyxlDataValidationOperator.LessThanOrEqual => "lessThanOrEqual",
            OpenPyxlDataValidationOperator.GreaterThan => "greaterThan",
            OpenPyxlDataValidationOperator.GreaterThanOrEqual => "greaterThanOrEqual",
            _ => throw new InvalidOperationException($"Unknown data-validation operator '{value}'."),
        };
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
                "dataValidation" => new PyList(_worksheet.DataValidations.Select(validation => (object)validation)),
                "count" => new BigInteger(_worksheet.DataValidations.Count),
                "append" => BoundCallable.Create(Append, "DataValidationList.append", ["data_validation"]),
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

}
