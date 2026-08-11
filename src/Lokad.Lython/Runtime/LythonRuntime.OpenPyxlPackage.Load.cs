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
    private static partial class OpenPyxlPackage
    {
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
                .ToDictionary(item => item.Id.RequireNotNull(), item => item.Format.RequireNotNull(), EqualityComparer<int>.Default) ?? new Dictionary<int, string>();

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
            var size = (string?)font.Element(XlsxMain + "sz")?.Attribute("val") is { } sizeText &&
                       double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSize)
                ? (object)parsedSize
                : PyNone.Instance;
            var bold = font.Element(XlsxMain + "b") is not null;
            var italic = font.Element(XlsxMain + "i") is not null;
            var strike = font.Element(XlsxMain + "strike") is not null;
            var underline = ReadUnderlineValue(font.Element(XlsxMain + "u"));
            return new OpenPyxlStyleValue(new OpenPyxlFontStylePayload(
                ReadStyleElementAttribute(font, "name", "val"),
                size,
                bold,
                italic,
                ReadColorValue(font.Element(XlsxMain + "color")),
                underline,
                strike));
        }

        private static OpenPyxlStyleValue ReadFillStyle(XElement fill)
        {
            var pattern = fill.Element(XlsxMain + "patternFill");
            var fillType = ReadStyleAttribute(pattern, "patternType");
            var fgColor = ReadColorValue(pattern?.Element(XlsxMain + "fgColor"));
            var bgColor = ReadColorValue(pattern?.Element(XlsxMain + "bgColor"));
            return new OpenPyxlStyleValue(new OpenPyxlPatternFillStylePayload(fillType, fgColor, bgColor));
        }

        private static OpenPyxlStyleValue ReadBorderStyle(XElement border)
            => new(new OpenPyxlBorderStylePayload(
                ReadSideStyle(border.Element(XlsxMain + "left")),
                ReadSideStyle(border.Element(XlsxMain + "right")),
                ReadSideStyle(border.Element(XlsxMain + "top")),
                ReadSideStyle(border.Element(XlsxMain + "bottom"))));

        private static OpenPyxlStyleValue ReadSideStyle(XElement? side)
        {
            var style = ReadStyleAttribute(side, "style");
            return new OpenPyxlStyleValue(new OpenPyxlSideStylePayload(
                style,
                ReadColorValue(side?.Element(XlsxMain + "color"))));
        }

        private static OpenPyxlStyleValue? ReadAlignmentStyle(XElement? alignment)
            => alignment is null
                ? null
                : new OpenPyxlStyleValue(new OpenPyxlAlignmentStylePayload(
                    ReadStyleAttribute(alignment, "horizontal"),
                    ReadStyleAttribute(alignment, "vertical"),
                    ReadStyleBooleanAttribute(alignment, "wrapText"),
                    ReadStyleAttribute(alignment, "textRotation"),
                    ReadStyleBooleanAttribute(alignment, "shrinkToFit")));

        private static OpenPyxlStyleValue? ReadProtectionStyle(XElement? protection)
            => protection is null
                ? null
                : new OpenPyxlStyleValue(new OpenPyxlProtectionStylePayload(
                    ReadStyleBooleanAttribute(protection, "locked"),
                    ReadStyleBooleanAttribute(protection, "hidden")));

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
                return OpenPyxlColor.FromRgb(rgb, tint);
            }

            if (ReadColorIntegerAttribute(color, "indexed") is { } indexed)
            {
                return OpenPyxlColor.FromIndexed(indexed, tint);
            }

            if (ReadColorIntegerAttribute(color, "theme") is { } theme)
            {
                return OpenPyxlColor.FromTheme(theme, tint);
            }

            if (ReadColorBooleanAttribute(color, "auto") is { } auto)
            {
                return OpenPyxlColor.FromAuto(auto, tint);
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

    }
}
