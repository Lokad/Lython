namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal enum OpenPyxlStyleKind
    {
        Font,
        PatternFill,
        Border,
        Side,
        Alignment,
        Protection,
        NamedStyle,
    }

    internal enum OpenPyxlCellStyleComponent
    {
        Font,
        Fill,
        Border,
        Alignment,
        Protection,
    }

    private static bool TryParseCellStyleComponent(string name, out OpenPyxlCellStyleComponent component)
    {
        component = name switch
        {
            "font" => OpenPyxlCellStyleComponent.Font,
            "fill" => OpenPyxlCellStyleComponent.Fill,
            "border" => OpenPyxlCellStyleComponent.Border,
            "alignment" => OpenPyxlCellStyleComponent.Alignment,
            "protection" => OpenPyxlCellStyleComponent.Protection,
            _ => default,
        };
        return name is "font" or "fill" or "border" or "alignment" or "protection";
    }

    private static string CellStyleComponentName(OpenPyxlCellStyleComponent component)
        => component switch
        {
            OpenPyxlCellStyleComponent.Font => "font",
            OpenPyxlCellStyleComponent.Fill => "fill",
            OpenPyxlCellStyleComponent.Border => "border",
            OpenPyxlCellStyleComponent.Alignment => "alignment",
            OpenPyxlCellStyleComponent.Protection => "protection",
            _ => throw new ArgumentOutOfRangeException(nameof(component), component, "Unknown cell style component."),
        };

    internal abstract record OpenPyxlStylePayload(OpenPyxlStyleKind Kind)
    {
        /// <summary>
        /// Projects a Python-visible style member and reports absence without
        /// manufacturing a value; implementations must expose only their style kind's members.
        /// </summary>
        public abstract bool TryGetMember(string name, [MaybeNullWhen(false)] out object value);
    }

    internal sealed record OpenPyxlFontStylePayload(
        object Name,
        object Size,
        object Bold,
        object Italic,
        object Color,
        object Underline,
        object Strike)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.Font)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => Name,
                "sz" or "size" => Size,
                "bold" or "b" => Bold,
                "italic" or "i" => Italic,
                "color" => Color,
                "underline" or "u" => Underline,
                "strike" or "strikethrough" => Strike,
                _ => PyNone.Instance,
            };
            return name is "name" or "sz" or "size" or "bold" or "b" or "italic" or "i" or
                "color" or "underline" or "u" or "strike" or "strikethrough";
        }
    }

    internal sealed record OpenPyxlPatternFillStylePayload(
        object FillType,
        object ForegroundColor,
        object BackgroundColor)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.PatternFill)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "fill_type" or "patternType" => FillType,
                "start_color" or "fgColor" => ForegroundColor,
                "end_color" or "bgColor" => BackgroundColor,
                _ => PyNone.Instance,
            };
            return name is "fill_type" or "patternType" or "start_color" or "fgColor" or "end_color" or "bgColor";
        }
    }

    internal sealed record OpenPyxlBorderStylePayload(object Left, object Right, object Top, object Bottom)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.Border)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "left" => Left,
                "right" => Right,
                "top" => Top,
                "bottom" => Bottom,
                _ => PyNone.Instance,
            };
            return name is "left" or "right" or "top" or "bottom";
        }
    }

    internal sealed record OpenPyxlSideStylePayload(object Style, object Color)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.Side)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "style" or "border_style" => Style,
                "color" => Color,
                _ => PyNone.Instance,
            };
            return name is "style" or "border_style" or "color";
        }
    }

    internal sealed record OpenPyxlAlignmentStylePayload(
        object Horizontal,
        object Vertical,
        object WrapText,
        object TextRotation,
        object ShrinkToFit)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.Alignment)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "horizontal" => Horizontal,
                "vertical" => Vertical,
                "wrap_text" or "wrapText" => WrapText,
                "text_rotation" or "textRotation" => TextRotation,
                "shrink_to_fit" or "shrinkToFit" => ShrinkToFit,
                _ => PyNone.Instance,
            };
            return name is "horizontal" or "vertical" or "wrap_text" or "wrapText" or
                "text_rotation" or "textRotation" or "shrink_to_fit" or "shrinkToFit";
        }
    }

    internal sealed record OpenPyxlProtectionStylePayload(object Locked, object Hidden)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.Protection)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "locked" => Locked,
                "hidden" => Hidden,
                _ => PyNone.Instance,
            };
            return name is "locked" or "hidden";
        }
    }

    internal sealed record OpenPyxlNamedStylePayload(
        object Name,
        object NumberFormat,
        OpenPyxlStyleValue? Font,
        OpenPyxlStyleValue? Fill,
        OpenPyxlStyleValue? Border,
        OpenPyxlStyleValue? Alignment,
        OpenPyxlStyleValue? Protection)
        : OpenPyxlStylePayload(OpenPyxlStyleKind.NamedStyle)
    {
        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => Name,
                "number_format" => NumberFormat,
                "font" => Font is null ? PyNone.Instance : Font,
                "fill" => Fill is null ? PyNone.Instance : Fill,
                "border" => Border is null ? PyNone.Instance : Border,
                "alignment" => Alignment is null ? PyNone.Instance : Alignment,
                "protection" => Protection is null ? PyNone.Instance : Protection,
                _ => PyNone.Instance,
            };
            return name is "name" or "number_format" or "font" or "fill" or "border" or "alignment" or "protection";
        }
    }

    private static readonly string[] OpenPyxlFontMemberNames = ["name", "sz", "bold", "italic", "color", "underline", "strike"];
    private static readonly string[] OpenPyxlPatternFillMemberNames = ["fill_type", "fgColor", "bgColor"];
    private static readonly string[] OpenPyxlBorderMemberNames = ["left", "right", "top", "bottom"];
    private static readonly string[] OpenPyxlSideMemberNames = ["style", "color"];
    private static readonly string[] OpenPyxlAlignmentMemberNames = ["horizontal", "vertical", "wrap_text", "text_rotation", "shrink_to_fit"];
    private static readonly string[] OpenPyxlProtectionMemberNames = ["locked", "hidden"];
    private static readonly string[] OpenPyxlNamedStyleMemberNames = ["name", "number_format", "font", "fill", "border", "alignment", "protection"];

    // Equality, hashing, and XLSX style deduplication must observe the same members.
    // Returning cached arrays also avoids rebuilding these fixed inventories per cell.
    private static IReadOnlyList<string> GetOpenPyxlStyleMemberNames(OpenPyxlStyleKind kind)
        => kind switch
        {
            OpenPyxlStyleKind.Font => OpenPyxlFontMemberNames,
            OpenPyxlStyleKind.PatternFill => OpenPyxlPatternFillMemberNames,
            OpenPyxlStyleKind.Border => OpenPyxlBorderMemberNames,
            OpenPyxlStyleKind.Side => OpenPyxlSideMemberNames,
            OpenPyxlStyleKind.Alignment => OpenPyxlAlignmentMemberNames,
            OpenPyxlStyleKind.Protection => OpenPyxlProtectionMemberNames,
            OpenPyxlStyleKind.NamedStyle => OpenPyxlNamedStyleMemberNames,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown OpenPyxl style kind."),
        };

    private static string OpenPyxlStyleQualifiedName(OpenPyxlStyleKind kind)
        => kind switch
        {
            OpenPyxlStyleKind.Font => "openpyxl.styles.Font",
            OpenPyxlStyleKind.PatternFill => "openpyxl.styles.PatternFill",
            OpenPyxlStyleKind.Border => "openpyxl.styles.Border",
            OpenPyxlStyleKind.Side => "openpyxl.styles.Side",
            OpenPyxlStyleKind.Alignment => "openpyxl.styles.Alignment",
            OpenPyxlStyleKind.Protection => "openpyxl.styles.Protection",
            OpenPyxlStyleKind.NamedStyle => "openpyxl.styles.NamedStyle",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown OpenPyxl style kind."),
        };
}
