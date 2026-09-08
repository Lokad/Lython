using System.Collections.Frozen;
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
    private static readonly XNamespace XlsxMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace XlsxRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace XmlNamespace = XNamespace.Xml;

    private sealed class OpenPyxlModule : PyModule
    {
        public static readonly OpenPyxlModule Instance = new();

        private OpenPyxlModule() : base("openpyxl")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Workbook" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlWorkbook, Workbook),
                "load_workbook" => BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlLoadWorkbook, LoadWorkbook, LoadWorkbookAsync),
                "__version__" => PyString.FromString("3.1.0+lython.0"),
                "utils" => OpenPyxlUtilsModule.Instance,
                "workbook" => OpenPyxlWorkbookModule.Instance,
                "reader" => OpenPyxlReaderModule.Instance,
                "styles" => OpenPyxlStylesModule.Instance,
                "comments" => OpenPyxlCommentsModule.Instance,
                "chart" => OpenPyxlChartModule.Instance,
                "cell" => OpenPyxlCellModule.Instance,
                "worksheet" => OpenPyxlWorksheetModule.Instance,
                "drawing" => OpenPyxlDrawingModule.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object Workbook(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var writeOnly = OptionalBool(arguments, 0, false, "openpyxl.Workbook", "write_only", span);
            var isoDates = OptionalBool(arguments, 1, false, "openpyxl.Workbook", "iso_dates", span);
            return OpenPyxlWorkbook.CreateNew(writeOnly, isoDates);
        }

        private static object LoadWorkbook(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var request = ParseLoadWorkbookArguments(arguments, span);
            var path = NormalizeWorkbookPath(request.Filename, context, span);
            using var payload = ReadGovernedHostBytes(path, context, span);
            return OpenPyxlPackage.Load(payload.Memory, request.Options, span, context);
        }

        private static async ValueTask<object> LoadWorkbookAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var request = ParseLoadWorkbookArguments(arguments, span);
            var path = NormalizeWorkbookPath(request.Filename, context, span);
            using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
            return OpenPyxlPackage.Load(payload.Memory, request.Options, span, context);
        }

        private static LoadWorkbookRequest ParseLoadWorkbookArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "openpyxl.load_workbook(filename, ...) missing required argument 'filename'.", span);
            }

            var options = OpenPyxlLoadOptions.None;
            if (OptionalBool(arguments, 1, false, "openpyxl.load_workbook", "read_only", span))
            {
                options |= OpenPyxlLoadOptions.ReadOnly;
            }

            if (OptionalBool(arguments, 2, false, "openpyxl.load_workbook", "keep_vba", span))
            {
                options |= OpenPyxlLoadOptions.KeepVba;
            }

            if (OptionalBool(arguments, 3, false, "openpyxl.load_workbook", "data_only", span))
            {
                options |= OpenPyxlLoadOptions.DataOnly;
            }

            if (OptionalBool(arguments, 4, true, "openpyxl.load_workbook", "keep_links", span))
            {
                options |= OpenPyxlLoadOptions.KeepLinks;
            }

            _ = OptionalBool(arguments, 5, false, "openpyxl.load_workbook", "rich_text", span);

            return new LoadWorkbookRequest(arguments[0], options);
        }

        private sealed record LoadWorkbookRequest(object Filename, OpenPyxlLoadOptions Options);
    }

    [Flags]
    private enum OpenPyxlLoadOptions
    {
        None = 0,
        ReadOnly = 1,
        DataOnly = 2,
        KeepLinks = 4,
        KeepVba = 8
    }

    private class OpenPyxlDeferredModule : PyModule
    {
        private readonly FrozenDictionary<string, object> _members;

        public OpenPyxlDeferredModule(string name, IReadOnlyDictionary<string, object> members) : base(name)
        {
            _members = members.ToFrozenDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => _members.TryGetValue(name, out value);
    }

    private static BuiltinCallable UnsupportedOpenPyxlCallable(string qualifiedName)
        => BuiltinCallable.Create(
            qualifiedName,
            (arguments, span, context) =>
            {
                _ = arguments;
                _ = context;
                throw new LythonRuntimeException("NotImplementedError", qualifiedName + " is not implemented.", span);
            });

    private sealed class OpenPyxlCommentsModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlCommentsModule Instance = new();

        private OpenPyxlCommentsModule()
            : base("openpyxl.comments", new Dictionary<string, object>
            {
                ["Comment"] = BuiltinCallable.Create("openpyxl.comments.Comment", CreateComment, ["text", "author"], requiredCount: 2),
            })
        {
        }
    }

    internal sealed class OpenPyxlComment : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlComment(string text, string author)
        {
            Text = text;
            Author = author;
        }

        public string Text { get; private set; }

        public string Author { get; private set; }

        public OpenPyxlComment Copy() => new(Text, Author);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "text" => PyString.FromString(Text),
                "author" => PyString.FromString(Author),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "text":
                    Text = ExpectString(value, "Comment.text", null);
                    return true;
                case "author":
                    Author = ExpectString(value, "Comment.author", null);
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.comments.comments.Comment>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class OpenPyxlChartModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlChartModule Instance = new();

        private OpenPyxlChartModule()
            : base("openpyxl.chart", new Dictionary<string, object>
            {
                ["BarChart"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlBarChart, (arguments, span, context) => CreateChart("BarChart", arguments, span, context)),
                ["LineChart"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlLineChart, (arguments, span, context) => CreateChart("LineChart", arguments, span, context)),
                ["PieChart"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlPieChart, (arguments, span, context) => CreateChart("PieChart", arguments, span, context)),
                ["ScatterChart"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlScatterChart, (arguments, span, context) => CreateChart("ScatterChart", arguments, span, context)),
                ["Reference"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlChartReference, CreateChartReference),
                ["Series"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlChartSeries, CreateChartSeries),
            })
        {
        }
    }

    private sealed class OpenPyxlCellModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlCellModule Instance = new();

        private OpenPyxlCellModule()
            : base("openpyxl.cell", new Dictionary<string, object>
            {
                ["cell"] = OpenPyxlCellCellModule.Instance,
            })
        {
        }
    }

    private sealed class OpenPyxlCellCellModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlCellCellModule Instance = new();

        private OpenPyxlCellCellModule()
            : base("openpyxl.cell.cell", new Dictionary<string, object>
            {
                ["Cell"] = UnsupportedOpenPyxlCallable("openpyxl.cell.cell.Cell"),
            })
        {
        }
    }

    private sealed class OpenPyxlDrawingModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlDrawingModule Instance = new();

        private OpenPyxlDrawingModule()
            : base("openpyxl.drawing", new Dictionary<string, object>
            {
                ["image"] = OpenPyxlDrawingImageModule.Instance,
            })
        {
        }
    }

    private sealed class OpenPyxlDrawingImageModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlDrawingImageModule Instance = new();

        private OpenPyxlDrawingImageModule()
            : base("openpyxl.drawing.image", new Dictionary<string, object>
            {
                ["Image"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlDrawingImage, CreateImage),
            })
        {
        }
    }

    private static object CreateChart(string chartType, object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"openpyxl.chart.{chartType}() expects no arguments.", span);
        }

        return new OpenPyxlChartStub(chartType);
    }

    private static object CreateChartReference(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var worksheet = arguments[0] is PyNone ? null : arguments[0] as OpenPyxlWorksheet;
        if (worksheet is null && arguments[0] is not PyNone)
        {
            throw new LythonRuntimeException("TypeError", "openpyxl.chart.Reference(worksheet, ...) expects a worksheet or None.", span);
        }

        var minColumn = arguments.Length > 1 ? NormalizeOptionalPositiveInt(arguments[1], "Reference.min_col", span) : null;
        var minRow = arguments.Length > 2 ? NormalizeOptionalPositiveInt(arguments[2], "Reference.min_row", span) : null;
        var maxColumn = arguments.Length > 3 ? NormalizeOptionalPositiveInt(arguments[3], "Reference.max_col", span) : null;
        var maxRow = arguments.Length > 4 ? NormalizeOptionalPositiveInt(arguments[4], "Reference.max_row", span) : null;
        var rangeString = arguments.Length > 5 && arguments[5] is not PyNone
            ? ExpectString(arguments[5], "Reference.range_string", span)
            : null;
        return new OpenPyxlChartReference(worksheet, minColumn, minRow, maxColumn, maxRow, rangeString);
    }

    private static object CreateChartSeries(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        _ = span;
        return new OpenPyxlChartSeries(
            arguments.Length > 0 ? arguments[0] : PyNone.Instance,
            arguments.Length > 1 ? arguments[1] : PyNone.Instance,
            arguments.Length > 2 ? arguments[2] : PyNone.Instance);
    }

    private static object CreateImage(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var source = arguments[0] switch
        {
            PyPath path => path.Value.AsString(),
            _ when PyStringOps.TryAsString(arguments[0], out var text) => text.AsString(),
            _ => throw new LythonRuntimeException("TypeError", "openpyxl.drawing.image.Image(img) expects a path-like image reference.", span)
        };

        return new OpenPyxlImageStub(source);
    }

    internal sealed class OpenPyxlChartStub : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        private readonly List<object> _series = new();
        private object? _categories;

        public OpenPyxlChartStub(string chartType)
        {
            ChartType = chartType;
            XAxis = new OpenPyxlChartAxis();
            YAxis = new OpenPyxlChartAxis();
        }

        public string ChartType { get; }

        public string? Title { get; private set; }

        public object? Style { get; private set; }

        public object? Anchor { get; set; }

        public object? Width { get; private set; }

        public object? Height { get; private set; }

        public OpenPyxlChartAxis XAxis { get; }

        public OpenPyxlChartAxis YAxis { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => PyString.FromString(ChartType),
                "path" => PyNone.Instance,
                "_path" => PyNone.Instance,
                "package_path" => PyNone.Instance,
                "relationship_id" => PyNone.Instance,
                "drawing_path" => PyNone.Instance,
                "title" => Title is null ? PyNone.Instance : PyString.FromString(Title),
                "style" => Style ?? PyNone.Instance,
                "anchor" => Anchor ?? PyNone.Instance,
                "width" => Width ?? PyNone.Instance,
                "height" => Height ?? PyNone.Instance,
                "x_axis" => XAxis,
                "y_axis" => YAxis,
                "series" => new PyList(_series),
                "categories" => _categories ?? PyNone.Instance,
                "add_data" => BoundCallable.Create(AddData, "Chart.add_data", ["data", "titles_from_data", "from_rows"], requiredCount: 1),
                "set_categories" => BoundCallable.Create(SetCategories, "Chart.set_categories", ["labels"]),
                "append" => BoundCallable.Create(Append, "Chart.append", ["value"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "title":
                    Title = value is PyNone ? null : ExpectString(value, "Chart.title", null);
                    return true;
                case "style":
                    Style = value is PyNone ? null : value;
                    return true;
                case "anchor":
                    Anchor = value is PyNone ? null : value;
                    return true;
                case "width":
                    Width = NormalizeOptionalNonNegativeDouble(value, "Chart.width", null) ?? null;
                    return true;
                case "height":
                    Height = NormalizeOptionalNonNegativeDouble(value, "Chart.height", null) ?? null;
                    return true;
                default:
                    return false;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.chart.{ChartType}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object AddData(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length is < 1 or > 3)
            {
                throw new LythonRuntimeException("TypeError", "Chart.add_data(data[, titles_from_data][, from_rows]) expects one to three arguments.", span);
            }

            _ = OptionalBool(arguments, 1, false, "Chart.add_data", "titles_from_data", span);
            _ = OptionalBool(arguments, 2, false, "Chart.add_data", "from_rows", span);
            _series.Add(arguments[0]);
            return PyNone.Instance;
        }

        private object SetCategories(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Chart.set_categories(labels) expects one argument.", span);
            }

            _categories = arguments[0];
            return PyNone.Instance;
        }

        private object Append(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Chart.append(value) expects one argument.", span);
            }

            _series.Add(arguments[0]);
            return PyNone.Instance;
        }
    }

    internal sealed class OpenPyxlChartAxis : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        public string? Title { get; private set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "title" => Title is null ? PyNone.Instance : PyString.FromString(Title),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name != "title")
            {
                return false;
            }

            Title = value is PyNone ? null : ExpectString(value, "ChartAxis.title", null);
            return true;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.chart.axis.ChartAxis>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlChartReference : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlChartReference(OpenPyxlWorksheet? worksheet, int? minColumn, int? minRow, int? maxColumn, int? maxRow, string? rangeString)
        {
            Worksheet = worksheet;
            MinColumn = minColumn;
            MinRow = minRow;
            MaxColumn = maxColumn;
            MaxRow = maxRow;
            RangeString = rangeString;
        }

        public OpenPyxlWorksheet? Worksheet { get; }

        public int? MinColumn { get; }

        public int? MinRow { get; }

        public int? MaxColumn { get; }

        public int? MaxRow { get; }

        public string? RangeString { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "worksheet" => Worksheet is null ? PyNone.Instance : Worksheet,
                "min_col" => MinColumn is null ? PyNone.Instance : new BigInteger(MinColumn.Value),
                "min_row" => MinRow is null ? PyNone.Instance : new BigInteger(MinRow.Value),
                "max_col" => MaxColumn is null ? PyNone.Instance : new BigInteger(MaxColumn.Value),
                "max_row" => MaxRow is null ? PyNone.Instance : new BigInteger(MaxRow.Value),
                "range_string" => ReferenceText() is { } text ? PyString.FromString(text) : PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.chart.reference.Reference>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private string? ReferenceText()
        {
            if (RangeString is not null)
            {
                return RangeString;
            }

            if (Worksheet is null || MinColumn is null || MinRow is null)
            {
                return null;
            }

            var maxColumn = MaxColumn ?? MinColumn.Value;
            var maxRow = MaxRow ?? MinRow.Value;
            return $"{Worksheet.Title}!{CellReference(MinRow.Value, MinColumn.Value)}:{CellReference(maxRow, maxColumn)}";
        }
    }

    internal sealed class OpenPyxlChartSeries : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlChartSeries(object values, object xvalues, object title)
        {
            Values = values;
            XValues = xvalues;
            Title = title;
        }

        public object Values { get; }

        public object XValues { get; }

        public object Title { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "values" => Values,
                "xvalues" => XValues,
                "title" => Title,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.chart.series.Series>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlImageStub : IPyMutableDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlImageStub(string source)
        {
            Source = source;
        }

        public string Source { get; }

        public object? Anchor { get; set; }

        public object? Width { get; private set; }

        public object? Height { get; private set; }

        public string Format => PathOps.Suffix(Source).TrimStart('.').ToLowerInvariant();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ref" => PyString.FromString(Source),
                "path" => PyString.FromString(Source),
                "_path" => PyString.FromString(Source),
                "package_path" => PyNone.Instance,
                "relationship_id" => PyNone.Instance,
                "drawing_path" => PyNone.Instance,
                "format" => PyString.FromString(Format),
                "anchor" => Anchor ?? PyNone.Instance,
                "width" => Width ?? PyNone.Instance,
                "height" => Height ?? PyNone.Instance,
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            if (name == "anchor")
            {
                Anchor = value is PyNone ? null : value;
                return true;
            }

            if (name == "width")
            {
                Width = NormalizeOptionalNonNegativeDouble(value, "Image.width", null) ?? null;
                return true;
            }

            if (name == "height")
            {
                Height = NormalizeOptionalNonNegativeDouble(value, "Image.height", null) ?? null;
                return true;
            }

            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<openpyxl.drawing.image.Image ref='{Source}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

}
