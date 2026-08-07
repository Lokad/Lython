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
                "Workbook" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlWorkbook, Workbook),
                "load_workbook" => new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlLoadWorkbook, LoadWorkbook, LoadWorkbookAsync),
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
            return OpenPyxlPackage.Load(payload.Memory, request.DataOnly, request.ReadOnly, request.KeepLinks, request.KeepVba, span);
        }

        private static async ValueTask<object> LoadWorkbookAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var request = ParseLoadWorkbookArguments(arguments, span);
            var path = NormalizeWorkbookPath(request.Filename, context, span);
            using var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
            return OpenPyxlPackage.Load(payload.Memory, request.DataOnly, request.ReadOnly, request.KeepLinks, request.KeepVba, span);
        }

        private static LoadWorkbookRequest ParseLoadWorkbookArguments(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "openpyxl.load_workbook(filename, ...) missing required argument 'filename'.", span);
            }

            var keepVba = OptionalBool(arguments, 2, false, "openpyxl.load_workbook", "keep_vba", span);
            _ = OptionalBool(arguments, 5, false, "openpyxl.load_workbook", "rich_text", span);

            return new LoadWorkbookRequest(
                arguments[0],
                OptionalBool(arguments, 1, false, "openpyxl.load_workbook", "read_only", span),
                OptionalBool(arguments, 3, false, "openpyxl.load_workbook", "data_only", span),
                OptionalBool(arguments, 4, true, "openpyxl.load_workbook", "keep_links", span),
                keepVba);
        }

        private sealed record LoadWorkbookRequest(object Filename, bool ReadOnly, bool DataOnly, bool KeepLinks, bool KeepVba);
    }

    private class OpenPyxlDeferredModule : PyModule
    {
        private readonly Dictionary<string, object> _members;

        public OpenPyxlDeferredModule(string name, IReadOnlyDictionary<string, object> members) : base(name)
        {
            _members = new Dictionary<string, object>(members, StringComparer.Ordinal);
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => _members.TryGetValue(name, out value);
    }

    private static BuiltinCallable UnsupportedOpenPyxlCallable(string qualifiedName)
        => new(
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
                ["Comment"] = new BuiltinCallable("openpyxl.comments.Comment", CreateComment, ["text", "author"], requiredCount: 2),
            })
        {
        }
    }

    internal sealed class OpenPyxlComment : IPyDynamicAttributes, IPyRenderableValue
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
                ["BarChart"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlBarChart, (arguments, span, context) => CreateChart("BarChart", arguments, span, context)),
                ["LineChart"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlLineChart, (arguments, span, context) => CreateChart("LineChart", arguments, span, context)),
                ["PieChart"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlPieChart, (arguments, span, context) => CreateChart("PieChart", arguments, span, context)),
                ["ScatterChart"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlScatterChart, (arguments, span, context) => CreateChart("ScatterChart", arguments, span, context)),
                ["Reference"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlChartReference, CreateChartReference),
                ["Series"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlChartSeries, CreateChartSeries),
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
                ["Image"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlDrawingImage, CreateImage),
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

    internal sealed class OpenPyxlChartStub : IPyDynamicAttributes, IPyRenderableValue
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
                "series" => new PyList(_series.ToArray()),
                "categories" => _categories ?? PyNone.Instance,
                "add_data" => new BoundCallable(AddData, "Chart.add_data", ["data", "titles_from_data", "from_rows"], requiredCount: 1),
                "set_categories" => new BoundCallable(SetCategories, "Chart.set_categories", ["labels"]),
                "append" => new BoundCallable(Append, "Chart.append", ["value"]),
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

    internal sealed class OpenPyxlChartAxis : IPyDynamicAttributes, IPyRenderableValue
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

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
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

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<openpyxl.chart.series.Series>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class OpenPyxlImageStub : IPyDynamicAttributes, IPyRenderableValue
    {
        public OpenPyxlImageStub(string source)
        {
            Source = source;
        }

        public string Source { get; }

        public object? Anchor { get; set; }

        public object? Width { get; private set; }

        public object? Height { get; private set; }

        public string Format => Path.GetExtension(Source).TrimStart('.').ToLowerInvariant();

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

    private sealed class OpenPyxlStylesModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlStylesModule Instance = new();

        private OpenPyxlStylesModule()
            : base("openpyxl.styles", new Dictionary<string, object>
            {
                ["Font"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlFont, CreateFont),
                ["PatternFill"] = new BuiltinCallable("openpyxl.styles.PatternFill", CreatePatternFill, ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], requiredCount: 0),
                ["GradientFill"] = UnsupportedOpenPyxlCallable("openpyxl.styles.GradientFill"),
                ["Border"] = new BuiltinCallable("openpyxl.styles.Border", CreateBorder, ["left", "right", "top", "bottom"], requiredCount: 0),
                ["Side"] = new BuiltinCallable("openpyxl.styles.Side", CreateSide, ["style", "color", "border_style"], requiredCount: 0),
                ["Alignment"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlAlignment, CreateAlignment),
                ["Protection"] = new BuiltinCallable("openpyxl.styles.Protection", CreateProtection, ["locked", "hidden"], requiredCount: 0),
                ["NamedStyle"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlNamedStyle, CreateNamedStyle),
                ["colors"] = OpenPyxlStylesColorsModule.Instance,
            })
        {
        }
    }

    private sealed class OpenPyxlStylesColorsModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlStylesColorsModule Instance = new();

        private OpenPyxlStylesColorsModule()
            : base("openpyxl.styles.colors", new Dictionary<string, object>
            {
                ["Color"] = new BuiltinCallable(LythonKnownCallableSignatures.OpenPyxlColor, CreateColor),
                ["BLACK"] = PyString.FromString("00000000"),
                ["WHITE"] = PyString.FromString("00FFFFFF"),
                ["BLUE"] = PyString.FromString("000000FF"),
            })
        {
        }
    }

    internal sealed class OpenPyxlColor : IPyDynamicAttributes, IPyRenderableValue, IPyStringCoercibleValue, IEquatable<OpenPyxlColor>
    {
        public OpenPyxlColor(
            string type,
            string? rgb,
            BigInteger? indexed,
            BigInteger? theme,
            double tint,
            bool? auto)
        {
            Type = type;
            Rgb = rgb is null ? null : NormalizeRgbColor(rgb, "openpyxl color", null);
            Indexed = indexed;
            Theme = theme;
            Tint = tint;
            Auto = auto;
        }

        public string Type { get; }

        public string? Rgb { get; }

        public BigInteger? Indexed { get; }

        public BigInteger? Theme { get; }

        public double Tint { get; }

        public bool? Auto { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "type" => PyString.FromString(Type),
                "rgb" => OptionalStringValue(Rgb),
                "indexed" => OptionalIntegerValue(Indexed),
                "theme" => OptionalIntegerValue(Theme),
                "tint" => Tint,
                "auto" => Auto is { } auto ? auto : PyNone.Instance,
                "index" => ColorIndexValue(),
                "value" => ColorIndexValue(),
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

        public PyString ToPyString() => PyString.FromString(ColorIndexText());

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return ToPyString();
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(OpenPyxlColor? other)
            => other is not null &&
               string.Equals(Type, other.Type, StringComparison.Ordinal) &&
               string.Equals(Rgb, other.Rgb, StringComparison.Ordinal) &&
               Indexed == other.Indexed &&
               Theme == other.Theme &&
               Tint.Equals(other.Tint) &&
               Auto == other.Auto;

        public override bool Equals(object? obj) => obj is OpenPyxlColor other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Type, Rgb, Indexed, Theme, Tint, Auto);

        public string Key
            => string.Join(
                ":",
                "color",
                Type,
                Rgb ?? string.Empty,
                Indexed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Theme?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Tint.ToString(CultureInfo.InvariantCulture),
                Auto?.ToString() ?? string.Empty);

        private object ColorIndexValue()
            => Type switch
            {
                "rgb" => OptionalStringValue(Rgb),
                "indexed" => OptionalIntegerValue(Indexed),
                "theme" => OptionalIntegerValue(Theme),
                "auto" => Auto is { } auto ? auto : PyNone.Instance,
                _ => OptionalStringValue(Rgb),
            };

        private string ColorIndexText()
            => Type switch
            {
                "rgb" => Rgb ?? string.Empty,
                "indexed" => Indexed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "theme" => Theme?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                "auto" => Auto is { } auto ? (auto ? "1" : "0") : string.Empty,
                _ => Rgb ??
                    Indexed?.ToString(CultureInfo.InvariantCulture) ??
                    Theme?.ToString(CultureInfo.InvariantCulture) ??
                    string.Empty,
            };

        private static object OptionalStringValue(string? value)
            => value is null ? PyNone.Instance : PyString.FromString(value);

        private static object OptionalIntegerValue(BigInteger? value)
            => value is null ? PyNone.Instance : value.Value;
    }

    internal sealed class OpenPyxlStyleValue : IPyDynamicAttributes, IPyRenderableValue, IEquatable<OpenPyxlStyleValue>
    {
        private readonly Dictionary<string, object> _members;

        public OpenPyxlStyleValue(string qualifiedName, IReadOnlyDictionary<string, object> members)
        {
            QualifiedName = qualifiedName;
            _members = new Dictionary<string, object>(members, StringComparer.Ordinal);
        }

        public string QualifiedName { get; }

        public OpenPyxlStyleValue Copy(bool deep)
        {
            var members = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in _members)
            {
                members[pair.Key] = deep && pair.Value is OpenPyxlStyleValue style
                    ? style.Copy(deep: true)
                    : pair.Value;
            }

            return new OpenPyxlStyleValue(QualifiedName, members);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => _members.TryGetValue(name, out value);

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            if (QualifiedName == "openpyxl.styles.NamedStyle" &&
                TryGetMember("name", out var name) &&
                PyStringOps.TryAsString(name, out var text))
            {
                return text;
            }

            return PyString.FromString("<" + QualifiedName + ">");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(OpenPyxlStyleValue? other) => StyleValuesEqual(this, other);

        public override bool Equals(object? obj) => obj is OpenPyxlStyleValue other && Equals(other);

        public override int GetHashCode() => StyleValueHashCode(this);
    }

    private static bool StyleValuesEqual(OpenPyxlStyleValue? left, OpenPyxlStyleValue? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || !string.Equals(left.QualifiedName, right.QualifiedName, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var name in ComparableStyleMemberNames(left.QualifiedName))
        {
            var leftValue = ComparableStyleValue(left, name);
            var rightValue = ComparableStyleValue(right, name);
            if (!StyleObjectsEqual(leftValue, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool StyleObjectsEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right) || IsMissingStyleValue(left) && IsMissingStyleValue(right))
        {
            return true;
        }

        return (left, right) switch
        {
            (OpenPyxlStyleValue leftStyle, OpenPyxlStyleValue rightStyle) => StyleValuesEqual(leftStyle, rightStyle),
            (OpenPyxlColor leftColor, OpenPyxlColor rightColor) => leftColor.Equals(rightColor),
            (null or PyNone, _) or (_, null or PyNone) => false,
            _ => PyEquality.AreEqual(left.RequireNotNull(), right.RequireNotNull()),
        };
    }

    private static bool IsMissingStyleValue(object? value) => value is null or PyNone;

    private static object? ComparableStyleValue(OpenPyxlStyleValue style, string name)
        => style.TryGetMember(name, out var value) && value is not PyNone ? value : null;

    private static int StyleValueHashCode(OpenPyxlStyleValue style)
    {
        var hash = new HashCode();
        hash.Add(style.QualifiedName, StringComparer.Ordinal);
        foreach (var name in ComparableStyleMemberNames(style.QualifiedName))
        {
            hash.Add(name, StringComparer.Ordinal);
            hash.Add(StyleObjectHashCode(ComparableStyleValue(style, name)));
        }

        return hash.ToHashCode();
    }

    private static int StyleObjectHashCode(object? value)
    {
        if (IsMissingStyleValue(value))
        {
            return 0;
        }

        if (value is OpenPyxlStyleValue style)
        {
            return StyleValueHashCode(style);
        }

        if (value is OpenPyxlColor color)
        {
            return color.GetHashCode();
        }

        try
        {
            return PyValueComparer.Instance.GetHashCode(value.RequireNotNull());
        }
        catch (InvalidOperationException)
        {
            return value.RequireNotNull().GetHashCode();
        }
    }

    private static string[] ComparableStyleMemberNames(string qualifiedName)
        => qualifiedName switch
        {
            "openpyxl.styles.Font" => ["name", "sz", "bold", "italic", "color", "underline", "strike"],
            "openpyxl.styles.PatternFill" => ["fill_type", "fgColor", "bgColor"],
            "openpyxl.styles.Border" => ["left", "right", "top", "bottom"],
            "openpyxl.styles.Side" => ["style", "color"],
            "openpyxl.styles.Alignment" => ["horizontal", "vertical", "wrap_text", "text_rotation", "shrink_to_fit"],
            "openpyxl.styles.Protection" => ["locked", "hidden"],
            "openpyxl.styles.NamedStyle" => ["name", "number_format", "font", "fill", "border", "alignment", "protection"],
            _ => [],
        };

    private static object CreateColor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var rgb = OptionalColorString(arguments, 0, "openpyxl.styles.colors.Color.rgb", span);
        var indexed = OptionalColorInteger(arguments, 1, "openpyxl.styles.colors.Color.indexed", span);
        var auto = OptionalColorBool(arguments, 2, "openpyxl.styles.colors.Color.auto", span);
        var theme = OptionalColorInteger(arguments, 3, "openpyxl.styles.colors.Color.theme", span);
        var tint = OptionalColorDouble(arguments, 4, 0d, "openpyxl.styles.colors.Color.tint", span);
        var explicitType = OptionalColorString(arguments, 5, "openpyxl.styles.colors.Color.type", span);
        var type = explicitType ??
            (indexed is not null ? "indexed" :
                auto is not null ? "auto" :
                theme is not null ? "theme" :
                "rgb");

        return new OpenPyxlColor(type, rgb, indexed, theme, tint, auto);
    }

    private static object CreateFont(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = context;
        var bold = OptionalStyleBool(arguments, 2, OptionalStyleBool(arguments, 6, false, "openpyxl.styles.Font.b", span), "openpyxl.styles.Font.bold", span);
        var italic = OptionalStyleBool(arguments, 3, OptionalStyleBool(arguments, 7, false, "openpyxl.styles.Font.i", span), "openpyxl.styles.Font.italic", span);
        var sizeValue = FirstStyleValue(arguments, 8, 1);
        var size = sizeValue is PyNone
            ? (object)PyNone.Instance
            : NormalizeOptionalNonNegativeDouble(sizeValue, "openpyxl.styles.Font.size", span).RequireNotNull();
        var underline = FirstStyleValue(arguments, 5, 9);
        var strike = OptionalStyleBool(arguments, 11, OptionalStyleBool(arguments, 10, false, "openpyxl.styles.Font.strike", span), "openpyxl.styles.Font.strikethrough", span);
        return new OpenPyxlStyleValue("openpyxl.styles.Font", new Dictionary<string, object>
        {
            ["name"] = OptionalStyleValue(arguments, 0),
            ["sz"] = size,
            ["size"] = size,
            ["bold"] = bold,
            ["b"] = bold,
            ["italic"] = italic,
            ["i"] = italic,
            ["color"] = OptionalColorStyleValue(arguments, 4),
            ["underline"] = underline,
            ["u"] = underline,
            ["strike"] = strike,
            ["strikethrough"] = strike,
        });
    }

    private static object CreatePatternFill(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = span;
        _ = context;
        var fillType = FirstStyleValue(arguments, 0, 5);
        return new OpenPyxlStyleValue("openpyxl.styles.PatternFill", new Dictionary<string, object>
        {
            ["fill_type"] = fillType,
            ["patternType"] = fillType,
            ["start_color"] = FirstColorStyleValue(arguments, 1, 3),
            ["fgColor"] = FirstColorStyleValue(arguments, 3, 1),
            ["end_color"] = FirstColorStyleValue(arguments, 2, 4),
            ["bgColor"] = FirstColorStyleValue(arguments, 4, 2),
        });
    }

    private static object CreateBorder(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = span;
        _ = context;
        return new OpenPyxlStyleValue("openpyxl.styles.Border", new Dictionary<string, object>
        {
            ["left"] = OptionalStyleValue(arguments, 0),
            ["right"] = OptionalStyleValue(arguments, 1),
            ["top"] = OptionalStyleValue(arguments, 2),
            ["bottom"] = OptionalStyleValue(arguments, 3),
        });
    }

    private static object CreateSide(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = span;
        _ = context;
        var style = FirstStyleValue(arguments, 0, 2);
        return new OpenPyxlStyleValue("openpyxl.styles.Side", new Dictionary<string, object>
        {
            ["style"] = style,
            ["border_style"] = style,
            ["color"] = OptionalColorStyleValue(arguments, 1),
        });
    }

    private static object CreateAlignment(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = context;
        var wrapText = OptionalStyleBool(arguments, 2, OptionalStyleBool(arguments, 4, false, "openpyxl.styles.Alignment.wrapText", span), "openpyxl.styles.Alignment.wrap_text", span);
        var textRotation = FirstStyleValue(arguments, 3, 5);
        var shrinkToFit = OptionalStyleBool(arguments, 7, OptionalStyleBool(arguments, 6, false, "openpyxl.styles.Alignment.shrinkToFit", span), "openpyxl.styles.Alignment.shrink_to_fit", span);
        return new OpenPyxlStyleValue("openpyxl.styles.Alignment", new Dictionary<string, object>
        {
            ["horizontal"] = OptionalStyleValue(arguments, 0),
            ["vertical"] = OptionalStyleValue(arguments, 1),
            ["wrap_text"] = wrapText,
            ["wrapText"] = wrapText,
            ["text_rotation"] = textRotation,
            ["textRotation"] = textRotation,
            ["shrink_to_fit"] = shrinkToFit,
            ["shrinkToFit"] = shrinkToFit,
        });
    }

    private static object CreateProtection(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = context;
        return new OpenPyxlStyleValue("openpyxl.styles.Protection", new Dictionary<string, object>
        {
            ["locked"] = OptionalStyleBool(arguments, 0, true, "openpyxl.styles.Protection.locked", span),
            ["hidden"] = OptionalStyleBool(arguments, 1, false, "openpyxl.styles.Protection.hidden", span),
        });
    }

    private static object CreateNamedStyle(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = span;
        _ = context;
        var name = OptionalStyleValue(arguments, 0);
        if (name is PyNone)
        {
            name = PyString.FromString("Normal");
        }

        return CreateNamedStyleValue(
            name,
            OptionalStyleValue(arguments, 5),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 1), "font"),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 2), "fill"),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 3), "border"),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 4), "alignment"),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 6), "protection"));
    }

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name)
        => CreateNamedStyleValue(name, null, null, null, null, null, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name, object? numberFormat)
        => CreateNamedStyleValue(name, numberFormat, null, null, null, null, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name, object? numberFormat, object? font)
        => CreateNamedStyleValue(name, numberFormat, font, null, null, null, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name, object? numberFormat, object? font, object? fill)
        => CreateNamedStyleValue(name, numberFormat, font, fill, null, null, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name, object? numberFormat, object? font, object? fill, object? border)
        => CreateNamedStyleValue(name, numberFormat, font, fill, border, null, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name, object? numberFormat, object? font, object? fill, object? border, object? alignment)
        => CreateNamedStyleValue(name, numberFormat, font, fill, border, alignment, null);

    private static OpenPyxlStyleValue CreateNamedStyleValue(
        object name,
        object? numberFormat,
        object? font,
        object? fill,
        object? border,
        object? alignment,
        object? protection)
    {
        return new OpenPyxlStyleValue("openpyxl.styles.NamedStyle", new Dictionary<string, object>
        {
            ["name"] = name,
            ["number_format"] = numberFormat ?? PyNone.Instance,
            ["font"] = font ?? PyNone.Instance,
            ["fill"] = fill ?? PyNone.Instance,
            ["border"] = border ?? PyNone.Instance,
            ["alignment"] = alignment ?? PyNone.Instance,
            ["protection"] = protection ?? PyNone.Instance,
        });
    }

    private static object NormalizeNamedStyleComponent(object value, string name)
    {
        if (value is PyNone)
        {
            return PyNone.Instance;
        }

        var expected = ExpectedStyleType(name);
        return value is OpenPyxlStyleValue style && style.QualifiedName == expected
            ? style
            : throw new LythonRuntimeException("TypeError", "NamedStyle." + name + " expects " + expected + ".", null);
    }

    private static string NamedStyleName(OpenPyxlStyleValue style, LythonSourceSpan? span)
    {
        if (!style.TryGetMember("name", out var value))
        {
            return "Normal";
        }

        return ExpectString(value, "NamedStyle.name", span);
    }

    private static OpenPyxlStyleValue? NamedStyleComponent(OpenPyxlStyleValue style, string name)
        => style.TryGetMember(name, out var value) &&
           value is OpenPyxlStyleValue component &&
           component.QualifiedName == ExpectedStyleType(name)
            ? component
            : null;

    private static string? NamedStyleNumberFormat(OpenPyxlStyleValue style)
        => style.TryGetMember("number_format", out var value) &&
           value is not PyNone &&
           PyStringOps.TryAsString(value, out var text)
            ? text.AsString()
            : null;

    private static object CreateComment(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var text = ExpectString(arguments[0], "openpyxl.comments.Comment(text)", span);
        var author = ExpectString(arguments[1], "openpyxl.comments.Comment(author)", span);
        return new OpenPyxlComment(text, author);
    }

    private static object CreateTable(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var displayName = arguments.Length > 0 && arguments[0] is not PyNone
            ? ExpectString(arguments[0], "openpyxl.worksheet.table.Table(displayName)", span)
            : "Table1";
        var reference = arguments.Length > 1 && arguments[1] is not PyNone
            ? ExpectString(arguments[1], "openpyxl.worksheet.table.Table(ref)", span)
            : "A1";
        return new OpenPyxlTable(displayName, reference);
    }

    private static object CreateTableStyleInfo(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var name = arguments.Length > 0 && arguments[0] is not PyNone
            ? ExpectString(arguments[0], "openpyxl.worksheet.table.TableStyleInfo(name)", span)
            : "TableStyleMedium2";
        return new OpenPyxlTableStyleInfo(
            name,
            OptionalStyleBool(arguments, 1, false, "openpyxl.worksheet.table.TableStyleInfo.showFirstColumn", span),
            OptionalStyleBool(arguments, 2, false, "openpyxl.worksheet.table.TableStyleInfo.showLastColumn", span),
            OptionalStyleBool(arguments, 3, true, "openpyxl.worksheet.table.TableStyleInfo.showRowStripes", span),
            OptionalStyleBool(arguments, 4, false, "openpyxl.worksheet.table.TableStyleInfo.showColumnStripes", span));
    }

    private static object CreateDataValidation(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return new OpenPyxlDataValidation(
            OptionalNullableString(arguments, 0, "openpyxl.worksheet.datavalidation.DataValidation.type", span),
            OptionalNullableString(arguments, 1, "openpyxl.worksheet.datavalidation.DataValidation.formula1", span),
            OptionalNullableString(arguments, 2, "openpyxl.worksheet.datavalidation.DataValidation.formula2", span),
            OptionalStyleBool(arguments, 3, false, "openpyxl.worksheet.datavalidation.DataValidation.allow_blank", span),
            OptionalStyleBool(arguments, 4, true, "openpyxl.worksheet.datavalidation.DataValidation.showErrorMessage", span),
            OptionalStyleBool(arguments, 5, true, "openpyxl.worksheet.datavalidation.DataValidation.showInputMessage", span),
            OptionalNullableString(arguments, 6, "openpyxl.worksheet.datavalidation.DataValidation.operator", span),
            OptionalNullableString(arguments, 7, "openpyxl.worksheet.datavalidation.DataValidation.errorTitle", span),
            OptionalNullableString(arguments, 8, "openpyxl.worksheet.datavalidation.DataValidation.error", span),
            OptionalNullableString(arguments, 9, "openpyxl.worksheet.datavalidation.DataValidation.promptTitle", span),
            OptionalNullableString(arguments, 10, "openpyxl.worksheet.datavalidation.DataValidation.prompt", span));
    }

    private static string? OptionalNullableString(object[] arguments, int index, string owner, LythonSourceSpan span)
        => arguments.Length <= index || arguments[index] is PyNone
            ? null
            : ExpectString(arguments[index], owner, span);

    private static object NormalizeDimensionStyle(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return PyNone.Instance;
        }

        return value is OpenPyxlStyleValue style && style.QualifiedName == "openpyxl.styles.NamedStyle"
            ? PyString.FromString(NamedStyleName(style, span))
            : PyString.FromString(ExpectString(value, owner, span));
    }

    private static object OptionalStyleValue(object[] arguments, int index)
        => arguments.Length > index && arguments[index] is not PyNone ? arguments[index] : PyNone.Instance;

    private static object FirstStyleValue(object[] arguments, int first, int second)
    {
        var value = OptionalStyleValue(arguments, first);
        return value is PyNone ? OptionalStyleValue(arguments, second) : value;
    }

    private static object OptionalColorStyleValue(object[] arguments, int index)
        => NormalizeColorStyleValue(OptionalStyleValue(arguments, index));

    private static object FirstColorStyleValue(object[] arguments, int first, int second)
    {
        var value = OptionalColorStyleValue(arguments, first);
        return value is PyNone ? OptionalColorStyleValue(arguments, second) : value;
    }

    private static object NormalizeColorStyleValue(object value)
    {
        if (value is PyNone or OpenPyxlColor)
        {
            return value;
        }

        return PyStringOps.TryAsString(value, out var text)
            ? new OpenPyxlColor("rgb", NormalizeRgbColor(text.AsString(), "openpyxl style color", null), null, null, 0d, null)
            : value;
    }

    private static string NormalizeRgbColor(string value, string owner, LythonSourceSpan? span)
    {
        if (value.Length is not (6 or 8) || value.Any(static character => !Uri.IsHexDigit(character)))
        {
            throw new LythonRuntimeException("ValueError", owner + " expects a 6- or 8-digit hexadecimal RGB value.", span);
        }

        return value.Length == 6 ? "00" + value : value;
    }

    private static string? OptionalColorString(object[] arguments, int index, string owner, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return null;
        }

        return PyStringOps.TryAsString(arguments[index], out var text)
            ? text.AsString()
            : throw new LythonRuntimeException("TypeError", owner + " expects a string.", span);
    }

    private static BigInteger? OptionalColorInteger(object[] arguments, int index, string owner, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return null;
        }

        return PyNumberOps.TryAsInteger(arguments[index], out var integer)
            ? integer
            : throw new LythonRuntimeException("TypeError", owner + " expects an integer.", span);
    }

    private static bool? OptionalColorBool(object[] arguments, int index, string owner, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return null;
        }

        return arguments[index] is bool value
            ? value
            : throw new LythonRuntimeException("TypeError", owner + " expects a bool.", span);
    }

    private static double OptionalColorDouble(object[] arguments, int index, double defaultValue, string owner, LythonSourceSpan span)
    {
        if (arguments.Length <= index || arguments[index] is PyNone)
        {
            return defaultValue;
        }

        return arguments[index] switch
        {
            double floating => floating,
            BigInteger integer => (double)integer,
            int integer => integer,
            _ => throw new LythonRuntimeException("TypeError", owner + " expects a number.", span),
        };
    }

    private static bool OptionalStyleBool(object[] arguments, int index, bool defaultValue, string owner, LythonSourceSpan? span)
        => arguments.Length <= index || arguments[index] is PyNone
            ? defaultValue
            : arguments[index] is bool value
                ? value
                : throw new LythonRuntimeException("TypeError", owner + " expects a bool.", span);

    private static object DefaultCellStyle(string name)
        => name switch
        {
            "font" => CreateFont([], null, null),
            "fill" => CreatePatternFill([], null, null),
            "border" => CreateBorder([], null, null),
            "alignment" => CreateAlignment([], null, null),
            "protection" => CreateProtection([], null, null),
            _ => PyNone.Instance,
        };

    private static string ExpectedStyleType(string name)
        => name switch
        {
            "font" => "openpyxl.styles.Font",
            "fill" => "openpyxl.styles.PatternFill",
            "border" => "openpyxl.styles.Border",
            "alignment" => "openpyxl.styles.Alignment",
            "protection" => "openpyxl.styles.Protection",
            _ => "openpyxl style",
        };

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
