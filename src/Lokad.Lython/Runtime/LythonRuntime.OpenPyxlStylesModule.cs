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
    private sealed class OpenPyxlStylesModule : OpenPyxlDeferredModule
    {
        public static readonly OpenPyxlStylesModule Instance = new();

        private OpenPyxlStylesModule()
            : base("openpyxl.styles", new Dictionary<string, object>
            {
                ["Font"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlFont, CreateFont),
                ["PatternFill"] = BuiltinCallable.Create("openpyxl.styles.PatternFill", CreatePatternFill, ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], requiredCount: 0),
                ["GradientFill"] = UnsupportedOpenPyxlCallable("openpyxl.styles.GradientFill"),
                ["Border"] = BuiltinCallable.Create("openpyxl.styles.Border", CreateBorder, ["left", "right", "top", "bottom"], requiredCount: 0),
                ["Side"] = BuiltinCallable.Create("openpyxl.styles.Side", CreateSide, ["style", "color", "border_style"], requiredCount: 0),
                ["Alignment"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlAlignment, CreateAlignment),
                ["Protection"] = BuiltinCallable.Create("openpyxl.styles.Protection", CreateProtection, ["locked", "hidden"], requiredCount: 0),
                ["NamedStyle"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlNamedStyle, CreateNamedStyle),
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
                ["Color"] = BuiltinCallable.Create(LythonKnownCallableSignatures.OpenPyxlColor, CreateColor),
                ["BLACK"] = PyString.FromString("00000000"),
                ["WHITE"] = PyString.FromString("00FFFFFF"),
                ["BLUE"] = PyString.FromString("000000FF"),
            })
        {
        }
    }

    internal sealed class OpenPyxlColor : IPyDynamicAttributes, IPyRenderableValue, IPyStringCoercibleValue, IEquatable<OpenPyxlColor>
    {
        private readonly ColorValue _value;

        private OpenPyxlColor(ColorValue value, double tint)
        {
            _value = value;
            Tint = tint;
        }

        public static OpenPyxlColor FromRgb(string? rgb, double tint)
            => new(new RgbColorValue(rgb is null ? null : NormalizeRgbColor(rgb, "openpyxl color", null)), tint);

        public static OpenPyxlColor FromIndexed(BigInteger? indexed, double tint)
            => new(new IndexedColorValue(indexed), tint);

        public static OpenPyxlColor FromTheme(BigInteger? theme, double tint)
            => new(new ThemeColorValue(theme), tint);

        public static OpenPyxlColor FromAuto(bool? auto, double tint)
            => new(new AutoColorValue(auto), tint);

        public OpenPyxlColorKind Kind => _value.Kind;

        public string Type => Kind switch
        {
            OpenPyxlColorKind.Rgb => "rgb",
            OpenPyxlColorKind.Indexed => "indexed",
            OpenPyxlColorKind.Theme => "theme",
            OpenPyxlColorKind.Auto => "auto",
            _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown openpyxl color kind."),
        };

        public string? Rgb => (_value as RgbColorValue)?.Value;

        public BigInteger? Indexed => (_value as IndexedColorValue)?.Value;

        public BigInteger? Theme => (_value as ThemeColorValue)?.Value;

        public double Tint { get; }

        public bool? Auto => (_value as AutoColorValue)?.Value;

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
        public PyString ToPyString() => PyString.FromString(ColorIndexText());

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return ToPyString();
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public bool Equals(OpenPyxlColor? other)
            => other is not null &&
               _value == other._value &&
               Tint.Equals(other.Tint);

        public override bool Equals(object? obj) => obj is OpenPyxlColor other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_value, Tint);

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
            => Kind switch
            {
                OpenPyxlColorKind.Rgb => OptionalStringValue(Rgb),
                OpenPyxlColorKind.Indexed => OptionalIntegerValue(Indexed),
                OpenPyxlColorKind.Theme => OptionalIntegerValue(Theme),
                OpenPyxlColorKind.Auto => Auto is { } auto ? auto : PyNone.Instance,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown openpyxl color kind."),
            };

        private string ColorIndexText()
            => Kind switch
            {
                OpenPyxlColorKind.Rgb => Rgb ?? string.Empty,
                OpenPyxlColorKind.Indexed => Indexed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                OpenPyxlColorKind.Theme => Theme?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                OpenPyxlColorKind.Auto => Auto is { } auto ? (auto ? "1" : "0") : string.Empty,
                _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown openpyxl color kind."),
            };

        private static object OptionalIntegerValue(BigInteger? value)
            => value is null ? PyNone.Instance : value.Value;

        private abstract record ColorValue(OpenPyxlColorKind Kind);

        private sealed record RgbColorValue(string? Value) : ColorValue(OpenPyxlColorKind.Rgb);

        private sealed record IndexedColorValue(BigInteger? Value) : ColorValue(OpenPyxlColorKind.Indexed);

        private sealed record ThemeColorValue(BigInteger? Value) : ColorValue(OpenPyxlColorKind.Theme);

        private sealed record AutoColorValue(bool? Value) : ColorValue(OpenPyxlColorKind.Auto);
    }

    internal enum OpenPyxlColorKind
    {
        Rgb,
        Indexed,
        Theme,
        Auto,
    }

    internal sealed class OpenPyxlStyleValue : IPyDynamicAttributes, IPyRenderableValue, IEquatable<OpenPyxlStyleValue>
    {
        public OpenPyxlStyleValue(OpenPyxlStylePayload payload) => Payload = payload;

        public OpenPyxlStylePayload Payload { get; }

        public OpenPyxlStyleKind Kind => Payload.Kind;

        public string QualifiedName => OpenPyxlStyleQualifiedName(Kind);

        internal OpenPyxlStyleValue Copy(CopyDepth depth)
        {
            if (depth == CopyDepth.Shallow)
            {
                return new OpenPyxlStyleValue(Payload);
            }

            var copied = Payload switch
            {
                OpenPyxlBorderStylePayload border => border with
                {
                    Left = CopyMember(border.Left),
                    Right = CopyMember(border.Right),
                    Top = CopyMember(border.Top),
                    Bottom = CopyMember(border.Bottom),
                },
                OpenPyxlNamedStylePayload named => named with
                {
                    Font = named.Font?.Copy(CopyDepth.Deep),
                    Fill = named.Fill?.Copy(CopyDepth.Deep),
                    Border = named.Border?.Copy(CopyDepth.Deep),
                    Alignment = named.Alignment?.Copy(CopyDepth.Deep),
                    Protection = named.Protection?.Copy(CopyDepth.Deep),
                },
                _ => Payload,
            };
            return new OpenPyxlStyleValue(copied);

            static object CopyMember(object value)
                => value is OpenPyxlStyleValue style ? style.Copy(CopyDepth.Deep) : value;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => Payload.TryGetMember(name, out value);
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            if (Kind == OpenPyxlStyleKind.NamedStyle &&
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

        if (left is null || right is null || left.Kind != right.Kind)
        {
            return false;
        }

        foreach (var name in GetOpenPyxlStyleMemberNames(left.Kind))
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
        hash.Add(style.Kind);
        foreach (var name in GetOpenPyxlStyleMemberNames(style.Kind))
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

    // Constructed style values retain one wrapper plus payload record. Factories
    // built without a context (internal defaults) stay free like other constants.
    private const long StyleValueBytes = 128;

    private static void ChargeStyleValue(ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.MemoryGovernor.Reserve(StyleValueBytes, span);
        context?.MemoryGovernor.Commit(StyleValueBytes);
    }

    private static object CreateColor(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        var rgb = OptionalColorString(arguments, 0, "openpyxl.styles.colors.Color.rgb", span);
        var indexed = OptionalColorInteger(arguments, 1, "openpyxl.styles.colors.Color.indexed", span);
        var auto = OptionalColorBool(arguments, 2, "openpyxl.styles.colors.Color.auto", span);
        var theme = OptionalColorInteger(arguments, 3, "openpyxl.styles.colors.Color.theme", span);
        var tint = OptionalColorDouble(arguments, 4, 0d, "openpyxl.styles.colors.Color.tint", span);

        // openpyxl accepts its legacy `type` argument but derives the actual kind
        // exclusively from the first populated payload in this precedence order.
        ChargeStyleValue(context, span);
        return indexed is not null
            ? OpenPyxlColor.FromIndexed(indexed, tint)
            : auto is not null
                ? OpenPyxlColor.FromAuto(auto, tint)
                : theme is not null
                    ? OpenPyxlColor.FromTheme(theme, tint)
                    : OpenPyxlColor.FromRgb(rgb, tint);
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
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlFontStylePayload(
            OptionalStyleValue(arguments, 0),
            size,
            bold,
            italic,
            OptionalColorStyleValue(arguments, 4),
            underline,
            strike));
    }

    private static object CreatePatternFill(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = span;
        _ = context;
        var fillType = FirstStyleValue(arguments, 0, 5);
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlPatternFillStylePayload(
            fillType,
            FirstColorStyleValue(arguments, 1, 3),
            FirstColorStyleValue(arguments, 2, 4)));
    }

    private static object CreateBorder(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = span;
        _ = context;
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlBorderStylePayload(
            OptionalStyleValue(arguments, 0),
            OptionalStyleValue(arguments, 1),
            OptionalStyleValue(arguments, 2),
            OptionalStyleValue(arguments, 3)));
    }

    private static object CreateSide(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = span;
        _ = context;
        var style = FirstStyleValue(arguments, 0, 2);
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlSideStylePayload(
            style,
            OptionalColorStyleValue(arguments, 1)));
    }

    private static object CreateAlignment(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = context;
        var wrapText = OptionalStyleBool(arguments, 2, OptionalStyleBool(arguments, 4, false, "openpyxl.styles.Alignment.wrapText", span), "openpyxl.styles.Alignment.wrap_text", span);
        var textRotation = FirstStyleValue(arguments, 3, 5);
        var shrinkToFit = OptionalStyleBool(arguments, 7, OptionalStyleBool(arguments, 6, false, "openpyxl.styles.Alignment.shrinkToFit", span), "openpyxl.styles.Alignment.shrink_to_fit", span);
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlAlignmentStylePayload(
            OptionalStyleValue(arguments, 0),
            OptionalStyleValue(arguments, 1),
            wrapText,
            textRotation,
            shrinkToFit));
    }

    private static object CreateProtection(object[] arguments, LythonSourceSpan? span, ExecutionContext? context)
    {
        _ = context;
        ChargeStyleValue(context, span);
        return new OpenPyxlStyleValue(new OpenPyxlProtectionStylePayload(
            OptionalStyleBool(arguments, 0, true, "openpyxl.styles.Protection.locked", span),
            OptionalStyleBool(arguments, 1, false, "openpyxl.styles.Protection.hidden", span)));
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

        ChargeStyleValue(context, span);
        return CreateNamedStyleValue(
            name,
            OptionalStyleValue(arguments, 5),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 1), OpenPyxlCellStyleComponent.Font),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 2), OpenPyxlCellStyleComponent.Fill),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 3), OpenPyxlCellStyleComponent.Border),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 4), OpenPyxlCellStyleComponent.Alignment),
            NormalizeNamedStyleComponent(OptionalStyleValue(arguments, 6), OpenPyxlCellStyleComponent.Protection));
    }

    private static OpenPyxlStyleValue CreateNamedStyleValue(object name)
        => new(new OpenPyxlNamedStylePayload(name, PyNone.Instance, null, null, null, null, null));

    private static OpenPyxlStyleValue CreateNamedStyleValue(
        object name,
        object numberFormat,
        OpenPyxlStyleValue? font,
        OpenPyxlStyleValue? fill,
        OpenPyxlStyleValue? border,
        OpenPyxlStyleValue? alignment,
        OpenPyxlStyleValue? protection)
        => new(new OpenPyxlNamedStylePayload(name, numberFormat, font, fill, border, alignment, protection));

    private static OpenPyxlStyleValue? NormalizeNamedStyleComponent(object value, OpenPyxlCellStyleComponent component)
    {
        if (value is PyNone)
        {
            return null;
        }

        var expected = ExpectedStyleKind(component);
        return value is OpenPyxlStyleValue style && style.Kind == expected
            ? style
            : throw new LythonRuntimeException("TypeError", "NamedStyle." + CellStyleComponentName(component) + " expects " + OpenPyxlStyleQualifiedName(expected) + ".", null);
    }

    private static string NamedStyleName(OpenPyxlStyleValue style, LythonSourceSpan? span)
    {
        if (style.Payload is not OpenPyxlNamedStylePayload named)
        {
            return "Normal";
        }

        return ExpectString(named.Name, "NamedStyle.name", span);
    }

    private static OpenPyxlStyleValue? NamedStyleComponent(OpenPyxlStyleValue style, OpenPyxlCellStyleComponent component)
        => style.Payload is OpenPyxlNamedStylePayload named
            ? component switch
            {
                OpenPyxlCellStyleComponent.Font => named.Font,
                OpenPyxlCellStyleComponent.Fill => named.Fill,
                OpenPyxlCellStyleComponent.Border => named.Border,
                OpenPyxlCellStyleComponent.Alignment => named.Alignment,
                OpenPyxlCellStyleComponent.Protection => named.Protection,
                _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown cell style component."),
            }
            : null;

    private static string? NamedStyleNumberFormat(OpenPyxlStyleValue style)
        => style.Payload is OpenPyxlNamedStylePayload { NumberFormat: var value } &&
           value is not PyNone && PyStringOps.TryAsString(value, out var text)
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
            ParseDataValidationType(
                OptionalNullableString(arguments, 0, "openpyxl.worksheet.datavalidation.DataValidation.type", span),
                "openpyxl.worksheet.datavalidation.DataValidation.type",
                span),
            OptionalNullableString(arguments, 1, "openpyxl.worksheet.datavalidation.DataValidation.formula1", span),
            OptionalNullableString(arguments, 2, "openpyxl.worksheet.datavalidation.DataValidation.formula2", span),
            OptionalStyleBool(arguments, 3, false, "openpyxl.worksheet.datavalidation.DataValidation.allow_blank", span),
            OptionalStyleBool(arguments, 4, true, "openpyxl.worksheet.datavalidation.DataValidation.showErrorMessage", span),
            OptionalStyleBool(arguments, 5, true, "openpyxl.worksheet.datavalidation.DataValidation.showInputMessage", span),
            ParseDataValidationOperator(
                OptionalNullableString(arguments, 6, "openpyxl.worksheet.datavalidation.DataValidation.operator", span),
                "openpyxl.worksheet.datavalidation.DataValidation.operator",
                span),
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

        return value is OpenPyxlStyleValue style && style.Kind == OpenPyxlStyleKind.NamedStyle
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
            ? OpenPyxlColor.FromRgb(NormalizeRgbColor(text.AsString(), "openpyxl style color", null), 0d)
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

    private static object DefaultCellStyle(OpenPyxlCellStyleComponent component)
        => component switch
        {
            OpenPyxlCellStyleComponent.Font => CreateFont([], null, null),
            OpenPyxlCellStyleComponent.Fill => CreatePatternFill([], null, null),
            OpenPyxlCellStyleComponent.Border => CreateBorder([], null, null),
            OpenPyxlCellStyleComponent.Alignment => CreateAlignment([], null, null),
            OpenPyxlCellStyleComponent.Protection => CreateProtection([], null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown cell style component."),
        };

    private static OpenPyxlStyleKind ExpectedStyleKind(OpenPyxlCellStyleComponent component)
        => component switch
        {
            OpenPyxlCellStyleComponent.Font => OpenPyxlStyleKind.Font,
            OpenPyxlCellStyleComponent.Fill => OpenPyxlStyleKind.PatternFill,
            OpenPyxlCellStyleComponent.Border => OpenPyxlStyleKind.Border,
            OpenPyxlCellStyleComponent.Alignment => OpenPyxlStyleKind.Alignment,
            OpenPyxlCellStyleComponent.Protection => OpenPyxlStyleKind.Protection,
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown cell style component."),
        };

}
