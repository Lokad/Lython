using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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
        public static OpenPyxlWorkbook Load(ReadOnlyMemory<byte> payload, bool dataOnly, bool readOnly, bool keepLinks, bool keepVba, LythonSourceSpan span)
        {
            try
            {
                using var stream = MemoryMarshal.TryGetArray(payload, out var segment)
                    ? new MemoryStream(segment.Array.RequireNotNull(), segment.Offset, segment.Count, writable: false)
                    : new MemoryStream(payload.ToArray(), writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                var sharedStrings = LoadSharedStrings(archive);
                var cellStyles = LoadCellStyles(archive, span);
                var namedStyles = LoadNamedStyles(archive, span);
                var workbook = LoadXml(archive, "xl/workbook.xml", span);
                var date1904 = ReadBooleanAttribute(
                    workbook.Root?.Element(XlsxMain + "workbookPr") ?? new XElement(XlsxMain + "workbookPr"),
                    "date1904",
                    defaultValue: false,
                    span);
                var workbookRels = LoadRelationships(archive, "xl/_rels/workbook.xml.rels", span);
                var sheets = workbook.Root?
                    .Element(XlsxMain + "sheets")?
                    .Elements(XlsxMain + "sheet")
                    .ToArray() ?? [];

                var worksheets = new List<OpenPyxlWorksheet>();
                var worksheetPaths = new List<string>();
                foreach (var sheet in sheets)
                {
                    var name = (string?)sheet.Attribute("name") ?? "Sheet";
                    var relationshipId = (string?)sheet.Attribute(XlsxRelationships + "id");
                    if (relationshipId is null || !workbookRels.TryGetValue(relationshipId, out var target))
                    {
                        continue;
                    }

                    var path = ResolvePackagePath("xl/workbook.xml", target);
                    worksheetPaths.Add(path);
                    var worksheet = new OpenPyxlWorksheet(name) { SourcePath = path };
                    LoadWorksheetCells(archive, path, worksheet, sharedStrings, cellStyles, date1904, dataOnly, span);
                    worksheets.Add(worksheet);
                }

                if (!keepLinks && PackageHasExternalLinks(archive, workbook, span))
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        "openpyxl.load_workbook(..., keep_links=False) cannot drop external workbook links in Lython.",
                        span);
                }

                LoadWorkbookDefinedNames(workbook, worksheets, span);
                var activeIndex = ReadWorkbookActiveIndex(workbook, worksheets.Count, span);
                var hasVbaProject = PackageHasVbaProject(archive);
                var saveGuard = AnalyzeSaveGuard(archive, workbook, worksheetPaths, keepVba, span);
                if (dataOnly)
                {
                    saveGuard = AddDataOnlySaveGuard(saveGuard);
                }

                var snapshot = CapturePackageSnapshot(archive);
                var result = OpenPyxlWorkbook.FromWorksheets(worksheets, readOnly, date1904, activeIndex, saveGuard, snapshot, hasVbaProject: keepVba && hasVbaProject);
                result.SetLoadedNamedStyles(namedStyles);
                LoadWorkbookSecurity(workbook, result.Security, span);
                return result;
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or XmlException)
            {
                throw new LythonRuntimeException("InvalidFileException", $"Invalid .xlsx workbook: {ex.Message}", span);
            }
        }

        private static OpenPyxlSaveGuard AnalyzeSaveGuard(
            ZipArchive archive,
            XDocument workbook,
            IReadOnlyList<string> worksheetPaths,
            bool keepVba,
            LythonSourceSpan span)
        {
            var unsupported = new List<string>();
            AddUnsupportedPackageParts(archive, unsupported, keepVba);
            AddUnsupportedWorkbookFeatures(workbook, worksheetPaths.Count, unsupported);
            AddUnsupportedWorkbookRelationshipFeatures(archive, unsupported, span);
            foreach (var worksheetPath in worksheetPaths)
            {
                AddUnsupportedWorksheetFeatures(archive, worksheetPath, unsupported, span);
                AddUnsupportedWorksheetRelationshipFeatures(archive, worksheetPath, unsupported, span);
            }

            return unsupported.Count == 0
                ? OpenPyxlSaveGuard.Safe
                : OpenPyxlSaveGuard.Unsafe(SummarizeUnsupportedContent(unsupported));
        }

        private static bool PackageHasExternalLinks(ZipArchive archive, XDocument workbook, LythonSourceSpan span)
        {
            if (workbook.Root?.Element(XlsxMain + "externalReferences") is not null)
            {
                return true;
            }

            if (archive.Entries.Any(entry => IsExternalLinkPackagePart(NormalizePackagePartName(entry.FullName))))
            {
                return true;
            }

            var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels", span);
            return relationships.Root?
                .Elements(PackageRelationships + "Relationship")
                .Any(relationship => IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink")) == true;
        }

        private static bool PackageHasVbaProject(ZipArchive archive)
            => archive.Entries.Any(entry => IsVbaProjectPackagePart(NormalizePackagePartName(entry.FullName)));

        private static OpenPyxlSaveGuard AddDataOnlySaveGuard(OpenPyxlSaveGuard saveGuard)
        {
            const string reason = "workbook was loaded with data_only=True";
            return saveGuard.CanSave
                ? OpenPyxlSaveGuard.Unsafe(reason)
                : OpenPyxlSaveGuard.Unsafe(saveGuard.Reason + ", " + reason);
        }

        private static void AddUnsupportedPackageParts(
            ZipArchive archive,
            List<string> unsupported,
            bool keepVba)
        {
            foreach (var entry in archive.Entries)
            {
                var name = NormalizePackagePartName(entry.FullName);
                if (name.Length == 0)
                {
                    continue;
                }

                if (IsExternalLinkPackagePart(name))
                {
                    unsupported.Add("external link package part " + name);
                    continue;
                }

                if (IsVbaProjectPackagePart(name))
                {
                    if (!keepVba)
                    {
                        unsupported.Add("VBA project package part " + name + " without keep_vba=True");
                    }

                    continue;
                }

                if (IsUnsupportedBinaryOfficePart(name))
                {
                    unsupported.Add("binary Office package part " + name);
                }
            }
        }

        private static void AddUnsupportedWorkbookFeatures(XDocument workbook, int worksheetCount, List<string> unsupported)
        {
            _ = worksheetCount;
            foreach (var child in workbook.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "externalReferences")
                {
                    unsupported.Add("workbook external link references");
                }
            }
        }

        private static void AddUnsupportedWorkbookRelationshipFeatures(
            ZipArchive archive,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            AddUnsupportedRelationshipSaveRisks(
                archive,
                "xl/_rels/workbook.xml.rels",
                "workbook relationships",
                allowExternalHyperlinks: false,
                unsupported,
                span);
        }

        private static void AddUnsupportedBookViews(XElement bookViews, int worksheetCount, List<string> unsupported)
        {
            AddUnsupportedAttributes(bookViews, "workbook bookViews", unsupported);
            var workbookViewCount = 0;
            foreach (var child in bookViews.Elements())
            {
                if (child.Name != XlsxMain + "workbookView")
                {
                    unsupported.Add("workbook bookViews element " + child.Name.LocalName);
                    continue;
                }

                workbookViewCount++;
                if (workbookViewCount > 1)
                {
                    unsupported.Add("workbook bookViews multiple workbookView elements");
                }

                AddUnsupportedAttributes(child, "workbook workbookView", unsupported, "activeTab");
                var activeTab = (string?)child.Attribute("activeTab");
                if (activeTab is not null &&
                    (!int.TryParse(activeTab, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sheetId) ||
                     sheetId < 0 ||
                     sheetId >= worksheetCount))
                {
                    unsupported.Add("workbook workbookView activeTab");
                }

                AddUnsupportedChildElements(child, "workbook workbookView", unsupported);
            }
        }

        private static void AddUnsupportedDefinedNames(XElement definedNames, int worksheetCount, List<string> unsupported)
        {
            AddUnsupportedAttributes(definedNames, "workbook definedNames", unsupported);
            foreach (var definedName in definedNames.Elements())
            {
                if (definedName.Name != XlsxMain + "definedName")
                {
                    unsupported.Add("workbook definedNames element " + definedName.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(definedName, "workbook definedName", unsupported, "name", "localSheetId");
                var name = (string?)definedName.Attribute("name");
                var localSheetId = (string?)definedName.Attribute("localSheetId");
                if (name is not "_xlnm.Print_Area" and not "_xlnm.Print_Titles" ||
                    localSheetId is null ||
                    !int.TryParse(localSheetId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sheetId) ||
                    sheetId < 0 ||
                    sheetId >= worksheetCount)
                {
                    unsupported.Add("workbook definedName " + (name ?? "(missing name)"));
                }

                AddUnsupportedChildElements(definedName, "workbook definedName", unsupported);
            }
        }

        private static void AddUnsupportedStylesFeatures(ZipArchive archive, List<string> unsupported, LythonSourceSpan span)
        {
            var styles = LoadXml(archive, "xl/styles.xml", span);
            AddUnsupportedAttributes(styles.Root ?? new XElement(XlsxMain + "styleSheet"), "styles", unsupported);
            foreach (var child in styles.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "numFmts")
                {
                    AddUnsupportedNumberFormatFeatures(child, unsupported);
                    continue;
                }

                if (child.Name == XlsxMain + "cellXfs")
                {
                    AddUnsupportedCellFormatFeatures(child, unsupported);
                    continue;
                }

                if (child.Name == XlsxMain + "fonts" ||
                    child.Name == XlsxMain + "fills" ||
                    child.Name == XlsxMain + "borders" ||
                    child.Name == XlsxMain + "cellStyleXfs" ||
                    child.Name == XlsxMain + "cellStyles")
                {
                    AddUnsupportedDefaultStyleCollectionFeatures(child, unsupported);
                    continue;
                }

                unsupported.Add("styles element " + child.Name.LocalName);
            }
        }

        private static void AddUnsupportedNumberFormatFeatures(XElement numberFormats, List<string> unsupported)
        {
            AddUnsupportedAttributes(numberFormats, "styles numFmts", unsupported, "count");
            foreach (var child in numberFormats.Elements())
            {
                if (child.Name != XlsxMain + "numFmt")
                {
                    unsupported.Add("styles numFmts element " + child.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(child, "styles numFmt", unsupported, "numFmtId", "formatCode");
                AddUnsupportedChildElements(child, "styles numFmt", unsupported);
            }
        }

        private static void AddUnsupportedCellFormatFeatures(XElement cellFormats, List<string> unsupported)
        {
            AddUnsupportedAttributes(cellFormats, "styles cellXfs", unsupported, "count");
            foreach (var child in cellFormats.Elements())
            {
                if (child.Name != XlsxMain + "xf")
                {
                    unsupported.Add("styles cellXfs element " + child.Name.LocalName);
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    "styles cellXfs xf",
                    unsupported,
                    "numFmtId",
                    "fontId",
                    "fillId",
                    "borderId",
                    "xfId",
                    "applyNumberFormat",
                    "pivotButton",
                    "quotePrefix");
                AddUnsupportedChildElements(child, "styles cellXfs xf", unsupported);
                if (!IsZeroStyleReference(child, "fontId") ||
                    !IsZeroStyleReference(child, "fillId") ||
                    !IsZeroStyleReference(child, "borderId"))
                {
                    unsupported.Add("styles cellXfs non-number formatting");
                }
            }
        }

        private static void AddUnsupportedDefaultStyleCollectionFeatures(XElement collection, List<string> unsupported)
        {
            AddUnsupportedAttributes(collection, "styles " + collection.Name.LocalName, unsupported, "count");
            var count = collection.Elements().Count();
            var expected = collection.Name.LocalName == "fills" ? 2 : 1;
            if (count > expected)
            {
                unsupported.Add("styles " + collection.Name.LocalName + " custom records");
            }
        }

        private static bool IsZeroStyleReference(XElement element, string name)
            => (string?)element.Attribute(name) is null or "0";

        private static void AddUnsupportedWorksheetFeatures(
            ZipArchive archive,
            string worksheetPath,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var worksheet = LoadXml(archive, worksheetPath, span);
            foreach (var child in worksheet.Root?.Elements() ?? [])
            {
                if (child.Name == XlsxMain + "oleObjects")
                {
                    unsupported.Add($"{worksheetPath} OLE objects");
                }
                else if (child.Name == XlsxMain + "controls")
                {
                    unsupported.Add($"{worksheetPath} ActiveX controls");
                }
            }
        }

        private static void AddUnsupportedWorksheetRelationshipFeatures(
            ZipArchive archive,
            string worksheetPath,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var relationshipsPath = WorksheetRelationshipsPath(worksheetPath);
            if (archive.GetEntry(relationshipsPath) is null)
            {
                return;
            }

            AddUnsupportedRelationshipSaveRisks(
                archive,
                relationshipsPath,
                relationshipsPath,
                allowExternalHyperlinks: true,
                unsupported,
                span);
        }

        private static void AddUnsupportedRelationshipSaveRisks(
            ZipArchive archive,
            string relationshipsPath,
            string owner,
            bool allowExternalHyperlinks,
            List<string> unsupported,
            LythonSourceSpan span)
        {
            var relationships = LoadXml(archive, relationshipsPath, span);
            foreach (var relationship in relationships.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink"))
                {
                    unsupported.Add(owner + " external link relationship");
                    continue;
                }

                if (IsExternalRelationship(relationship) &&
                    !(allowExternalHyperlinks && IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink")))
                {
                    unsupported.Add(owner + " external relationship");
                }
            }
        }

        private static bool IsExternalRelationship(XElement relationship)
            => string.Equals((string?)relationship.Attribute("TargetMode"), "External", StringComparison.Ordinal);

        private static bool IsExternalLinkPackagePart(string name)
            => name.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase);

        private static bool IsVbaProjectPackagePart(string name)
            => string.Equals(name, "xl/vbaProject.bin", StringComparison.OrdinalIgnoreCase);

        private static bool IsUnsupportedBinaryOfficePart(string name)
            => name.StartsWith("xl/activeX/", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("xl/ctrlProps/", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase);

        private static void AddUnsupportedHyperlinkFeatures(XElement hyperlinks, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(hyperlinks, $"{worksheetPath} hyperlinks", unsupported);
            foreach (var child in hyperlinks.Elements())
            {
                if (child.Name != XlsxMain + "hyperlink")
                {
                    unsupported.Add($"{worksheetPath} hyperlinks element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(child, $"{worksheetPath} hyperlink", unsupported, "ref", "id", "location", "tooltip", "display");
                AddUnsupportedChildElements(child, $"{worksheetPath} hyperlink", unsupported);
            }
        }

        private static void AddUnsupportedSheetViewsFeatures(XElement sheetViews, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(sheetViews, $"{worksheetPath} sheetViews", unsupported);
            var sheetViewCount = 0;
            foreach (var child in sheetViews.Elements())
            {
                if (child.Name != XlsxMain + "sheetView")
                {
                    unsupported.Add($"{worksheetPath} sheetViews element {child.Name.LocalName}");
                    continue;
                }

                sheetViewCount++;
                if (sheetViewCount > 1)
                {
                    unsupported.Add($"{worksheetPath} sheetViews multiple sheetView elements");
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} sheetView",
                    unsupported,
                    "showGridLines",
                    "tabSelected",
                    "workbookViewId");
                var paneCount = 0;
                var selectionCount = 0;
                foreach (var viewChild in child.Elements())
                {
                    if (viewChild.Name == XlsxMain + "pane")
                    {
                        paneCount++;
                        if (paneCount > 1)
                        {
                            unsupported.Add($"{worksheetPath} sheetView multiple pane elements");
                        }

                        AddUnsupportedAttributes(
                            viewChild,
                            $"{worksheetPath} pane",
                            unsupported,
                            "xSplit",
                            "ySplit",
                            "topLeftCell",
                            "state");
                        if (!string.Equals((string?)viewChild.Attribute("state"), "frozen", StringComparison.Ordinal))
                        {
                            unsupported.Add($"{worksheetPath} pane state");
                        }

                        AddUnsupportedChildElements(viewChild, $"{worksheetPath} pane", unsupported);
                        continue;
                    }

                    if (viewChild.Name == XlsxMain + "selection")
                    {
                        selectionCount++;
                        if (selectionCount > 1)
                        {
                            unsupported.Add($"{worksheetPath} sheetView multiple selection elements");
                        }

                        AddUnsupportedAttributes(
                            viewChild,
                            $"{worksheetPath} selection",
                            unsupported,
                            "pane",
                            "activeCell",
                            "sqref");
                        AddUnsupportedChildElements(viewChild, $"{worksheetPath} selection", unsupported);
                        continue;
                    }

                    unsupported.Add($"{worksheetPath} sheetView element {viewChild.Name.LocalName}");
                }
            }
        }

        private static void AddUnsupportedColumnDimensionFeatures(XElement columns, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(columns, $"{worksheetPath} cols", unsupported);
            foreach (var child in columns.Elements())
            {
                if (child.Name != XlsxMain + "col")
                {
                    unsupported.Add($"{worksheetPath} cols element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} col",
                    unsupported,
                    "min",
                    "max",
                    "width",
                    "hidden",
                    "customWidth");
                AddUnsupportedChildElements(child, $"{worksheetPath} col", unsupported);
            }
        }

        private static void AddUnsupportedRowDimensionFeatures(XElement sheetData, string worksheetPath, List<string> unsupported)
        {
            AddUnsupportedAttributes(sheetData, $"{worksheetPath} sheetData", unsupported);
            foreach (var child in sheetData.Elements())
            {
                if (child.Name != XlsxMain + "row")
                {
                    unsupported.Add($"{worksheetPath} sheetData element {child.Name.LocalName}");
                    continue;
                }

                AddUnsupportedAttributes(
                    child,
                    $"{worksheetPath} row",
                    unsupported,
                    "r",
                    "spans",
                    "ht",
                    "hidden",
                    "customHeight");
            }
        }

        private static void AddUnsupportedAttributes(
            XElement element,
            string owner,
            List<string> unsupported,
            params string[] supported)
        {
            var supportedAttributes = new HashSet<string>(supported, StringComparer.Ordinal);
            foreach (var attribute in element.Attributes())
            {
                if (!attribute.IsNamespaceDeclaration && !supportedAttributes.Contains(attribute.Name.LocalName))
                {
                    unsupported.Add($"{owner} attribute {attribute.Name.LocalName}");
                }
            }
        }

        private static void AddUnsupportedChildElements(XElement element, string owner, List<string> unsupported)
        {
            foreach (var child in element.Elements())
            {
                unsupported.Add($"{owner} element {child.Name.LocalName}");
            }
        }

        private static string SummarizeUnsupportedContent(List<string> unsupported)
        {
            var distinct = unsupported.Distinct(StringComparer.Ordinal).Take(6).ToArray();
            var suffix = unsupported.Distinct(StringComparer.Ordinal).Count() > distinct.Length
                ? ", ..."
                : string.Empty;
            return string.Join(", ", distinct) + suffix;
        }

        public static byte[] Save(OpenPyxlWorkbook workbook, ExecutionContext context, LythonSourceSpan span)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var styleRegistry = OpenPyxlStyleRegistry.Create(workbook);
                var generateStyles = ShouldGenerateStyles(workbook, styleRegistry);
                var preserveLoadedStyleIds = ShouldPreserveOriginalStyles(workbook);
                var generatedParts = GeneratedPackagePartNames(workbook, generateStyles);
                var workbookRelationshipPlan = CreateWorkbookRelationshipPlan(workbook, generateStyles);
                WritePreservedPackageParts(archive, workbook.PackageSnapshot, generatedParts);
                WriteXml(archive, "[Content_Types].xml", CreateContentTypes(workbook, generateStyles, generatedParts));
                WriteXml(archive, "_rels/.rels", CreateRootRelationships(workbook.PackageSnapshot));
                WriteXml(archive, "xl/workbook.xml", CreateWorkbookXml(workbook, workbookRelationshipPlan));
                WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships(workbook, generateStyles, workbookRelationshipPlan));
                if (generateStyles)
                {
                    WriteXml(archive, "xl/styles.xml", CreateStylesXml(styleRegistry));
                }

                for (var i = 0; i < workbook.Worksheets.Count; i++)
                {
                    context.CheckExecutionBudget(span);
                    var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                    var worksheetRelationshipPlan = CreateWorksheetRelationshipPlan(workbook.Worksheets[i]);
                    WriteXml(archive, worksheetPath, CreateWorksheetXml(workbook.Worksheets[i], styleRegistry, preserveLoadedStyleIds, worksheetRelationshipPlan));
                    if (worksheetRelationshipPlan.HasRelationships)
                    {
                        WriteXml(archive, WorksheetRelationshipsPath(worksheetPath), CreateWorksheetRelationships(workbook.Worksheets[i], worksheetRelationshipPlan));
                    }
                }

                WriteUpdatedLoadedTableParts(archive, workbook);
                WriteUpdatedLoadedCommentsParts(archive, workbook);
            }

            var payload = stream.ToArray();
            context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(payload.Length), span);
            return payload;
        }

        private static OpenPyxlPackageSnapshot CapturePackageSnapshot(ZipArchive archive)
        {
            var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in archive.Entries)
            {
                var name = NormalizePackagePartName(entry.FullName);
                if (name.Length == 0 || parts.ContainsKey(name))
                {
                    continue;
                }

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                parts[name] = buffer.ToArray();
            }

            return new OpenPyxlPackageSnapshot(parts);
        }

        private static XDocument? LoadSnapshotXml(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            if (snapshot is null || !snapshot.Value.Parts.TryGetValue(NormalizePackagePartName(path), out var payload))
            {
                return null;
            }

            using var stream = new MemoryStream(payload, writable: false);
            return XDocument.Load(stream);
        }

        public static string? StructuralMutationPreservedFeatureReason(OpenPyxlWorksheet worksheet)
        {
            if (!worksheet.HasStructuralMutation || worksheet.SourcePath is null)
            {
                return null;
            }

            var features = PreservedStructuralWorksheetFeatures(worksheet);
            return features.Count == 0
                ? null
                : $"worksheet '{worksheet.Title}' has preserved {string.Join(", ", features)}";
        }

        private static IReadOnlyList<string> PreservedStructuralWorksheetFeatures(OpenPyxlWorksheet worksheet)
        {
            var snapshot = worksheet.Workbook?.PackageSnapshot;
            var worksheetPath = worksheet.SourcePath.RequireNotNull();
            var features = new SortedSet<string>(StringComparer.Ordinal);
            var worksheetDocument = LoadSnapshotXml(snapshot, worksheetPath);
            foreach (var child in worksheetDocument?.Root?.Elements() ?? [])
            {
                if (PreservedStructuralWorksheetElementName(child) is { } name)
                {
                    features.Add(name);
                }
            }

            var relationships = LoadSnapshotXml(snapshot, WorksheetRelationshipsPath(worksheetPath));
            foreach (var relationship in relationships?.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if (PreservedStructuralWorksheetRelationshipName(snapshot, worksheetPath, worksheet, relationship) is { } name)
                {
                    features.Add(name);
                }
            }

            return features.ToArray();
        }

        private static string? PreservedStructuralWorksheetElementName(XElement element)
        {
            if (element.Name == XlsxMain + "mergeCells")
            {
                return "merged ranges";
            }

            if (element.Name == XlsxMain + "picture")
            {
                return "images";
            }

            return null;
        }

        private static string? PreservedStructuralWorksheetRelationshipName(
            OpenPyxlPackageSnapshot? snapshot,
            string worksheetPath,
            OpenPyxlWorksheet worksheet,
            XElement relationship)
        {
            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments"))
            {
                var target = (string?)relationship.Attribute("Target");
                if (target is not null &&
                    worksheet.HasLoadedCommentsUpdate &&
                    worksheet.CommentsSourcePath is not null &&
                    string.Equals(ResolvePackagePath(worksheetPath, target), worksheet.CommentsSourcePath, StringComparison.Ordinal))
                {
                    return null;
                }

                return "comments";
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/vmlDrawing"))
            {
                var target = (string?)relationship.Attribute("Target");
                return target is null || IsVmlDrawingStructurallyAnchored(snapshot, ResolvePackagePath(worksheetPath, target))
                    ? "legacy drawings/comments"
                    : null;
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing"))
            {
                var target = (string?)relationship.Attribute("Target");
                return target is null || IsSpreadsheetDrawingStructurallyAnchored(snapshot, ResolvePackagePath(worksheetPath, target))
                    ? "drawings/images/charts"
                    : null;
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"))
            {
                return "images";
            }

            if (IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart"))
            {
                return "charts";
            }

            return null;
        }

        private static bool IsVmlDrawingStructurallyAnchored(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            var document = LoadSnapshotXml(snapshot, path);
            if (document?.Root is null)
            {
                return true;
            }

            return document.Root
                .Descendants()
                .Any(element =>
                    element.Name.LocalName is "ClientData" or "Row" or "Column");
        }

        private static bool IsSpreadsheetDrawingStructurallyAnchored(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            var document = LoadSnapshotXml(snapshot, path);
            if (document?.Root is null)
            {
                return true;
            }

            return document.Root
                .Descendants()
                .Any(element =>
                    element.Name.LocalName is "oneCellAnchor" or "twoCellAnchor" or "absoluteAnchor" or "from" or "to");
        }

        private static bool ShouldPreserveOriginalStyles(OpenPyxlWorkbook workbook)
            => workbook.PackageSnapshot?.Parts.ContainsKey("xl/styles.xml") == true;

        private static bool ShouldGenerateStyles(OpenPyxlWorkbook workbook, OpenPyxlStyleRegistry styleRegistry)
            => styleRegistry.HasCustomStyles && !ShouldPreserveOriginalStyles(workbook);

        private static HashSet<string> GeneratedPackagePartNames(OpenPyxlWorkbook workbook, bool generateStyles)
        {
            var generated = new HashSet<string>(StringComparer.Ordinal)
            {
                "[Content_Types].xml",
                "_rels/.rels",
                "xl/workbook.xml",
                "xl/_rels/workbook.xml.rels",
                "xl/sharedStrings.xml",
            };

            if (generateStyles)
            {
                generated.Add("xl/styles.xml");
            }

            foreach (var tablePath in UpdatedLoadedTablePartPaths(workbook))
            {
                generated.Add(tablePath);
            }

            foreach (var commentsPath in UpdatedLoadedCommentsPartPaths(workbook))
            {
                generated.Add(commentsPath);
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                generated.Add(worksheetPath);
                generated.Add(WorksheetRelationshipsPath(worksheetPath));
            }

            return generated;
        }

        private static IEnumerable<string> UpdatedLoadedTablePartPaths(OpenPyxlWorkbook workbook)
            => workbook.Worksheets
                .SelectMany(worksheet => worksheet.Tables.Values)
                .Where(table => table.HasLoadedPartUpdate && table.SourcePath is not null)
                .Select(table => table.SourcePath.RequireNotNull())
                .Distinct(StringComparer.Ordinal);

        private static IEnumerable<string> UpdatedLoadedCommentsPartPaths(OpenPyxlWorkbook workbook)
            => workbook.Worksheets
                .Where(worksheet => worksheet.HasLoadedCommentsUpdate)
                .Select(worksheet => worksheet.CommentsSourcePath)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal);

        private static void WritePreservedPackageParts(ZipArchive archive, OpenPyxlPackageSnapshot? snapshot, IReadOnlySet<string> generatedParts)
        {
            if (snapshot is null)
            {
                return;
            }

            foreach (var pair in snapshot.Value.Parts)
            {
                if (generatedParts.Contains(pair.Key))
                {
                    continue;
                }

                var entry = archive.CreateEntry(pair.Key);
                using var stream = entry.Open();
                stream.Write(pair.Value, 0, pair.Value.Length);
            }
        }

        private static void WriteUpdatedLoadedTableParts(ZipArchive archive, OpenPyxlWorkbook workbook)
        {
            foreach (var table in workbook.Worksheets
                .SelectMany(worksheet => worksheet.Tables.Values)
                .Where(table => table.HasLoadedPartUpdate && table.SourcePath is not null)
                .OrderBy(table => table.SourcePath, StringComparer.Ordinal))
            {
                WriteXml(archive, table.SourcePath.RequireNotNull(), CreateLoadedTableXml(workbook.PackageSnapshot, table));
            }
        }

        private static void WriteUpdatedLoadedCommentsParts(ZipArchive archive, OpenPyxlWorkbook workbook)
        {
            foreach (var worksheet in workbook.Worksheets
                .Where(worksheet => worksheet.HasLoadedCommentsUpdate && worksheet.CommentsSourcePath is not null)
                .OrderBy(worksheet => worksheet.CommentsSourcePath, StringComparer.Ordinal))
            {
                WriteXml(archive, worksheet.CommentsSourcePath.RequireNotNull(), CreateLoadedCommentsXml(worksheet));
            }
        }

        private static XDocument CreateLoadedTableXml(OpenPyxlPackageSnapshot? snapshot, OpenPyxlTable table)
        {
            var document = table.SourcePath is null ? null : LoadSnapshotXml(snapshot, table.SourcePath);
            var root = document?.Root is null
                ? new XElement(XlsxMain + "table")
                : new XElement(document.Root);
            root.SetAttributeValue("name", table.DisplayName);
            root.SetAttributeValue("displayName", table.DisplayName);
            root.SetAttributeValue("ref", table.Reference);
            root.Element(XlsxMain + "autoFilter")?.SetAttributeValue("ref", table.Reference);

            if (table.TableStyleInfo is OpenPyxlTableStyleInfo styleInfo)
            {
                var style = root.Element(XlsxMain + "tableStyleInfo");
                if (style is null)
                {
                    style = new XElement(XlsxMain + "tableStyleInfo");
                    root.Add(style);
                }

                style.SetAttributeValue("name", styleInfo.Name);
                style.SetAttributeValue("showFirstColumn", styleInfo.ShowFirstColumn ? "1" : "0");
                style.SetAttributeValue("showLastColumn", styleInfo.ShowLastColumn ? "1" : "0");
                style.SetAttributeValue("showRowStripes", styleInfo.ShowRowStripes ? "1" : "0");
                style.SetAttributeValue("showColumnStripes", styleInfo.ShowColumnStripes ? "1" : "0");
            }

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static XDocument CreateLoadedCommentsXml(OpenPyxlWorksheet worksheet)
        {
            var authors = worksheet.Comments
                .OrderBy(pair => pair.Key.Row)
                .ThenBy(pair => pair.Key.Column)
                .Select(pair => pair.Value.Author)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var authorIds = authors
                .Select((author, index) => new { author, index })
                .ToDictionary(item => item.author, item => item.index, StringComparer.Ordinal);

            var root = new XElement(
                XlsxMain + "comments",
                new XElement(
                    XlsxMain + "authors",
                    authors.Select(author => new XElement(XlsxMain + "author", author))),
                new XElement(
                    XlsxMain + "commentList",
                    worksheet.Comments
                        .OrderBy(pair => pair.Key.Row)
                        .ThenBy(pair => pair.Key.Column)
                        .Select(pair => new XElement(
                            XlsxMain + "comment",
                            new XAttribute("ref", CellReference(pair.Key.Row, pair.Key.Column)),
                            new XAttribute("authorId", authorIds[pair.Value.Author]),
                            new XElement(
                                XlsxMain + "text",
                                new XElement(XlsxMain + "t", pair.Value.Text))))));

            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

    }
}
