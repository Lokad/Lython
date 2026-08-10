namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly string[] OpenPyxlFontMemberNames = ["name", "sz", "bold", "italic", "color", "underline", "strike"];
    private static readonly string[] OpenPyxlPatternFillMemberNames = ["fill_type", "fgColor", "bgColor"];
    private static readonly string[] OpenPyxlBorderMemberNames = ["left", "right", "top", "bottom"];
    private static readonly string[] OpenPyxlSideMemberNames = ["style", "color"];
    private static readonly string[] OpenPyxlAlignmentMemberNames = ["horizontal", "vertical", "wrap_text", "text_rotation", "shrink_to_fit"];
    private static readonly string[] OpenPyxlProtectionMemberNames = ["locked", "hidden"];
    private static readonly string[] OpenPyxlNamedStyleMemberNames = ["name", "number_format", "font", "fill", "border", "alignment", "protection"];

    // Equality, hashing, and XLSX style deduplication must observe the same members.
    // Returning cached arrays also avoids rebuilding these fixed inventories per cell.
    private static IReadOnlyList<string> GetOpenPyxlStyleMemberNames(string qualifiedName)
        => qualifiedName switch
        {
            "openpyxl.styles.Font" => OpenPyxlFontMemberNames,
            "openpyxl.styles.PatternFill" => OpenPyxlPatternFillMemberNames,
            "openpyxl.styles.Border" => OpenPyxlBorderMemberNames,
            "openpyxl.styles.Side" => OpenPyxlSideMemberNames,
            "openpyxl.styles.Alignment" => OpenPyxlAlignmentMemberNames,
            "openpyxl.styles.Protection" => OpenPyxlProtectionMemberNames,
            "openpyxl.styles.NamedStyle" => OpenPyxlNamedStyleMemberNames,
            _ => Array.Empty<string>(),
        };
}
