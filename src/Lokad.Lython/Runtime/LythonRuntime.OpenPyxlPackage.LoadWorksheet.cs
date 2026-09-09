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
        private static XDocument LoadWorksheetCells(
            OpenPyxlLoadSession session,
            string path,
            OpenPyxlWorksheet worksheet,
            IReadOnlyList<string> sharedStrings,
            IReadOnlyList<OpenPyxlCellStyleSnapshot> cellStyles,
            ExcelDateSystem dateSystem,
            bool dataOnly,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var document = session.LoadXmlDocument(path);
            var worksheetRelationships = LoadOptionalRelationships(session, WorksheetRelationshipsPath(path), context, span);
            // Values, formulas, and styles are independent in OOXML. Preserve each
            // backing store even when a cell has no ordinary Python value.
            var loadedCells = 0;
            var textBytes = 0L;
            foreach (var cell in document.Descendants(XlsxMain + "c"))
            {
                if ((++loadedCells & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    var cellCharge = (ModelCellBytes * ArchiveBudgetCheckInterval) + textBytes;
                    context.MemoryGovernor.Reserve(cellCharge, span);
                    context.MemoryGovernor.Commit(cellCharge);
                    textBytes = 0;
                    context.CheckExecutionBudget(span);
                }

                // Retained values and formulas scale with their text, so the
                // concatenated content joins the per-cell charge.
                textBytes += Encoding.UTF8.GetByteCount(cell.Value);

                var reference = (string?)cell.Attribute("r");
                if (reference is null)
                {
                    continue;
                }

                var address = ParseCellAddress(reference, span);
                var styleId = ReadNonNegativeIntAttribute(cell, "s", span);
                var cellStyle = ReadCellStyle(cell, cellStyles, span);
                var format = cellStyle.NumberFormat;
                var value = ReadCellValue(cell, sharedStrings, format, dateSystem, dataOnly, span);
                worksheet.SetLoadedCellValue(address.Row, address.Column, value);
                worksheet.SetLoadedFormulaCachedValue(address.Row, address.Column, ReadFormulaCachedCellValue(cell, sharedStrings, format, dateSystem, span));
                worksheet.SetLoadedFormulaXml(address.Row, address.Column, cell.Element(XlsxMain + "f"));
                worksheet.SetLoadedCellNumberFormat(address.Row, address.Column, format);
                worksheet.SetLoadedCellStyleId(address.Row, address.Column, styleId);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, OpenPyxlCellStyleComponent.Font, cellStyle.Font);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, OpenPyxlCellStyleComponent.Fill, cellStyle.Fill);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, OpenPyxlCellStyleComponent.Border, cellStyle.Border);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, OpenPyxlCellStyleComponent.Alignment, cellStyle.Alignment);
                worksheet.SetLoadedCellStyle(address.Row, address.Column, OpenPyxlCellStyleComponent.Protection, cellStyle.Protection);
                worksheet.SetLoadedCellNamedStyle(address.Row, address.Column, cellStyle.NamedStyleName);
                worksheet.SetLoadedCellDataType(address.Row, address.Column, (string?)cell.Attribute("t"));
            }

            // The per-64 charging above leaves a final partial block uncharged;
            // top it up so small sheets pay proportionally too.
            var tailCells = loadedCells & (ArchiveBudgetCheckInterval - 1);
            if (tailCells > 0 || textBytes > 0)
            {
                var tailCharge = (ModelCellBytes * tailCells) + textBytes;
                context.MemoryGovernor.Reserve(tailCharge, span);
                context.MemoryGovernor.Commit(tailCharge);
            }

            LoadWorksheetStructure(document, worksheet, worksheetRelationships, context, span);

            LoadWorksheetComments(session, path, worksheet, context, span);
            LoadWorksheetTables(session, path, document, worksheet, worksheetRelationships, context, span);
            LoadWorksheetDataValidations(document, worksheet, context, span);
            LoadWorksheetConditionalFormatting(document, worksheet, context, span);
            LoadWorksheetProtection(document, worksheet, span);
            LoadWorksheetDrawings(session, path, document, worksheet, worksheetRelationships, context, span);
            worksheet.SetLoadedAutoFilter((string?)document.Descendants(XlsxMain + "autoFilter").FirstOrDefault()?.Attribute("ref"));
            LoadWorksheetViewAndPageLayout(document, worksheet, span);
            return document;
        }

        private static void LoadWorksheetStructure(
            XDocument document,
            OpenPyxlWorksheet worksheet,
            IReadOnlyDictionary<string, string> worksheetRelationships,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var structured = 0;
            foreach (var column in document.Descendants(XlsxMain + "col"))
            {
                if ((++structured & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                var min = ReadPositiveIntAttribute(column, "min", span);
                var max = ReadPositiveIntAttribute(column, "max", span);
                if (min is null || max is null)
                {
                    continue;
                }

                for (var index = min.Value; index <= max.Value; index++)
                {
                    if ((++structured & (ArchiveBudgetCheckInterval - 1)) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }

                    ValidateRowColumn(1, index, span);
                    var dimension = worksheet.GetColumnDimension(index);
                    dimension.Width = ReadNonNegativeDoubleAttribute(column, "width", span);
                    dimension.Hidden = ReadBooleanAttribute(column, "hidden", span);
                }
            }

            foreach (var rowElement in document.Descendants(XlsxMain + "row"))
            {
                if ((++structured & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

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
                if ((++structured & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

                var reference = (string?)mergeCell.Attribute("ref");
                if (reference is not null)
                {
                    worksheet.AddLoadedMergedRange(ParseCellRange(reference, span));
                }
            }

            foreach (var hyperlink in document.Descendants(XlsxMain + "hyperlink"))
            {
                if ((++structured & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

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
        }

        private static void LoadWorksheetViewAndPageLayout(
            XDocument document,
            OpenPyxlWorksheet worksheet,
            LythonSourceSpan span)
        {
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
            OpenPyxlLoadSession session,
            string worksheetPath,
            XDocument worksheetDocument,
            OpenPyxlWorksheet worksheet,
            IReadOnlyDictionary<string, string> worksheetRelationships,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var scannedTables = 0;
            foreach (var tablePart in worksheetDocument.Descendants(XlsxMain + "tablePart"))
            {
                if ((++scannedTables & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }
                var relationshipId = (string?)tablePart.Attribute(XlsxRelationships + "id");
                if (relationshipId is null || !worksheetRelationships.TryGetValue(relationshipId, out var target))
                {
                    continue;
                }

                var table = LoadTable(session, ResolvePackagePath(worksheetPath, target), context, span);
                if (table is not null)
                {
                    worksheet.SetLoadedTable(table);
                }
            }
        }

        private static OpenPyxlTable? LoadTable(OpenPyxlLoadSession session, string tablePath, ExecutionContext context, LythonSourceSpan span)
        {
            var document = session.LoadXmlDocument(tablePath);
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
            string? tableStyleName = null;
            var style = root.Element(XlsxMain + "tableStyleInfo");
            if (style is not null && (string?)style.Attribute("name") is { } resolvedStyleName)
            {
                tableStyleName = resolvedStyleName;
                table.TableStyleInfo = new OpenPyxlTableStyleInfo(
                    tableStyleName,
                    ReadBooleanAttribute(style, "showFirstColumn", defaultValue: false, span),
                    ReadBooleanAttribute(style, "showLastColumn", defaultValue: false, span),
                    ReadBooleanAttribute(style, "showRowStripes", defaultValue: true, span),
                    ReadBooleanAttribute(style, "showColumnStripes", defaultValue: false, span));
            }

            // The model keeps display and style names plus the source path.
            var tableCharge = ModelCellBytes + Encoding.UTF8.GetByteCount(displayName) + Encoding.UTF8.GetByteCount(tablePath);
            if (tableStyleName is not null)
            {
                tableCharge += Encoding.UTF8.GetByteCount(tableStyleName);
            }

            context.MemoryGovernor.Reserve(tableCharge, span);
            context.MemoryGovernor.Commit(tableCharge);

            return table;
        }

        // Retained validation strings: formulas plus the four message titles and bodies.
        private static long ValidationTextBytes(XElement element)
        {
            var bytes = 0L;
            bytes += Encoding.UTF8.GetByteCount(element.Element(XlsxMain + "formula1")?.Value ?? string.Empty);
            bytes += Encoding.UTF8.GetByteCount(element.Element(XlsxMain + "formula2")?.Value ?? string.Empty);
            bytes += Encoding.UTF8.GetByteCount((string?)element.Attribute("errorTitle") ?? string.Empty);
            bytes += Encoding.UTF8.GetByteCount((string?)element.Attribute("error") ?? string.Empty);
            bytes += Encoding.UTF8.GetByteCount((string?)element.Attribute("promptTitle") ?? string.Empty);
            bytes += Encoding.UTF8.GetByteCount((string?)element.Attribute("prompt") ?? string.Empty);
            return bytes;
        }

        private static void LoadWorksheetDataValidations(XDocument worksheetDocument, OpenPyxlWorksheet worksheet, ExecutionContext context, LythonSourceSpan span)
        {
            var validated = 0;
            var validationTextBytes = 0L;
            foreach (var element in worksheetDocument.Root?.Element(XlsxMain + "dataValidations")?.Elements(XlsxMain + "dataValidation") ?? [])
            {
                if ((++validated & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    var validationCharge = (ModelCellBytes * ArchiveBudgetCheckInterval) + validationTextBytes;
                    context.MemoryGovernor.Reserve(validationCharge, span);
                    context.MemoryGovernor.Commit(validationCharge);
                    validationTextBytes = 0;
                    context.CheckExecutionBudget(span);
                }

                var validation = new OpenPyxlDataValidation(
                    ParseDataValidationType((string?)element.Attribute("type"), "worksheet data-validation type", span),
                    element.Element(XlsxMain + "formula1")?.Value,
                    element.Element(XlsxMain + "formula2")?.Value,
                    ReadBooleanAttribute(element, "allowBlank", defaultValue: false, span),
                    ReadBooleanAttribute(element, "showErrorMessage", defaultValue: true, span),
                    ReadBooleanAttribute(element, "showInputMessage", defaultValue: true, span),
                    ParseDataValidationOperator((string?)element.Attribute("operator"), "worksheet data-validation operator", span),
                    (string?)element.Attribute("errorTitle"),
                    (string?)element.Attribute("error"),
                    (string?)element.Attribute("promptTitle"),
                    (string?)element.Attribute("prompt"));

                foreach (var reference in ((string?)element.Attribute("sqref") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    validation.AddRange(reference, span);
                }

                validationTextBytes += ValidationTextBytes(element);
                worksheet.AddDataValidation(validation);
            }

            var tailValidated = validated & (ArchiveBudgetCheckInterval - 1);
            if (tailValidated > 0 || validationTextBytes > 0)
            {
                var tailCharge = (ModelCellBytes * tailValidated) + validationTextBytes;
                context.MemoryGovernor.Reserve(tailCharge, span);
                context.MemoryGovernor.Commit(tailCharge);
            }
        }

        private static void LoadWorksheetConditionalFormatting(XDocument worksheetDocument, OpenPyxlWorksheet worksheet, ExecutionContext context, LythonSourceSpan span)
        {
            var formatted = 0;
            var formattingTextBytes = 0L;
            foreach (var element in worksheetDocument.Root?.Elements(XlsxMain + "conditionalFormatting") ?? [])
            {
                if ((++formatted & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    var formattingCharge = (ModelCellBytes * ArchiveBudgetCheckInterval) + formattingTextBytes;
                    context.MemoryGovernor.Reserve(formattingCharge, span);
                    context.MemoryGovernor.Commit(formattingCharge);
                    formattingTextBytes = 0;
                    context.CheckExecutionBudget(span);
                }

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
                foreach (var rule in rules)
                {
                    // The model keeps a full copy of each rule element.
                    formattingTextBytes += Encoding.UTF8.GetByteCount(rule.SourceXml?.ToString() ?? string.Empty);
                }

                worksheet.AddLoadedConditionalFormatting(sqref, rules, span);
            }

            var tailFormatted = formatted & (ArchiveBudgetCheckInterval - 1);
            if (tailFormatted > 0 || formattingTextBytes > 0)
            {
                var tailCharge = (ModelCellBytes * tailFormatted) + formattingTextBytes;
                context.MemoryGovernor.Reserve(tailCharge, span);
                context.MemoryGovernor.Commit(tailCharge);
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
            OpenPyxlLoadSession session,
            string worksheetPath,
            XDocument worksheetDocument,
            OpenPyxlWorksheet worksheet,
            IReadOnlyDictionary<string, string> worksheetRelationships,
            ExecutionContext context,
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
                // The model keeps drawing and child paths plus relationship ids.
                var drawingTextBytes = Encoding.UTF8.GetByteCount(drawingPath) + Encoding.UTF8.GetByteCount(relationshipId);
                var scannedDrawings = 0;
                var childTextBytes = 0L;
                foreach (var relationship in LoadOptionalRelationshipElements(session, PartRelationshipsPath(drawingPath), context, span))
                {
                    if ((++scannedDrawings & (ArchiveBudgetCheckInterval - 1)) == 0)
                    {
                        var drawingCharge = (ModelCellBytes * ArchiveBudgetCheckInterval) + drawingTextBytes + childTextBytes;
                        context.MemoryGovernor.Reserve(drawingCharge, span);
                        context.MemoryGovernor.Commit(drawingCharge);
                        drawingTextBytes = 0;
                        childTextBytes = 0;
                        context.CheckExecutionBudget(span);
                    }

                    var childTarget = (string?)relationship.Attribute("Target");
                    var childRelationshipId = (string?)relationship.Attribute("Id") ?? string.Empty;
                    if (childTarget is null)
                    {
                        continue;
                    }

                    if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart"))
                    {
                        childTextBytes += Encoding.UTF8.GetByteCount(ResolvePackagePath(drawingPath, childTarget)) + Encoding.UTF8.GetByteCount(childRelationshipId);
                        drawing.AddChart(new OpenPyxlLoadedChart(ResolvePackagePath(drawingPath, childTarget), childRelationshipId, drawingPath));
                        continue;
                    }

                    if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"))
                    {
                        childTextBytes += Encoding.UTF8.GetByteCount(ResolvePackagePath(drawingPath, childTarget)) + Encoding.UTF8.GetByteCount(childRelationshipId);
                        drawing.AddImage(new OpenPyxlLoadedImage(ResolvePackagePath(drawingPath, childTarget), childRelationshipId, drawingPath));
                    }
                }

                var tailDrawings = scannedDrawings & (ArchiveBudgetCheckInterval - 1);
                if (tailDrawings > 0 || drawingTextBytes > 0 || childTextBytes > 0)
                {
                    var tailCharge = (ModelCellBytes * tailDrawings) + drawingTextBytes + childTextBytes;
                    context.MemoryGovernor.Reserve(tailCharge, span);
                    context.MemoryGovernor.Commit(tailCharge);
                }

                worksheet.AddLoadedDrawing(drawing);
            }
        }

        private static void LoadWorksheetComments(OpenPyxlLoadSession session, string worksheetPath, OpenPyxlWorksheet worksheet, ExecutionContext context, LythonSourceSpan span)
        {
            var target = LoadRelationshipTargetByType(
                session,
                WorksheetRelationshipsPath(worksheetPath),
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments",
                context,
                span);
            if (target is null)
            {
                return;
            }

            var commentsPath = ResolvePackagePath(worksheetPath, target);
            worksheet.SetLoadedCommentsSource(commentsPath);
            var comments = session.LoadXmlDocument(commentsPath);
            var authors = comments.Root
                ?.Element(XlsxMain + "authors")
                ?.Elements(XlsxMain + "author")
                .Select(author => author.Value)
                .ToArray() ?? [];

            // Author strings persist through per-comment references, so they pay
            // once up front instead of per comment.
            var authorBytes = 0L;
            foreach (var authorName in authors)
            {
                authorBytes += Encoding.UTF8.GetByteCount(authorName);
            }

            if (authorBytes > 0)
            {
                context.MemoryGovernor.Reserve(authorBytes, span);
                context.MemoryGovernor.Commit(authorBytes);
            }

            var scannedComments = 0;
            var commentTextBytes = 0L;
            foreach (var comment in comments.Root?.Element(XlsxMain + "commentList")?.Elements(XlsxMain + "comment") ?? [])
            {
                if ((++scannedComments & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    var commentCharge = (ModelCellBytes * ArchiveBudgetCheckInterval) + commentTextBytes;
                    context.MemoryGovernor.Reserve(commentCharge, span);
                    context.MemoryGovernor.Commit(commentCharge);
                    commentTextBytes = 0;
                    context.CheckExecutionBudget(span);
                }

                var reference = (string?)comment.Attribute("ref");
                if (reference is null)
                {
                    continue;
                }

                var address = ParseCellAddress(reference, span);
                var authorId = ReadNonNegativeIntAttribute(comment, "authorId", span) ?? 0;
                var author = authorId < authors.Length ? authors[authorId] : string.Empty;
                var text = string.Concat(comment.Element(XlsxMain + "text")?.Descendants(XlsxMain + "t").Select(t => t.Value) ?? []);
                commentTextBytes += Encoding.UTF8.GetByteCount(text);
                worksheet.SetLoadedComment(address.Row, address.Column, new OpenPyxlComment(text, author));
            }

            var tailComments = scannedComments & (ArchiveBudgetCheckInterval - 1);
            if (tailComments > 0 || commentTextBytes > 0)
            {
                var tailCharge = (ModelCellBytes * tailComments) + commentTextBytes;
                context.MemoryGovernor.Reserve(tailCharge, span);
                context.MemoryGovernor.Commit(tailCharge);
            }
        }

        private static object ReadCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            ExcelDateSystem dateSystem,
            bool dataOnly,
            LythonSourceSpan span)
        {
            var formula = cell.Element(XlsxMain + "f");
            if (formula is not null && !dataOnly)
            {
                return PyString.FromString("=" + formula.Value);
            }

            return ReadStoredCellValue(cell, sharedStrings, numberFormat, dateSystem, span);
        }

        private static object ReadFormulaCachedCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            ExcelDateSystem dateSystem,
            LythonSourceSpan span)
            => cell.Element(XlsxMain + "f") is null
                ? PyNone.Instance
                : ReadStoredCellValue(cell, sharedStrings, numberFormat, dateSystem, span);

        private static object ReadStoredCellValue(
            XElement cell,
            IReadOnlyList<string> sharedStrings,
            string numberFormat,
            ExcelDateSystem dateSystem,
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
                _ => ParseNumericCell(rawValue, numberFormat, dateSystem),
            };

            static object ParseNumericCell(string rawValue, string numberFormat, ExcelDateSystem dateSystem)
            {
                if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
                    IsDateNumberFormat(numberFormat))
                {
                    return DateValueFromExcelSerial(serial, numberFormat, dateSystem);
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
        }

        private static object ReadSharedStringValue(string rawValue, IReadOnlyList<string> sharedStrings, LythonSourceSpan span)
        {
            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                index < 0 ||
                index >= sharedStrings.Count)
            {
                throw InvalidFileException("Invalid .xlsx workbook: shared string index is out of range.", span);
            }

            return PyString.FromString(sharedStrings[index]);
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

            throw InvalidFileException($"Invalid .xlsx workbook: attribute {name} expects a positive integer.", span);
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

            throw InvalidFileException($"Invalid .xlsx workbook: attribute {name} expects a non-negative integer.", span);
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

            throw InvalidFileException($"Invalid .xlsx workbook: attribute {name} expects a non-negative finite number.", span);
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
                _ => throw InvalidFileException($"Invalid .xlsx workbook: attribute {name} expects a boolean.", span)
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
                _ => throw InvalidFileException($"Invalid .xlsx workbook: attribute {name} expects a boolean.", span)
            };
        }

    }
}
