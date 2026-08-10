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

        private static object OptionalStringValue(string? value)
            => value is null ? PyNone.Instance : PyString.FromString(value);

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
        private readonly Dictionary<string, object> _members;

        public OpenPyxlStyleValue(string qualifiedName, IReadOnlyDictionary<string, object> members)
        {
            QualifiedName = qualifiedName;
            _members = new Dictionary<string, object>(members, StringComparer.Ordinal);
        }

        public string QualifiedName { get; }

        internal OpenPyxlStyleValue Copy(CopyDepth depth)
        {
            var members = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in _members)
            {
                members[pair.Key] = depth == CopyDepth.Deep && pair.Value is OpenPyxlStyleValue style
                    ? style.Copy(CopyDepth.Deep)
                    : pair.Value;
            }

            return new OpenPyxlStyleValue(QualifiedName, members);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => _members.TryGetValue(name, out value);
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

        foreach (var name in GetOpenPyxlStyleMemberNames(left.QualifiedName))
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
        foreach (var name in GetOpenPyxlStyleMemberNames(style.QualifiedName))
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

}
