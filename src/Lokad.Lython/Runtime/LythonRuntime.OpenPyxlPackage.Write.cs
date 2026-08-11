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
        private static XDocument CreateContentTypes(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            IReadOnlySet<string> generatedParts,
            UpdatedPackageParts updatedParts)
        {
            var root = CreateSeededContentTypesRoot(workbook.PackageSnapshot, generatedParts);
            AddOrReplaceContentTypeDefault(root, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            AddOrReplaceContentTypeDefault(root, "xml", "application/xml");
            AddOrReplaceContentTypeOverride(root, "/xl/workbook.xml", WorkbookContentType(workbook));

            if (generateStyles)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/xl/styles.xml",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    $"/xl/worksheets/sheet{i + 1}.xml",
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            }

            foreach (var table in updatedParts.Tables)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + table.Path,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml");
            }

            foreach (var comments in updatedParts.Comments)
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + comments.Path,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml");
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XElement CreateSeededContentTypesRoot(OpenPyxlPackageSnapshot? snapshot, IReadOnlySet<string> generatedParts)
        {
            var root = new XElement(ContentTypes + "Types");
            var original = LoadSnapshotXml(snapshot, "[Content_Types].xml");
            foreach (var child in original?.Root?.Elements() ?? [])
            {
                if (child.Name == ContentTypes + "Override")
                {
                    var partName = (string?)child.Attribute("PartName");
                    if (partName is not null && generatedParts.Contains(NormalizePackagePartName(partName)))
                    {
                        continue;
                    }
                }

                root.Add(new XElement(child));
            }

            return root;
        }

        private static void AddOrReplaceContentTypeDefault(XElement root, string extension, string contentType)
        {
            root.Elements(ContentTypes + "Default")
                .Where(element => string.Equals((string?)element.Attribute("Extension"), extension, StringComparison.OrdinalIgnoreCase))
                .Remove();
            root.Add(new XElement(
                ContentTypes + "Default",
                new XAttribute("Extension", extension),
                new XAttribute("ContentType", contentType)));
        }

        private static void AddOrReplaceContentTypeOverride(XElement root, string partName, string contentType)
        {
            root.Elements(ContentTypes + "Override")
                .Where(element => string.Equals(
                    NormalizePackagePartName((string?)element.Attribute("PartName") ?? string.Empty),
                    NormalizePackagePartName(partName),
                    StringComparison.Ordinal))
                .Remove();
            root.Add(new XElement(
                ContentTypes + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType)));
        }

        private sealed record OpenPyxlWorkbookRelationshipPlan(IReadOnlyList<string> WorksheetIds, string? StylesId);

        private static XDocument CreateRootRelationships(OpenPyxlPackageSnapshot? snapshot)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in PreservedRootRelationships(snapshot))
            {
                root.Add(relationship);
            }

            root.Add(new XElement(
                PackageRelationships + "Relationship",
                new XAttribute("Id", NextRelationshipId(RelationshipIds(root))),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "xl/workbook.xml")));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static OpenPyxlWorkbookRelationshipPlan CreateWorkbookRelationshipPlan(OpenPyxlWorkbook workbook, bool generateStyles)
        {
            var usedIds = PreservedWorkbookRelationships(workbook.PackageSnapshot, generateStyles)
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            var worksheetIds = new string[workbook.Worksheets.Count];
            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                worksheetIds[i] = NextRelationshipId(usedIds);
                usedIds.Add(worksheetIds[i]);
            }

            string? stylesId = null;
            if (generateStyles)
            {
                stylesId = NextRelationshipId(usedIds);
            }

            return new OpenPyxlWorkbookRelationshipPlan(worksheetIds, stylesId);
        }

        private static XDocument CreateWorkbookRelationships(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            OpenPyxlWorkbookRelationshipPlan plan)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in PreservedWorkbookRelationships(workbook.PackageSnapshot, generateStyles))
            {
                root.Add(relationship);
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.WorksheetIds[i]),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", $"worksheets/sheet{i + 1}.xml")));
            }

            if (generateStyles && plan.StylesId is not null)
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.StylesId),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml")));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static IEnumerable<XElement> PreservedRootRelationships(OpenPyxlPackageSnapshot? snapshot)
        {
            var original = LoadSnapshotXml(snapshot, "_rels/.rels");
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static IEnumerable<XElement> PreservedWorkbookRelationships(OpenPyxlPackageSnapshot? snapshot, bool generateStyles)
        {
            var original = LoadSnapshotXml(snapshot, "xl/_rels/workbook.xml.rels");
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet") ||
                    IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings") ||
                    generateStyles && IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static bool IsRelationshipType(XElement relationship, string type)
            => string.Equals((string?)relationship.Attribute("Type"), type, StringComparison.Ordinal);

        private static HashSet<string> RelationshipIds(XElement root)
            => root.Elements(PackageRelationships + "Relationship")
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

        private static string NextRelationshipId(ISet<string> usedIds)
        {
            var index = 1;
            while (true)
            {
                var id = "rId" + index.ToString(CultureInfo.InvariantCulture);
                if (!usedIds.Contains(id))
                {
                    return id;
                }

                index++;
            }
        }

        private sealed record OpenPyxlHyperlinkRelationship(CellAddress Address, string Target, string Id);

        private sealed record OpenPyxlWorksheetRelationshipPlan(IReadOnlyList<OpenPyxlHyperlinkRelationship> Hyperlinks, IReadOnlyList<XElement> PreservedRelationships)
        {
            public bool HasRelationships => Hyperlinks.Count > 0 || PreservedRelationships.Count > 0;
        }

        private static OpenPyxlWorksheetRelationshipPlan CreateWorksheetRelationshipPlan(OpenPyxlWorksheet worksheet)
        {
            var preserved = PreservedWorksheetRelationships(worksheet).ToArray();
            var usedIds = preserved
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            var hyperlinks = new List<OpenPyxlHyperlinkRelationship>(worksheet.Hyperlinks.Count);
            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                var id = NextRelationshipId(usedIds);
                usedIds.Add(id);
                hyperlinks.Add(new OpenPyxlHyperlinkRelationship(pair.Key, pair.Value, id));
            }

            return new OpenPyxlWorksheetRelationshipPlan(hyperlinks, preserved);
        }

        private static XDocument CreateWorksheetRelationships(OpenPyxlWorksheetRelationshipPlan plan)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in plan.PreservedRelationships)
            {
                root.Add(new XElement(relationship));
            }

            foreach (var hyperlink in plan.Hyperlinks)
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", hyperlink.Id),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                    new XAttribute("Target", hyperlink.Target),
                    new XAttribute("TargetMode", "External")));
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static IEnumerable<XElement> PreservedWorksheetRelationships(OpenPyxlWorksheet worksheet)
        {
            var snapshot = worksheet.Workbook?.PackageSnapshot;
            var relationshipsPath = worksheet.SourcePath is null ? null : WorksheetRelationshipsPath(worksheet.SourcePath);
            var original = relationshipsPath is null ? null : LoadSnapshotXml(snapshot, relationshipsPath);
            foreach (var relationship in original?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"))
                {
                    continue;
                }

                yield return new XElement(relationship);
            }
        }

        private static XDocument CreateWorkbookXml(OpenPyxlWorkbook workbook, OpenPyxlWorkbookRelationshipPlan relationshipPlan)
        {
            var sheets = new XElement(XlsxMain + "sheets");
            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                sheets.Add(new XElement(
                    XlsxMain + "sheet",
                    new XAttribute("name", workbook.Worksheets[i].Title),
                    new XAttribute("sheetId", i + 1),
                    new XAttribute(XlsxRelationships + "id", relationshipPlan.WorksheetIds[i])));
            }

            var originalRoot = LoadSnapshotXml(workbook.PackageSnapshot, "xl/workbook.xml")?.Root;
            var root = CreateSeededWorkbookRoot(originalRoot);
            root.SetAttributeValue(XNamespace.Xmlns + "r", XlsxRelationships);
            root.Add(
                CreateWorkbookPropertiesXml(workbook, originalRoot),
                CreateWorkbookProtectionXml(workbook.Security),
                CreateBookViewsXml(workbook, originalRoot),
                sheets,
                CreateDefinedNamesXml(workbook, originalRoot),
                CreateCalcPropertiesXml(workbook, originalRoot));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XElement CreateSeededWorkbookRoot(XElement? originalRoot)
        {
            if (originalRoot is null)
            {
                return new XElement(XlsxMain + "workbook");
            }

            var root = new XElement(originalRoot);
            root.Elements(XlsxMain + "workbookPr").Remove();
            root.Elements(XlsxMain + "workbookProtection").Remove();
            root.Elements(XlsxMain + "bookViews").Remove();
            root.Elements(XlsxMain + "sheets").Remove();
            root.Elements(XlsxMain + "definedNames").Remove();
            root.Elements(XlsxMain + "calcPr").Remove();
            return root;
        }

        private static XElement? CreateWorkbookPropertiesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "workbookPr");
            if (element is null && workbook.DateSystem == ExcelDateSystem.Windows1900)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "workbookPr");
            if (workbook.DateSystem == ExcelDateSystem.Mac1904)
            {
                element.SetAttributeValue("date1904", "1");
            }
            else
            {
                element.SetAttributeValue("date1904", null);
            }

            return element.HasAttributes || element.HasElements ? element : null;
        }

        private static XElement? CreateWorkbookProtectionXml(OpenPyxlWorkbookSecurity security)
        {
            if (!security.HasSettings)
            {
                return null;
            }

            var attributes = new List<XAttribute>();
            AddOptionalBoolAttribute(attributes, "lockStructure", security.LockStructure);
            AddOptionalBoolAttribute(attributes, "lockWindows", security.LockWindows);
            AddOptionalBoolAttribute(attributes, "lockRevision", security.LockRevision);
            AddOptionalAttribute(attributes, "workbookPassword", security.WorkbookPassword);
            AddOptionalAttribute(attributes, "workbookPasswordCharacterSet", security.WorkbookPasswordCharacterSet);
            AddOptionalAttribute(attributes, "revisionsPassword", security.RevisionsPassword);
            AddOptionalAttribute(attributes, "revisionsPasswordCharacterSet", security.RevisionsPasswordCharacterSet);
            AddOptionalAttribute(attributes, "workbookAlgorithmName", security.WorkbookAlgorithmName);
            AddOptionalAttribute(attributes, "workbookHashValue", security.WorkbookHashValue);
            AddOptionalAttribute(attributes, "workbookSaltValue", security.WorkbookSaltValue);
            if (security.WorkbookSpinCount is not null)
            {
                attributes.Add(new XAttribute("workbookSpinCount", security.WorkbookSpinCount.Value));
            }

            AddOptionalAttribute(attributes, "revisionsAlgorithmName", security.RevisionsAlgorithmName);
            AddOptionalAttribute(attributes, "revisionsHashValue", security.RevisionsHashValue);
            AddOptionalAttribute(attributes, "revisionsSaltValue", security.RevisionsSaltValue);
            if (security.RevisionsSpinCount is not null)
            {
                attributes.Add(new XAttribute("revisionsSpinCount", security.RevisionsSpinCount.Value));
            }

            return new XElement(XlsxMain + "workbookProtection", attributes);
        }

        private static XElement? CreateBookViewsXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "bookViews");
            if (element is null && workbook.ActiveIndex == 0)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "bookViews");
            var view = element.Elements(XlsxMain + "workbookView").FirstOrDefault();
            if (view is null)
            {
                view = new XElement(XlsxMain + "workbookView");
                element.Add(view);
            }

            view.SetAttributeValue("activeTab", workbook.ActiveIndex);
            return element;
        }

        private static XElement? CreateDefinedNamesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var definedNames = CloneWorkbookChild(originalRoot, XlsxMain + "definedNames") ?? new XElement(XlsxMain + "definedNames");
            definedNames.Elements(XlsxMain + "definedName")
                .Where(element => (string?)element.Attribute("name") is "_xlnm.Print_Area" or "_xlnm.Print_Titles")
                .Remove();

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                var worksheet = workbook.Worksheets[i];
                if (worksheet.PrintArea is not null)
                {
                    definedNames.Add(new XElement(
                        XlsxMain + "definedName",
                        new XAttribute("name", "_xlnm.Print_Area"),
                        new XAttribute("localSheetId", i),
                        CreatePrintAreaDefinedNameText(worksheet)));
                }

                if (worksheet.PrintTitleRows is not null || worksheet.PrintTitleCols is not null)
                {
                    definedNames.Add(new XElement(
                        XlsxMain + "definedName",
                        new XAttribute("name", "_xlnm.Print_Titles"),
                        new XAttribute("localSheetId", i),
                        CreatePrintTitlesDefinedNameText(worksheet)));
                }
            }

            return definedNames.Elements().Any() ? definedNames : null;
        }

        private static XElement? CreateCalcPropertiesXml(OpenPyxlWorkbook workbook, XElement? originalRoot)
        {
            var element = CloneWorkbookChild(originalRoot, XlsxMain + "calcPr");
            var hasFormulaCells = workbook.HasFormulaCells;
            if (element is null && !hasFormulaCells)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "calcPr");
            if (hasFormulaCells)
            {
                element.SetAttributeValue("fullCalcOnLoad", "1");
                element.SetAttributeValue("forceFullCalc", "1");
            }

            return element;
        }

        private static XElement? CloneWorkbookChild(XElement? originalRoot, XName name)
            => originalRoot?.Element(name) is { } child ? new XElement(child) : null;

        private static string CreatePrintAreaDefinedNameText(OpenPyxlWorksheet worksheet)
        {
            return string.Join(
                ",",
                worksheet.PrintArea.RequireNotNull().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(range => SheetQualifiedReference(worksheet.Title, AbsoluteCellOrRangeReference(range))));

            static string AbsoluteCellOrRangeReference(string reference)
            {
                if (!reference.Contains(':', StringComparison.Ordinal))
                {
                    return AbsoluteCellReference(reference);
                }

                var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
                return AbsoluteCellReference(parts[0]) + ":" + AbsoluteCellReference(parts[1]);
            }

            static string AbsoluteCellReference(string reference)
            {
                var address = ParseCellAddress(reference, null);
                return "$" + ColumnName(address.Column) + "$" + address.Row.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string CreatePrintTitlesDefinedNameText(OpenPyxlWorksheet worksheet)
        {
            var references = new List<string>();
            if (worksheet.PrintTitleRows is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteAxisRangeReference(worksheet.PrintTitleRows)));
            }

            if (worksheet.PrintTitleCols is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteAxisRangeReference(worksheet.PrintTitleCols)));
            }

            return string.Join(",", references);

            static string AbsoluteAxisRangeReference(string reference)
            {
                var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
                return "$" + parts[0] + ":$" + parts[1];
            }
        }

        private static string SheetQualifiedReference(string sheetTitle, string reference)
            => "'" + sheetTitle.Replace("'", "''", StringComparison.Ordinal) + "'!" + reference;

        public static Dictionary<string, int> CreateNumberFormatStyleMap(OpenPyxlWorkbook workbook)
        {
            var formats = workbook.Worksheets
                .SelectMany(worksheet => worksheet.NumberFormats.Values)
                .Where(format => format != "General")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < formats.Length; index++)
            {
                result[formats[index]] = index + 1;
            }

            return result;
        }

        private sealed class OpenPyxlStyleRegistry
        {
            private const int FirstCustomNumberFormatId = 164;
            private readonly Dictionary<string, int> _cellStyleIds = new(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _numberFormatIds = new(StringComparer.Ordinal);
            private readonly Dictionary<OpenPyxlStyleValue, int> _fontIds = new(OpenPyxlStyleValueComparer.Instance);
            private readonly Dictionary<OpenPyxlStyleValue, int> _fillIds = new(OpenPyxlStyleValueComparer.Instance);
            private readonly Dictionary<OpenPyxlStyleValue, int> _borderIds = new(OpenPyxlStyleValueComparer.Instance);

            private OpenPyxlStyleRegistry()
            {
            }

            public List<OpenPyxlCellStyleDefinition> CellStyles { get; } = [];

            public List<KeyValuePair<string, int>> NumberFormats { get; } = [];

            public List<OpenPyxlStyleValue> Fonts { get; } = [];

            public List<OpenPyxlStyleValue> Fills { get; } = [];

            public List<OpenPyxlStyleValue> Borders { get; } = [];

            public bool HasCustomStyles => CellStyles.Count > 0;

            public static OpenPyxlStyleRegistry Create(OpenPyxlWorkbook workbook)
            {
                var registry = new OpenPyxlStyleRegistry();
                var definitions = new Dictionary<string, OpenPyxlCellStyleDefinition>(StringComparer.Ordinal);
                foreach (var worksheet in workbook.Worksheets)
                {
                    foreach (var address in StyledAddresses(worksheet))
                    {
                        var definition = CreateCellStyleDefinition(worksheet, address);
                        if (definition.IsDefault)
                        {
                            continue;
                        }

                        definitions.TryAdd(definition.Key, definition);
                    }
                }

                foreach (var definition in definitions.Values
                    .OrderBy(definition => definition.NumberFormat, StringComparer.Ordinal)
                    .ThenBy(definition => definition.Key, StringComparer.Ordinal))
                {
                    registry.AddCellStyle(definition);
                }

                return registry;
            }

            public bool TryGetCellStyleId(OpenPyxlWorksheet worksheet, int row, int column, out int styleId)
            {
                var definition = CreateCellStyleDefinition(worksheet, new CellAddress(row, column));
                return _cellStyleIds.TryGetValue(definition.Key, out styleId);
            }

            public int NumberFormatId(string format)
            {
                if (format == "General")
                {
                    return 0;
                }

                if (_numberFormatIds.TryGetValue(format, out var id))
                {
                    return id;
                }

                id = FirstCustomNumberFormatId + _numberFormatIds.Count;
                _numberFormatIds[format] = id;
                NumberFormats.Add(new KeyValuePair<string, int>(format, id));
                return id;
            }

            public int FontId(OpenPyxlStyleValue? font)
                => AddStyleComponent(font, _fontIds, Fonts, firstCustomId: 1);

            public int FillId(OpenPyxlStyleValue? fill)
                => AddStyleComponent(fill, _fillIds, Fills, firstCustomId: 2);

            public int BorderId(OpenPyxlStyleValue? border)
                => AddStyleComponent(border, _borderIds, Borders, firstCustomId: 1);

            private void AddCellStyle(OpenPyxlCellStyleDefinition definition)
            {
                if (_cellStyleIds.ContainsKey(definition.Key))
                {
                    return;
                }

                _cellStyleIds[definition.Key] = CellStyles.Count + 1;
                _ = NumberFormatId(definition.NumberFormat);
                _ = FontId(definition.Font);
                _ = FillId(definition.Fill);
                _ = BorderId(definition.Border);
                CellStyles.Add(definition);
            }

            private static int AddStyleComponent(
                OpenPyxlStyleValue? style,
                Dictionary<OpenPyxlStyleValue, int> ids,
                List<OpenPyxlStyleValue> styles,
                int firstCustomId)
            {
                if (style is null)
                {
                    return 0;
                }

                if (ids.TryGetValue(style, out var id))
                {
                    return id;
                }

                id = firstCustomId + styles.Count;
                ids[style] = id;
                styles.Add(style);
                return id;
            }

            private sealed class OpenPyxlStyleValueComparer : IEqualityComparer<OpenPyxlStyleValue>
            {
                public static readonly OpenPyxlStyleValueComparer Instance = new();

                public bool Equals(OpenPyxlStyleValue? left, OpenPyxlStyleValue? right) => StyleValuesEqual(left, right);

                public int GetHashCode(OpenPyxlStyleValue value) => value.GetHashCode();
            }

            private static IEnumerable<CellAddress> StyledAddresses(OpenPyxlWorksheet worksheet)
                => worksheet.NumberFormats.Keys
                    .Concat(worksheet.CellStyles.Keys.Select(key => key.Address))
                    .Distinct()
                    .OrderBy(address => address.Row)
                    .ThenBy(address => address.Column);

            private static OpenPyxlCellStyleDefinition CreateCellStyleDefinition(OpenPyxlWorksheet worksheet, CellAddress address)
            {
                var numberFormat = worksheet.GetCellNumberFormat(address.Row, address.Column);
                var font = worksheet.GetAssignedCellStyle(address.Row, address.Column, "font");
                var fill = worksheet.GetAssignedCellStyle(address.Row, address.Column, "fill");
                var border = worksheet.GetAssignedCellStyle(address.Row, address.Column, "border");
                var alignment = worksheet.GetAssignedCellStyle(address.Row, address.Column, "alignment");
                var protection = worksheet.GetAssignedCellStyle(address.Row, address.Column, "protection");
                return new OpenPyxlCellStyleDefinition(
                    CellStyleKey(numberFormat, font, fill, border, alignment, protection),
                    numberFormat,
                    font,
                    fill,
                    border,
                    alignment,
                    protection);
            }

            private static string CellStyleKey(
                string numberFormat,
                OpenPyxlStyleValue? font,
                OpenPyxlStyleValue? fill,
                OpenPyxlStyleValue? border,
                OpenPyxlStyleValue? alignment,
                OpenPyxlStyleValue? protection)
                => string.Join(
                    "|",
                    numberFormat,
                    StyleValueKey(font),
                    StyleValueKey(fill),
                    StyleValueKey(border),
                    StyleValueKey(alignment),
                    StyleValueKey(protection));
        }

        private sealed record OpenPyxlCellStyleDefinition(
            string Key,
            string NumberFormat,
            OpenPyxlStyleValue? Font,
            OpenPyxlStyleValue? Fill,
            OpenPyxlStyleValue? Border,
            OpenPyxlStyleValue? Alignment,
            OpenPyxlStyleValue? Protection)
        {
            public bool IsDefault =>
                NumberFormat == "General" &&
                Font is null &&
                Fill is null &&
                Border is null &&
                Alignment is null &&
                Protection is null;
        }

        private static Dictionary<string, string> LoadOptionalRelationships(ZipArchive archive, string path, LythonSourceSpan span)
        {
            if (archive.GetEntry(path) is null)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            return LoadRelationships(archive, path, span);
        }

        private static IReadOnlyList<XElement> LoadOptionalRelationshipElements(ZipArchive archive, string path, LythonSourceSpan span)
        {
            if (archive.GetEntry(path) is null)
            {
                return [];
            }

            return LoadXml(archive, path, span)
                .Root?
                .Elements(PackageRelationships + "Relationship")
                .Select(relationship => new XElement(relationship))
                .ToArray() ?? [];
        }

    }
}
