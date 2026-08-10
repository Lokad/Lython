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
