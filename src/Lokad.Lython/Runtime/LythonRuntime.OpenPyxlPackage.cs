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
    private static class OpenPyxlPackage
    {
        public static OpenPyxlWorkbook Load(ReadOnlyMemory<byte> payload, bool dataOnly, bool readOnly, bool keepLinks, bool keepVba, LythonSourceSpan span)
        {
            try
            {
                using var stream = new MemoryStream(payload.ToArray(), writable: false);
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

        private static XDocument CreateContentTypes(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            IReadOnlySet<string> generatedParts)
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

            foreach (var tablePath in UpdatedLoadedTablePartPaths(workbook))
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + tablePath,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml");
            }

            foreach (var commentsPath in UpdatedLoadedCommentsPartPaths(workbook))
            {
                AddOrReplaceContentTypeOverride(
                    root,
                    "/" + commentsPath,
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

        private sealed record OpenPyxlWorksheetRelationshipPlan(IReadOnlyDictionary<CellAddress, string> HyperlinkIds, IReadOnlyList<XElement> PreservedRelationships)
        {
            public bool HasRelationships => HyperlinkIds.Count > 0 || PreservedRelationships.Count > 0;
        }

        private static OpenPyxlWorksheetRelationshipPlan CreateWorksheetRelationshipPlan(OpenPyxlWorksheet worksheet)
        {
            var preserved = PreservedWorksheetRelationships(worksheet).ToArray();
            var usedIds = preserved
                .Select(relationship => (string?)relationship.Attribute("Id"))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
            var hyperlinkIds = new Dictionary<CellAddress, string>();
            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                var id = NextRelationshipId(usedIds);
                usedIds.Add(id);
                hyperlinkIds[pair.Key] = id;
            }

            return new OpenPyxlWorksheetRelationshipPlan(hyperlinkIds, preserved);
        }

        private static XDocument CreateWorksheetRelationships(OpenPyxlWorksheet worksheet, OpenPyxlWorksheetRelationshipPlan plan)
        {
            var root = new XElement(PackageRelationships + "Relationships");
            foreach (var relationship in plan.PreservedRelationships)
            {
                root.Add(new XElement(relationship));
            }

            foreach (var pair in worksheet.Hyperlinks.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
            {
                root.Add(new XElement(
                    PackageRelationships + "Relationship",
                    new XAttribute("Id", plan.HyperlinkIds[pair.Key]),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                    new XAttribute("Target", pair.Value),
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
            if (element is null && !workbook.Date1904)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "workbookPr");
            if (workbook.Date1904)
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
            if (element is null && !workbook.HasFormulaCells)
            {
                return null;
            }

            element ??= new XElement(XlsxMain + "calcPr");
            if (workbook.HasFormulaCells)
            {
                element.SetAttributeValue("fullCalcOnLoad", "1");
                element.SetAttributeValue("forceFullCalc", "1");
            }

            return element;
        }

        private static XElement? CloneWorkbookChild(XElement? originalRoot, XName name)
            => originalRoot?.Element(name) is { } child ? new XElement(child) : null;

        private static string CreatePrintAreaDefinedNameText(OpenPyxlWorksheet worksheet)
            => string.Join(
                ",",
                worksheet.PrintArea.RequireNotNull().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(range => SheetQualifiedReference(worksheet.Title, AbsoluteCellOrRangeReference(range))));

        private static string CreatePrintTitlesDefinedNameText(OpenPyxlWorksheet worksheet)
        {
            var references = new List<string>();
            if (worksheet.PrintTitleRows is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteRowRangeReference(worksheet.PrintTitleRows)));
            }

            if (worksheet.PrintTitleCols is not null)
            {
                references.Add(SheetQualifiedReference(worksheet.Title, AbsoluteColumnRangeReference(worksheet.PrintTitleCols)));
            }

            return string.Join(",", references);
        }

        private static string SheetQualifiedReference(string sheetTitle, string reference)
            => "'" + sheetTitle.Replace("'", "''", StringComparison.Ordinal) + "'!" + reference;

        private static string AbsoluteCellOrRangeReference(string reference)
        {
            if (!reference.Contains(':', StringComparison.Ordinal))
            {
                return AbsoluteCellReference(reference);
            }

            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return AbsoluteCellReference(parts[0]) + ":" + AbsoluteCellReference(parts[1]);
        }

        private static string AbsoluteCellReference(string reference)
        {
            var address = ParseCellAddress(reference, null);
            return "$" + ColumnName(address.Column) + "$" + address.Row.ToString(CultureInfo.InvariantCulture);
        }

        private static string AbsoluteRowRangeReference(string reference)
        {
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return "$" + parts[0] + ":$" + parts[1];
        }

        private static string AbsoluteColumnRangeReference(string reference)
        {
            var parts = reference.Split(':', 2, StringSplitOptions.TrimEntries);
            return "$" + parts[0] + ":$" + parts[1];
        }

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

        private static XDocument CreateStylesXml(OpenPyxlStyleRegistry styles)
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(
                    XlsxMain + "styleSheet",
                    styles.NumberFormats.Count == 0
                        ? null
                        : new XElement(
                            XlsxMain + "numFmts",
                            new XAttribute("count", styles.NumberFormats.Count),
                            styles.NumberFormats.OrderBy(pair => pair.Value).Select(pair => new XElement(
                                XlsxMain + "numFmt",
                                new XAttribute("numFmtId", pair.Value),
                                new XAttribute("formatCode", pair.Key)))),
                    new XElement(
                        XlsxMain + "fonts",
                        new XAttribute("count", styles.Fonts.Count + 1),
                        CreateDefaultFontXml(),
                        styles.Fonts.Select(CreateFontXml)),
                    new XElement(
                        XlsxMain + "fills",
                        new XAttribute("count", styles.Fills.Count + 2),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "none"))),
                        new XElement(XlsxMain + "fill", new XElement(XlsxMain + "patternFill", new XAttribute("patternType", "gray125"))),
                        styles.Fills.Select(CreateFillXml)),
                    new XElement(
                        XlsxMain + "borders",
                        new XAttribute("count", styles.Borders.Count + 1),
                        CreateDefaultBorderXml(),
                        styles.Borders.Select(CreateBorderXml)),
                    new XElement(
                        XlsxMain + "cellStyleXfs",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"))),
                    new XElement(
                        XlsxMain + "cellXfs",
                        new XAttribute("count", styles.CellStyles.Count + 1),
                        new XElement(
                            XlsxMain + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"),
                            new XAttribute("xfId", "0")),
                        styles.CellStyles.Select(style => CreateCellFormatXml(styles, style))),
                    new XElement(
                        XlsxMain + "cellStyles",
                        new XAttribute("count", "1"),
                        new XElement(
                            XlsxMain + "cellStyle",
                            new XAttribute("name", "Normal"),
                            new XAttribute("xfId", "0"),
                            new XAttribute("builtinId", "0")))));
        }

        private static XElement CreateDefaultFontXml()
            => new(
                XlsxMain + "font",
                new XElement(XlsxMain + "sz", new XAttribute("val", "11")),
                new XElement(XlsxMain + "color", new XAttribute("theme", "1")),
                new XElement(XlsxMain + "name", new XAttribute("val", "Calibri")),
                new XElement(XlsxMain + "family", new XAttribute("val", "2")),
                new XElement(XlsxMain + "scheme", new XAttribute("val", "minor")));

        private static XElement CreateFontXml(OpenPyxlStyleValue font)
        {
            var children = new List<object>();
            if (StyleBool(font, "bold"))
            {
                children.Add(new XElement(XlsxMain + "b"));
            }

            if (StyleBool(font, "italic"))
            {
                children.Add(new XElement(XlsxMain + "i"));
            }

            if (StyleBool(font, "strike"))
            {
                children.Add(new XElement(XlsxMain + "strike"));
            }

            if (StyleString(font, "underline") is { } underline)
            {
                children.Add(underline == "single"
                    ? new XElement(XlsxMain + "u")
                    : new XElement(XlsxMain + "u", new XAttribute("val", underline)));
            }

            if (StyleString(font, "sz") is { } size)
            {
                children.Add(new XElement(XlsxMain + "sz", new XAttribute("val", size)));
            }

            if (CreateColorXml("color", StyleValue(font, "color")) is { } color)
            {
                children.Add(color);
            }

            if (StyleString(font, "name") is { } name)
            {
                children.Add(new XElement(XlsxMain + "name", new XAttribute("val", name)));
            }

            return new XElement(XlsxMain + "font", children);
        }

        private static XElement CreateFillXml(OpenPyxlStyleValue fill)
        {
            var pattern = new XElement(
                XlsxMain + "patternFill",
                new XAttribute("patternType", StyleString(fill, "fill_type") ?? "none"));
            if (CreateColorXml("fgColor", StyleValue(fill, "fgColor")) is { } fgColor)
            {
                pattern.Add(fgColor);
            }

            if (CreateColorXml("bgColor", StyleValue(fill, "bgColor")) is { } bgColor)
            {
                pattern.Add(bgColor);
            }

            return new XElement(XlsxMain + "fill", pattern);
        }

        private static XElement CreateDefaultBorderXml()
            => new(
                XlsxMain + "border",
                new XElement(XlsxMain + "left"),
                new XElement(XlsxMain + "right"),
                new XElement(XlsxMain + "top"),
                new XElement(XlsxMain + "bottom"),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderXml(OpenPyxlStyleValue border)
            => new(
                XlsxMain + "border",
                CreateBorderSideXml("left", StyleValue(border, "left")),
                CreateBorderSideXml("right", StyleValue(border, "right")),
                CreateBorderSideXml("top", StyleValue(border, "top")),
                CreateBorderSideXml("bottom", StyleValue(border, "bottom")),
                new XElement(XlsxMain + "diagonal"));

        private static XElement CreateBorderSideXml(string name, object? value)
        {
            if (value is not OpenPyxlStyleValue side)
            {
                return new XElement(XlsxMain + name);
            }

            var attributes = new List<XAttribute>();
            if (StyleString(side, "style") is { } style)
            {
                attributes.Add(new XAttribute("style", style));
            }

            var element = new XElement(XlsxMain + name, attributes);
            if (CreateColorXml("color", StyleValue(side, "color")) is { } color)
            {
                element.Add(color);
            }

            return element;
        }

        private static XElement? CreateColorXml(string elementName, object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is OpenPyxlColor color)
            {
                var attributes = new List<XAttribute>();
                switch (color.Type)
                {
                    case "indexed" when color.Indexed is { } indexed:
                        attributes.Add(new XAttribute("indexed", indexed.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case "theme" when color.Theme is { } theme:
                        attributes.Add(new XAttribute("theme", theme.ToString(CultureInfo.InvariantCulture)));
                        break;
                    case "auto" when color.Auto is { } auto:
                        attributes.Add(new XAttribute("auto", auto ? "1" : "0"));
                        break;
                    default:
                        if (color.Rgb is { } rgb)
                        {
                            attributes.Add(new XAttribute("rgb", rgb));
                        }

                        break;
                }

                if (Math.Abs(color.Tint) > double.Epsilon)
                {
                    attributes.Add(new XAttribute("tint", color.Tint.ToString(CultureInfo.InvariantCulture)));
                }

                return attributes.Count == 0 ? null : new XElement(XlsxMain + elementName, attributes);
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return new XElement(XlsxMain + elementName, new XAttribute("rgb", NormalizeRgbColor(text.AsString(), "openpyxl style color", null)));
            }

            return null;
        }

        private static XElement CreateCellFormatXml(OpenPyxlStyleRegistry registry, OpenPyxlCellStyleDefinition style)
        {
            var attributes = new List<XAttribute>
            {
                new("numFmtId", registry.NumberFormatId(style.NumberFormat)),
                new("fontId", registry.FontId(style.Font)),
                new("fillId", registry.FillId(style.Fill)),
                new("borderId", registry.BorderId(style.Border)),
                new("xfId", "0"),
            };
            if (style.NumberFormat != "General")
            {
                attributes.Add(new XAttribute("applyNumberFormat", "1"));
            }

            if (style.Font is not null)
            {
                attributes.Add(new XAttribute("applyFont", "1"));
            }

            if (style.Fill is not null)
            {
                attributes.Add(new XAttribute("applyFill", "1"));
            }

            if (style.Border is not null)
            {
                attributes.Add(new XAttribute("applyBorder", "1"));
            }

            var children = new List<object>();
            if (style.Alignment is not null)
            {
                attributes.Add(new XAttribute("applyAlignment", "1"));
                children.Add(CreateAlignmentXml(style.Alignment));
            }

            if (style.Protection is not null)
            {
                attributes.Add(new XAttribute("applyProtection", "1"));
                children.Add(CreateProtectionXml(style.Protection));
            }

            return new XElement(XlsxMain + "xf", attributes, children);
        }

        private static XElement CreateAlignmentXml(OpenPyxlStyleValue alignment)
        {
            var attributes = new List<XAttribute>();
            AddOptionalAttribute(attributes, "horizontal", StyleString(alignment, "horizontal"));
            AddOptionalAttribute(attributes, "vertical", StyleString(alignment, "vertical"));
            if (StyleBool(alignment, "wrap_text"))
            {
                attributes.Add(new XAttribute("wrapText", "1"));
            }

            if (StyleBool(alignment, "shrink_to_fit"))
            {
                attributes.Add(new XAttribute("shrinkToFit", "1"));
            }

            AddOptionalAttribute(attributes, "textRotation", StyleString(alignment, "text_rotation"));
            return new XElement(XlsxMain + "alignment", attributes);
        }

        private static XElement CreateProtectionXml(OpenPyxlStyleValue protection)
            => new(
                XlsxMain + "protection",
                new XAttribute("locked", StyleBool(protection, "locked") ? "1" : "0"),
                new XAttribute("hidden", StyleBool(protection, "hidden") ? "1" : "0"));

        private static void AddOptionalAttribute(List<XAttribute> attributes, string name, string? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value));
            }
        }

        private static void AddOptionalBoolAttribute(List<XAttribute> attributes, string name, bool? value)
        {
            if (value is not null)
            {
                attributes.Add(new XAttribute(name, value.Value ? "1" : "0"));
            }
        }

        private static object? StyleValue(OpenPyxlStyleValue style, string name)
            => style.TryGetMember(name, out var value) && value is not PyNone ? value : null;

        private static string? StyleString(OpenPyxlStyleValue style, string name)
        {
            var value = StyleValue(style, name);
            if (value is null)
            {
                return null;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return text.AsString();
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return integer.ToString(CultureInfo.InvariantCulture);
            }

            return value is double floating
                ? floating.ToString(CultureInfo.InvariantCulture)
                : null;
        }

        private static bool StyleBool(OpenPyxlStyleValue style, string name)
            => StyleValue(style, name) is bool value && value;

        private static string StyleValueKey(OpenPyxlStyleValue? style)
        {
            if (style is null)
            {
                return string.Empty;
            }

            return style.QualifiedName + "(" + string.Join(
                ",",
                StyleMemberNames(style.QualifiedName).Select(name => name + "=" + StyleObjectKey(StyleValue(style, name)))) + ")";
        }

        private static string StyleObjectKey(object? value)
        {
            if (value is null or PyNone)
            {
                return "none";
            }

            if (value is OpenPyxlStyleValue style)
            {
                return StyleValueKey(style);
            }

            if (value is OpenPyxlColor color)
            {
                return color.Key;
            }

            if (PyStringOps.TryAsString(value, out var text))
            {
                return "str:" + text.AsString();
            }

            if (value is bool boolean)
            {
                return boolean ? "bool:true" : "bool:false";
            }

            if (PyNumberOps.TryAsInteger(value, out var integer))
            {
                return "int:" + integer.ToString(CultureInfo.InvariantCulture);
            }

            if (value is double floating)
            {
                return "float:" + floating.ToString(CultureInfo.InvariantCulture);
            }

            return value.GetType().FullName + ":" + value;
        }

        private static string[] StyleMemberNames(string qualifiedName)
            => qualifiedName switch
            {
                "openpyxl.styles.Font" => ["name", "sz", "bold", "italic", "color", "underline", "strike"],
                "openpyxl.styles.PatternFill" => ["fill_type", "fgColor", "bgColor"],
                "openpyxl.styles.Border" => ["left", "right", "top", "bottom"],
                "openpyxl.styles.Side" => ["style", "color"],
                "openpyxl.styles.Alignment" => ["horizontal", "vertical", "wrap_text", "text_rotation", "shrink_to_fit"],
                "openpyxl.styles.Protection" => ["locked", "hidden"],
                "openpyxl.styles.NamedStyle" => ["name", "number_format", "font", "fill", "border", "alignment", "protection"],
                _ => [],
            };

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
                        worksheet.Workbook?.Date1904 ?? false,
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
            bool date1904,
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
                        CreateFormulaCachedValueXml(formulaCachedValue, date1904));
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
                new XElement(XlsxMain + "v", CellNumberText(value, date1904)));
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

        private static XElement? CreateFormulaCachedValueXml(object value, bool date1904)
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

            return new XElement(XlsxMain + "v", CellNumberText(value, date1904));
        }

        private static string CellNumberText(object value, bool date1904)
        {
            return value switch
            {
                BigInteger integer => integer.ToString(CultureInfo.InvariantCulture),
                double floating => floating.ToString("R", CultureInfo.InvariantCulture),
                PyDecimal decimalValue => decimalValue.Value.ToString(CultureInfo.InvariantCulture),
                PyDate date => ExcelSerialFromDate(date, date1904).ToString("R", CultureInfo.InvariantCulture),
                PyDateTime dateTime => ExcelSerialFromDateTime(dateTime, date1904).ToString("R", CultureInfo.InvariantCulture),
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
