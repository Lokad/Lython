using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// R02/R36: the package load session releases charges it never transfers, so
/// failed loads return the governor to baseline instead of leaking. Reflection
/// reaches the private session type; renames fail loudly here by design.
/// </summary>
public sealed class OpenPyxlAccountingTests
{
    private static string FixturePath(string caseId, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "Fixtures", "openpyxl", "cases", caseId, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("OpenPyXL fixture not found: " + caseId + "/" + file);
    }

    private static object NewLoadSession(ZipArchive archive, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var sessionType = package.GetNestedType("OpenPyxlLoadSession", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlLoadSession not found.");
        var ctor = sessionType.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(ZipArchive), typeof(LythonRuntime.ExecutionContext), typeof(LythonSourceSpan)])
            ?? throw new InvalidOperationException("Session constructor not found.");
        return ctor.Invoke([archive, context, span]);
    }

    private static byte[]? InvokeGetPartBytes(object session, string path)
    {
        var method = session.GetType().GetMethod("GetPartBytes", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetPartBytes not found.");
        return (byte[]?)method.Invoke(session, [path]);
    }

    private static void InvokeFinish(object session, bool preserve)
    {
        var method = session.GetType().GetMethod("Finish", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Finish not found.");
        method.Invoke(session, [preserve]);
    }

    [Fact]
    public void FailedLoadReleasesSessionCharges()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        Assert.NotNull(InvokeGetPartBytes(session, "xl/styles.xml"));
        Assert.NotNull(InvokeGetPartBytes(session, "xl/workbook.xml"));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0, "expected session charges");
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void FinishedSessionDisposeIsBalanced()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        Assert.NotNull(InvokeGetPartBytes(session, "xl/styles.xml"));
        InvokeFinish(session, preserve: false);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void SessionDomChargeCoversConsumption()
    {
        var bytes = File.ReadAllBytes(FixturePath("excel-basic", "input.xlsx"));
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var payload = InvokeGetPartBytes(session, "xl/workbook.xml") ?? throw new InvalidOperationException("workbook part missing.");
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var document = InvokeLoadOptionalXmlDocument(session, "xl/workbook.xml");
        Assert.NotNull(document);
        // The DOM outlives its parse reservation: 16 bytes per payload byte
        // (XmlDocumentBytesPerByte) stay committed while the load consumes it.
        Assert.Equal(before + 16L * payload.Length, context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static object? InvokeLoadOptionalXmlDocument(object session, string path)
    {
        var method = session.GetType().GetMethod("LoadOptionalXmlDocument", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("LoadOptionalXmlDocument not found.");
        return method.Invoke(session, [path]);
    }


    private static byte[] BuildArchive(params (string Path, string Content)[] parts)
    {
        using var output = new MemoryStream();
        using (var writer = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var part in parts)
            {
                var entry = writer.CreateEntry(part.Path);
                using var destination = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(part.Content);
                destination.Write(bytes, 0, bytes.Length);
            }
        }

        return output.ToArray();
    }

    private static string SharedStringsXml(params string[] values)
        => @"<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">"
            + string.Concat(values.Select(v => "<si><t>" + v + "</t></si>"))
            + "</sst>";

    private static string SheetXml(string inner)
        => @"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData>"
            + inner
            + "</sheetData></worksheet>";

    private static object InvokeLoadSharedStrings(object session, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadSharedStrings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadSharedStrings not found.");
        return method.Invoke(null, [session, context, span])
            ?? throw new InvalidOperationException("LoadSharedStrings returned null.");
    }

    private static object NewWorksheet(string title)
    {
        var type = typeof(LythonRuntime).GetNestedType("OpenPyxlWorksheet", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlWorksheet not found.");
        var ctor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(string)])
            ?? throw new InvalidOperationException("Worksheet constructor not found.");
        return ctor.Invoke([title]);
    }

    private static object InvokeLoadWorksheetCells(object session, string path, object worksheet, object sharedStrings, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetCells", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetCells not found.");
        var snapshotType = package.GetNestedType("OpenPyxlCellStyleSnapshot", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlCellStyleSnapshot not found.");
        var dateSystemType = typeof(LythonRuntime).GetNestedType("ExcelDateSystem", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ExcelDateSystem not found.");
        var dateSystem = Activator.CreateInstance(dateSystemType)
            ?? throw new InvalidOperationException("ExcelDateSystem default missing.");
        return method.Invoke(null, [session, path, worksheet, sharedStrings, Array.CreateInstance(snapshotType, 0), dateSystem, false, context, span])
            ?? throw new InvalidOperationException("LoadWorksheetCells returned null.");
    }

    [Fact]
    public void SharedStringTableTextIsCharged()
    {
        // R02: the shared-string table outlives the load, so its text pays here
        // rather than at per-cell reference sites.
        var xml = SharedStringsXml("abc", "defgh");
        using var stream = new MemoryStream(BuildArchive(("xl/sharedStrings.xml", xml)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var strings = Assert.IsAssignableFrom<IReadOnlyList<string>>(InvokeLoadSharedStrings(session, context, span));
        Assert.Equal(["abc", "defgh"], strings);
        // Part bytes (96 + length) plus the live DOM (16 per byte) plus the
        // table tail (2 strings at 40 bytes plus 8 text bytes).
        var length = Encoding.UTF8.GetByteCount(xml);
        Assert.Equal((96L + length) + ((long)16 * length) + (2L * 40L + 8L), context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
    [Fact]
    public void CellTextIsChargedProportionally()
    {
        // R02: retained value text joins the per-cell charge instead of hiding
        // inside the flat 512-byte allowance.
        var xml = SheetXml(@"<c r=""A1""/><c r=""A2""><v>12</v></c><c r=""A3"" t=""inlineStr""><is><t>hello</t></is></c>");
        using var stream = new MemoryStream(BuildArchive(("xl/worksheets/sheet1.xml", xml)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var document = InvokeLoadWorksheetCells(session, "xl/worksheets/sheet1.xml", NewWorksheet("Sheet1"), new List<string>(), context, span);
        Assert.NotNull(document);
        // Part bytes plus the live DOM plus three cells (3 at 512 bytes plus
        // the 0-, 2- and 5-byte concatenated contents).
        var length = Encoding.UTF8.GetByteCount(xml);
        Assert.Equal((96L + length) + ((long)16 * length) + (3L * 512L + 7L), context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        // The worksheet still owns its cells, so only the part and DOM charges release.
        Assert.Equal(3L * 512L + 7L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


    [Fact]
    public void OverlappingDomChargesAccumulate()
    {
        // R02: live DOMs coexist, so each parse adds its charge instead of
        // reusing one transient reservation.
        const string first = @"<a xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><b>one</b></a>";
        const string second = @"<a xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><b>two</b></a>";
        using var stream = new MemoryStream(BuildArchive(("p1.xml", first), ("p2.xml", second)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        Assert.NotNull(InvokeLoadOptionalXmlDocument(session, "p1.xml"));
        var firstLength = Encoding.UTF8.GetByteCount(first);
        Assert.Equal((96L + firstLength) + ((long)16 * firstLength), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.NotNull(InvokeLoadOptionalXmlDocument(session, "p2.xml"));
        var secondLength = Encoding.UTF8.GetByteCount(second);
        Assert.Equal((96L + firstLength) + ((long)16 * firstLength) + (96L + secondLength) + ((long)16 * secondLength), context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static void InvokeLoadWorksheetComments(object session, string path, object worksheet, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetComments", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetComments not found.");
        method.Invoke(null, [session, path, worksheet, context, span]);
    }

    [Fact]
    public void CommentTextIsChargedProportionally()
    {
        // R02: retained comment and author text joins the per-comment charge
        // instead of hiding behind bare budget checks.
        const string rels = @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments"" Target=""../comments1.xml""/></Relationships>";
        const string comments = @"<comments xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><authors><author>Ann</author></authors><commentList><comment ref=""A1"" authorId=""0""><text><t>hi there</t></text></comment><comment ref=""A2"" authorId=""0""><text><t>yo</t></text></comment></commentList></comments>";
        using var stream = new MemoryStream(BuildArchive(("xl/worksheets/_rels/sheet1.xml.rels", rels), ("xl/comments1.xml", comments)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        InvokeLoadWorksheetComments(session, "xl/worksheets/sheet1.xml", NewWorksheet("Sheet1"), context, span);
        var relsLength = Encoding.UTF8.GetByteCount(rels);
        var commentsLength = Encoding.UTF8.GetByteCount(comments);
        Assert.Equal((96L + relsLength) + ((long)16 * relsLength) + (96L + commentsLength) + ((long)16 * commentsLength) + 3L + (2L * 512L + 10L), context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        // The worksheet still owns its comments, so only part and DOM charges release.
        Assert.Equal(3L + (2L * 512L + 10L), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


    [Fact]
    public void CancelledCellLoadReleasesEverything()
    {
        // R02: a load that dies on cancellation retains nothing, since no
        // model charge commits before the first guard fires.
        var xml = SheetXml(@"<c r=""A1""/>");
        using var stream = new MemoryStream(BuildArchive(("xl/worksheets/sheet1.xml", xml)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var failure = Assert.Throws<TargetInvocationException>(() =>
            InvokeLoadWorksheetCells(session, "xl/worksheets/sheet1.xml", NewWorksheet("Sheet1"), new List<string>(), context, span));
        Assert.Equal("LythonRuntimeException", failure.InnerException?.GetType().Name);
        Assert.Contains("execution canceled", failure.InnerException?.Message ?? string.Empty, StringComparison.Ordinal);
        ((IDisposable)session).Dispose();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


    private static void InvokeLoadWorksheetDataValidations(object document, object worksheet, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetDataValidations", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetDataValidations not found.");
        method.Invoke(null, [document, worksheet, context, span]);
    }

    [Fact]
    public void ValidationTextIsChargedProportionally()
    {
        // R02: retained validation strings join the per-validation charge.
        const string first = @"<dataValidation type=""whole"" operator=""between"" errorTitle=""ET"" error=""E"" promptTitle=""PT"" prompt=""P""><formula1>1</formula1><formula2>10</formula2><sqref>A1</sqref></dataValidation>";
        const string second = @"<dataValidation type=""whole"" operator=""between"" error=""E2""><formula1>100</formula1><sqref>A2</sqref></dataValidation>";
        var document = XDocument.Parse(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><dataValidations>" + first + second + "</dataValidations></worksheet>");
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        InvokeLoadWorksheetDataValidations(document, NewWorksheet("Sheet1"), context, span);
        // Two validations at 512 bytes plus 9 and 5 retained text bytes.
        Assert.Equal(2L * 512L + 14L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void ConditionalFormattingMarkupIsCharged()
    {
        // R02: the model keeps a full copy of each rule element, so its
        // markup joins the per-rule charge.
        const string rule = @"<cfRule type=""cellIs"" operator=""greaterThan"" priority=""1""><formula>5</formula></cfRule>";
        var document = XDocument.Parse(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><conditionalFormatting sqref=""A1"">" + rule + "</conditionalFormatting></worksheet>");
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        InvokeLoadWorksheetConditionalFormatting(document, NewWorksheet("Sheet1"), context, span);
        var sourceRule = document.Descendants().First(e => e.Name.LocalName == "cfRule");
        // The charge covers the detached model copy, so replicate the copy here.
        var markup = System.Text.Encoding.UTF8.GetByteCount(new XElement(sourceRule).ToString());
        Assert.Equal(512L + markup, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static void InvokeLoadWorksheetConditionalFormatting(object document, object worksheet, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetConditionalFormatting", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetConditionalFormatting not found.");
        method.Invoke(null, [document, worksheet, context, span]);
    }

    private static void InvokeLoadWorksheetTables(object session, string path, object document, object worksheet, object relationships, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetTables", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetTables not found.");
        method.Invoke(null, [session, path, document, worksheet, relationships, context, span]);
    }

    [Fact]
    public void TableNamesAreCharged()
    {
        // R02: the model keeps table display/style names and the source path.
        const string table = @"<table id=""1"" name=""T1"" displayName=""Table1"" ref=""A1:B2"" xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><tableStyleInfo name=""TableStyleMedium9""/></table>";
        using var stream = new MemoryStream(BuildArchive(("xl/tables/table1.xml", table)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var document = XDocument.Parse(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><tableParts><tablePart r:id=""rId1"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""/></tableParts></worksheet>");
        var relationships = new Dictionary<string, string> { ["rId1"] = "../tables/table1.xml" };
        InvokeLoadWorksheetTables(session, "xl/worksheets/sheet1.xml", document, NewWorksheet("Sheet1"), relationships, context, span);
        var length = Encoding.UTF8.GetByteCount(table);
        Assert.Equal((96L + length) + ((long)16 * length) + 512L + 6L + 20L + 17L, context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        // The worksheet still owns its table, so only part and DOM charges release.
        Assert.Equal(512L + 6L + 20L + 17L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


    private static void InvokeLoadWorksheetDrawings(object session, string path, object document, object worksheet, object relationships, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetDrawings", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetDrawings not found.");
        method.Invoke(null, [session, path, document, worksheet, relationships, context, span]);
    }

    [Fact]
    public void DrawingPathsAreCharged()
    {
        // R02: the model keeps drawing and child paths plus relationship ids.
        const string rels = @"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships""><Relationship Id=""rId9"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart"" Target=""charts/chart1.xml""/></Relationships>";
        using var stream = new MemoryStream(BuildArchive(("xl/drawings/_rels/drawing1.xml.rels", rels)), writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var session = NewLoadSession(archive, context, span);
        var document = XDocument.Parse(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><drawing r:id=""rId1"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""/></worksheet>");
        var relationships = new Dictionary<string, string> { ["rId1"] = "../drawings/drawing1.xml" };
        InvokeLoadWorksheetDrawings(session, "xl/worksheets/sheet1.xml", document, NewWorksheet("Sheet1"), relationships, context, span);
        var length = Encoding.UTF8.GetByteCount(rels);
        Assert.Equal((96L + length) + ((long)16 * length) + 512L + 24L + 4L + 29L + 4L, context.MemoryGovernor.CurrentCommittedBytes);
        ((IDisposable)session).Dispose();
        // The worksheet still owns its drawing, so only part and DOM charges release.
        Assert.Equal(512L + 24L + 4L + 29L + 4L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }


    private static void InvokeLoadWorksheetProtection(object document, object worksheet, object context, object span)
    {
        var package = typeof(LythonRuntime).GetNestedType("OpenPyxlPackage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OpenPyxlPackage not found.");
        var method = package.GetMethod("LoadWorksheetProtection", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadWorksheetProtection not found.");
        method.Invoke(null, [document, worksheet, context, span]);
    }

    [Fact]
    public void ProtectionAuthStringsAreCharged()
    {
        // R02: the model keeps protection auth strings; charge them with the entry.
        var document = XDocument.Parse(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetProtection password=""secret"" algorithmName=""SHA-512"" hashValue=""AB"" saltValue=""CD""/></worksheet>");
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        InvokeLoadWorksheetProtection(document, NewWorksheet("Sheet1"), context, span);
        Assert.Equal(512L + 6L + 7L + 2L + 2L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

}
