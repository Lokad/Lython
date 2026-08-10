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
