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
                ["underline"] = underline,
                ["u"] = underline,
                ["strike"] = strike,
                ["strikethrough"] = strike,
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
                    ["wrapText"] = ReadStyleBooleanAttribute(alignment, "wrapText"),
                    ["text_rotation"] = ReadStyleAttribute(alignment, "textRotation"),
                    ["textRotation"] = ReadStyleAttribute(alignment, "textRotation"),
                    ["shrink_to_fit"] = ReadStyleBooleanAttribute(alignment, "shrinkToFit"),
                    ["shrinkToFit"] = ReadStyleBooleanAttribute(alignment, "shrinkToFit"),
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

    }
}
