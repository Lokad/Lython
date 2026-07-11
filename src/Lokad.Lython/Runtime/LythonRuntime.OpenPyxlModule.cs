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

        public override bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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
            var payload = ReadGovernedHostBytes(path, context, span);
            return OpenPyxlPackage.Load(payload, request.DataOnly, request.ReadOnly, request.KeepLinks, request.KeepVba, span);
        }

        private static async ValueTask<object> LoadWorkbookAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var request = ParseLoadWorkbookArguments(arguments, span);
            var path = NormalizeWorkbookPath(request.Filename, context, span);
            var payload = await ReadGovernedHostBytesAsync(path, context, span).ConfigureAwait(false);
            return OpenPyxlPackage.Load(payload, request.DataOnly, request.ReadOnly, request.KeepLinks, request.KeepVba, span);
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

        public override bool TryGetMember(string name, out object value)
            => _members.TryGetValue(name, out value!);
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "text" => PyString.FromString(Text),
                "author" => PyString.FromString(Author),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "title" => Title is null ? PyNone.Instance : PyString.FromString(Title),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "worksheet" => Worksheet is null ? PyNone.Instance : Worksheet,
                "min_col" => MinColumn is null ? PyNone.Instance : new BigInteger(MinColumn.Value),
                "min_row" => MinRow is null ? PyNone.Instance : new BigInteger(MinRow.Value),
                "max_col" => MaxColumn is null ? PyNone.Instance : new BigInteger(MaxColumn.Value),
                "max_row" => MaxRow is null ? PyNone.Instance : new BigInteger(MaxRow.Value),
                "range_string" => ReferenceText() is { } text ? PyString.FromString(text) : PyNone.Instance,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "values" => Values,
                "xvalues" => XValues,
                "title" => Title,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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
                ["Font"] = new BuiltinCallable("openpyxl.styles.Font", CreateFont, ["name", "sz", "bold", "italic", "color", "underline", "b", "i"], requiredCount: 0),
                ["PatternFill"] = new BuiltinCallable("openpyxl.styles.PatternFill", CreatePatternFill, ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], requiredCount: 0),
                ["GradientFill"] = UnsupportedOpenPyxlCallable("openpyxl.styles.GradientFill"),
                ["Border"] = new BuiltinCallable("openpyxl.styles.Border", CreateBorder, ["left", "right", "top", "bottom"], requiredCount: 0),
                ["Side"] = new BuiltinCallable("openpyxl.styles.Side", CreateSide, ["style", "color", "border_style"], requiredCount: 0),
                ["Alignment"] = new BuiltinCallable("openpyxl.styles.Alignment", CreateAlignment, ["horizontal", "vertical", "wrap_text", "text_rotation"], requiredCount: 0),
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

    private sealed class OpenPyxlColor : IPyDynamicAttributes, IPyRenderableValue, IPyStringCoercibleValue
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
            Rgb = rgb;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };
            return value is not null;
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

    internal sealed class OpenPyxlStyleValue : IPyDynamicAttributes, IPyRenderableValue
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

        public bool TryGetMember(string name, out object value)
            => _members.TryGetValue(name, out value!);

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
    }

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

    private static object CreateFont(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var bold = OptionalStyleBool(arguments, 2, OptionalStyleBool(arguments, 6, false, "openpyxl.styles.Font.b", span), "openpyxl.styles.Font.bold", span);
        var italic = OptionalStyleBool(arguments, 3, OptionalStyleBool(arguments, 7, false, "openpyxl.styles.Font.i", span), "openpyxl.styles.Font.italic", span);
        return new OpenPyxlStyleValue("openpyxl.styles.Font", new Dictionary<string, object>
        {
            ["name"] = OptionalStyleValue(arguments, 0),
            ["sz"] = OptionalStyleValue(arguments, 1),
            ["size"] = OptionalStyleValue(arguments, 1),
            ["bold"] = bold,
            ["b"] = bold,
            ["italic"] = italic,
            ["i"] = italic,
            ["color"] = OptionalColorStyleValue(arguments, 4),
            ["underline"] = OptionalStyleValue(arguments, 5),
        });
    }

    private static object CreatePatternFill(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

    private static object CreateBorder(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

    private static object CreateAlignment(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        return new OpenPyxlStyleValue("openpyxl.styles.Alignment", new Dictionary<string, object>
        {
            ["horizontal"] = OptionalStyleValue(arguments, 0),
            ["vertical"] = OptionalStyleValue(arguments, 1),
            ["wrap_text"] = OptionalStyleBool(arguments, 2, false, "openpyxl.styles.Alignment.wrap_text", span),
            ["text_rotation"] = OptionalStyleValue(arguments, 3),
        });
    }

    private static object CreateProtection(object[] arguments, LythonSourceSpan span, ExecutionContext context)
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

    private static OpenPyxlStyleValue CreateNamedStyleValue(
        object name,
        object? numberFormat = null,
        object? font = null,
        object? fill = null,
        object? border = null,
        object? alignment = null,
        object? protection = null)
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
            ? new OpenPyxlColor("rgb", text.AsString(), null, null, 0d, null)
            : value;
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

    private static bool OptionalStyleBool(object[] arguments, int index, bool defaultValue, string owner, LythonSourceSpan span)
        => arguments.Length <= index || arguments[index] is PyNone
            ? defaultValue
            : arguments[index] is bool value
                ? value
                : throw new LythonRuntimeException("TypeError", owner + " expects a bool.", span);

    private static object DefaultCellStyle(string name)
        => name switch
        {
            "font" => CreateFont([], null!, null!),
            "fill" => CreatePatternFill([], null!, null!),
            "border" => CreateBorder([], null!, null!),
            "alignment" => CreateAlignment([], null!, null!),
            "protection" => CreateProtection([], null!, null!),
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
            var rewritten = rewrite(ParseCellRange(Reference, null!));
            if (rewritten is null)
            {
                return false;
            }

            Reference = rewritten.Value.Reference;
            return true;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "displayName" => PyString.FromString(DisplayName),
                "name" => PyString.FromString(DisplayName),
                "ref" => PyString.FromString(Reference),
                "tableStyleInfo" => TableStyleInfo,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => PyString.FromString(Name),
                "showFirstColumn" => ShowFirstColumn,
                "showLastColumn" => ShowLastColumn,
                "showRowStripes" => ShowRowStripes,
                "showColumnStripes" => ShowColumnStripes,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "dataValidation" => new PyList(_worksheet.DataValidations.Select(validation => (object)validation).ToArray()),
                "count" => new BigInteger(_worksheet.DataValidations.Count),
                "append" => new BoundCallable(Append, "DataValidationList.append", ["data_validation"]),
                _ => null!,
            };

            return value is not null;
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
        public OpenPyxlConditionalFormattingRule(string? type, string? operatorValue, int? priority, IReadOnlyList<string> formulas, XElement? sourceXml = null)
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "type" => Type is null ? PyNone.Instance : PyString.FromString(Type),
                "operator" => Operator is null ? PyNone.Instance : PyString.FromString(Operator),
                "priority" => Priority is null ? PyNone.Instance : new BigInteger(Priority.Value),
                "formula" => new PyList(Formulas.Select(formula => (object)PyString.FromString(formula)).ToArray()),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ranges" => new PyList(_worksheet.ConditionalFormattings.Select(formatting => (object)PyString.FromString(formatting.Sqref)).ToArray()),
                "items" => new BoundCallable(Items, "ConditionalFormattingList.items", []),
                "add" => new BoundCallable(Add, "ConditionalFormattingList.add", ["range_string", "rule"], requiredCount: 2),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public override bool TryGetMember(string name, out object value)
        {
            if (name == "Workbook")
            {
                return OpenPyxlModule.Instance.TryGetMember("Workbook", out value);
            }

            value = null!;
            return false;
        }
    }

    private sealed class OpenPyxlReaderModule : PyModule
    {
        public static readonly OpenPyxlReaderModule Instance = new();

        private OpenPyxlReaderModule() : base("openpyxl.reader")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "excel" => OpenPyxlReaderExcelModule.Instance,
                _ => null!,
            };

            return value is not null;
        }
    }

    private sealed class OpenPyxlReaderExcelModule : PyModule
    {
        public static readonly OpenPyxlReaderExcelModule Instance = new();

        private OpenPyxlReaderExcelModule() : base("openpyxl.reader.excel")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            if (name == "load_workbook")
            {
                return OpenPyxlModule.Instance.TryGetMember("load_workbook", out value);
            }

            value = null!;
            return false;
        }
    }

    private sealed class OpenPyxlUtilsModule : PyModule
    {
        public static readonly OpenPyxlUtilsModule Instance = new();

        private OpenPyxlUtilsModule() : base("openpyxl.utils")
        {
        }

        public override bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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
                [PyString.FromString(ColumnName(part.Column!.Value)), new BigInteger(part.Row.Value)],
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
                [new BigInteger(part.Row.Value), new BigInteger(part.Column!.Value)],
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
            for (var row = bounds.MinRow!.Value; row <= bounds.MaxRow!.Value; row++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxColumn!.Value - bounds.MinColumn!.Value + 1];
                for (var column = bounds.MinColumn.Value; column <= bounds.MaxColumn.Value; column++)
                {
                    cells[column - bounds.MinColumn.Value] = PyString.FromString(CellReference(row, column));
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
            for (var column = bounds.MinColumn!.Value; column <= bounds.MaxColumn!.Value; column++)
            {
                context.CheckExecutionBudget(span);
                var cells = new object[bounds.MaxRow!.Value - bounds.MinRow!.Value + 1];
                for (var row = bounds.MinRow.Value; row <= bounds.MaxRow.Value; row++)
                {
                    cells[row - bounds.MinRow.Value] = PyString.FromString(CellReference(row, column));
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
                    Math.Min(start.Column!.Value, end.Column!.Value),
                    Math.Min(start.Row!.Value, end.Row!.Value),
                    Math.Max(start.Column.Value, end.Column.Value),
                    Math.Max(start.Row.Value, end.Row.Value)),
                UtilityReferenceKind.Column => new UtilityRangeBoundaries(
                    Math.Min(start.Column!.Value, end.Column!.Value),
                    null,
                    Math.Max(start.Column.Value, end.Column.Value),
                    null),
                UtilityReferenceKind.Row => new UtilityRangeBoundaries(
                    null,
                    Math.Min(start.Row!.Value, end.Row!.Value),
                    null,
                    Math.Max(start.Row.Value, end.Row.Value)),
                _ => throw new InvalidOperationException("Unsupported reference kind."),
            };
        }

        private static string AbsoluteUtilityReference(string part, string original, LythonSourceSpan span)
        {
            var reference = ParseUtilityReferencePart(part, allowCell: true, allowColumn: true, allowRow: true, span);
            return reference.Kind switch
            {
                UtilityReferenceKind.Cell => "$" + ColumnName(reference.Column!.Value) + "$" + reference.Row!.Value.ToString(CultureInfo.InvariantCulture),
                UtilityReferenceKind.Column => "$" + ColumnName(reference.Column!.Value),
                UtilityReferenceKind.Row => "$" + reference.Row!.Value.ToString(CultureInfo.InvariantCulture),
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

        public override bool TryGetMember(string name, out object value)
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

            value = null!;
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

    internal sealed class OpenPyxlWorkbook :
        IPyDynamicAttributes,
        IPyIterableValue,
        IEnumerable<object>,
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

        public bool TryGetMember(string name, out object value)
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
                "named_styles" => new PyList(_namedStyles.Cast<object>().ToArray()),
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
                _ => null!,
            };

            return value is not null;
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

            var worksheet = ResolveOwnedWorksheet(value, "Workbook.active", span!);
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

        private OpenPyxlWorksheet ResolveOwnedWorksheet(object value, string owner, LythonSourceSpan span)
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
        private readonly Dictionary<int, OpenPyxlColumnDimension> _columnDimensions = new();
        private readonly Dictionary<int, OpenPyxlRowDimension> _rowDimensions = new();
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

        public bool TryGetMember(string name, out object value)
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
                "rows" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null!),
                "columns" => ColumnsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: false, null, null!),
                "values" => RowsTuple(1, MaxRow, 1, MaxColumn, valuesOnly: true, null, null!),
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
                _ => null!,
            };

            return value is not null;
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
            return new OpenPyxlCell(this, address.Row, address.Column);
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

        internal int MinRow => _cells.Count == 0 ? 1 : _cells.Keys.Min(cell => cell.Row);

        internal int MaxRow => _cells.Count == 0 ? 1 : _cells.Keys.Max(cell => cell.Row);

        internal int MinColumn => _cells.Count == 0 ? 1 : _cells.Keys.Min(cell => cell.Column);

        internal int MaxColumn => _cells.Count == 0 ? 1 : _cells.Keys.Max(cell => cell.Column);

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
                _cells.Remove(address);
                _dataTypes.Remove(address);
                _formulaCachedValues.Remove(address);
                _formulaXml.Remove(address);
                return;
            }

            _cells[address] = normalized;
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
                _cells[new CellAddress(row, column)] = value;
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

            return new OpenPyxlCell(this, row, column);
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
            if (!_mergedRanges.Contains(range))
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
            if (!_mergedRanges.Remove(range))
            {
                throw new LythonRuntimeException("ValueError", $"Cell range {range.Reference} is not merged.", span);
            }

            return PyNone.Instance;
        }

        private PyList MergedRangeList()
            => new(_mergedRanges.Select(range => (object)PyString.FromString(range.Reference)).ToArray());

        internal void AddLoadedMergedRange(CellRangeAddress range)
        {
            if (!_mergedRanges.Contains(range))
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

            MoveRangeEntries(_cells, range, rowOffset, columnOffset);
            MoveRangeEntries(_comments, range, rowOffset, columnOffset);
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
            RewriteCellAddressedMap(_comments, rewrite);
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

            var target = rewrite(ParseCellOrRange(_autoFilterRef, null!));
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

        private PyTuple ColumnsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan span)
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

        private PyTuple RowsTuple(int minRow, int maxRow, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan span)
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

        private PyTuple RowTuple(int row, int minColumn, int maxColumn, bool valuesOnly, ExecutionContext? context, LythonSourceSpan span)
        {
            var items = new List<object>();
            for (var column = minColumn; column <= maxColumn; column++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : new OpenPyxlCell(this, row, column));
            }

            return context is null ? new PyTuple(items) : new PyTuple(items, context.MemoryGovernor, span);
        }

        private PyTuple ColumnTuple(int minRow, int maxRow, int column, bool valuesOnly, ExecutionContext? context, LythonSourceSpan span)
        {
            var items = new List<object>();
            for (var row = minRow; row <= maxRow; row++)
            {
                items.Add(valuesOnly ? GetCellValue(row, column) : new OpenPyxlCell(this, row, column));
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

        public int Row { get; }

        public int Column { get; }

        public object Value
        {
            get => _worksheet.GetCellValue(Row, Column);
            set => _worksheet.SetCellValue(Row, Column, value);
        }

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

            return new OpenPyxlCell(_worksheet, (int)row, (int)column);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"<Cell '{CellReference(Row, Column)}'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ref" => PyString.FromString(Reference),
                "target" => PyString.FromString(Target),
                "location" => PyNone.Instance,
                "tooltip" => PyNone.Instance,
                "display" => PyString.FromString(Target),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ranges" => RangeList(),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "ref" => _worksheet.AutoFilterRef is null ? PyNone.Instance : PyString.FromString(_worksheet.AutoFilterRef),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "showGridLines" or "show_gridlines" => _worksheet.ShowGridLines,
                "tabSelected" => _worksheet.TabSelected,
                "workbookViewId" => new BigInteger(_worksheet.WorkbookViewId),
                "selection" => new PyList([new OpenPyxlSelection(_worksheet)]),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "activeCell" => PyString.FromString(_worksheet.SelectionActiveCell),
                "sqref" => PyString.FromString(_worksheet.SelectionSqref),
                "pane" => _worksheet.SelectionPane is null ? PyNone.Instance : PyString.FromString(_worksheet.SelectionPane),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "left" => Left,
                "right" => Right,
                "top" => Top,
                "bottom" => Bottom,
                "header" => Header,
                "footer" => Footer,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "orientation" => Orientation is null ? PyNone.Instance : PyString.FromString(Orientation),
                "paperSize" or "paper_size" => PaperSize is null ? PyNone.Instance : new BigInteger(PaperSize.Value),
                "fitToWidth" or "fit_to_width" => FitToWidth is null ? PyNone.Instance : new BigInteger(FitToWidth.Value),
                "fitToHeight" or "fit_to_height" => FitToHeight is null ? PyNone.Instance : new BigInteger(FitToHeight.Value),
                "scale" => Scale is null ? PyNone.Instance : new BigInteger(Scale.Value),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "keys" => new BoundCallable(Keys, "TableList.keys", []),
                "values" => new BoundCallable(Values, "TableList.values", []),
                "items" => new BoundCallable(Items, "TableList.items", []),
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "index" => PyString.FromString(ColumnName(Column)),
                "width" => Width is null ? PyNone.Instance : Width.Value,
                "hidden" => Hidden,
                "style" => Style,
                _ => null!,
            };

            return value is not null;
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

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "index" => new BigInteger(Row),
                "height" => Height is null ? PyNone.Instance : Height.Value,
                "hidden" => Hidden,
                "style" => Style,
                _ => null!,
            };

            return value is not null;
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

    private static class OpenPyxlPackage
    {
        public static OpenPyxlWorkbook Load(ReadOnlyMemory<byte> payload, bool dataOnly, bool readOnly, bool keepLinks, bool keepVba, LythonSourceSpan span)
        {
            try
            {
                using var stream = new MemoryStream(payload.ToArray(), writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var sharedStrings = LoadSharedStrings(archive);
                var cellStyles = LoadCellStyles(archive, span);
                var namedStyles = LoadNamedStyles(archive, span);
                var workbook = LoadXml(archive, "xl/workbook.xml", span);
                var date1904 = ReadBooleanAttribute(
                    workbook.Root?.Element(XlsxMain + "workbookPr") ?? new XElement(XlsxMain + "workbookPr"),
                    "date1904",
                    defaultValue: false,
                    span);
                var workbookRels = LoadRelationships(archive, "xl/_rels/workbook.xml.rels", span);
                var sheets = workbook.Root?
                    .Element(XlsxMain + "sheets")?
                    .Elements(XlsxMain + "sheet")
                    .ToArray() ?? [];

                var worksheets = new List<OpenPyxlWorksheet>();
                var worksheetPaths = new List<string>();
                foreach (var sheet in sheets)
                {
                    var name = (string?)sheet.Attribute("name") ?? "Sheet";
                    var relationshipId = (string?)sheet.Attribute(XlsxRelationships + "id");
                    if (relationshipId is null || !workbookRels.TryGetValue(relationshipId, out var target))
                    {
                        continue;
                    }

                    var path = ResolvePackagePath("xl/workbook.xml", target);
                    worksheetPaths.Add(path);
                    var worksheet = new OpenPyxlWorksheet(name) { SourcePath = path };
                    LoadWorksheetCells(archive, path, worksheet, sharedStrings, cellStyles, date1904, dataOnly, span);
                    worksheets.Add(worksheet);
                }

                if (!keepLinks && PackageHasExternalLinks(archive, workbook, span))
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        "openpyxl.load_workbook(..., keep_links=False) cannot drop external workbook links in Lython.",
                        span);
                }

                LoadWorkbookDefinedNames(workbook, worksheets, span);
                var activeIndex = ReadWorkbookActiveIndex(workbook, worksheets.Count, span);
                var hasVbaProject = PackageHasVbaProject(archive);
                var saveGuard = AnalyzeSaveGuard(archive, workbook, worksheetPaths, keepVba, span);
                if (dataOnly)
                {
                    saveGuard = AddDataOnlySaveGuard(saveGuard);
                }

                var snapshot = CapturePackageSnapshot(archive);
                var result = OpenPyxlWorkbook.FromWorksheets(worksheets, readOnly, date1904, activeIndex, saveGuard, snapshot, hasVbaProject: keepVba && hasVbaProject);
                result.SetLoadedNamedStyles(namedStyles);
                LoadWorkbookSecurity(workbook, result.Security, span);
                return result;
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
            {
                throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: {ex.Message}", span);
            }
        }

        private static OpenPyxlSaveGuard AnalyzeSaveGuard(
            ZipArchive archive,
            XDocument workbook,
            IReadOnlyList<string> worksheetPaths,
            bool keepVba,
            LythonSourceSpan span)
        {
            var unsupported = new List<string>();
            AddUnsupportedPackageParts(archive, unsupported, keepVba);
            AddUnsupportedWorkbookFeatures(workbook, worksheetPaths.Count, unsupported);
            AddUnsupportedWorkbookRelationshipFeatures(archive, unsupported, span);
            foreach (var worksheetPath in worksheetPaths)
            {
                AddUnsupportedWorksheetFeatures(archive, worksheetPath, unsupported, span);
                AddUnsupportedWorksheetRelationshipFeatures(archive, worksheetPath, unsupported, span);
            }

            return unsupported.Count == 0
                ? OpenPyxlSaveGuard.Safe
                : OpenPyxlSaveGuard.Unsafe(SummarizeUnsupportedContent(unsupported));
        }

        private static bool PackageHasExternalLinks(ZipArchive archive, XDocument workbook, LythonSourceSpan span)
        {
            if (workbook.Root?.Element(XlsxMain + "externalReferences") is not null)
            {
                return true;
            }

            if (archive.Entries.Any(entry => IsExternalLinkPackagePart(NormalizePackagePartName(entry.FullName))))
            {
                return true;
            }

            var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels", span);
            return relationships.Root?
                .Elements(PackageRelationships + "Relationship")
                .Any(relationship => IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink")) == true;
        }

        private static bool PackageHasVbaProject(ZipArchive archive)
            => archive.Entries.Any(entry => IsVbaProjectPackagePart(NormalizePackagePartName(entry.FullName)));

        private static OpenPyxlSaveGuard AddDataOnlySaveGuard(OpenPyxlSaveGuard saveGuard)
        {
            const string reason = "workbook was loaded with data_only=True";
            return saveGuard.CanSave
                ? OpenPyxlSaveGuard.Unsafe(reason)
                : OpenPyxlSaveGuard.Unsafe(saveGuard.Reason + ", " + reason);
        }

        private static void AddUnsupportedPackageParts(
            ZipArchive archive,
            List<string> unsupported,
            bool keepVba)
        {
            foreach (var entry in archive.Entries)
            {
                var name = NormalizePackagePartName(entry.FullName);
                if (name.Length == 0)
                {
                    continue;
                }

                if (IsExternalLinkPackagePart(name))
                {
                    unsupported.Add("external link package part " + name);
                    continue;
                }

                if (IsVbaProjectPackagePart(name))
                {
                    if (!keepVba)
                    {
                        unsupported.Add("VBA project package part " + name + " without keep_vba=True");
                    }

                    continue;
                }

                if (IsUnsupportedBinaryOfficePart(name))
                {
                    unsupported.Add("binary Office package part " + name);
                }
            }
        }

        private static void AddUnsupportedWorkbookFeatures(XDocument workbook, int worksheetCount, List<string> unsupported)
        {
            _ = worksheetCount;
            foreach (var child in workbook.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "externalReferences")
                {
                    unsupported.Add("workbook external link references");
                }
            }
        }

        private static void AddUnsupportedWorkbookRelationshipFeatures(
            ZipArchive archive,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            AddUnsupportedRelationshipSaveRisks(
                archive,
                "xl/_rels/workbook.xml.rels",
                "workbook relationships",
                allowExternalHyperlinks: false,
                unsupported,
                span);
        }

        private static void AddUnsupportedBookViews(XElement bookViews, int worksheetCount, List<string> unsupported)
        {
            AddUnsupportedAttributes(bookViews, "workbook bookViews", unsupported);
            var workbookViewCount = 0;
            foreach (var child in bookViews.Elements())
            {
                if (child.Name != XlsxMain + "workbookView")
                {
                    unsupported.Add("workbook bookViews element " + child.Name.LocalName);
                    continue;
                }

                workbookViewCount++;
                if (workbookViewCount > 1)
                {
                    unsupported.Add("workbook bookViews multiple workbookView elements");
                }

                AddUnsupportedAttributes(child, "workbook workbookView", unsupported, "activeTab");
                var activeTab = (string?)child.Attribute("activeTab");
                if (activeTab is not null &&
                    (!int.TryParse(activeTab, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sheetId) ||
                     sheetId < 0 ||
                     sheetId >= worksheetCount))
                {
                    unsupported.Add("workbook workbookView activeTab");
                }

                AddUnsupportedChildElements(child, "workbook workbookView", unsupported);
            }
        }

        private static void AddUnsupportedDefinedNames(XElement definedNames, int worksheetCount, List<string> unsupported)
        {
            AddUnsupportedAttributes(definedNames, "workbook definedNames", unsupported);
            foreach (var definedName in definedNames.Elements())
            {
                if (definedName.Name != XlsxMain + "definedName")
                {
                    unsupported.Add("workbook definedNames element " + definedName.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(definedName, "workbook definedName", unsupported, "name", "localSheetId");
                var name = (string?)definedName.Attribute("name");
                var localSheetId = (string?)definedName.Attribute("localSheetId");
                if (name is not "_xlnm.Print_Area" and not "_xlnm.Print_Titles" ||
                    localSheetId is null ||
                    !int.TryParse(localSheetId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sheetId) ||
                    sheetId < 0 ||
                    sheetId >= worksheetCount)
                {
                    unsupported.Add("workbook definedName " + (name ?? "(missing name)"));
                }

                AddUnsupportedChildElements(definedName, "workbook definedName", unsupported);
            }
        }

        private static void AddUnsupportedStylesFeatures(ZipArchive archive, List<string> unsupported, LythonSourceSpan span)
        {
            var styles = LoadXml(archive, "xl/styles.xml", span);
            AddUnsupportedAttributes(styles.Root ?? new XElement(XlsxMain + "styleSheet"), "styles", unsupported);
            foreach (var child in styles.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "numFmts")
                {
                    AddUnsupportedNumberFormatFeatures(child, unsupported);
                    continue;
                }

                if (child.Name == XlsxMain + "cellXfs")
                {
                    AddUnsupportedCellFormatFeatures(child, unsupported);
                    continue;
                }

                if (child.Name == XlsxMain + "fonts" ||
                    child.Name == XlsxMain + "fills" ||
                    child.Name == XlsxMain + "borders" ||
                    child.Name == XlsxMain + "cellStyleXfs" ||
                    child.Name == XlsxMain + "cellStyles")
                {
                    AddUnsupportedDefaultStyleCollectionFeatures(child, unsupported);
                    continue;
                }

                unsupported.Add("styles element " + child.Name.LocalName);
            }
        }

        private static void AddUnsupportedNumberFormatFeatures(XElement numberFormats, List<string> unsupported)
        {
            AddUnsupportedAttributes(numberFormats, "styles numFmts", unsupported, "count");
            foreach (var child in numberFormats.Elements())
            {
                if (child.Name != XlsxMain + "numFmt")
                {
                    unsupported.Add("styles numFmts element " + child.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(child, "styles numFmt", unsupported, "numFmtId", "formatCode");
                AddUnsupportedChildElements(child, "styles numFmt", unsupported);
            }
        }

        private static void AddUnsupportedCellFormatFeatures(XElement cellFormats, List<string> unsupported)
        {
            AddUnsupportedAttributes(cellFormats, "styles cellXfs", unsupported, "count");
            foreach (var child in cellFormats.Elements())
            {
                if (child.Name != XlsxMain + "xf")
                {
                    unsupported.Add("styles cellXfs element " + child.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    "styles cellXfs xf",
                    unsupported,
                    "numFmtId",
                    "fontId",
                    "fillId",
                    "borderId",
                    "xfId",
                    "applyNumberFormat",
                    "pivotButton",
                    "quotePrefix");
                AddUnsupportedChildElements(child, "styles cellXfs xf", unsupported);
                if (!IsZeroStyleReference(child, "fontId") ||
                    !IsZeroStyleReference(child, "fillId") ||
                    !IsZeroStyleReference(child, "borderId"))
                {
                    unsupported.Add("styles cellXfs non-number formatting");
                }
            }
        }

        private static void AddUnsupportedDefaultStyleCollectionFeatures(XElement collection, List<string> unsupported)
        {
            AddUnsupportedAttributes(collection, "styles " + collection.Name.LocalName, unsupported, "count");
            var count = collection.Elements().Count();
            var expected = collection.Name.LocalName == "fills" ? 2 : 1;
            if (count > expected)
            {
                unsupported.Add("styles " + collection.Name.LocalName + " custom records");
            }
        }

        private static bool IsZeroStyleReference(XElement element, string name)
            => (string?)element.Attribute(name) is null or "0";

        private static void AddUnsupportedWorksheetFeatures(
            ZipArchive archive,
            string worksheetPath,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var worksheet = LoadXml(archive, worksheetPath, span);
            foreach (var child in worksheet.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "oleObjects")
                {
                    unsupported.Add($"{worksheetPath} OLE objects");
                }
                else if (child.Name == XlsxMain + "controls")
                {
                    unsupported.Add($"{worksheetPath} ActiveX controls");
                }
            }
        }

        private static void AddUnsupportedWorksheetRelationshipFeatures(
            ZipArchive archive,
            string worksheetPath,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var relationshipsPath = WorksheetRelationshipsPath(worksheetPath);
            if (archive.GetEntry(relationshipsPath) is null)
            {
                return;
            }

            AddUnsupportedRelationshipSaveRisks(
                archive,
                relationshipsPath,
                relationshipsPath,
                allowExternalHyperlinks: true,
                unsupported,
                span);
        }

        private static void AddUnsupportedRelationshipSaveRisks(
            ZipArchive archive,
            string relationshipsPath,
            string owner,
            bool allowExternalHyperlinks,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var relationships = LoadXml(archive, relationshipsPath, span);
            foreach (var relationship in relationships.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink"))
                {
                    unsupported.Add(owner + " external link relationship");
                    continue;
                }

                if (IsExternalRelationship(relationship) &&
                    !(allowExternalHyperlinks && IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink")))
                {
                    unsupported.Add(owner + " external relationship");
                }
            }
        }

        private static bool IsExternalRelationship(XElement relationship)
            => string.Equals((string?)relationship.Attribute("TargetMode"), "External", StringComparison.Ordinal);

        private static bool IsExternalLinkPackagePart(string name)
            => name.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase);

        private static bool IsVbaProjectPackagePart(string name)
            => string.Equals(name, "xl/vbaProject.bin", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnsupportedBinaryOfficePart(string name)
            => name.StartsWith("xl/activeX/", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("xl/ctrlProps/", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase);

        private static void AddUnsupportedHyperlinkFeatures(XElement hyperlinks, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(hyperlinks, $"{worksheetPath} hyperlinks", unsupported);
            foreach (var child in hyperlinks.Elements())
            {
                if (child.Name != XlsxMain + "hyperlink")
                {
                    unsupported.Add($"{worksheetPath} hyperlinks element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(child, $"{worksheetPath} hyperlink", unsupported, "ref", "id", "location", "tooltip", "display");
                AddUnsupportedChildElements(child, $"{worksheetPath} hyperlink", unsupported);
            }
        }

        private static void AddUnsupportedSheetViewsFeatures(XElement sheetViews, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(sheetViews, $"{worksheetPath} sheetViews", unsupported);
            var sheetViewCount = 0;
            foreach (var child in sheetViews.Elements())
            {
                if (child.Name != XlsxMain + "sheetView")
                {
                    unsupported.Add($"{worksheetPath} sheetViews element {child.Name.LocalName}");
                    continue;
                }

                sheetViewCount++;
                if (sheetViewCount > 1)
                {
                    unsupported.Add($"{worksheetPath} sheetViews multiple sheetView elements");
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} sheetView",
                    unsupported,
                    "showGridLines",
                    "tabSelected",
                    "workbookViewId");
                var paneCount = 0;
                var selectionCount = 0;
                foreach (var viewChild in child.Elements())
                {
                    if (viewChild.Name == XlsxMain + "pane")
                    {
                        paneCount++;
                        if (paneCount > 1)
                        {
                            unsupported.Add($"{worksheetPath} sheetView multiple pane elements");
                        }

                        AddUnsupportedAttributes(
                            viewChild,
                            $"{worksheetPath} pane",
                            unsupported,
                            "xSplit",
                            "ySplit",
                            "topLeftCell",
                            "state");
                        if (!string.Equals((string?)viewChild.Attribute("state"), "frozen", StringComparison.Ordinal))
                        {
                            unsupported.Add($"{worksheetPath} pane state");
                        }

                        AddUnsupportedChildElements(viewChild, $"{worksheetPath} pane", unsupported);
                        continue;
                    }

                    if (viewChild.Name == XlsxMain + "selection")
                    {
                        selectionCount++;
                        if (selectionCount > 1)
                        {
                            unsupported.Add($"{worksheetPath} sheetView multiple selection elements");
                        }

                        AddUnsupportedAttributes(
                            viewChild,
                            $"{worksheetPath} selection",
                            unsupported,
                            "pane",
                            "activeCell",
                            "sqref");
                        AddUnsupportedChildElements(viewChild, $"{worksheetPath} selection", unsupported);
                        continue;
                    }

                    unsupported.Add($"{worksheetPath} sheetView element {viewChild.Name.LocalName}");
                }
            }
        }

        private static void AddUnsupportedColumnDimensionFeatures(XElement columns, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(columns, $"{worksheetPath} cols", unsupported);
            foreach (var child in columns.Elements())
            {
                if (child.Name != XlsxMain + "col")
                {
                    unsupported.Add($"{worksheetPath} cols element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} col",
                    unsupported,
                    "min",
                    "max",
                    "width",
                    "hidden",
                    "customWidth");
                AddUnsupportedChildElements(child, $"{worksheetPath} col", unsupported);
            }
        }

        private static void AddUnsupportedRowDimensionFeatures(XElement sheetData, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(sheetData, $"{worksheetPath} sheetData", unsupported);
            foreach (var child in sheetData.Elements())
            {
                if (child.Name != XlsxMain + "row")
                {
                    unsupported.Add($"{worksheetPath} sheetData element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} row",
                    unsupported,
                    "r",
                    "spans",
                    "ht",
                    "hidden",
                    "customHeight");
            }
        }

        private static void AddUnsupportedAttributes(
            XElement element,
            string owner,
            List<string> unsupported,
            params string[] supported)
        {
            var supportedAttributes = new HashSet<string>(supported, StringComparer.Ordinal);
            foreach (var attribute in element.Attributes())
            {
                if (!attribute.IsNamespaceDeclaration && !supportedAttributes.Contains(attribute.Name.LocalName))
                {
                    unsupported.Add($"{owner} attribute {attribute.Name.LocalName}");
                }
            }
        }

        private static void AddUnsupportedChildElements(XElement element, string owner, List<string> unsupported)
        {
            foreach (var child in element.Elements())
            {
                unsupported.Add($"{owner} element {child.Name.LocalName}");
            }
        }

        private static string SummarizeUnsupportedContent(List<string> unsupported)
        {
            var distinct = unsupported.Distinct(StringComparer.Ordinal).Take(6).ToArray();
            var suffix = unsupported.Distinct(StringComparer.Ordinal).Count() > distinct.Length
                ? ", ..."
                : string.Empty;
            return string.Join(", ", distinct) + suffix;
        }

        public static byte[] Save(OpenPyxlWorkbook workbook, ExecutionContext context, LythonSourceSpan span)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var styleRegistry = OpenPyxlStyleRegistry.Create(workbook);
                var generateStyles = ShouldGenerateStyles(workbook, styleRegistry);
                var preserveLoadedStyleIds = ShouldPreserveOriginalStyles(workbook);
                var generatedParts = GeneratedPackagePartNames(workbook, generateStyles);
                var workbookRelationshipPlan = CreateWorkbookRelationshipPlan(workbook, generateStyles);
                WritePreservedPackageParts(archive, workbook.PackageSnapshot, generatedParts);
                WriteXml(archive, "[Content_Types].xml", CreateContentTypes(workbook, generateStyles, generatedParts));
                WriteXml(archive, "_rels/.rels", CreateRootRelationships(workbook.PackageSnapshot));
                WriteXml(archive, "xl/workbook.xml", CreateWorkbookXml(workbook, workbookRelationshipPlan));
                WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships(workbook, generateStyles, workbookRelationshipPlan));
                if (generateStyles)
                {
                    WriteXml(archive, "xl/styles.xml", CreateStylesXml(styleRegistry));
                }

                for (var i = 0; i < workbook.Worksheets.Count; i++)
                {
                    context.CheckExecutionBudget(span);
                    var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                    var worksheetRelationshipPlan = CreateWorksheetRelationshipPlan(workbook.Worksheets[i]);
                    WriteXml(archive, worksheetPath, CreateWorksheetXml(workbook.Worksheets[i], styleRegistry, preserveLoadedStyleIds, worksheetRelationshipPlan));
                    if (worksheetRelationshipPlan.HasRelationships)
                    {
                        WriteXml(archive, WorksheetRelationshipsPath(worksheetPath), CreateWorksheetRelationships(workbook.Worksheets[i], worksheetRelationshipPlan));
                    }
                }

                WriteUpdatedLoadedTableParts(archive, workbook);
                WriteUpdatedLoadedCommentsParts(archive, workbook);
            }

            var payload = stream.ToArray();
            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(payload.Length), span);
            return payload;
        }

        private static OpenPyxlPackageSnapshot CapturePackageSnapshot(ZipArchive archive)
        {
            var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                var name = NormalizePackagePartName(entry.FullName);
                if (name.Length == 0 || parts.ContainsKey(name))
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                parts[name] = buffer.ToArray();
            }

            return new OpenPyxlPackageSnapshot(parts);
        }

        private static XDocument? LoadSnapshotXml(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            if (snapshot is null || !snapshot.Value.Parts.TryGetValue(NormalizePackagePartName(path), out var payload))
            {
                return null;
            }

            using var stream = new MemoryStream(payload, writable: false);
            return XDocument.Load(stream);
        }

        public static string? StructuralMutationPreservedFeatureReason(OpenPyxlWorksheet worksheet)
        {
            if (!worksheet.HasStructuralMutation || worksheet.SourcePath is null)
            {
                return null;
            }

            var features = PreservedStructuralWorksheetFeatures(worksheet);
            return features.Count == 0
                ? null
                : $"worksheet '{worksheet.Title}' has preserved {string.Join(", ", features)}";
        }

        private static IReadOnlyList<string> PreservedStructuralWorksheetFeatures(OpenPyxlWorksheet worksheet)
        {
            var snapshot = worksheet.Workbook?.PackageSnapshot;
            var worksheetPath = worksheet.SourcePath!;
            var features = new SortedSet<string>(StringComparer.Ordinal);
            var worksheetDocument = LoadSnapshotXml(snapshot, worksheetPath);
            foreach (var child in worksheetDocument?.Root?.Elements() ?? [])
            {
                if (PreservedStructuralWorksheetElementName(child) is { } name)
                {
                    features.Add(name);
                }
            }

            var relationships = LoadSnapshotXml(snapshot, WorksheetRelationshipsPath(worksheetPath));
            foreach (var relationship in relationships?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (PreservedStructuralWorksheetRelationshipName(snapshot, worksheetPath, worksheet, relationship) is { } name)
                {
                    features.Add(name);
                }
            }

            return features.ToArray();
        }

        private static string? PreservedStructuralWorksheetElementName(XElement element)
        {
            if (element.Name == XlsxMain + "mergeCells")
            {
                return "merged ranges";
            }

            if (element.Name == XlsxMain + "picture")
            {
                return "images";
            }

            return null;
        }

        private static string? PreservedStructuralWorksheetRelationshipName(
            OpenPyxlPackageSnapshot? snapshot,
            string worksheetPath,
            OpenPyxlWorksheet worksheet,
            XElement relationship)
        {
            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments"))
            {
                var target = (string?)relationship.Attribute("Target");
                if (target is not null &&
                    worksheet.HasLoadedCommentsUpdate &&
                    worksheet.CommentsSourcePath is not null &&
                    string.Equals(ResolvePackagePath(worksheetPath, target), worksheet.CommentsSourcePath, StringComparison.Ordinal))
                {
                    return null;
                }

                return "comments";
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/vmlDrawing"))
            {
                var target = (string?)relationship.Attribute("Target");
                return target is null || IsVmlDrawingStructurallyAnchored(snapshot, ResolvePackagePath(worksheetPath, target))
                    ? "legacy drawings/comments"
                    : null;
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing"))
            {
                var target = (string?)relationship.Attribute("Target");
                return target is null || IsSpreadsheetDrawingStructurallyAnchored(snapshot, ResolvePackagePath(worksheetPath, target))
                    ? "drawings/images/charts"
                    : null;
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"))
            {
                return "images";
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart"))
            {
                return "charts";
            }

            return null;
        }

        private static bool IsVmlDrawingStructurallyAnchored(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            var document = LoadSnapshotXml(snapshot, path);
            if (document?.Root is null)
            {
                return true;
            }

            return document.Root
                .Descendants()
                .Any(element =>
                    element.Name.LocalName is "ClientData" or "Row" or "Column");
        }

        private static bool IsSpreadsheetDrawingStructurallyAnchored(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            var document = LoadSnapshotXml(snapshot, path);
            if (document?.Root is null)
            {
                return true;
            }

            return document.Root
                .Descendants()
                .Any(element =>
                    element.Name.LocalName is "oneCellAnchor" or "twoCellAnchor" or "absoluteAnchor" or "from" or "to");
        }

        private static bool ShouldPreserveOriginalStyles(OpenPyxlWorkbook workbook)
            => workbook.PackageSnapshot?.Parts.ContainsKey("xl/styles.xml") == true;

        private static bool ShouldGenerateStyles(OpenPyxlWorkbook workbook, OpenPyxlStyleRegistry styleRegistry)
            => styleRegistry.HasCustomStyles && !ShouldPreserveOriginalStyles(workbook);

        private static HashSet<string> GeneratedPackagePartNames(OpenPyxlWorkbook workbook, bool generateStyles)
        {
            var generated = new HashSet<string>(StringComparer.Ordinal)
            {
                "[Content_Types].xml",
                "_rels/.rels",
                "xl/workbook.xml",
                "xl/_rels/workbook.xml.rels",
                "xl/sharedStrings.xml",
            };

            if (generateStyles)
            {
                generated.Add("xl/styles.xml");
            }

            foreach (var tablePath in UpdatedLoadedTablePartPaths(workbook))
            {
                generated.Add(tablePath);
            }

            foreach (var commentsPath in UpdatedLoadedCommentsPartPaths(workbook))
            {
                generated.Add(commentsPath);
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                generated.Add(worksheetPath);
                generated.Add(WorksheetRelationshipsPath(worksheetPath));
            }

            return generated;
        }

        private static IEnumerable<string> UpdatedLoadedTablePartPaths(OpenPyxlWorkbook workbook)
            => workbook.Worksheets
                .SelectMany(worksheet => worksheet.Tables.Values)
                .Where(table => table.HasLoadedPartUpdate && table.SourcePath is not null)
                .Select(table => table.SourcePath!)
                .Distinct(StringComparer.Ordinal);

        private static IEnumerable<string> UpdatedLoadedCommentsPartPaths(OpenPyxlWorkbook workbook)
            => workbook.Worksheets
                .Where(worksheet => worksheet.HasLoadedCommentsUpdate)
                .Select(worksheet => worksheet.CommentsSourcePath)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal);

        private static void WritePreservedPackageParts(ZipArchive archive, OpenPyxlPackageSnapshot? snapshot, IReadOnlySet<string> generatedParts)
        {
            if (snapshot is null)
            {
                return;
            }

            foreach (var pair in snapshot.Value.Parts)
            {
                if (generatedParts.Contains(pair.Key))
                {
                    continue;
                }

                var entry = archive.CreateEntry(pair.Key);
                using var stream = entry.Open();
                stream.Write(pair.Value, 0, pair.Value.Length);
            }
        }

        private static void WriteUpdatedLoadedTableParts(ZipArchive archive, OpenPyxlWorkbook workbook)
        {
            foreach (var table in workbook.Worksheets
                .SelectMany(worksheet => worksheet.Tables.Values)
                .Where(table => table.HasLoadedPartUpdate && table.SourcePath is not null)
                .OrderBy(table => table.SourcePath, StringComparer.Ordinal))
            {
                WriteXml(archive, table.SourcePath!, CreateLoadedTableXml(workbook.PackageSnapshot, table));
            }
        }

        private static void WriteUpdatedLoadedCommentsParts(ZipArchive archive, OpenPyxlWorkbook workbook)
        {
            foreach (var worksheet in workbook.Worksheets
                .Where(worksheet => worksheet.HasLoadedCommentsUpdate && worksheet.CommentsSourcePath is not null)
                .OrderBy(worksheet => worksheet.CommentsSourcePath, StringComparer.Ordinal))
            {
                WriteXml(archive, worksheet.CommentsSourcePath!, CreateLoadedCommentsXml(worksheet));
            }
        }

        private static XDocument CreateLoadedTableXml(OpenPyxlPackageSnapshot? snapshot, OpenPyxlTable table)
        {
            var document = table.SourcePath is null ? null : LoadSnapshotXml(snapshot, table.SourcePath);
            var root = document?.Root is null
                ? new XElement(XlsxMain + "table")
                : new XElement(document.Root);
            root.SetAttributeValue("name", table.DisplayName);
            root.SetAttributeValue("displayName", table.DisplayName);
            root.SetAttributeValue("ref", table.Reference);
            root.Element(XlsxMain + "autoFilter")?.SetAttributeValue("ref", table.Reference);

            if (table.TableStyleInfo is OpenPyxlTableStyleInfo styleInfo)
            {
                var style = root.Element(XlsxMain + "tableStyleInfo");
                if (style is null)
                {
                    style = new XElement(XlsxMain + "tableStyleInfo");
                    root.Add(style);
                }

                style.SetAttributeValue("name", styleInfo.Name);
                style.SetAttributeValue("showFirstColumn", styleInfo.ShowFirstColumn ? "1" : "0");
                style.SetAttributeValue("showLastColumn", styleInfo.ShowLastColumn ? "1" : "0");
                style.SetAttributeValue("showRowStripes", styleInfo.ShowRowStripes ? "1" : "0");
                style.SetAttributeValue("showColumnStripes", styleInfo.ShowColumnStripes ? "1" : "0");
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XDocument CreateLoadedCommentsXml(OpenPyxlWorksheet worksheet)
        {
            var authors = worksheet.Comments
                .OrderBy(pair => pair.Key.Row)
                .ThenBy(pair => pair.Key.Column)
                .Select(pair => pair.Value.Author)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var authorIds = authors
                .Select((author, index) => new { author, index })
                .ToDictionary(item => item.author, item => item.index, StringComparer.Ordinal);

            var root = new XElement(
                XlsxMain + "comments",
                new XElement(
                    XlsxMain + "authors",
                    authors.Select(author => new XElement(XlsxMain + "author", author))),
                new XElement(
                    XlsxMain + "commentList",
                    worksheet.Comments
                        .OrderBy(pair => pair.Key.Row)
                        .ThenBy(pair => pair.Key.Column)
                        .Select(pair => new XElement(
                            XlsxMain + "comment",
                            new XAttribute("ref", CellReference(pair.Key.Row, pair.Key.Column)),
                            new XAttribute("authorId", authorIds[pair.Value.Author]),
                            new XElement(
                                XlsxMain + "text",
                                new XElement(XlsxMain + "t", pair.Value.Text))))));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static IReadOnlyList<string> LoadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry is null)
            {
                return Array.Empty<string>();
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            return document.Root?
                .Elements(XlsxMain + "si")
                .Select(ReadSharedString)
                .ToArray() ?? [];
        }

        private static string ReadSharedString(XElement item)
        {
            var text = item.Element(XlsxMain + "t");
            if (text is not null)
            {
                return text.Value;
            }

            return string.Concat(item.Descendants(XlsxMain + "t").Select(element => element.Value));
        }

        private sealed record OpenPyxlCellStyleSnapshot(
            string NumberFormat,
            OpenPyxlStyleValue? Font,
            OpenPyxlStyleValue? Fill,
            OpenPyxlStyleValue? Border,
            OpenPyxlStyleValue? Alignment,
            OpenPyxlStyleValue? Protection,
            string? NamedStyleName);

        private static IReadOnlyList<OpenPyxlCellStyleSnapshot> LoadCellStyles(ZipArchive archive, LythonSourceSpan span)
        {
            var entry = archive.GetEntry("xl/styles.xml");
            if (entry is null)
            {
                return [DefaultCellStyleSnapshot()];
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var customFormats = ReadCustomNumberFormats(document, span);
            var fonts = ReadStyleCollection(document, "fonts", ReadFontStyle);
            var fills = ReadStyleCollection(document, "fills", ReadFillStyle);
            var borders = ReadStyleCollection(document, "borders", ReadBorderStyle);
            var namedStyleNamesByXfId = ReadNamedStyleNamesByXfId(document, span);
            var styles = new List<OpenPyxlCellStyleSnapshot>();
            foreach (var xf in document.Root?.Element(XlsxMain + "cellXfs")?.Elements(XlsxMain + "xf") ?? [])
            {
                var xfId = ReadNonNegativeIntAttribute(xf, "xfId", span);
                styles.Add(ReadCellStyleSnapshot(
                    xf,
                    customFormats,
                    fonts,
                    fills,
                    borders,
                    span,
                    xfId is not null && namedStyleNamesByXfId.TryGetValue(xfId.Value, out var namedStyleName)
                        ? namedStyleName
                        : null));
            }

            return styles.Count == 0 ? [DefaultCellStyleSnapshot()] : styles;
        }

        private static IReadOnlyList<OpenPyxlStyleValue> LoadNamedStyles(ZipArchive archive, LythonSourceSpan span)
        {
            var entry = archive.GetEntry("xl/styles.xml");
            if (entry is null)
            {
                return [CreateNamedStyleValue(PyString.FromString("Normal"))];
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var customFormats = ReadCustomNumberFormats(document, span);
            var fonts = ReadStyleCollection(document, "fonts", ReadFontStyle);
            var fills = ReadStyleCollection(document, "fills", ReadFillStyle);
            var borders = ReadStyleCollection(document, "borders", ReadBorderStyle);
            var styleXfs = (document.Root?.Element(XlsxMain + "cellStyleXfs")?.Elements(XlsxMain + "xf") ?? [])
                .Select(xf => ReadCellStyleSnapshot(xf, customFormats, fonts, fills, borders, span, namedStyleName: null))
                .ToArray();

            var styles = new List<OpenPyxlStyleValue>();
            foreach (var cellStyle in document.Root?.Element(XlsxMain + "cellStyles")?.Elements(XlsxMain + "cellStyle") ?? [])
            {
                if ((string?)cellStyle.Attribute("name") is not { } name)
                {
                    continue;
                }

                var xfId = ReadNonNegativeIntAttribute(cellStyle, "xfId", span) ?? 0;
                var snapshot = xfId < styleXfs.Length ? styleXfs[xfId] : DefaultCellStyleSnapshot();
                styles.Add(CreateNamedStyleValue(
                    PyString.FromString(name),
                    PyString.FromString(snapshot.NumberFormat),
                    snapshot.Font,
                    snapshot.Fill,
                    snapshot.Border,
                    snapshot.Alignment,
                    snapshot.Protection));
            }

            return styles.Count == 0 ? [CreateNamedStyleValue(PyString.FromString("Normal"))] : styles;
        }

        private static Dictionary<int, string> ReadCustomNumberFormats(XDocument document, LythonSourceSpan span)
            => document.Root?
                .Element(XlsxMain + "numFmts")?
                .Elements(XlsxMain + "numFmt")
                .Select(element => new
                {
                    Id = ReadNonNegativeIntAttribute(element, "numFmtId", span),
                    Format = (string?)element.Attribute("formatCode"),
                })
                .Where(item => item.Id is not null && item.Format is not null)
                .ToDictionary(item => item.Id!.Value, item => item.Format!, EqualityComparer<int>.Default) ?? new Dictionary<int, string>();

        private static OpenPyxlCellStyleSnapshot ReadCellStyle(
            XElement cell,
            IReadOnlyList<OpenPyxlCellStyleSnapshot> styles,
            LythonSourceSpan span)
        {
            var styleId = ReadNonNegativeIntAttribute(cell, "s", span) ?? 0;
            return styleId < styles.Count ? styles[styleId] : DefaultCellStyleSnapshot();
        }

        private static OpenPyxlCellStyleSnapshot DefaultCellStyleSnapshot()
            => new("General", null, null, null, null, null, "Normal");

        private static OpenPyxlCellStyleSnapshot ReadCellStyleSnapshot(
            XElement xf,
            IReadOnlyDictionary<int, string> customFormats,
            IReadOnlyList<OpenPyxlStyleValue?> fonts,
            IReadOnlyList<OpenPyxlStyleValue?> fills,
            IReadOnlyList<OpenPyxlStyleValue?> borders,
            LythonSourceSpan span,
            string? namedStyleName)
        {
            var numberFormatId = ReadNonNegativeIntAttribute(xf, "numFmtId", span) ?? 0;
            var fontId = ReadNonNegativeIntAttribute(xf, "fontId", span) ?? 0;
            var fillId = ReadNonNegativeIntAttribute(xf, "fillId", span) ?? 0;
            var borderId = ReadNonNegativeIntAttribute(xf, "borderId", span) ?? 0;
            return new OpenPyxlCellStyleSnapshot(
                ResolveNumberFormat(numberFormatId, customFormats),
                fontId > 0 && fontId < fonts.Count ? fonts[fontId] : null,
                fillId > 1 && fillId < fills.Count ? fills[fillId] : null,
                borderId > 0 && borderId < borders.Count ? borders[borderId] : null,
                ReadAlignmentStyle(xf.Element(XlsxMain + "alignment")),
                ReadProtectionStyle(xf.Element(XlsxMain + "protection")),
                namedStyleName);
        }

        private static Dictionary<int, string> ReadNamedStyleNamesByXfId(XDocument document, LythonSourceSpan span)
        {
            var result = new Dictionary<int, string>();
            foreach (var style in document.Root?.Element(XlsxMain + "cellStyles")?.Elements(XlsxMain + "cellStyle") ?? [])
            {
                var name = (string?)style.Attribute("name");
                var xfId = ReadNonNegativeIntAttribute(style, "xfId", span);
                if (name is not null && xfId is not null)
                {
                    result[xfId.Value] = name;
                }
            }

            return result;
        }

        private static List<OpenPyxlStyleValue?> ReadStyleCollection(
            XDocument document,
            string collectionName,
            Func<XElement, OpenPyxlStyleValue> read)
            => (document.Root?.Element(XlsxMain + collectionName)?.Elements().Select(element => (OpenPyxlStyleValue?)read(element)).ToList() ?? []);

        private static OpenPyxlStyleValue ReadFontStyle(XElement font)
        {
            var size = ReadStyleElementAttribute(font, "sz", "val");
            var bold = font.Element(XlsxMain + "b") is not null;
            var italic = font.Element(XlsxMain + "i") is not null;
            return new OpenPyxlStyleValue("openpyxl.styles.Font", new Dictionary<string, object>
            {
                ["name"] = ReadStyleElementAttribute(font, "name", "val"),
                ["sz"] = size,
                ["size"] = size,
                ["bold"] = bold,
                ["b"] = bold,
                ["italic"] = italic,
                ["i"] = italic,
                ["color"] = ReadColorValue(font.Element(XlsxMain + "color")),
                ["underline"] = ReadUnderlineValue(font.Element(XlsxMain + "u")),
            });
        }

        private static OpenPyxlStyleValue ReadFillStyle(XElement fill)
        {
            var pattern = fill.Element(XlsxMain + "patternFill");
            var fillType = ReadStyleAttribute(pattern, "patternType");
            var fgColor = ReadColorValue(pattern?.Element(XlsxMain + "fgColor"));
            var bgColor = ReadColorValue(pattern?.Element(XlsxMain + "bgColor"));
            return new OpenPyxlStyleValue("openpyxl.styles.PatternFill", new Dictionary<string, object>
            {
                ["fill_type"] = fillType,
                ["patternType"] = fillType,
                ["start_color"] = fgColor,
                ["fgColor"] = fgColor,
                ["end_color"] = bgColor,
                ["bgColor"] = bgColor,
            });
        }

        private static OpenPyxlStyleValue ReadBorderStyle(XElement border)
            => new("openpyxl.styles.Border", new Dictionary<string, object>
            {
                ["left"] = ReadSideStyle(border.Element(XlsxMain + "left")),
                ["right"] = ReadSideStyle(border.Element(XlsxMain + "right")),
                ["top"] = ReadSideStyle(border.Element(XlsxMain + "top")),
                ["bottom"] = ReadSideStyle(border.Element(XlsxMain + "bottom")),
            });

        private static OpenPyxlStyleValue ReadSideStyle(XElement? side)
        {
            var style = ReadStyleAttribute(side, "style");
            return new OpenPyxlStyleValue("openpyxl.styles.Side", new Dictionary<string, object>
            {
                ["style"] = style,
                ["border_style"] = style,
                ["color"] = ReadColorValue(side?.Element(XlsxMain + "color")),
            });
        }

        private static OpenPyxlStyleValue? ReadAlignmentStyle(XElement? alignment)
            => alignment is null
                ? null
                : new OpenPyxlStyleValue("openpyxl.styles.Alignment", new Dictionary<string, object>
                {
                    ["horizontal"] = ReadStyleAttribute(alignment, "horizontal"),
                    ["vertical"] = ReadStyleAttribute(alignment, "vertical"),
                    ["wrap_text"] = ReadStyleBooleanAttribute(alignment, "wrapText"),
                    ["text_rotation"] = ReadStyleAttribute(alignment, "textRotation"),
                });

        private static OpenPyxlStyleValue? ReadProtectionStyle(XElement? protection)
            => protection is null
                ? null
                : new OpenPyxlStyleValue("openpyxl.styles.Protection", new Dictionary<string, object>
                {
                    ["locked"] = ReadStyleBooleanAttribute(protection, "locked"),
                    ["hidden"] = ReadStyleBooleanAttribute(protection, "hidden"),
                });

        private static object ReadStyleElementAttribute(XElement parent, string elementName, string attributeName)
            => ReadStyleAttribute(parent.Element(XlsxMain + elementName), attributeName);

        private static object ReadStyleAttribute(XElement? element, string attributeName)
            => (string?)element?.Attribute(attributeName) is { } text ? PyString.FromString(text) : PyNone.Instance;

        private static bool ReadStyleBooleanAttribute(XElement? element, string attributeName)
            => (string?)element?.Attribute(attributeName) is "1" or "true" or "True";

        private static object ReadColorValue(XElement? color)
        {
            if (color is null)
            {
                return PyNone.Instance;
            }

            var tint = ReadColorDoubleAttribute(color, "tint") ?? 0d;
            if ((string?)color.Attribute("rgb") is { } rgb)
            {
                return new OpenPyxlColor("rgb", rgb, null, null, tint, null);
            }

            if (ReadColorIntegerAttribute(color, "indexed") is { } indexed)
            {
                return new OpenPyxlColor("indexed", null, indexed, null, tint, null);
            }

            if (ReadColorIntegerAttribute(color, "theme") is { } theme)
            {
                return new OpenPyxlColor("theme", null, null, theme, tint, null);
            }

            if (ReadColorBooleanAttribute(color, "auto") is { } auto)
            {
                return new OpenPyxlColor("auto", null, null, null, tint, auto);
            }

            return PyNone.Instance;
        }

        private static BigInteger? ReadColorIntegerAttribute(XElement color, string name)
            => BigInteger.TryParse((string?)color.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static double? ReadColorDoubleAttribute(XElement color, string name)
            => double.TryParse((string?)color.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static bool? ReadColorBooleanAttribute(XElement color, string name)
            => (string?)color.Attribute(name) switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => null,
            };

        private static object ReadUnderlineValue(XElement? underline)
            => underline is null
                ? PyNone.Instance
                : PyString.FromString((string?)underline.Attribute("val") ?? "single");

        private static string ResolveNumberFormat(int id, IReadOnlyDictionary<int, string> customFormats)
            => customFormats.TryGetValue(id, out var custom)
                ? custom
                : BuiltInNumberFormat(id);

        private static string BuiltInNumberFormat(int id)
            => id switch
            {
                0 => "General",
                1 => "0",
                2 => "0.00",
                3 => "#,##0",
                4 => "#,##0.00",
                9 => "0%",
                10 => "0.00%",
                11 => "0.00E+00",
                12 => "# ?/?",
                13 => "# ??/??",
                14 => "mm-dd-yy",
                15 => "d-mmm-yy",
                16 => "d-mmm",
                17 => "mmm-yy",
                18 => "h:mm AM/PM",
                19 => "h:mm:ss AM/PM",
                20 => "h:mm",
                21 => "h:mm:ss",
                22 => "m/d/yy h:mm",
                37 => "#,##0 ;(#,##0)",
                38 => "#,##0 ;[Red](#,##0)",
                39 => "#,##0.00;(#,##0.00)",
                40 => "#,##0.00;[Red](#,##0.00)",
                45 => "mm:ss",
                46 => "[h]:mm:ss",
                47 => "mmss.0",
                49 => "@",
                _ => "General",
            };

        private static Dictionary<string, string> LoadRelationships(ZipArchive archive, string path, LythonSourceSpan span)
        {
            var document = LoadXml(archive, path, span);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var relationship in document.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                var id = (string?)relationship.Attribute("Id");
                var target = (string?)relationship.Attribute("Target");
                if (id is not null && target is not null)
                {
                    result[id] = target;
                }
            }

            return result;
        }

        private static string? LoadRelationshipTargetByType(ZipArchive archive, string path, string type, LythonSourceSpan span)
        {
            if (archive.GetEntry(path) is null)
            {
                return null;
            }

            var document = LoadXml(archive, path, span);
            foreach (var relationship in document.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                var target = (string?)relationship.Attribute("Target");
                if (target is not null && IsRelationshipType(relationship, type))
                {
                    return target;
                }
            }

            return null;
        }

        private static XDocument LoadXml(ZipArchive archive, string path, LythonSourceSpan span)
        {
            var entry = archive.GetEntry(path);
            if (entry is null)
            {
                throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: missing {path}.", span);
            }

            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static void LoadWorkbookDefinedNames(XDocument workbook, IReadOnlyList<OpenPyxlWorksheet> worksheets, LythonSourceSpan span)
        {
            foreach (var definedName in workbook.Root?.Element(XlsxMain + "definedNames")?.Elements(XlsxMain + "definedName") ?? [])
            {
                var name = (string?)definedName.Attribute("name");
                var localSheetId = ReadNonNegativeIntAttribute(definedName, "localSheetId", span);
                if (name is null ||
                    localSheetId is null ||
                    localSheetId.Value >= worksheets.Count)
                {
                    continue;
                }

                var worksheet = worksheets[localSheetId.Value];
                if (name == "_xlnm.Print_Area")
                {
                    worksheet.SetLoadedPrintArea(NormalizePrintAreaText(definedName.Value, "Print_Area", span));
                    continue;
                }

                if (name == "_xlnm.Print_Titles")
                {
                    var titles = NormalizePrintTitlesText(definedName.Value, span);
                    worksheet.SetLoadedPrintTitles(titles.Rows, titles.Columns);
                }
            }
        }

        private static int ReadWorkbookActiveIndex(XDocument workbook, int worksheetCount, LythonSourceSpan span)
        {
            if (worksheetCount <= 0)
            {
                return 0;
            }

            var activeTab = ReadNonNegativeIntAttribute(
                workbook.Root?.Element(XlsxMain + "bookViews")?.Elements(XlsxMain + "workbookView").FirstOrDefault() ?? new XElement(XlsxMain + "workbookView"),
                "activeTab",
                span);
            return activeTab is not null && activeTab.Value < worksheetCount ? activeTab.Value : 0;
        }

        private static void LoadWorkbookSecurity(XDocument workbook, OpenPyxlWorkbookSecurity security, LythonSourceSpan span)
        {
            var protection = workbook.Root?.Element(XlsxMain + "workbookProtection");
            if (protection is null)
            {
                return;
            }

            security.SetLoaded(
                ReadOptionalBooleanAttribute(protection, "lockStructure", span),
                ReadOptionalBooleanAttribute(protection, "lockWindows", span),
                ReadOptionalBooleanAttribute(protection, "lockRevision", span),
                (string?)protection.Attribute("workbookPassword"),
                (string?)protection.Attribute("workbookPasswordCharacterSet"),
                (string?)protection.Attribute("revisionsPassword"),
                (string?)protection.Attribute("revisionsPasswordCharacterSet"),
                (string?)protection.Attribute("workbookAlgorithmName"),
                (string?)protection.Attribute("workbookHashValue"),
                (string?)protection.Attribute("workbookSaltValue"),
                ReadNonNegativeIntAttribute(protection, "workbookSpinCount", span),
                (string?)protection.Attribute("revisionsAlgorithmName"),
                (string?)protection.Attribute("revisionsHashValue"),
                (string?)protection.Attribute("revisionsSaltValue"),
                ReadNonNegativeIntAttribute(protection, "revisionsSpinCount", span));
        }

        private static void LoadWorksheetCells(
            ZipArchive archive,
            string path,
            OpenPyxlWorksheet worksheet,
            IReadOnlyList<string> sharedStrings,
            IReadOnlyList<OpenPyxlCellStyleSnapshot> cellStyles,
            bool date1904,
            bool dataOnly,
            LythonSourceSpan span)
        {
            var document = LoadXml(archive, path, span);
            var worksheetRelationships = LoadOptionalRelationships(archive, WorksheetRelationshipsPath(path), span);
            foreach (var cell in document.Descendants(XlsxMain + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                if (reference is null)
                {
                    continue;
                }

                var address = ParseCellAddress(reference, span);
                var styleId = ReadNonNegativeIntAttribute(cell, "s", span);
                var cellStyle = ReadCellStyle(cell, cellStyles, span);
                var format = cellStyle.NumberFormat;
                var value = ReadCellValue(cell, sharedStrings, format, date1904, dataOnly, span);
                worksheet.SetLoadedCellValue(address.Row, address.Column, value);
                worksheet.SetLoadedFormulaCachedValue(address.Row, address.Column, ReadFormulaCachedCellValue(cell, sharedStrings, format, date1904, span));
                worksheet.SetLoadedFormulaXml(address.Row, address.Column, cell.Element(XlsxMain + "f"));
                worksheet.SetLoadedCellNumberFormat(address.Row, address.Column, format);
                worksheet.SetLoadedCellStyleId(address.Row, address.Column, styleId);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, "font", cellStyle.Font);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, "fill", cellStyle.Fill);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, "border", cellStyle.Border);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, "alignment", cellStyle.Alignment);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, "protection", cellStyle.Protection);
                worksheet.SetLoadedCellNamedStyle(address.Row, address.Column, cellStyle.NamedStyleName);
                worksheet.SetLoadedCellDataType(address.Row, address.Column, (string?)cell.Attribute("t"));
            }

            foreach (var column in document.Descendants(XlsxMain + "col"))
            {
                var min = ReadPositiveIntAttribute(column, "min", span);
                var max = ReadPositiveIntAttribute(column, "max", span);
                if (min is null || max is null)
                {
                    continue;
                }

                for (var index = min.Value; index <= max.Value; index++)
                {
                    ValidateRowColumn(1, index, span);
                    var dimension = worksheet.GetColumnDimension(index);
                    dimension.Width = ReadNonNegativeDoubleAttribute(column, "width", span);
                    dimension.Hidden = ReadBooleanAttribute(column, "hidden", span);
                }
            }

            foreach (var rowElement in document.Descendants(XlsxMain + "row"))
            {
                var rowIndex = ReadPositiveIntAttribute(rowElement, "r", span);
                if (rowIndex is null)
                {
                    continue;
                }

                ValidateRowColumn(rowIndex.Value, 1, span);
                var dimension = worksheet.GetRowDimension(rowIndex.Value);
                dimension.Height = ReadNonNegativeDoubleAttribute(rowElement, "ht", span);
                dimension.Hidden = ReadBooleanAttribute(rowElement, "hidden", span);
            }

            foreach (var mergeCell in document.Descendants(XlsxMain + "mergeCell"))
            {
                var reference = (string?)mergeCell.Attribute("ref");
                if (reference is not null)
                {
                    worksheet.AddLoadedMergedRange(ParseCellRange(reference, span));
                }
            }

            foreach (var hyperlink in document.Descendants(XlsxMain + "hyperlink"))
            {
                var reference = (string?)hyperlink.Attribute("ref");
                if (reference is null)
                {
                    continue;
                }

                var relationshipId = (string?)hyperlink.Attribute(XlsxRelationships + "id");
                var location = (string?)hyperlink.Attribute("location");
                var target = relationshipId is not null && worksheetRelationships.TryGetValue(relationshipId, out var relationshipTarget)
                    ? relationshipTarget
                    : location;
                if (target is not null)
                {
                    worksheet.SetLoadedHyperlink(ParseCellOrRange(reference, span), target);
                }
            }

            LoadWorksheetComments(archive, path, worksheet, span);
            LoadWorksheetTables(archive, path, document, worksheet, worksheetRelationships, span);
            LoadWorksheetDataValidations(document, worksheet, span);
            LoadWorksheetConditionalFormatting(document, worksheet, span);
            LoadWorksheetProtection(document, worksheet, span);
            LoadWorksheetDrawings(archive, path, document, worksheet, worksheetRelationships, span);
            worksheet.SetLoadedAutoFilter((string?)document.Descendants(XlsxMain + "autoFilter").FirstOrDefault()?.Attribute("ref"));

            var sheetView = document.Descendants(XlsxMain + "sheetView").FirstOrDefault();
            if (sheetView is not null)
            {
                worksheet.SetLoadedSheetView(
                    ReadBooleanAttribute(sheetView, "showGridLines", defaultValue: true, span),
                    ReadBooleanAttribute(sheetView, "tabSelected", defaultValue: false, span),
                    ReadNonNegativeIntAttribute(sheetView, "workbookViewId", span) ?? 0);

                var selection = sheetView.Elements(XlsxMain + "selection").FirstOrDefault();
                if (selection is not null)
                {
                    var activeCell = (string?)selection.Attribute("activeCell");
                    var sqref = (string?)selection.Attribute("sqref");
                    var paneName = (string?)selection.Attribute("pane");
                    worksheet.SetLoadedSelection(
                        activeCell is null ? null : NormalizeCellReference(PyString.FromString(activeCell), "Selection.activeCell", span),
                        sqref is null ? null : NormalizeSelectionReference(PyString.FromString(sqref), "Selection.sqref", span),
                        paneName is null ? null : NormalizeOptionalPane(PyString.FromString(paneName), "Selection.pane", span));
                }
            }

            var pane = document.Descendants(XlsxMain + "pane").FirstOrDefault();
            var topLeftCell = (string?)pane?.Attribute("topLeftCell");
            if (topLeftCell is not null &&
                string.Equals((string?)pane?.Attribute("state"), "frozen", StringComparison.Ordinal))
            {
                worksheet.SetLoadedFreezePanes(NormalizeOptionalCellReference(PyString.FromString(topLeftCell), "Worksheet.freeze_panes", span));
            }

            var pageMargins = document.Descendants(XlsxMain + "pageMargins").FirstOrDefault();
            if (pageMargins is not null)
            {
                worksheet.SetLoadedPageMargins(
                    ReadNonNegativeDoubleAttribute(pageMargins, "left", span) ?? worksheet.PageMargins.Left,
                    ReadNonNegativeDoubleAttribute(pageMargins, "right", span) ?? worksheet.PageMargins.Right,
                    ReadNonNegativeDoubleAttribute(pageMargins, "top", span) ?? worksheet.PageMargins.Top,
                    ReadNonNegativeDoubleAttribute(pageMargins, "bottom", span) ?? worksheet.PageMargins.Bottom,
                    ReadNonNegativeDoubleAttribute(pageMargins, "header", span) ?? worksheet.PageMargins.Header,
                    ReadNonNegativeDoubleAttribute(pageMargins, "footer", span) ?? worksheet.PageMargins.Footer);
            }

            var pageSetup = document.Descendants(XlsxMain + "pageSetup").FirstOrDefault();
            if (pageSetup is not null)
            {
                var orientation = (string?)pageSetup.Attribute("orientation");
                worksheet.PageSetup.Orientation = orientation is null
                    ? null
                    : NormalizePageOrientation(PyString.FromString(orientation), "PageSetup.orientation", span);
                worksheet.PageSetup.PaperSize = ReadPositiveIntAttribute(pageSetup, "paperSize", span);
                worksheet.PageSetup.FitToWidth = ReadNonNegativeIntAttribute(pageSetup, "fitToWidth", span);
                worksheet.PageSetup.FitToHeight = ReadNonNegativeIntAttribute(pageSetup, "fitToHeight", span);
                worksheet.PageSetup.Scale = ReadPositiveIntAttribute(pageSetup, "scale", span);
            }
        }

        private static void LoadWorksheetTables(
            ZipArchive archive,
            string worksheetPath,
            XDocument worksheetDocument,
            OpenPyxlWorksheet worksheet,
            IReadOnlyDictionary<string, string> worksheetRelationships,
            LythonSourceSpan span)
        {
            foreach (var tablePart in worksheetDocument.Descendants(XlsxMain + "tablePart"))
            {
                var relationshipId = (string?)tablePart.Attribute(XlsxRelationships + "id");
                if (relationshipId is null || !worksheetRelationships.TryGetValue(relationshipId, out var target))
                {
                    continue;
                }

                var table = LoadTable(archive, ResolvePackagePath(worksheetPath, target), span);
                if (table is not null)
                {
                    worksheet.SetLoadedTable(table);
                }
            }
        }

        private static OpenPyxlTable? LoadTable(ZipArchive archive, string tablePath, LythonSourceSpan span)
        {
            var document = LoadXml(archive, tablePath, span);
            var root = document.Root;
            if (root is null)
            {
                return null;
            }

            var displayName = (string?)root.Attribute("displayName") ??
                (string?)root.Attribute("name") ??
                ((string?)root.Attribute("id") is { } id ? "Table" + id : null);
            var reference = (string?)root.Attribute("ref");
            if (displayName is null || reference is null)
            {
                return null;
            }

            var table = new OpenPyxlTable(displayName, ParseCellRange(reference, span).Reference);
            table.SetLoadedSource(tablePath);
            var style = root.Element(XlsxMain + "tableStyleInfo");
            if (style is not null && (string?)style.Attribute("name") is { } styleName)
            {
                table.TableStyleInfo = new OpenPyxlTableStyleInfo(
                    styleName,
                    ReadBooleanAttribute(style, "showFirstColumn", defaultValue: false, span),
                    ReadBooleanAttribute(style, "showLastColumn", defaultValue: false, span),
                    ReadBooleanAttribute(style, "showRowStripes", defaultValue: true, span),
                    ReadBooleanAttribute(style, "showColumnStripes", defaultValue: false, span));
            }

            return table;
        }

        private static void LoadWorksheetDataValidations(XDocument worksheetDocument, OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            foreach (var element in worksheetDocument.Root?.Element(XlsxMain + "dataValidations")?.Elements(XlsxMain + "dataValidation") ?? [])
            {
                var validation = new OpenPyxlDataValidation(
                    (string?)element.Attribute("type"),
                    element.Element(XlsxMain + "formula1")?.Value,
                    element.Element(XlsxMain + "formula2")?.Value,
                    ReadBooleanAttribute(element, "allowBlank", defaultValue: false, span),
                    ReadBooleanAttribute(element, "showErrorMessage", defaultValue: true, span),
                    ReadBooleanAttribute(element, "showInputMessage", defaultValue: true, span),
                    (string?)element.Attribute("operator"),
                    (string?)element.Attribute("errorTitle"),
                    (string?)element.Attribute("error"),
                    (string?)element.Attribute("promptTitle"),
                    (string?)element.Attribute("prompt"));

                foreach (var reference in ((string?)element.Attribute("sqref") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    validation.AddRange(reference, span);
                }

                worksheet.AddDataValidation(validation);
            }
        }

        private static void LoadWorksheetConditionalFormatting(XDocument worksheetDocument, OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            foreach (var element in worksheetDocument.Root?.Elements(XlsxMain + "conditionalFormatting") ?? [])
            {
                var sqref = (string?)element.Attribute("sqref");
                if (sqref is null)
                {
                    continue;
                }

                var rules = element.Elements(XlsxMain + "cfRule")
                    .Select(rule => new OpenPyxlConditionalFormattingRule(
                        (string?)rule.Attribute("type"),
                        (string?)rule.Attribute("operator"),
                        ReadNonNegativeIntAttribute(rule, "priority", span),
                        rule.Elements(XlsxMain + "formula").Select(formula => formula.Value).ToArray(),
                        new XElement(rule)))
                    .ToArray();
                worksheet.AddLoadedConditionalFormatting(sqref, rules, span);
            }
        }

        private static void LoadWorksheetProtection(XDocument worksheetDocument, OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            var protection = worksheetDocument.Root?.Element(XlsxMain + "sheetProtection");
            if (protection is null)
            {
                return;
            }

            worksheet.Protection.SetLoaded(
                ReadBooleanAttribute(protection, "sheet", defaultValue: false, span),
                ReadBooleanAttribute(protection, "objects", defaultValue: false, span),
                ReadBooleanAttribute(protection, "scenarios", defaultValue: false, span),
                (string?)protection.Attribute("password"),
                (string?)protection.Attribute("algorithmName"),
                (string?)protection.Attribute("hashValue"),
                (string?)protection.Attribute("saltValue"),
                ReadNonNegativeIntAttribute(protection, "spinCount", span));
        }

        private static void LoadWorksheetDrawings(
            ZipArchive archive,
            string worksheetPath,
            XDocument worksheetDocument,
            OpenPyxlWorksheet worksheet,
            IReadOnlyDictionary<string, string> worksheetRelationships,
            LythonSourceSpan span)
        {
            foreach (var drawingElement in worksheetDocument.Root?.Elements(XlsxMain + "drawing") ?? [])
            {
                var relationshipId = (string?)drawingElement.Attribute(XlsxRelationships + "id");
                if (relationshipId is null || !worksheetRelationships.TryGetValue(relationshipId, out var target))
                {
                    continue;
                }

                var drawingPath = ResolvePackagePath(worksheetPath, target);
                var drawing = new OpenPyxlLoadedDrawing(drawingPath, relationshipId);
                foreach (var relationship in LoadOptionalRelationshipElements(archive, PartRelationshipsPath(drawingPath), span))
                {
                    var childTarget = (string?)relationship.Attribute("Target");
                    var childRelationshipId = (string?)relationship.Attribute("Id") ?? string.Empty;
                    if (childTarget is null)
                    {
                        continue;
                    }

                    if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart"))
                    {
                        drawing.AddChart(new OpenPyxlLoadedChart(ResolvePackagePath(drawingPath, childTarget), childRelationshipId, drawingPath));
                        continue;
                    }

                    if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"))
                    {
                        drawing.AddImage(new OpenPyxlLoadedImage(ResolvePackagePath(drawingPath, childTarget), childRelationshipId, drawingPath));
                    }
                }

                worksheet.AddLoadedDrawing(drawing);
            }
        }

        private static void LoadWorksheetComments(ZipArchive archive, string worksheetPath, OpenPyxlWorksheet worksheet, LythonSourceSpan span)
        {
            var target = LoadRelationshipTargetByType(
                archive,
                WorksheetRelationshipsPath(worksheetPath),
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments",
                span);
            if (target is null)
            {
                return;
            }

            var commentsPath = ResolvePackagePath(worksheetPath, target);
            worksheet.SetLoadedCommentsSource(commentsPath);
            var comments = LoadXml(archive, commentsPath, span);
            var authors = comments.Root
                ?.Element(XlsxMain + "authors")
                ?.Elements(XlsxMain + "author")
                .Select(author => author.Value)
                .ToArray() ?? [];

            foreach (var comment in comments.Root?.Element(XlsxMain + "commentList")?.Elements(XlsxMain + "comment") ?? [])
            {
                var reference = (string?)comment.Attribute("ref");
                if (reference is null)
                {
                    continue;
                }

                var address = ParseCellAddress(reference, span);
                var authorId = ReadNonNegativeIntAttribute(comment, "authorId", span) ?? 0;
                var author = authorId < authors.Length ? authors[authorId] : string.Empty;
                var text = string.Concat(comment.Element(XlsxMain + "text")?.Descendants(XlsxMain + "t").Select(t => t.Value) ?? []);
                worksheet.SetLoadedComment(address.Row, address.Column, new OpenPyxlComment(text, author));
            }
        }

        private static object ReadCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            bool date1904,
            bool dataOnly,
            LythonSourceSpan span)
        {
            var formula = cell.Element(XlsxMain + "f");
            if (formula is not null && !dataOnly)
            {
                return PyString.FromString("=" + formula.Value);
            }

            return ReadStoredCellValue(cell, sharedStrings, numberFormat, date1904, span);
        }

        private static object ReadFormulaCachedCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            bool date1904,
            LythonSourceSpan span)
            => cell.Element(XlsxMain + "f") is null
                ? PyNone.Instance
                : ReadStoredCellValue(cell, sharedStrings, numberFormat, date1904, span);

        private static object ReadStoredCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            bool date1904,
            LythonSourceSpan span)
        {
            var type = (string?)cell.Attribute("t");
            if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
            {
                return PyString.FromString(string.Concat(cell.Element(XlsxMain + "is")?.Descendants(XlsxMain + "t").Select(t => t.Value) ?? []));
            }

            var rawValue = cell.Element(XlsxMain + "v")?.Value;
            if (rawValue is null)
            {
                return PyNone.Instance;
            }

            return type switch
            {
                "s" => ReadSharedStringValue(rawValue, sharedStrings, span),
                "b" => rawValue == "1",
                "str" => PyString.FromString(rawValue),
                "e" => PyString.FromString(rawValue),
                _ => ParseNumericCell(rawValue, numberFormat, date1904),
            };
        }

        private static object ReadSharedStringValue(string rawValue, IReadOnlyList<string> sharedStrings, LythonSourceSpan span)
        {
            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                index < 0 ||
                index >= sharedStrings.Count)
            {
                throw new LythonRuntimeException("InvalidFileException", "Invalid .xlsx workbook: shared string index is out of range.", span);
            }

            return PyString.FromString(sharedStrings[index]);
        }

        private static object ParseNumericCell(string rawValue, string numberFormat, bool date1904)
        {
            if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
                IsDateNumberFormat(numberFormat))
            {
                return DateValueFromExcelSerial(serial, numberFormat, date1904);
            }

            if (rawValue.IndexOfAny(['.', 'e', 'E']) < 0 &&
                BigInteger.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
            {
                return integer;
            }

            return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
                ? floating
                : PyString.FromString(rawValue);
        }

        private static int? ReadPositiveIntAttribute(XElement element, string name, LythonSourceSpan span)
        {
            var text = (string?)element.Attribute(name);
            if (text is null)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            {
                return value;
            }

            throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: attribute {name} expects a positive integer.", span);
        }

        private static int? ReadNonNegativeIntAttribute(XElement element, string name, LythonSourceSpan span)
        {
            var text = (string?)element.Attribute(name);
            if (text is null)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0)
            {
                return value;
            }

            throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: attribute {name} expects a non-negative integer.", span);
        }

        private static double? ReadNonNegativeDoubleAttribute(XElement element, string name, LythonSourceSpan span)
        {
            var text = (string?)element.Attribute(name);
            if (text is null)
            {
                return null;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                double.IsFinite(value) &&
                value >= 0)
            {
                return value;
            }

            throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: attribute {name} expects a non-negative finite number.", span);
        }

        private static bool ReadBooleanAttribute(XElement element, string name, LythonSourceSpan span)
            => ReadBooleanAttribute(element, name, defaultValue: false, span);

        private static bool ReadBooleanAttribute(XElement element, string name, bool defaultValue, LythonSourceSpan span)
        {
            var text = (string?)element.Attribute(name);
            if (text is null)
            {
                return defaultValue;
            }

            return text switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: attribute {name} expects a boolean.", span)
            };
        }

        private static bool? ReadOptionalBooleanAttribute(XElement element, string name, LythonSourceSpan span)
        {
            var text = (string?)element.Attribute(name);
            if (text is null)
            {
                return null;
            }

            return text switch
            {
                "1" or "true" or "True" => true,
                "0" or "false" or "False" => false,
                _ => throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: attribute {name} expects a boolean.", span)
            };
        }

        private static XDocument CreateContentTypes(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            IReadOnlySet<string> generatedParts)
        {
            var root = CreateSeededContentTypesRoot(workbook.PackageSnapshot, generatedParts);
            AddOrReplaceContentTypeDefault(root, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            AddOrReplaceContentTypeDefault(root, "xml", "application/xml");
            AddOrReplaceContentTypeOverride(root, "/xl/workbook.xml", WorkbookContentType(workbook));

            if (generateStyles)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/xl/styles.xml",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    $"/xl/worksheets/sheet{i + 1}.xml",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            }

            foreach (var tablePath in UpdatedLoadedTablePartPaths(workbook))
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + tablePath,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml");
            }

            foreach (var commentsPath in UpdatedLoadedCommentsPartPaths(workbook))
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + commentsPath,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml");
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XElement CreateSeededContentTypesRoot(OpenPyxlPackageSnapshot? snapshot, IReadOnlySet<string> generatedParts)
        {
            var root = new XElement(ContentTypes + "Types");
            var original = LoadSnapshotXml(snapshot, "[Content_Types].xml");
            foreach (var child in original?.Root?.Elements() ?? [])
            {
                if (child.Name == ContentTypes + "Override")
                {
                    var partName = (string?)child.Attribute("PartName");
                    if (partName is not null && generatedParts.Contains(NormalizePackagePartName(partName)))
                    {
                        continue;
                    }
                }

                root.Add(new XElement(child));
            }

            return root;
        }

        private static void AddOrReplaceContentTypeDefault(XElement root, string extension, string contentType)
        {
            root.Elements(ContentTypes + "Default")
                .Where(element => string.Equals((string?)element.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase))
                .Remove();
            root.Add(new XElement(
                ContentTypes + "Default",
                new XAttribute("Extension", extension),
                new XAttribute("ContentType", contentType)));
        }

        private static void AddOrReplaceContentTypeOverride(XElement root, string partName, string contentType)
        {
            root.Elements(ContentTypes + "Override")
                .Where(element => string.Equals(
                    NormalizePackagePartName((string?)element.Attribute("PartName") ?? string.Empty),
                    NormalizePackagePartName(partName),
                    StringComparison.Ordinal))
                .Remove();
            root.Add(new XElement(
                ContentTypes + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }

        private sealed record OpenPyxlWorkbookRelationshipPlan(IReadOnlyList<string> WorksheetIds, string? StylesId);

        private static XDocument CreateRootRelationships(OpenPyxlPackageSnapshot? snapshot)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in PreservedRootRelationships(snapshot))
            {
                root.Add(relationship);
            }

            root.Add(new XElement(
                PackageRelationships + "Relationship",
                new XAttribute("Id", NextRelationshipId(RelationshipIds(root))),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml")));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static OpenPyxlWorkbookRelationshipPlan CreateWorkbookRelationshipPlan(OpenPyxlWorkbook workbook, bool generateStyles)
        {
            var usedIds = PreservedWorkbookRelationships(workbook.PackageSnapshot, generateStyles)
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            var worksheetIds = new string[workbook.Worksheets.Count];
            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                worksheetIds[i] = NextRelationshipId(usedIds);
                usedIds.Add(worksheetIds[i]);
            }

            string? stylesId = null;
            if (generateStyles)
            {
                stylesId = NextRelationshipId(usedIds);
            }

            return new OpenPyxlWorkbookRelationshipPlan(worksheetIds, stylesId);
        }

        private static XDocument CreateWorkbookRelationships(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            OpenPyxlWorkbookRelationshipPlan plan)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in PreservedWorkbookRelationships(workbook.PackageSnapshot, generateStyles))
            {
                root.Add(relationship);
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.WorksheetIds[i]),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")));
            }

            if (generateStyles && plan.StylesId is not null)
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.StylesId),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml")));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static IEnumerable<XElement> PreservedRootRelationships(OpenPyxlPackageSnapshot? snapshot)
        {
            var original = LoadSnapshotXml(snapshot, "_rels/.rels");
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static IEnumerable<XElement> PreservedWorkbookRelationships(OpenPyxlPackageSnapshot? snapshot, bool generateStyles)
        {
            var original = LoadSnapshotXml(snapshot, "xl/_rels/workbook.xml.rels");
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet") ||
                    IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings") ||
                    generateStyles && IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static bool IsRelationshipType(XElement relationship, string type)
            => string.Equals((string?)relationship.Attribute("Type"), type, StringComparison.Ordinal);

        private static HashSet<string> RelationshipIds(XElement root)
            => root.Elements(PackageRelationships + "Relationship")
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

        private static string NextRelationshipId(ISet<string> usedIds)
        {
            var index = 1;
            while (true)
            {
                var id = "rId" + index.ToString(CultureInfo.InvariantCulture);
                if (!usedIds.Contains(id))
                {
                    return id;
                }

                index++;
            }
        }

        private sealed record OpenPyxlWorksheetRelationshipPlan(IReadOnlyDictionary<CellAddress, string> HyperlinkIds, IReadOnlyList<XElement> PreservedRelationships)
        {
            public bool HasRelationships => HyperlinkIds.Count > 0 || PreservedRelationships.Count > 0;
        }

        private static OpenPyxlWorksheetRelationshipPlan CreateWorksheetRelationshipPlan(OpenPyxlWorksheet worksheet)
        {
            var preserved = PreservedWorksheetRelationships(worksheet).ToArray();
            var usedIds = preserved
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            var hyperlinkIds = new Dictionary<CellAddress, string>();
            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                var id = NextRelationshipId(usedIds);
                usedIds.Add(id);
                hyperlinkIds[pair.Key] = id;
            }

            return new OpenPyxlWorksheetRelationshipPlan(hyperlinkIds, preserved);
        }

        private static XDocument CreateWorksheetRelationships(OpenPyxlWorksheet worksheet, OpenPyxlWorksheetRelationshipPlan plan)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in plan.PreservedRelationships)
            {
                root.Add(new XElement(relationship));
            }

            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.HyperlinkIds[pair.Key]),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                    new XAttribute("Target", pair.Value),
                    new XAttribute("TargetMode", "External")));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static IEnumerable<XElement> PreservedWorksheetRelationships(OpenPyxlWorksheet worksheet)
        {
            var snapshot = worksheet.Workbook?.PackageSnapshot;
            var relationshipsPath = worksheet.SourcePath is null ? null : WorksheetRelationshipsPath(worksheet.SourcePath);
            var original = relationshipsPath is null ? null : LoadSnapshotXml(snapshot, relationshipsPath);
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static XDocument CreateWorkbookXml(OpenPyxlWorkbook workbook, OpenPyxlWorkbookRelationshipPlan relationshipPlan)
        {
            var sheets = new XElement(XlsxMain + "sheets");
            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                sheets.Add(new XElement(
                    XlsxMain + "sheet",
                    new XAttribute("name", workbook.Worksheets[i].Title),
                    new XAttribute("sheetId", i + 1),
                    new XAttribute(XlsxRelationships + "id", relationshipPlan.WorksheetIds[i])));
            }

            var originalRoot = LoadSnapshotXml(workbook.PackageSnapshot, "xl/workbook.xml")?.Root;
            var root = CreateSeededWorkbookRoot(originalRoot);
            root.SetAttributeValue(XNamespace.Xmlns + "r", XlsxRelationships);
            root.Add(
                CreateWorkbookPropertiesXml(workbook, originalRoot),
                CreateWorkbookProtectionXml(workbook.Security),
                CreateBookViewsXml(workbook, originalRoot),
                sheets,
                CreateDefinedNamesXml(workbook, originalRoot),
                CreateCalcPropertiesXml(workbook, originalRoot));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XElement CreateSeededWorkbookRoot(XElement? originalRoot)
        {
            if (originalRoot is null)
            {
                return new XElement(XlsxMain + "workbook");
            }

            var root = new XElement(originalRoot);
            root.Elements(XlsxMain + "workbookPr").Remove();
            root.Elements(XlsxMain + "workbookProtection").Remove();
            root.Elements(XlsxMain + "bookViews").Remove();
            root.Elements(XlsxMain + "sheets").Remove();
            root.Elements(XlsxMain + "definedNames").Remove();
            root.Elements(XlsxMain + "calcPr").Remove();
            return root;
        }

        private static XElement? CreateWorkbookPropertiesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "workbookPr");
            if (element is null && !workbook.Date1904)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "workbookPr");
            if (workbook.Date1904)
            {
                element.SetAttributeValue("date1904", "1");
            }
            else
            {
                element.SetAttributeValue("date1904", null);
            }

            return element.HasAttributes || element.HasElements ? element : null;
        }

        private static XElement? CreateWorkbookProtectionXml(OpenPyxlWorkbookSecurity security)
        {
            if (!security.HasSettings)
            {
                return null;
            }

            var attributes = new List<XAttribute>();
            AddOptionalBoolAttribute(attributes, "lockStructure", security.LockStructure);
            AddOptionalBoolAttribute(attributes, "lockWindows", security.LockWindows);
            AddOptionalBoolAttribute(attributes, "lockRevision", security.LockRevision);
            AddOptionalAttribute(attributes, "workbookPassword", security.WorkbookPassword);
            AddOptionalAttribute(attributes, "workbookPasswordCharacterSet", security.WorkbookPasswordCharacterSet);
            AddOptionalAttribute(attributes, "revisionsPassword", security.RevisionsPassword);
            AddOptionalAttribute(attributes, "revisionsPasswordCharacterSet", security.RevisionsPasswordCharacterSet);
            AddOptionalAttribute(attributes, "workbookAlgorithmName", security.WorkbookAlgorithmName);
            AddOptionalAttribute(attributes, "workbookHashValue", security.WorkbookHashValue);
            AddOptionalAttribute(attributes, "workbookSaltValue", security.WorkbookSaltValue);
            if (security.WorkbookSpinCount is not null)
            {
                attributes.Add(new XAttribute("workbookSpinCount", security.WorkbookSpinCount.Value));
            }

            AddOptionalAttribute(attributes, "revisionsAlgorithmName", security.RevisionsAlgorithmName);
            AddOptionalAttribute(attributes, "revisionsHashValue", security.RevisionsHashValue);
            AddOptionalAttribute(attributes, "revisionsSaltValue", security.RevisionsSaltValue);
            if (security.RevisionsSpinCount is not null)
            {
                attributes.Add(new XAttribute("revisionsSpinCount", security.RevisionsSpinCount.Value));
            }

            return new XElement(XlsxMain + "workbookProtection", attributes);
        }

        private static XElement? CreateBookViewsXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "bookViews");
            if (element is null && workbook.ActiveIndex == 0)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "bookViews");
            var view = element.Elements(XlsxMain + "workbookView").FirstOrDefault();
            if (view is null)
            {
                view = new XElement(XlsxMain + "workbookView");
                element.Add(view);
            }

            view.SetAttributeValue("activeTab", workbook.ActiveIndex);
            return element;
        }

        private static XElement? CreateDefinedNamesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var definedNames = CloneWorkbookChild(originalRoot, XlsxMain + "definedNames") ?? new XElement(XlsxMain + "definedNames");
            definedNames.Elements(XlsxMain + "definedName")
                .Where(element => (string?)element.Attribute("name") is "_xlnm.Print_Area" or "_xlnm.Print_Titles")
                .Remove();

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                var worksheet = workbook.Worksheets[i];
                if (worksheet.PrintArea is not null)
                {
                    definedNames.Add(new XElement(
                        XlsxMain + "definedName",
                        new XAttribute("name", "_xlnm.Print_Area"),
                        new XAttribute("localSheetId", i),
                        CreatePrintAreaDefinedNameText(worksheet)));
                }

                if (worksheet.PrintTitleRows is not null || worksheet.PrintTitleCols is not null)
                {
                    definedNames.Add(new XElement(
                        XlsxMain + "definedName",
                        new XAttribute("name", "_xlnm.Print_Titles"),
                        new XAttribute("localSheetId", i),
                        CreatePrintTitlesDefinedNameText(worksheet)));
                }
            }

            return definedNames.Elements().Any() ? definedNames : null;
        }

        private static XElement? CreateCalcPropertiesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "calcPr");
            if (element is null && !workbook.HasFormulaCells)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "calcPr");
            if (workbook.HasFormulaCells)
            {
                element.SetAttributeValue("fullCalcOnLoad", "1");
                element.SetAttributeValue("forceFullCalc", "1");
            }

            return element;
        }

        private static XElement? CloneWorkbookChild(XElement? originalRoot, XName name)
            => originalRoot?.Element(name) is { } child ? new XElement(child) : null;

        private static string CreatePrintAreaDefinedNameText(OpenPyxlWorksheet worksheet)
            => string.Join(
                ",",
                worksheet.PrintArea!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(range => SheetQualifiedReference(worksheet.Title, AbsoluteCellOrRangeReference(range))));

        private static string CreatePrintTitlesDefinedNameText(OpenPyxlWorksheet worksheet)
        {
            var references = new List<string>();
            if (worksheet.PrintTitleRows is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteRowRangeReference(worksheet.PrintTitleRows)));
            }

            if (worksheet.PrintTitleCols is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteColumnRangeReference(worksheet.PrintTitleCols)));
            }

            return string.Join(",", references);
        }

        private static string SheetQualifiedReference(string sheetTitle, string reference)
            => "'" + sheetTitle.Replace("'", "''", StringComparison.Ordinal) + "'!" + reference;

        private static string AbsoluteCellOrRangeReference(string reference)
        {
            if (!reference.Contains(':', StringComparison.Ordinal))
            {
                return AbsoluteCellReference(reference);
            }

            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return AbsoluteCellReference(parts[0]) + ":" + AbsoluteCellReference(parts[1]);
        }

        private static string AbsoluteCellReference(string reference)
        {
            var address = ParseCellAddress(reference, null!);
            return "$" + ColumnName(address.Column) + "$" + address.Row.ToString(CultureInfo.InvariantCulture);
        }

        private static string AbsoluteRowRangeReference(string reference)
        {
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return "$" + parts[0] + ":$" + parts[1];
        }

        private static string AbsoluteColumnRangeReference(string reference)
        {
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return "$" + parts[0] + ":$" + parts[1];
        }

        public static Dictionary<string, int> CreateNumberFormatStyleMap(OpenPyxlWorkbook workbook)
        {
            var formats = workbook.Worksheets
                .SelectMany(worksheet => worksheet.NumberFormats.Values)
                .Where(format => format != "General")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < formats.Length; index++)
            {
                result[formats[index]] = index + 1;
            }

            return result;
        }

        private sealed class OpenPyxlStyleRegistry
        {
            private const int FirstCustomNumberFormatId = 164;
            private readonly Dictionary<string, int> _cellStyleIds = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _numberFormatIds = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _fontIds = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _fillIds = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _borderIds = new(StringComparer.Ordinal);

            private OpenPyxlStyleRegistry()
            {
            }

            public List<OpenPyxlCellStyleDefinition> CellStyles { get; } = [];

            public List<KeyValuePair<string, int>> NumberFormats { get; } = [];

            public List<OpenPyxlStyleValue> Fonts { get; } = [];

            public List<OpenPyxlStyleValue> Fills { get; } = [];

            public List<OpenPyxlStyleValue> Borders { get; } = [];

            public bool HasCustomStyles => CellStyles.Count > 0;

            public static OpenPyxlStyleRegistry Create(OpenPyxlWorkbook workbook)
            {
                var registry = new OpenPyxlStyleRegistry();
                var definitions = new Dictionary<string, OpenPyxlCellStyleDefinition>(StringComparer.Ordinal);
                foreach (var worksheet in workbook.Worksheets)
                {
                    foreach (var address in StyledAddresses(worksheet))
                    {
                        var definition = CreateCellStyleDefinition(worksheet, address);
                        if (definition.IsDefault)
                        {
                            continue;
                        }

                        definitions.TryAdd(definition.Key, definition);
                    }
                }

                foreach (var definition in definitions.Values
                    .OrderBy(definition => definition.NumberFormat, StringComparer.Ordinal)
                    .ThenBy(definition => definition.Key, StringComparer.Ordinal))
                {
                    registry.AddCellStyle(definition);
                }

                return registry;
            }

            public bool TryGetCellStyleId(OpenPyxlWorksheet worksheet, int row, int column, out int styleId)
            {
                var definition = CreateCellStyleDefinition(worksheet, new CellAddress(row, column));
                return _cellStyleIds.TryGetValue(definition.Key, out styleId);
            }

            public int NumberFormatId(string format)
            {
                if (format == "General")
                {
                    return 0;
                }

                if (_numberFormatIds.TryGetValue(format, out var id))
                {
                    return id;
                }

                id = FirstCustomNumberFormatId + _numberFormatIds.Count;
                _numberFormatIds[format] = id;
                NumberFormats.Add(new KeyValuePair<string, int>(format, id));
                return id;
            }

            public int FontId(OpenPyxlStyleValue? font)
                => AddStyleComponent(font, _fontIds, Fonts, firstCustomId: 1);

            public int FillId(OpenPyxlStyleValue? fill)
                => AddStyleComponent(fill, _fillIds, Fills, firstCustomId: 2);

            public int BorderId(OpenPyxlStyleValue? border)
                => AddStyleComponent(border, _borderIds, Borders, firstCustomId: 1);

            private void AddCellStyle(OpenPyxlCellStyleDefinition definition)
            {
                if (_cellStyleIds.ContainsKey(definition.Key))
                {
                    return;
                }

                _cellStyleIds[definition.Key] = CellStyles.Count + 1;
                _ = NumberFormatId(definition.NumberFormat);
                _ = FontId(definition.Font);
                _ = FillId(definition.Fill);
                _ = BorderId(definition.Border);
                CellStyles.Add(definition);
            }

            private static int AddStyleComponent(
                OpenPyxlStyleValue? style,
                Dictionary<string, int> ids,
                List<OpenPyxlStyleValue> styles,
                int firstCustomId)
            {
                if (style is null)
                {
                    return 0;
                }

                var key = StyleValueKey(style);
                if (ids.TryGetValue(key, out var id))
                {
                    return id;
                }

                id = firstCustomId + styles.Count;
                ids[key] = id;
                styles.Add(style);
                return id;
            }

            private static IEnumerable<CellAddress> StyledAddresses(OpenPyxlWorksheet worksheet)
                => worksheet.NumberFormats.Keys
                    .Concat(worksheet.CellStyles.Keys.Select(key => key.Address))
                    .Distinct()
                    .OrderBy(address => address.Row)
                    .ThenBy(address => address.Column);

            private static OpenPyxlCellStyleDefinition CreateCellStyleDefinition(OpenPyxlWorksheet worksheet, CellAddress address)
            {
                var numberFormat = worksheet.GetCellNumberFormat(address.Row, address.Column);
                var font = worksheet.GetAssignedCellStyle(address.Row, address.Column, "font");
                var fill = worksheet.GetAssignedCellStyle(address.Row, address.Column, "fill");
                var border = worksheet.GetAssignedCellStyle(address.Row, address.Column, "border");
                var alignment = worksheet.GetAssignedCellStyle(address.Row, address.Column, "alignment");
                var protection = worksheet.GetAssignedCellStyle(address.Row, address.Column, "protection");
                return new OpenPyxlCellStyleDefinition(
                    CellStyleKey(numberFormat, font, fill, border, alignment, protection),
                    numberFormat,
                    font,
                    fill,
                    border,
                    alignment,
                    protection);
            }

            private static string CellStyleKey(
                string numberFormat,
                OpenPyxlStyleValue? font,
                OpenPyxlStyleValue? fill,
                OpenPyxlStyleValue? border,
                OpenPyxlStyleValue? alignment,
                OpenPyxlStyleValue? protection)
                => string.Join(
                    "|",
                    numberFormat,
                    StyleValueKey(font),
                    StyleValueKey(fill),
                    StyleValueKey(border),
                    StyleValueKey(alignment),
                    StyleValueKey(protection));
        }

        private sealed record OpenPyxlCellStyleDefinition(
            string Key,
            string NumberFormat,
            OpenPyxlStyleValue? Font,
            OpenPyxlStyleValue? Fill,
            OpenPyxlStyleValue? Border,
            OpenPyxlStyleValue? Alignment,
            OpenPyxlStyleValue? Protection)
        {
            public bool IsDefault =>
                NumberFormat == "General" &&
                Font is null &&
                Fill is null &&
                Border is null &&
                Alignment is null &&
                Protection is null;
        }

        private static Dictionary<string, string> LoadOptionalRelationships(ZipArchive archive, string path, LythonSourceSpan span)
        {
            if (archive.GetEntry(path) is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            return LoadRelationships(archive, path, span);
        }

        private static IReadOnlyList<XElement> LoadOptionalRelationshipElements(ZipArchive archive, string path, LythonSourceSpan span)
        {
            if (archive.GetEntry(path) is null)
            {
                return [];
            }

            return LoadXml(archive, path, span)
                .Root?
                .Elements(PackageRelationships + "Relationship")
                .Select(relationship => new XElement(relationship))
                .ToArray() ?? [];
        }

        private static XDocument CreateStylesXml(OpenPyxlStyleRegistry styles)
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    XlsxMain + "styleSheet",
                    styles.NumberFormats.Count == 0
                        ? null
                        : new XElement(
                            XlsxMain + "numFmts",
                            new XAttribute("count", styles.NumberFormats.Count),
                            styles.NumberFormats.OrderBy(pair => pair.Value).Select(pair => new XElement(
                                XlsxMain + "numFmt",
                                new XAttribute("numFmtId", pair.Value),
                                new XAttribute("formatCode", pair.Key)))),
                    new XElement(
                        XlsxMain + "fonts",
                        new XAttribute("count", styles.Fonts.Count + 1),
                        CreateDefaultFontXml(),
                        styles.Fonts.Select(CreateFontXml)),
                    new XElement(
                        XlsxMain + "fills",
                        new XAttribute("count", styles.Fills.Count + 2),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "none"))),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "gray125"))),
                        styles.Fills.Select(CreateFillXml)),
                    new XElement(
                        XlsxMain + "borders",
                        new XAttribute("count", styles.Borders.Count + 1),
                        CreateDefaultBorderXml(),
                        styles.Borders.Select(CreateBorderXml)),
                    new XElement(
                        XlsxMain + "cellStyleXfs",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"))),
                    new XElement(
                        XlsxMain + "cellXfs",
                        new XAttribute("count", styles.CellStyles.Count + 1),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"),
                            new XAttribute("xfId", "0")),
                        styles.CellStyles.Select(style => CreateCellFormatXml(styles, style))),
                    new XElement(
                        XlsxMain + "cellStyles",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "cellStyle",
                            new XAttribute("name", "Normal"),
                            new XAttribute("xfId", "0"),
                            new XAttribute("builtinId", "0")))));
        }

        private static XElement CreateDefaultFontXml()
            => new(
                XlsxMain + "font",
                new XElement(XlsxMain + "sz", new XAttribute("val", "11")),
                new XElement(XlsxMain + "color", new XAttribute("theme", "1")),
                new XElement(XlsxMain + "name", new XAttribute("val", "Calibri")),
                new XElement(XlsxMain + "family", new XAttribute("val", "2")),
                new XElement(XlsxMain + "scheme", new XAttribute("val", "minor")));

        private static XElement CreateFontXml(OpenPyxlStyleValue font)
        {
            var children = new List<object>();
            if (StyleBool(font, "bold"))
            {
                children.Add(new XElement(XlsxMain + "b"));
            }

            if (StyleBool(font, "italic"))
            {
                children.Add(new XElement(XlsxMain + "i"));
            }

            if (StyleString(font, "underline") is { } underline)
            {
                children.Add(underline == "single"
                    ? new XElement(XlsxMain + "u")
                    : new XElement(XlsxMain + "u", new XAttribute("val", underline)));
            }

            if (StyleString(font, "sz") is { } size)
            {
                children.Add(new XElement(XlsxMain + "sz", new XAttribute("val", size)));
            }

            if (CreateColorXml("color", StyleValue(font, "color")) is { } color)
            {
                children.Add(color);
            }

            if (StyleString(font, "name") is { } name)
            {
                children.Add(new XElement(XlsxMain + "name", new XAttribute("val", name)));
            }

            return new XElement(XlsxMain + "font", children);
        }

        private static XElement CreateFillXml(OpenPyxlStyleValue fill)
        {
            var pattern = new XElement(
                XlsxMain + "patternFill",
                new XAttribute("patternType", StyleString(fill, "fill_type") ?? "none"));
            if (CreateColorXml("fgColor", StyleValue(fill, "fgColor")) is { } fgColor)
            {
                pattern.Add(fgColor);
            }

            if (CreateColorXml("bgColor", StyleValue(fill, "bgColor")) is { } bgColor)
            {
                pattern.Add(bgColor);
            }

            return new XElement(XlsxMain + "fill", pattern);
        }

        private static XElement CreateDefaultBorderXml()
            => new(
                XlsxMain + "border",
                new XElement(XlsxMain + "left"),
                new XElement(XlsxMain + "right"),
                new XElement(XlsxMain + "top"),
                new XElement(XlsxMain + "bottom"),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderXml(OpenPyxlStyleValue border)
            => new(
                XlsxMain + "border",
                CreateBorderSideXml("left", StyleValue(border, "left")),
                CreateBorderSideXml("right", StyleValue(border, "right")),
                CreateBorderSideXml("top", StyleValue(border, "top")),
                CreateBorderSideXml("bottom", StyleValue(border, "bottom")),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderSideXml(string name, object? value)
        {
            if (value is not OpenPyxlStyleValue side)
            {
                return new XElement(XlsxMain + name);
            }

            var attributes = new List<XAttribute>();
            if (StyleString(side, "style") is { } style)
            {
                attributes.Add(new XAttribute("style", style));
            }

            var element = new XElement(XlsxMain + name, attributes);
            if (CreateColorXml("color", StyleValue(side, "color")) is { } color)
            {
                element.Add(color);
            }

            return element;
        }

        private static XElement? CreateColorXml(string elementName, object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is OpenPyxlColor color)
            {
                var attributes = new List<XAttribute>();
                switch (color.Type)
                {
                    case "indexed" when color.Indexed is { } indexed:
                        attributes.Add(new XAttribute("indexed", indexed.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case "theme" when color.Theme is { } theme:
                        attributes.Add(new XAttribute("theme", theme.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case "auto" when color.Auto is { } auto:
                        attributes.Add(new XAttribute("auto", auto ? "1" : "0"));
                        break;
                    default:
                        if (color.Rgb is { } rgb)
                        {
                            attributes.Add(new XAttribute("rgb", rgb));
                        }

                        break;
                }

                if (Math.Abs(color.Tint) > double.Epsilon)
                {
                    attributes.Add(new XAttribute("tint", color.Tint.ToString(CultureInfo.InvariantCulture)));
                }

                return attributes.Count == 0 ? null : new XElement(XlsxMain + elementName, attributes);
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return new XElement(XlsxMain + elementName, new XAttribute("rgb", text.AsString()));
            }

            return null;
        }

        private static XElement CreateCellFormatXml(OpenPyxlStyleRegistry registry, OpenPyxlCellStyleDefinition style)
        {
            var attributes = new List<XAttribute>
            {
                new("numFmtId", registry.NumberFormatId(style.NumberFormat)),
                new("fontId", registry.FontId(style.Font)),
                new("fillId", registry.FillId(style.Fill)),
                new("borderId", registry.BorderId(style.Border)),
                new("xfId", "0"),
            };
            if (style.NumberFormat != "General")
            {
                attributes.Add(new XAttribute("applyNumberFormat", "1"));
            }

            if (style.Font is not null)
            {
                attributes.Add(new XAttribute("applyFont", "1"));
            }

            if (style.Fill is not null)
            {
                attributes.Add(new XAttribute("applyFill", "1"));
            }

            if (style.Border is not null)
            {
                attributes.Add(new XAttribute("applyBorder", "1"));
            }

            var children = new List<object>();
            if (style.Alignment is not null)
            {
                attributes.Add(new XAttribute("applyAlignment", "1"));
                children.Add(CreateAlignmentXml(style.Alignment));
            }

            if (style.Protection is not null)
            {
                attributes.Add(new XAttribute("applyProtection", "1"));
                children.Add(CreateProtectionXml(style.Protection));
            }

            return new XElement(XlsxMain + "xf", attributes, children);
        }

        private static XElement CreateAlignmentXml(OpenPyxlStyleValue alignment)
        {
            var attributes = new List<XAttribute>();
            AddOptionalAttribute(attributes, "horizontal", StyleString(alignment, "horizontal"));
            AddOptionalAttribute(attributes, "vertical", StyleString(alignment, "vertical"));
            if (StyleBool(alignment, "wrap_text"))
            {
                attributes.Add(new XAttribute("wrapText", "1"));
            }

            AddOptionalAttribute(attributes, "textRotation", StyleString(alignment, "text_rotation"));
            return new XElement(XlsxMain + "alignment", attributes);
        }

        private static XElement CreateProtectionXml(OpenPyxlStyleValue protection)
            => new(
                XlsxMain + "protection",
                new XAttribute("locked", StyleBool(protection, "locked") ? "1" : "0"),
                new XAttribute("hidden", StyleBool(protection, "hidden") ? "1" : "0"));

        private static void AddOptionalAttribute(List<XAttribute> attributes, string name, string? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value));
            }
        }

        private static void AddOptionalBoolAttribute(List<XAttribute> attributes, string name, bool? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value.Value ? "1" : "0"));
            }
        }

        private static object? StyleValue(OpenPyxlStyleValue style, string name)
            => style.TryGetMember(name, out var value) && value is not PyNone ? value : null;

        private static string? StyleString(OpenPyxlStyleValue style, string name)
        {
            var value = StyleValue(style, name);
            if (value is null)
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return integer.ToString(CultureInfo.InvariantCulture);
            }

            return value is double floating
                ? floating.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        private static bool StyleBool(OpenPyxlStyleValue style, string name)
            => StyleValue(style, name) is bool value && value;

        private static string StyleValueKey(OpenPyxlStyleValue? style)
        {
            if (style is null)
            {
                return string.Empty;
            }

            return style.QualifiedName + "(" + string.Join(
                ",",
                StyleMemberNames(style.QualifiedName).Select(name => name + "=" + StyleObjectKey(StyleValue(style, name)))) + ")";
        }

        private static string StyleObjectKey(object? value)
        {
            if (value is null or PyNone)
            {
                return "none";
            }

            if (value is OpenPyxlStyleValue style)
            {
                return StyleValueKey(style);
            }

            if (value is OpenPyxlColor color)
            {
                return color.Key;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return "str:" + text.AsString();
            }

            if (value is bool boolean)
            {
                return boolean ? "bool:true" : "bool:false";
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return "int:" + integer.ToString(CultureInfo.InvariantCulture);
            }

            if (value is double floating)
            {
                return "float:" + floating.ToString(CultureInfo.InvariantCulture);
            }

            return value.GetType().FullName + ":" + value;
        }

        private static string[] StyleMemberNames(string qualifiedName)
            => qualifiedName switch
            {
                "openpyxl.styles.Font" => ["name", "sz", "bold", "italic", "color", "underline"],
                "openpyxl.styles.PatternFill" => ["fill_type", "fgColor", "bgColor"],
                "openpyxl.styles.Border" => ["left", "right", "top", "bottom"],
                "openpyxl.styles.Side" => ["style", "color"],
                "openpyxl.styles.Alignment" => ["horizontal", "vertical", "wrap_text", "text_rotation"],
                "openpyxl.styles.Protection" => ["locked", "hidden"],
                "openpyxl.styles.NamedStyle" => ["name"],
                _ => [],
            };

        private static XDocument CreateWorksheetXml(
            OpenPyxlWorksheet worksheet,
            OpenPyxlStyleRegistry styleRegistry,
            bool preserveLoadedStyleIds,
            OpenPyxlWorksheetRelationshipPlan relationshipPlan)
        {
            var sheetData = new XElement(XlsxMain + "sheetData");
            var cellsByRow = worksheet.Cells
                .OrderBy(pair => pair.Key.Row)
                .ThenBy(pair => pair.Key.Column)
                .GroupBy(pair => pair.Key.Row)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var numberFormatsByRow = worksheet.NumberFormats
                .OrderBy(pair => pair.Key.Row)
                .ThenBy(pair => pair.Key.Column)
                .GroupBy(pair => pair.Key.Row)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var loadedStyleIdsByRow = worksheet.LoadedStyleIds
                .OrderBy(pair => pair.Key.Row)
                .ThenBy(pair => pair.Key.Column)
                .GroupBy(pair => pair.Key.Row)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var styledAddressesByRow = worksheet.CellStyles.Keys
                .Select(key => key.Address)
                .OrderBy(address => address.Row)
                .ThenBy(address => address.Column)
                .GroupBy(address => address.Row)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var rowIndexes = cellsByRow.Keys
                .Concat(numberFormatsByRow.Keys)
                .Concat(loadedStyleIdsByRow.Keys)
                .Concat(styledAddressesByRow.Keys)
                .Concat(worksheet.RowDimensions.Keys)
                .Distinct()
                .OrderBy(row => row);
            foreach (var rowIndex in rowIndexes)
            {
                worksheet.RowDimensions.TryGetValue(rowIndex, out var rowDimension);
                var row = CreateRowXml(rowIndex, rowDimension);
                var rowAddresses = new SortedSet<int>();
                if (cellsByRow.TryGetValue(rowIndex, out var rowCells))
                {
                    foreach (var pair in rowCells)
                    {
                        rowAddresses.Add(pair.Key.Column);
                    }
                }

                if (numberFormatsByRow.TryGetValue(rowIndex, out var rowFormats))
                {
                    foreach (var pair in rowFormats)
                    {
                        rowAddresses.Add(pair.Key.Column);
                    }
                }

                if (loadedStyleIdsByRow.TryGetValue(rowIndex, out var rowStyleIds))
                {
                    foreach (var pair in rowStyleIds)
                    {
                        rowAddresses.Add(pair.Key.Column);
                    }
                }

                if (styledAddressesByRow.TryGetValue(rowIndex, out var rowStyles))
                {
                    foreach (var address in rowStyles)
                    {
                        rowAddresses.Add(address.Column);
                    }
                }

                foreach (var column in rowAddresses)
                {
                    var address = new CellAddress(rowIndex, column);
                    worksheet.Cells.TryGetValue(address, out var value);
                    worksheet.LoadedStyleIds.TryGetValue(address, out var loadedStyleId);
                    row.Add(CreateCellXml(
                        worksheet,
                        rowIndex,
                        column,
                        value ?? PyNone.Instance,
                        worksheet.GetCellNumberFormat(rowIndex, column),
                        worksheet.GetCellDataType(rowIndex, column),
                        worksheet.GetFormulaCachedValue(rowIndex, column),
                        worksheet.GetFormulaXml(rowIndex, column),
                        worksheet.Workbook?.Date1904 ?? false,
                        styleRegistry,
                        preserveLoadedStyleIds,
                        loadedStyleId));
                }

                sheetData.Add(row);
            }

            var root = CreateSeededWorksheetRoot(worksheet);
            if (worksheet.Hyperlinks.Count > 0)
            {
                root.SetAttributeValue(XNamespace.Xmlns + "r", XlsxRelationships);
            }

            root.Add(
                new XElement(XlsxMain + "dimension", new XAttribute("ref", WorksheetDimension(worksheet))),
                CreateSheetViewsXml(worksheet),
                CreateColsXml(worksheet),
                sheetData,
                CreateAutoFilterXml(worksheet),
                CreateMergeCellsXml(worksheet),
                CreateConditionalFormattingXml(worksheet),
                CreateDataValidationsXml(worksheet),
                CreateSheetProtectionXml(worksheet),
                CreateHyperlinksXml(worksheet, relationshipPlan),
                CreatePageMarginsXml(worksheet),
                CreatePageSetupXml(worksheet));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XElement CreateSeededWorksheetRoot(OpenPyxlWorksheet worksheet)
        {
            var snapshot = worksheet.Workbook?.PackageSnapshot;
            var originalRoot = worksheet.SourcePath is null
                ? null
                : LoadSnapshotXml(snapshot, worksheet.SourcePath)?.Root;
            if (originalRoot is null)
            {
                return new XElement(XlsxMain + "worksheet");
            }

            var root = new XElement(originalRoot);
            root.Elements(XlsxMain + "dimension").Remove();
            root.Elements(XlsxMain + "sheetViews").Remove();
            root.Elements(XlsxMain + "cols").Remove();
            root.Elements(XlsxMain + "sheetData").Remove();
            root.Elements(XlsxMain + "autoFilter").Remove();
            root.Elements(XlsxMain + "mergeCells").Remove();
            root.Elements(XlsxMain + "conditionalFormatting").Remove();
            root.Elements(XlsxMain + "dataValidations").Remove();
            root.Elements(XlsxMain + "sheetProtection").Remove();
            root.Elements(XlsxMain + "hyperlinks").Remove();
            root.Elements(XlsxMain + "pageMargins").Remove();
            root.Elements(XlsxMain + "pageSetup").Remove();
            return root;
        }

        private static XElement CreateRowXml(int rowIndex, OpenPyxlRowDimension? dimension)
        {
            var attributes = new List<object> { new XAttribute("r", rowIndex) };
            if (dimension?.Height is not null)
            {
                attributes.Add(new XAttribute("ht", dimension.Height.Value.ToString("R", CultureInfo.InvariantCulture)));
                attributes.Add(new XAttribute("customHeight", "1"));
            }

            if (dimension?.Hidden == true)
            {
                attributes.Add(new XAttribute("hidden", "1"));
            }

            return new XElement(XlsxMain + "row", attributes);
        }

        private static XElement? CreateColsXml(OpenPyxlWorksheet worksheet)
        {
            var dimensions = worksheet.ColumnDimensions.Values
                .Where(dimension => dimension.Width is not null || dimension.Hidden)
                .OrderBy(dimension => dimension.Column)
                .ToArray();
            if (dimensions.Length == 0)
            {
                return null;
            }

            return new XElement(
                XlsxMain + "cols",
                dimensions.Select(dimension =>
                {
                    var attributes = new List<object>
                    {
                        new XAttribute("min", dimension.Column),
                        new XAttribute("max", dimension.Column),
                    };
                    if (dimension.Width is not null)
                    {
                        attributes.Add(new XAttribute("width", dimension.Width.Value.ToString("R", CultureInfo.InvariantCulture)));
                        attributes.Add(new XAttribute("customWidth", "1"));
                    }

                    if (dimension.Hidden)
                    {
                        attributes.Add(new XAttribute("hidden", "1"));
                    }

                    return new XElement(XlsxMain + "col", attributes);
                }));
        }

        private static XElement? CreateSheetViewsXml(OpenPyxlWorksheet worksheet)
        {
            var hasPane = worksheet.FreezePanes is not null;
            var hasSelection =
                worksheet.SelectionActiveCell != "A1" ||
                worksheet.SelectionSqref != "A1" ||
                worksheet.SelectionPane is not null;
            if (!hasPane &&
                !hasSelection &&
                worksheet.ShowGridLines &&
                !worksheet.TabSelected &&
                worksheet.WorkbookViewId == 0)
            {
                return null;
            }

            var sheetViewAttributes = new List<object>
            {
                new XAttribute("workbookViewId", worksheet.WorkbookViewId),
            };
            if (!worksheet.ShowGridLines)
            {
                sheetViewAttributes.Add(new XAttribute("showGridLines", "0"));
            }

            if (worksheet.TabSelected)
            {
                sheetViewAttributes.Add(new XAttribute("tabSelected", "1"));
            }

            var sheetViewChildren = new List<object>();
            if (hasPane)
            {
                var address = ParseCellAddress(worksheet.FreezePanes!, null!);
                var paneAttributes = new List<object>
                {
                    new XAttribute("topLeftCell", worksheet.FreezePanes!),
                    new XAttribute("state", "frozen"),
                };
                if (address.Column > 1)
                {
                    paneAttributes.Add(new XAttribute("xSplit", address.Column - 1));
                }

                if (address.Row > 1)
                {
                    paneAttributes.Add(new XAttribute("ySplit", address.Row - 1));
                }

                sheetViewChildren.Add(new XElement(XlsxMain + "pane", paneAttributes));
            }

            if (hasSelection)
            {
                var selectionAttributes = new List<object>();
                if (worksheet.SelectionPane is not null)
                {
                    selectionAttributes.Add(new XAttribute("pane", worksheet.SelectionPane));
                }

                if (worksheet.SelectionActiveCell != "A1")
                {
                    selectionAttributes.Add(new XAttribute("activeCell", worksheet.SelectionActiveCell));
                }

                if (worksheet.SelectionSqref != "A1")
                {
                    selectionAttributes.Add(new XAttribute("sqref", worksheet.SelectionSqref));
                }

                sheetViewChildren.Add(new XElement(XlsxMain + "selection", selectionAttributes));
            }

            return new XElement(
                XlsxMain + "sheetViews",
                new XElement(XlsxMain + "sheetView", sheetViewAttributes, sheetViewChildren));
        }

        private static XElement? CreateAutoFilterXml(OpenPyxlWorksheet worksheet)
            => worksheet.AutoFilterRef is null
                ? null
                : new XElement(XlsxMain + "autoFilter", new XAttribute("ref", worksheet.AutoFilterRef));

        private static XElement? CreateMergeCellsXml(OpenPyxlWorksheet worksheet)
        {
            if (worksheet.MergedRanges.Count == 0)
            {
                return null;
            }

            return new XElement(
                XlsxMain + "mergeCells",
                new XAttribute("count", worksheet.MergedRanges.Count),
                worksheet.MergedRanges.Select(range => new XElement(XlsxMain + "mergeCell", new XAttribute("ref", range.Reference))));
        }

        private static IEnumerable<XElement> CreateConditionalFormattingXml(OpenPyxlWorksheet worksheet)
            => worksheet.ConditionalFormattings
                .Where(formatting => formatting.Ranges.Count > 0)
                .Select(formatting => new XElement(
                    XlsxMain + "conditionalFormatting",
                    new XAttribute("sqref", formatting.Sqref),
                    formatting.Rules.Select(CreateConditionalFormattingRuleXml)));

        private static XElement CreateConditionalFormattingRuleXml(OpenPyxlConditionalFormattingRule rule)
        {
            if (rule.SourceXml is not null)
            {
                return new XElement(rule.SourceXml);
            }

            var attributes = new List<XAttribute>();
            AddOptionalAttribute(attributes, "type", rule.Type);
            AddOptionalAttribute(attributes, "operator", rule.Operator);
            if (rule.Priority is not null)
            {
                attributes.Add(new XAttribute("priority", rule.Priority.Value));
            }

            return new XElement(
                XlsxMain + "cfRule",
                attributes,
                rule.Formulas.Select(formula => new XElement(XlsxMain + "formula", formula)));
        }

        private static XElement? CreateDataValidationsXml(OpenPyxlWorksheet worksheet)
        {
            var validations = worksheet.DataValidations
                .Where(validation => validation.Ranges.Count > 0)
                .ToArray();
            if (validations.Length == 0)
            {
                return null;
            }

            return new XElement(
                XlsxMain + "dataValidations",
                new XAttribute("count", validations.Length),
                validations.Select(CreateDataValidationXml));
        }

        private static XElement CreateDataValidationXml(OpenPyxlDataValidation validation)
        {
            var attributes = new List<XAttribute>
            {
                new XAttribute("sqref", validation.Sqref),
            };
            AddOptionalAttribute(attributes, "type", validation.Type);
            AddOptionalAttribute(attributes, "operator", validation.Operator);
            AddOptionalAttribute(attributes, "errorTitle", validation.ErrorTitle);
            AddOptionalAttribute(attributes, "error", validation.Error);
            AddOptionalAttribute(attributes, "promptTitle", validation.PromptTitle);
            AddOptionalAttribute(attributes, "prompt", validation.Prompt);
            if (validation.AllowBlank)
            {
                attributes.Add(new XAttribute("allowBlank", "1"));
            }

            if (!validation.ShowErrorMessage)
            {
                attributes.Add(new XAttribute("showErrorMessage", "0"));
            }

            if (!validation.ShowInputMessage)
            {
                attributes.Add(new XAttribute("showInputMessage", "0"));
            }

            return new XElement(
                XlsxMain + "dataValidation",
                attributes,
                validation.Formula1 is null ? null : new XElement(XlsxMain + "formula1", validation.Formula1),
                validation.Formula2 is null ? null : new XElement(XlsxMain + "formula2", validation.Formula2));
        }

        private static XElement? CreateSheetProtectionXml(OpenPyxlWorksheet worksheet)
        {
            var protection = worksheet.Protection;
            if (!protection.HasSettings)
            {
                return null;
            }

            var attributes = new List<XAttribute>();
            if (protection.Sheet)
            {
                attributes.Add(new XAttribute("sheet", "1"));
            }

            if (protection.Objects)
            {
                attributes.Add(new XAttribute("objects", "1"));
            }

            if (protection.Scenarios)
            {
                attributes.Add(new XAttribute("scenarios", "1"));
            }

            AddOptionalAttribute(attributes, "password", protection.Password);
            AddOptionalAttribute(attributes, "algorithmName", protection.AlgorithmName);
            AddOptionalAttribute(attributes, "hashValue", protection.HashValue);
            AddOptionalAttribute(attributes, "saltValue", protection.SaltValue);
            if (protection.SpinCount is not null)
            {
                attributes.Add(new XAttribute("spinCount", protection.SpinCount.Value));
            }

            return new XElement(XlsxMain + "sheetProtection", attributes);
        }

        private static XElement? CreateHyperlinksXml(OpenPyxlWorksheet worksheet, OpenPyxlWorksheetRelationshipPlan relationshipPlan)
        {
            if (worksheet.Hyperlinks.Count == 0)
            {
                return null;
            }

            var hyperlinks = new XElement(XlsxMain + "hyperlinks");
            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                hyperlinks.Add(new XElement(
                    XlsxMain + "hyperlink",
                    new XAttribute("ref", CellReference(pair.Key.Row, pair.Key.Column)),
                    new XAttribute(XlsxRelationships + "id", relationshipPlan.HyperlinkIds[pair.Key])));
            }

            return hyperlinks;
        }

        private static XElement? CreatePageMarginsXml(OpenPyxlWorksheet worksheet)
        {
            if (!worksheet.HasPageMargins)
            {
                return null;
            }

            var margins = worksheet.PageMargins;
            return new XElement(
                XlsxMain + "pageMargins",
                new XAttribute("left", margins.Left.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("right", margins.Right.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("top", margins.Top.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("bottom", margins.Bottom.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("header", margins.Header.ToString("R", CultureInfo.InvariantCulture)),
                new XAttribute("footer", margins.Footer.ToString("R", CultureInfo.InvariantCulture)));
        }

        private static XElement? CreatePageSetupXml(OpenPyxlWorksheet worksheet)
        {
            var setup = worksheet.PageSetup;
            if (!setup.HasSettings)
            {
                return null;
            }

            var attributes = new List<object>();
            if (setup.Orientation is not null)
            {
                attributes.Add(new XAttribute("orientation", setup.Orientation));
            }

            if (setup.PaperSize is not null)
            {
                attributes.Add(new XAttribute("paperSize", setup.PaperSize.Value));
            }

            if (setup.FitToWidth is not null)
            {
                attributes.Add(new XAttribute("fitToWidth", setup.FitToWidth.Value));
            }

            if (setup.FitToHeight is not null)
            {
                attributes.Add(new XAttribute("fitToHeight", setup.FitToHeight.Value));
            }

            if (setup.Scale is not null)
            {
                attributes.Add(new XAttribute("scale", setup.Scale.Value));
            }

            return new XElement(XlsxMain + "pageSetup", attributes);
        }

        private static XElement CreateCellXml(
            OpenPyxlWorksheet worksheet,
            int row,
            int column,
            object value,
            string numberFormat,
            string dataType,
            object formulaCachedValue,
            XElement? formulaXml,
            bool date1904,
            OpenPyxlStyleRegistry styleRegistry,
            bool preserveLoadedStyleIds,
            int loadedStyleId)
        {
            var reference = CellReference(row, column);
            var attributes = CellAttributes(worksheet, row, column, reference, styleRegistry, preserveLoadedStyleIds, loadedStyleId);
            if (value is PyNone)
            {
                return new XElement(XlsxMain + "c", attributes);
            }

            if (value is bool boolean)
            {
                return new XElement(
                    XlsxMain + "c",
                    attributes,
                    new XAttribute("t", "b"),
                    new XElement(XlsxMain + "v", boolean ? "1" : "0"));
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                var rawText = text.AsString();
                if (dataType == "e")
                {
                    return new XElement(
                        XlsxMain + "c",
                        attributes,
                        new XAttribute("t", "e"),
                        new XElement(XlsxMain + "v", rawText));
                }

                if (IsFormulaText(rawText))
                {
                    var formulaText = rawText[1..];
                    return new XElement(
                        XlsxMain + "c",
                        attributes,
                        CreateFormulaXml(formulaText, formulaXml),
                        CreateFormulaCachedValueXml(formulaCachedValue, date1904));
                }

                var textElement = new XElement(XlsxMain + "t", rawText);
                if (rawText.Length != rawText.Trim().Length)
                {
                    textElement.SetAttributeValue(XmlNamespace + "space", "preserve");
                }

                return new XElement(
                    XlsxMain + "c",
                    attributes,
                    new XAttribute("t", "inlineStr"),
                    new XElement(XlsxMain + "is", textElement));
            }

            return new XElement(
                XlsxMain + "c",
                attributes,
                new XElement(XlsxMain + "v", CellNumberText(value, date1904)));
        }

        private static XElement CreateFormulaXml(string formulaText, XElement? loadedFormulaXml)
        {
            if (loadedFormulaXml is not null && string.Equals(loadedFormulaXml.Value, formulaText, StringComparison.Ordinal))
            {
                return new XElement(loadedFormulaXml);
            }

            return new XElement(XlsxMain + "f", formulaText);
        }

        private static List<XAttribute> CellAttributes(
            OpenPyxlWorksheet worksheet,
            int row,
            int column,
            string reference,
            OpenPyxlStyleRegistry styleRegistry,
            bool preserveLoadedStyleIds,
            int loadedStyleId)
        {
            var attributes = new List<XAttribute> { new("r", reference) };
            if (preserveLoadedStyleIds && loadedStyleId > 0)
            {
                attributes.Add(new XAttribute("s", loadedStyleId));
            }
            else if (styleRegistry.TryGetCellStyleId(
                worksheet,
                row,
                column,
                out var styleIndex))
            {
                attributes.Add(new XAttribute("s", styleIndex));
            }

            return attributes;
        }

        private static XElement? CreateFormulaCachedValueXml(object value, bool date1904)
        {
            if (value is PyNone)
            {
                return null;
            }

            if (value is bool boolean)
            {
                return new XElement(XlsxMain + "v", boolean ? "1" : "0");
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return new XElement(XlsxMain + "v", text.AsString());
            }

            return new XElement(XlsxMain + "v", CellNumberText(value, date1904));
        }

        private static string CellNumberText(object value, bool date1904)
        {
            return value switch
            {
                BigInteger integer => integer.ToString(CultureInfo.InvariantCulture),
                double floating => floating.ToString("R", CultureInfo.InvariantCulture),
                PyDecimal decimalValue => decimalValue.Value.ToString(CultureInfo.InvariantCulture),
                PyDate date => ExcelSerialFromDate(date, date1904).ToString("R", CultureInfo.InvariantCulture),
                PyDateTime dateTime => ExcelSerialFromDateTime(dateTime, date1904).ToString("R", CultureInfo.InvariantCulture),
                PyTime time => ExcelSerialFromTime(time).ToString("R", CultureInfo.InvariantCulture),
                PyTimedelta delta => ExcelSerialFromTimedelta(delta).ToString("R", CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException("Unsupported cell value.")
            };
        }

        private static void WriteXml(ZipArchive archive, string path, XDocument document)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using var stream = entry.Open();
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                OmitXmlDeclaration = false,
            });
            document.Save(writer);
        }

        private static string ResolvePackagePath(string sourcePart, string target)
        {
            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                return target.TrimStart('/');
            }

            var slash = sourcePart.LastIndexOf('/');
            var directory = slash < 0 ? string.Empty : sourcePart[..(slash + 1)];
            var combined = directory + target;
            var parts = new List<string>();
            foreach (var part in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".")
                {
                    continue;
                }

                if (part == "..")
                {
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }

                    continue;
                }

                parts.Add(part);
            }

            return string.Join("/", parts);
        }

        private static string WorksheetRelationshipsPath(string worksheetPath)
            => PartRelationshipsPath(worksheetPath);

        private static string PartRelationshipsPath(string partPath)
        {
            var slash = partPath.LastIndexOf('/');
            var directory = slash < 0 ? string.Empty : partPath[..(slash + 1)];
            var fileName = slash < 0 ? partPath : partPath[(slash + 1)..];
            return directory + "_rels/" + fileName + ".rels";
        }
    }

    internal readonly record struct OpenPyxlSaveGuard(bool CanSave, string? Reason)
    {
        public static readonly OpenPyxlSaveGuard Safe = new(true, null);

        public static OpenPyxlSaveGuard Unsafe(string reason) => new(false, reason);
    }

    internal readonly record struct OpenPyxlPackageSnapshot(IReadOnlyDictionary<string, byte[]> Parts);

    internal readonly record struct CellAddress(int Row, int Column);

    internal readonly record struct CellRangeAddress(CellAddress Start, CellAddress End)
    {
        public string Reference => CellReference(Start.Row, Start.Column) + ":" + CellReference(End.Row, End.Column);

        public string CellOrRangeReference => Start.Equals(End) ? CellReference(Start.Row, Start.Column) : Reference;
    }

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

    private static int ExpectPositiveInt(object value, string owner, LythonSourceSpan span)
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

        return ExpectPositiveInt(value, owner, span!);
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

        if (value is string text)
        {
            return PyString.FromString(text);
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

    private static CellAddress ParseCellAddress(string reference, LythonSourceSpan span)
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

    private static (CellAddress Start, CellAddress End) ParseRange(string reference, LythonSourceSpan span)
    {
        var parts = reference.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid cell range: {reference}", span);
        }

        var start = ParseCellAddress(parts[0], span);
        var end = ParseCellAddress(parts[1], span);
        return (
            new CellAddress(Math.Min(start.Row, end.Row), Math.Min(start.Column, end.Column)),
            new CellAddress(Math.Max(start.Row, end.Row), Math.Max(start.Column, end.Column)));
    }

    private static CellRangeAddress ParseCellRange(string reference, LythonSourceSpan span)
    {
        var (start, end) = ParseRange(reference, span);
        return new CellRangeAddress(start, end);
    }

    private static CellRangeAddress ParseCellOrRange(string reference, LythonSourceSpan span)
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
        foreach (var item in ToSequence(value, span!))
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
            ? ParseCellRange(text, span!).Reference
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

        var start = ParseColumnName(parts[0], span!);
        var end = ParseColumnName(parts[1], span!);
        return ColumnName(Math.Min(start, end)) + ":" + ColumnName(Math.Max(start, end));
    }

    private static (string? Rows, string? Columns) NormalizePrintTitlesText(string text, LythonSourceSpan? span)
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

        return (rows, columns);
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
        var address = ParseCellAddress(normalized, span!);
        if (address.Row == 1 && address.Column == 1)
        {
            return null;
        }

        return normalized;
    }

    private static string NormalizeCellReference(object value, string owner, LythonSourceSpan? span)
    {
        var address = ParseCellAddress(ExpectString(value, owner, span), span!);
        return CellReference(address.Row, address.Column);
    }

    private static string? NormalizeOptionalRangeReference(object value, string owner, LythonSourceSpan? span)
    {
        if (value is PyNone)
        {
            return null;
        }

        return ParseCellRange(ExpectString(value, owner, span), span!).Reference;
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
                ? ParseCellRange(parts[i], span!).Reference
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

    private static int ParseColumnName(string text, LythonSourceSpan span)
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
