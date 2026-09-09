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
using Lokad.Lython.Runtime.Zip;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static partial class OpenPyxlPackage
    {
        // Archive containment bounds. Isolated probing showed hostile archives
        // crash or stall the pipeline (deep XML hangs, megabytes retained under
        // kilobyte budgets) while legitimate package parts nest only a few levels.
        private const int MaxArchiveXmlElementDepth = 1024;
        private const long XmlDocumentBytesPerByte = 16;
        private const int ArchiveEntryCopyChunkBytes = 65536;
        private const long SnapshotBaseBytesPerEntry = 96;
        private const long ModelCellBytes = 512;
        // Must remain a power of two: chunk checks below use it as a bit mask.
        private const int ArchiveBudgetCheckInterval = 64;

        public static OpenPyxlWorkbook Load(
            ReadOnlyMemory<byte> payload,
            OpenPyxlLoadOptions options,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            try
            {
                ValidateArchiveDirectory(payload, context, span);
                using var stream = MemoryMarshal.TryGetArray(payload, out var segment)
                    ? new MemoryStream(segment.Array.RequireNotNull(), segment.Offset, segment.Count, writable: false)
                    : new MemoryStream(payload.ToArray(), writable: false);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
                using var session = new OpenPyxlLoadSession(archive, context, span);
                var sharedStrings = LoadSharedStrings(session, context, span);
                var stylesDocument = session.LoadOptionalXmlDocument("xl/styles.xml");
                var cellStyles = LoadCellStyles(stylesDocument, context, span);
                var namedStyles = LoadNamedStyles(stylesDocument, context, span);
                var workbook = session.LoadXmlDocument("xl/workbook.xml");
                var dateSystem = ReadBooleanAttribute(
                    workbook.Root?.Element(XlsxMain + "workbookPr") ?? new XElement(XlsxMain + "workbookPr"),
                    "date1904",
                    defaultValue: false,
                    span)
                    ? ExcelDateSystem.Mac1904
                    : ExcelDateSystem.Windows1900;
                var workbookRels = LoadRelationships(session, "xl/_rels/workbook.xml.rels", context, span);
                var sheets = workbook.Root?
                    .Element(XlsxMain + "sheets")?
                    .Elements(XlsxMain + "sheet")
                    .ToArray() ?? [];

                var worksheets = new List<OpenPyxlWorksheet>();
                var worksheetPaths = new List<string>();
                var worksheetFeatureRisks = new List<string>();
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
                    context.CheckExecutionBudget(span);
                    var sheetDocument = LoadWorksheetCells(
                        session,
                        path,
                        worksheet,
                        sharedStrings,
                        cellStyles,
                        dateSystem,
                        options.HasFlag(OpenPyxlLoadOptions.DataOnly),
                        context,
                        span);
                    AddUnsupportedWorksheetFeatures(sheetDocument, path, worksheetFeatureRisks);
                    worksheets.Add(worksheet);
                }

                if (!options.HasFlag(OpenPyxlLoadOptions.KeepLinks) && PackageHasExternalLinks(session, workbook, context, span))
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        "openpyxl.load_workbook(..., keep_links=False) cannot drop external workbook links in Lython.",
                        span);
                }

                LoadWorkbookDefinedNames(workbook, worksheets, context, span);
                var activeIndex = ReadWorkbookActiveIndex(workbook, worksheets.Count, span);
                var hasVbaProject = archive.Entries.Any(entry => IsVbaProjectPackagePart(NormalizePackagePartName(entry.FullName)));
                var saveGuard = AnalyzeSaveGuard(session, workbook, worksheetPaths, worksheetFeatureRisks, options, context, span);
                if (options.HasFlag(OpenPyxlLoadOptions.DataOnly))
                {
                    saveGuard = AddDataOnlySaveGuard(saveGuard);
                }

                var snapshot = session.Finish(preserve: !options.HasFlag(OpenPyxlLoadOptions.ReadOnly));
                var result = OpenPyxlWorkbook.FromWorksheets(
                    worksheets,
                    options.HasFlag(OpenPyxlLoadOptions.ReadOnly),
                    dateSystem,
                    activeIndex,
                    saveGuard,
                    snapshot,
                    hasVbaProject: options.HasFlag(OpenPyxlLoadOptions.KeepVba) && hasVbaProject);
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
                throw InvalidFileException($"Invalid .xlsx workbook: {ex.Message}", span);
            }
        }

        private static OpenPyxlSaveGuard AnalyzeSaveGuard(
            OpenPyxlLoadSession session,
            XDocument workbook,
            IReadOnlyList<string> worksheetPaths,
            IReadOnlyList<string> worksheetFeatureRisks,
            OpenPyxlLoadOptions options,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var unsupported = new List<string>(worksheetFeatureRisks);
            AddUnsupportedPackageParts(session.Archive, unsupported, options.HasFlag(OpenPyxlLoadOptions.KeepVba), context, span);
            AddUnsupportedWorkbookFeatures(workbook, worksheetPaths.Count, unsupported);
            AddUnsupportedWorkbookRelationshipFeatures(session, unsupported, context, span);
            foreach (var worksheetPath in worksheetPaths)
            {
                context.CheckExecutionBudget(span);
                AddUnsupportedWorksheetRelationshipFeatures(session, worksheetPath, unsupported, context, span);
            }

            return unsupported.Count == 0
                ? OpenPyxlSaveGuard.Safe
                : OpenPyxlSaveGuard.Unsafe(SummarizeUnsupportedContent(unsupported));
        }

        private static bool PackageHasExternalLinks(OpenPyxlLoadSession session, XDocument workbook, ExecutionContext context, LythonSourceSpan span)
        {
            if (workbook.Root?.Element(XlsxMain + "externalReferences") is not null)
            {
                return true;
            }

            if (session.Archive.Entries.Any(entry => IsExternalLinkPackagePart(NormalizePackagePartName(entry.FullName))))
            {
                return true;
            }

            var relationships = session.LoadXmlDocument("xl/_rels/workbook.xml.rels");
            return relationships.Root?
                .Elements(PackageRelationships + "Relationship")
                .Any(relationship => IsRelationshipType(relationship, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLink")) == true;
        }


        private static OpenPyxlSaveGuard AddDataOnlySaveGuard(OpenPyxlSaveGuard saveGuard)
        {
            const string reason = "workbook was loaded with data_only=True";
            return saveGuard.TryGetUnsafeReason(out var existingReason)
                ? OpenPyxlSaveGuard.Unsafe(existingReason + ", " + reason)
                : OpenPyxlSaveGuard.Unsafe(reason);
        }

        private static void AddUnsupportedPackageParts(
            ZipArchive archive,
            List<string> unsupported,
            bool keepVba,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var scanned = 0;
            foreach (var entry in archive.Entries)
            {
                if ((++scanned & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

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
            OpenPyxlLoadSession session,
            List<string> unsupported,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            AddUnsupportedRelationshipSaveRisks(
                session,
                "xl/_rels/workbook.xml.rels",
                "workbook relationships",
                allowExternalHyperlinks: false,
                unsupported,
                context,
                span);
        }

        private static void AddUnsupportedWorksheetFeatures(
            XDocument worksheet,
            string worksheetPath,
            List<string> unsupported)
        {
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
            OpenPyxlLoadSession session,
            string worksheetPath,
            List<string> unsupported,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var relationshipsPath = WorksheetRelationshipsPath(worksheetPath);
            if (!session.ContainsPart(relationshipsPath))
            {
                return;
            }

            AddUnsupportedRelationshipSaveRisks(
                session,
                relationshipsPath,
                relationshipsPath,
                allowExternalHyperlinks: true,
                unsupported,
                context,
                span);
        }

        private static void AddUnsupportedRelationshipSaveRisks(
            OpenPyxlLoadSession session,
            string relationshipsPath,
            string owner,
            bool allowExternalHyperlinks,
            List<string> unsupported,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var relationships = session.LoadXmlDocument(relationshipsPath);
            var scanned = 0;
            foreach (var relationship in relationships.Root?.Elements(PackageRelationships + "Relationship") ?? [])
            {
                if ((++scanned & (ArchiveBudgetCheckInterval - 1)) == 0)
                {
                    context.CheckExecutionBudget(span);
                }

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

        private static string SummarizeUnsupportedContent(List<string> unsupported)
        {
            var distinct = unsupported.Distinct(StringComparer.Ordinal).Take(7).ToArray();
            var displayedCount = Math.Min(6, distinct.Length);
            var suffix = distinct.Length > displayedCount ? ", ..." : string.Empty;
            return string.Join(", ", distinct, 0, displayedCount) + suffix;
        }

        /// <summary>
        /// Serialized workbook bytes with their committed governor charge. The
        /// caller transfers ownership by releasing <see cref="MemoryCharge"/>
        /// after host publication.
        /// </summary>
        internal readonly record struct OpenPyxlSavePayload(byte[] Bytes, long MemoryCharge);

        public static OpenPyxlSavePayload Save(OpenPyxlWorkbook workbook, ExecutionContext context, LythonSourceSpan span)
        {
            // Output capacity is reserved from model sizes before serializing so
            // unbounded output growth fails before it is materialized. The commit
            // transfers to the payload; the caller releases it after host publication.
            using var reservation = context.MemoryGovernor.ReserveTemporary(EstimateSaveBaseBytes(workbook), span);
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var styleRegistry = OpenPyxlStyleRegistry.Create(workbook);
                var generateStyles = ShouldGenerateStyles(workbook, styleRegistry);
                var preserveLoadedStyleIds = ShouldPreserveOriginalStyles(workbook);
                var updatedParts = UpdatedPackageParts.Create(workbook);
                var generatedParts = GeneratedPackagePartNames(workbook, generateStyles, updatedParts);
                var workbookRelationshipPlan = CreateWorkbookRelationshipPlan(workbook, generateStyles);
                var entryTimestamp = context.Host.LocalNow;
                WritePreservedPackageParts(archive, workbook.PackageSnapshot, generatedParts, entryTimestamp);
                WriteXml(archive, "[Content_Types].xml", CreateContentTypes(workbook, generateStyles, generatedParts, updatedParts), entryTimestamp);
                WriteXml(archive, "_rels/.rels", CreateRootRelationships(workbook.PackageSnapshot), entryTimestamp);
                WriteXml(archive, "xl/workbook.xml", CreateWorkbookXml(workbook, workbookRelationshipPlan), entryTimestamp);
                WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships(workbook, generateStyles, workbookRelationshipPlan), entryTimestamp);
                if (generateStyles)
                {
                    WriteXml(archive, "xl/styles.xml", CreateStylesXml(styleRegistry), entryTimestamp);
                }

                for (var i = 0; i < workbook.Worksheets.Count; i++)
                {
                    context.CheckExecutionBudget(span);
                    reservation.Grow(EstimateWorksheetOutputBytes(workbook.Worksheets[i]), span);
                    var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                    var worksheetRelationshipPlan = CreateWorksheetRelationshipPlan(workbook.Worksheets[i]);
                    WriteXml(archive, worksheetPath, CreateWorksheetXml(workbook.Worksheets[i], styleRegistry, preserveLoadedStyleIds, worksheetRelationshipPlan, context, span), entryTimestamp);
                    if (worksheetRelationshipPlan.HasRelationships)
                    {
                        WriteXml(archive, WorksheetRelationshipsPath(worksheetPath), CreateWorksheetRelationships(worksheetRelationshipPlan), entryTimestamp);
                    }
                }

                WriteUpdatedLoadedTableParts(archive, workbook.PackageSnapshot, updatedParts.Tables, entryTimestamp);
                WriteUpdatedLoadedCommentsParts(archive, updatedParts.Comments, entryTimestamp);
            }

            var payload = stream.ToArray();
            var payloadCharge = PyBytes.EstimateApproximateBytes(payload.Length);
            reservation.Grow(payloadCharge, span);
            context.MemoryGovernor.Reserve(payloadCharge, span);
            context.MemoryGovernor.Commit(payloadCharge);
            return new OpenPyxlSavePayload(payload, payloadCharge);
        }

        private static long EstimateSaveBaseBytes(OpenPyxlWorkbook workbook)
        {
            // Snapshot parts are copied verbatim into the output on top of the
            // retained snapshot charge, so output capacity accounts for them again.
            long total = 65536;
            if (workbook.PackageSnapshot is { } snapshot)
            {
                foreach (var part in snapshot.Parts.Values)
                {
                    if (part.Length > long.MaxValue - total)
                    {
                        return long.MaxValue;
                    }

                    total += part.Length;
                }
            }

            return total;
        }

        private static long EstimateWorksheetOutputBytes(OpenPyxlWorksheet worksheet)
            => 4096L + (256L * worksheet.Cells.Count);

        private static void ValidateArchiveDirectory(ReadOnlyMemory<byte> payload, ExecutionContext context, LythonSourceSpan span)
        {
            // Parse the central directory with the bounded ZIP metadata reader
            // BEFORE touching BCL ZipArchive entries, so hostile entry counts and
            // declared sizes fail before the platform materializes unbounded
            // directory objects. Nothing retains the result: release its charges
            // immediately after gating.
            ZipArchiveDirectory directory;
            try
            {
                directory = ZipDirectoryReader.Read(payload, forceUtf8Names: false, context, span);
            }
            catch (InvalidDataException exception)
            {
                throw InvalidFileException("Invalid .xlsx workbook: " + exception.Message, span);
            }

            try
            {
                var count = directory.Entries.Count;
                context.ObserveCollectionCount(count, span);
                long expandedTotal = 0;
                for (var index = 0; index < count; index++)
                {
                    if ((index & 63) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }

                    var entry = directory.Entries[index];
                    if (entry.UncompressedSize > (ulong)(long.MaxValue - SnapshotBaseBytesPerEntry))
                    {
                        throw InvalidFileException("Invalid .xlsx workbook: declared sizes overflow.", span);
                    }

                    var uncompressed = (long)entry.UncompressedSize;
                    if (uncompressed > long.MaxValue - expandedTotal - SnapshotBaseBytesPerEntry)
                    {
                        throw InvalidFileException("Invalid .xlsx workbook: declared sizes overflow.", span);
                    }

                    expandedTotal += uncompressed + SnapshotBaseBytesPerEntry;
                }

                context.MemoryGovernor.EnsureCanReserve(expandedTotal, span);
            }
            finally
            {
                context.MemoryGovernor.Release(directory.MetadataCharge);
            }
        }

        private static byte[] ReadBoundedEntryBytes(ZipArchiveEntry entry, ExecutionContext context, LythonSourceSpan span)
        {
            var declared = entry.Length;
            if (declared < 0 || declared > int.MaxValue)
            {
                throw InvalidFileException("Invalid .xlsx workbook: unsupported entry size.", span);
            }

            var length = (int)declared;
            using var reservation = context.MemoryGovernor.ReserveTemporary(32L + length, span);
            var result = new byte[length];
            using var stream = entry.Open();
            var offset = 0;
            var crc = 0xFFFFFFFFu;
            while (offset < length)
            {
                var read = stream.Read(result.AsSpan(offset, Math.Min(ArchiveEntryCopyChunkBytes, length - offset)));
                if (read == 0)
                {
                    throw InvalidFileException("Invalid .xlsx workbook: truncated entry.", span);
                }

                crc = Crc32.Update(crc, result.AsSpan(offset, read));

                offset += read;
                context.CheckExecutionBudget(span);
            }

            // A well-formed entry ends exactly at its declared length; trailing
            // bytes mean the directory lied.
            if (stream.ReadByte() != -1)
            {
                throw InvalidFileException("Invalid .xlsx workbook: entry longer than declared.", span);
            }

            // The platform does not validate content checksums on read, so compare
            // against the directory CRC explicitly before the bytes are used.
            if (~crc != entry.Crc32)
            {
                throw InvalidFileException("Invalid .xlsx workbook: entry failed checksum validation.", span);
            }

            return result;
        }

        /// <summary>
        /// Single-pass package XML reader: the DOM builder pulls through this
        /// wrapper, so depth validation and execution-budget checks share the
        /// one parse instead of scanning the bytes twice. Snapshot loads pass
        /// no context and keep depth-only enforcement.
        /// </summary>
        private sealed class BoundedPackageXmlReader : XmlReader
        {
            private readonly XmlReader _inner;
            private readonly ExecutionContext? _context;
            private readonly LythonSourceSpan? _span;
            private long _readsSinceCheck;
            private bool _disposed;
            public BoundedPackageXmlReader(XmlReader inner, ExecutionContext? context, LythonSourceSpan? span)
            {
                _inner = inner;
                _context = context;
                _span = span;
            }
            public override bool Read()
            {
                var moved = _inner.Read();
                if (moved)
                {
                    if (_inner.NodeType == XmlNodeType.Element && _inner.Depth > MaxArchiveXmlElementDepth)
                    {
                        throw InvalidFileException($"Invalid .xlsx workbook: XML nesting exceeds the maximum of {MaxArchiveXmlElementDepth} levels.", _span);
                    }
                    if (_context is not null && (++_readsSinceCheck & 1023) == 0)
                    {
                        _context.CheckExecutionBudget(_span);
                    }
                }
                return moved;
            }
            public override void Close() => _inner.Close();
            protected override void Dispose(bool disposing)
            {
                if (disposing && !_disposed)
                {
                    _disposed = true;
                    _inner.Dispose();
                }
                base.Dispose(disposing);
            }
            public override int AttributeCount => _inner.AttributeCount;
            public override string BaseURI => _inner.BaseURI;
            public override int Depth => _inner.Depth;
            public override bool EOF => _inner.EOF;
            public override string GetAttribute(int i) => _inner.GetAttribute(i);
            public override string GetAttribute(string name) => _inner.GetAttribute(name).RequireNotNull();
            public override string? GetAttribute(string name, string? namespaceURI) => _inner.GetAttribute(name, namespaceURI);
            public override bool HasValue => _inner.HasValue;
            public override bool IsEmptyElement => _inner.IsEmptyElement;
            public override string LocalName => _inner.LocalName;
            public override string? LookupNamespace(string prefix) => _inner.LookupNamespace(prefix);
            public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);
            public override bool MoveToAttribute(string name, string? ns) => _inner.MoveToAttribute(name, ns);
            public override void MoveToAttribute(int i) => _inner.MoveToAttribute(i);
            public override bool MoveToElement() => _inner.MoveToElement();
            public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();
            public override bool MoveToNextAttribute() => _inner.MoveToNextAttribute();
            public override string Name => _inner.Name;
            public override string NamespaceURI => _inner.NamespaceURI;
            public override XmlNameTable NameTable => _inner.NameTable;
            public override XmlNodeType NodeType => _inner.NodeType;
            public override string Prefix => _inner.Prefix;
            public override char QuoteChar => _inner.QuoteChar;
            public override bool ReadAttributeValue() => _inner.ReadAttributeValue();
            public override ReadState ReadState => _inner.ReadState;
            public override void ResolveEntity() => _inner.ResolveEntity();
            public override string Value => _inner.Value;
        }
        private static XDocument ParseBoundedXmlDocument(byte[] payload, MemoryGovernor governor, ExecutionContext? context, LythonSourceSpan? span)
        {
            using var reservation = governor.ReserveTemporary(XmlDocumentBytesPerByte * payload.Length, span);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
            };
            using var stream = new MemoryStream(payload, writable: false);
            using var inner = XmlReader.Create(stream, settings);
            using var reader = new BoundedPackageXmlReader(inner, context, span);
            return XDocument.Load(reader);
        }

        /// <summary>
        /// Bounded single-flight package reader for one workbook load. Each entry
        /// is inflated at most once under a retained charge; parsed-document charges
        /// accumulate in the session until the load finishes. <see cref="Finish"/>
        /// transfers retention for preservation parts (writable workbooks) or releases
        /// everything (read-only workbooks, which can never save).
        /// </summary>
        private sealed class OpenPyxlLoadSession : IDisposable
        {
            private readonly ZipArchive _archive;
            private readonly ExecutionContext _context;
            private readonly LythonSourceSpan _span;
            private readonly Dictionary<string, byte[]> _bytes = new(StringComparer.Ordinal);
            private readonly Dictionary<string, long> _charges = new(StringComparer.Ordinal);
            private readonly HashSet<string> _transferred = new(StringComparer.Ordinal);
            private long _domCharge;

            public OpenPyxlLoadSession(ZipArchive archive, ExecutionContext context, LythonSourceSpan span)
            {
                _archive = archive;
                _context = context;
                _span = span;
            }

            /// <summary>
            /// Gets the underlying archive for metadata-only scans (directory
            /// enumeration, presence checks) that never inflate entries. All
            /// byte reads go through the session.
            /// </summary>
            public ZipArchive Archive => _archive;

            public bool ContainsPart(string path)
                => _bytes.ContainsKey(path) || _archive.GetEntry(path) is not null;

            public byte[]? GetPartBytes(string path)
            {
                if (_bytes.TryGetValue(path, out var cached))
                {
                    return cached;
                }

                var entry = _archive.GetEntry(path);
                if (entry is null)
                {
                    return null;
                }

                var payload = ReadBoundedEntryBytes(entry, _context, _span);
                var charge = SnapshotBaseBytesPerEntry + payload.Length;
                _context.MemoryGovernor.Reserve(charge, _span);
                _context.MemoryGovernor.Commit(charge);
                _bytes[path] = payload;
                _charges[path] = charge;
                return payload;
            }

            public XDocument? LoadOptionalXmlDocument(string path)
            {
                var payload = GetPartBytes(path);
                if (payload is null)
                {
                    return null;
                }

                var document = ParseBoundedXmlDocument(payload, _context.MemoryGovernor, _context, _span);
                // Parsed documents coexist while later parts load and stay alive
                // through model consumption, so their charge outlives the parse
                // reservation above. Finish releases it for non-transferred parts
                // (DOMs never transfer); Dispose covers failed loads.
                var domCharge = XmlDocumentBytesPerByte * payload.Length;
                _context.MemoryGovernor.Reserve(domCharge, _span);
                _context.MemoryGovernor.Commit(domCharge);
                _domCharge = checked(_domCharge + domCharge);
                return document;
            }

            public XDocument LoadXmlDocument(string path)
            {
                return LoadOptionalXmlDocument(path)
                    ?? throw InvalidFileException($"Invalid .xlsx workbook: missing {path}.", _span);
            }

            /// <summary>
            /// Releases session-owned charges that were never transferred.
            /// Finish clears its dictionaries, so disposing after a successful
            /// finish is a no-op; failed loads dispose here instead of leaking.
            /// </summary>
            public void Dispose()
            {
                foreach (var pair in _charges)
                {
                    _context.MemoryGovernor.Release(pair.Value);
                }

                if (_domCharge > 0)
                {
                    // Parsed documents never transfer; their lifetime ends here.
                    _context.MemoryGovernor.Release(_domCharge);
                    _domCharge = 0;
                }

                _bytes.Clear();
                _charges.Clear();
                _transferred.Clear();
            }

            public OpenPyxlPackageSnapshot? Finish(bool preserve)
            {
                Dictionary<string, byte[]>? preserved = null;
                if (preserve)
                {
                    preserved = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                    foreach (var entry in _archive.Entries)
                    {
                        var name = NormalizePackagePartName(entry.FullName);
                        if (name.Length == 0 || preserved.ContainsKey(name))
                        {
                            continue;
                        }

                        // Exact-name reuse keeps single-flight inflation: parts already
                        // read during the load are shared, untouched parts inflate here
                        // exactly as the former end-of-load capture did.
                        var payload = GetPartBytes(entry.FullName);
                        if (payload is null)
                        {
                            continue;
                        }

                        preserved[name] = payload;
                        _transferred.Add(entry.FullName);
                    }
                }

                foreach (var pair in _charges)
                {
                    if (!_transferred.Contains(pair.Key))
                    {
                        _context.MemoryGovernor.Release(pair.Value);
                    }
                }

                if (_domCharge > 0)
                {
                    // Parsed documents never transfer; their lifetime ends here.
                    _context.MemoryGovernor.Release(_domCharge);
                    _domCharge = 0;
                }

                _bytes.Clear();
                _charges.Clear();
                _transferred.Clear();
                return preserved is null ? null : new OpenPyxlPackageSnapshot(preserved, _context.MemoryGovernor);
            }
        }

        private static XDocument? LoadSnapshotXml(OpenPyxlPackageSnapshot? snapshot, string path)
        {
            if (snapshot is null || !snapshot.Value.Parts.TryGetValue(NormalizePackagePartName(path), out var payload))
            {
                return null;
            }

            // Snapshot readers have no execution span of their own; the depth
            // diagnostic therefore carries no span. Charges are transient, so a
            // snapshot retained from an earlier run cannot leak committed bytes here.
            return ParseBoundedXmlDocument(payload, snapshot.Value.Governor, context: null, span: null);
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

        private static HashSet<string> GeneratedPackagePartNames(
            OpenPyxlWorkbook workbook,
            bool generateStyles,
            UpdatedPackageParts updatedParts)
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

            foreach (var table in updatedParts.Tables)
            {
                generated.Add(table.Path);
            }

            foreach (var comments in updatedParts.Comments)
            {
                generated.Add(comments.Path);
            }

            for (var i = 0; i < workbook.Worksheets.Count; i++)
            {
                var worksheetPath = $"xl/worksheets/sheet{i + 1}.xml";
                generated.Add(worksheetPath);
                generated.Add(WorksheetRelationshipsPath(worksheetPath));
            }

            return generated;
        }

        private static void WritePreservedPackageParts(ZipArchive archive, OpenPyxlPackageSnapshot? snapshot, IReadOnlySet<string> generatedParts, DateTimeOffset timestamp)
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

                var entry = CreatePackageEntry(archive, pair.Key, timestamp, CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(pair.Value, 0, pair.Value.Length);
            }
        }

        private static void WriteUpdatedLoadedTableParts(
            ZipArchive archive,
            OpenPyxlPackageSnapshot? snapshot,
            IReadOnlyList<UpdatedTablePart> tables,
            DateTimeOffset timestamp)
        {
            foreach (var table in tables)
            {
                WriteXml(archive, table.Path, CreateLoadedTableXml(snapshot, table.Table), timestamp);
            }
        }

        private static void WriteUpdatedLoadedCommentsParts(
            ZipArchive archive,
            IReadOnlyList<UpdatedCommentsPart> comments,
            DateTimeOffset timestamp)
        {
            foreach (var commentsPart in comments)
            {
                WriteXml(archive, commentsPart.Path, CreateLoadedCommentsXml(commentsPart.Worksheet), timestamp);
            }
        }

        /// <summary>Creates an archive entry stamped with the host clock so output never depends on ambient machine time.</summary>
        private static ZipArchiveEntry CreatePackageEntry(ZipArchive archive, string path, DateTimeOffset timestamp, CompressionLevel level)
        {
            var entry = archive.CreateEntry(path, level);
            entry.LastWriteTime = timestamp;
            return entry;
        }

        private sealed record UpdatedPackageParts(
            IReadOnlyList<UpdatedTablePart> Tables,
            IReadOnlyList<UpdatedCommentsPart> Comments)
        {
            public static UpdatedPackageParts Create(OpenPyxlWorkbook workbook)
            {
                // Every downstream package phase consumes this same materialized plan so
                // preservation, content types, and writes cannot disagree after mutation.
                var tablesByPath = new Dictionary<string, OpenPyxlTable>(StringComparer.Ordinal);
                var commentsByPath = new Dictionary<string, OpenPyxlWorksheet>(StringComparer.Ordinal);
                foreach (var worksheet in workbook.Worksheets)
                {
                    foreach (var table in worksheet.Tables.Values)
                    {
                        if (table.HasLoadedPartUpdate && table.SourcePath is { } tablePath)
                        {
                            tablesByPath.TryAdd(tablePath, table);
                        }
                    }

                    if (worksheet.HasLoadedCommentsUpdate && worksheet.CommentsSourcePath is { } commentsPath)
                    {
                        commentsByPath.TryAdd(commentsPath, worksheet);
                    }
                }

                return new UpdatedPackageParts(
                    tablesByPath
                        .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                        .Select(static pair => new UpdatedTablePart(pair.Key, pair.Value))
                        .ToArray(),
                    commentsByPath
                        .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                        .Select(static pair => new UpdatedCommentsPart(pair.Key, pair.Value))
                        .ToArray());
            }
        }

        private sealed record UpdatedTablePart(string Path, OpenPyxlTable Table);

        private sealed record UpdatedCommentsPart(string Path, OpenPyxlWorksheet Worksheet);

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

    private static LythonRuntimeException InvalidFileException(string message, LythonSourceSpan? span)
        => new(ModuleException("openpyxl.utils.exceptions", "InvalidFileException"), message, span);

    private static LythonRuntimeException WorkbookAlreadySaved(string message, LythonSourceSpan? span)
        => new(ModuleException("openpyxl.utils.exceptions", "WorkbookAlreadySaved"), message, span);
}
