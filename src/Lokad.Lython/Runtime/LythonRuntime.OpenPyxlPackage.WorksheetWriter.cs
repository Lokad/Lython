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
                        worksheet.Workbook?.DateSystem ?? ExcelDateSystem.Windows1900,
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
                var address = ParseCellAddress(worksheet.FreezePanes.RequireNotNull(), null);
                var paneAttributes = new List<object>
                {
                    new XAttribute("topLeftCell", worksheet.FreezePanes.RequireNotNull()),
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
            ExcelDateSystem dateSystem,
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
                        CreateFormulaCachedValueXml(formulaCachedValue, dateSystem));
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
                new XElement(XlsxMain + "v", CellNumberText(value, dateSystem)));
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

        private static XElement? CreateFormulaCachedValueXml(object value, ExcelDateSystem dateSystem)
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

            return new XElement(XlsxMain + "v", CellNumberText(value, dateSystem));
        }

        private static string CellNumberText(object value, ExcelDateSystem dateSystem)
        {
            return value switch
            {
                BigInteger integer => integer.ToString(CultureInfo.InvariantCulture),
                double floating => floating.ToString("R", CultureInfo.InvariantCulture),
                PyDecimal decimalValue => decimalValue.Value.ToString(CultureInfo.InvariantCulture),
                PyDate date => ExcelSerialFromDate(date, dateSystem).ToString("R", CultureInfo.InvariantCulture),
                PyDateTime dateTime => ExcelSerialFromDateTime(dateTime, dateSystem).ToString("R", CultureInfo.InvariantCulture),
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
}
